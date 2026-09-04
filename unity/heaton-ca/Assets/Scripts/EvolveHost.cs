using System;
using System.Collections.Generic;
using HeatonCA.Engine;
using UnityEngine;

namespace HeatonCAApp
{
    /// <summary>
    /// UI-free service running the MergeLife GA off the main thread. The engine's
    /// <see cref="Evolver"/> is deterministic AND resumable — <c>Run(n)</c> then
    /// <c>Run(n + k)</c> continues the same PCG32 stream — so evolution advances in
    /// small chunks with clean stop points in between; no cancellation plumbing
    /// inside the engine. Where those chunks execute is the platform's business,
    /// not the GA's: see <see cref="ChunkRunner"/>. Progress arrives via the
    /// evolver's per-evaluation callback and is exposed as immutable
    /// <see cref="Snapshot"/>s read on the main thread.
    ///
    /// The search is a <b>restart loop</b>, the shape of HeatonCA 1.x's PyQt
    /// trainer (tab_evolve.py): a single population converges — tournament pressure
    /// collapses diversity and the best score plateaus — so rather than breeding one
    /// population forever, a run that has gone <see cref="DefaultPatience"/>
    /// evaluations without a new best is declared converged, discarded, and replaced
    /// with a fresh random population. Automatically, forever, until the user stops.
    ///
    /// Runs execute on <b>concurrent lanes</b>: the GA's admission loop is
    /// spec-sequential (one PCG32 stream — the conformance contract), so a single
    /// run can occupy at most <c>evalCycles</c> worker threads and leaves a
    /// many-core machine mostly idle. Independent restart-runs have no such
    /// constraint — each lane breeds its own deterministic run (run number N always
    /// gets the same seed, drawn in run order from one PCG32 stream), and only the
    /// wall-clock order in which their finds land varies. No single run has to get
    /// lucky; the product of the search is the <see cref="Discovery"/> log — each
    /// run's champion at or above <see cref="Threshold"/>, upgraded in place while
    /// its run improves, accumulated across runs as a best-of catalog. Where the
    /// PyQt app wrote PNGs into a folder, this app fills the finds gallery.
    /// </summary>
    public sealed class EvolveHost
    {
        /// <summary>What the evolve screen tells the user the search is doing.</summary>
        public enum EvolveStatus
        {
            /// <summary>Never started this session.</summary>
            Idle,

            /// <summary>Filling the first random population; no breeding yet.</summary>
            Seeding,

            /// <summary>Breeding.</summary>
            Running,

            /// <summary>Stop requested; the in-flight chunks are finishing.</summary>
            Stopping,

            /// <summary>Paused by the app (the screen went away, or the player was backgrounded).</summary>
            Suspended,

            /// <summary>Paused because the browser tab lost focus and stopped ticking us.</summary>
            BrowserPaused,

            /// <summary>Started and finished; the readouts are the last run's.</summary>
            Stopped,
        }

        /// <summary>The composed, immutable view of the search that the screen reads.</summary>
        public sealed class Snapshot
        {
            /// <summary>Evaluations across the whole search (all runs, all lanes).</summary>
            public int Evals;

            /// <summary>Evaluations per minute, recomputed once a minute; 0 for the first one.</summary>
            public int EvalsPerMinute;

            /// <summary>Run number of the current leader (best score in flight).</summary>
            public int Run;

            /// <summary>Lowest run number still in flight.</summary>
            public int RunLow;

            /// <summary>Highest run number still in flight.</summary>
            public int RunHigh;

            /// <summary>Runs ever started (restarts included) — the restart odometer.</summary>
            public int TotalRunsStarted;

            /// <summary>Evaluations since the leader last improved its best.</summary>
            public int NoImprovement;

            /// <summary>Stall count at which a run restarts.</summary>
            public int Patience;

            /// <summary>Best score in flight, or negative infinity before the first evaluation.</summary>
            public double BestScore;

            /// <summary>Best genome in flight, or null before the first evaluation.</summary>
            public string BestGenome;

            /// <summary>The leader's population, genome and score.</summary>
            public List<(string Genome, double Score)> Population;

            /// <summary>
            /// Population members seeded so far out of the target — equal counts once
            /// the run is breeding, so <c>Done &lt; Total</c> is "still seeding".
            /// </summary>
            public (int Done, int Total) SeedingProgress;

            /// <summary>Distinct rules that ever cleared the threshold, dropped ones included.</summary>
            public int TotalFound;

            /// <summary>What to tell the user the search is doing.</summary>
            public EvolveStatus Status;
        }

        /// <summary>One rule the search turned up at or above the threshold.</summary>
        public sealed class Discovery
        {
            /// <summary>The rule, canonical dashed hex.</summary>
            public string Rule;

            /// <summary>Its objective score when it was logged.</summary>
            public double Score;

            /// <summary>Evaluation number within its run.</summary>
            public int FoundAtEval;

            /// <summary>The run that bred it.</summary>
            public int Run;
        }

        // ---- PyQt trainer configuration (tab_evolve.py "config") -------------------

        /// <summary>Lattice edge the PyQt trainer evolved on: 50x50.</summary>
        public const int EvolveGridSize = 50;

        /// <summary>Population the PyQt trainer bred.</summary>
        public const int DefaultPopulation = 100;

        /// <summary>Evaluation cycles per candidate; the objective takes the best of them.</summary>
        public const int DefaultEvalCycles = 5;

        /// <summary>Step ceiling for one evaluation cycle.</summary>
        public const int DefaultMaxSteps = 1000;

        /// <summary>Probability an admission crosses two parents rather than mutating one.</summary>
        public const double CrossoverRate = 0.75;

        /// <summary>Tournament size for both selection and eviction.</summary>
        public const int TournamentRounds = 5;

        /// <summary>Evaluations per chunk when the caller does not say.</summary>
        public const int DefaultChunkEvals = ThreadedChunkRunner.ChunkEvals;

        /// <summary>
        /// Evaluations without improvement before a run is declared converged and
        /// restarted — the PyQt trainer's config value. Small enough that a stalled
        /// run wastes minutes, not hours; large enough that a run on a streak is not
        /// cut down mid-climb.
        /// </summary>
        public const int DefaultPatience = 250;

        /// <summary>
        /// How many finds the log keeps. The search can score six figures of
        /// candidates, so the log is a <i>best-of</i>: once full, the lowest-scoring
        /// find is dropped, which means searching longer can only improve the
        /// catalog. The ceiling is set by the finds page, where every card renders a
        /// thumbnail.
        /// </summary>
        public const int MaxDiscoveries = 60;

        /// <summary>
        /// Default "worth keeping" score, and the middle of the slider's span: 3.5,
        /// the PyQt trainer's <c>scoreThreshold</c>. The engine's objective follows
        /// the 2018 trainer semantics, so 3.5 means what it meant in years of
        /// MergeLife breeding: a genuine treasure. The canonical gallery rules score
        /// well inside the span; 5.0 is the theoretical maximum.
        /// </summary>
        public const double DefaultThreshold = 3.5;

        /// <summary>Lowest threshold the slider offers — keep almost everything.</summary>
        public const double MinThreshold = -1.0;

        /// <summary>Highest threshold the slider offers — the objective's ceiling.</summary>
        public const double MaxThreshold = 5.0;

        /// <summary>Store key the finds log lives under.</summary>
        public const string StoreKey = "evolve-finds.json";

        /// <summary>Version of the finds-log JSON; a file from any other version is ignored.</summary>
        public const int Schema = 1;

        /// <summary>Seconds between throttled autosaves of the finds log.</summary>
        private const double SaveIntervalSeconds = 2.0;

        /// <summary>Seconds in an evaluations-per-minute window (ml_evolve.py's minute).</summary>
        private const double RateWindowSeconds = 60.0;

        private readonly object _lock = new object();
        private readonly List<Discovery> _discoveries = new List<Discovery>();
        private readonly HashSet<string> _seen = new HashSet<string>();
        private readonly HashSet<string> _deleted = new HashSet<string>();
        private readonly List<Lane> _lanes = new List<Lane>();

        // ---- persistence: the finds log survives app quits ----------------------
        // Saved throttled while breeding (Tick), on RequestStop and Suspend, and from
        // the app's lifecycle hooks; loaded lazily on first access. Only user deletes
        // persist as tombstones — in-log rules re-tombstone themselves on load, and
        // eviction tombstones are session-local (a re-found evictee would just be
        // evicted again).
        private IAppStore _store;
        private bool _loaded;
        private bool _dirty;      // guarded by _lock
        private double _lastSave; // main thread only

        private bool _stopRequested;
        private bool _suspended;
        private bool _browserPaused;
        private bool _everStarted;
        private double _threshold = DefaultThreshold;
        private int _totalFound;

        // ---- restart loop state (mutated only between chunks, on the main thread)
        private Pcg32 _seedRng;        // per-run seeds; seq 2 (0 = lattice, 1 = trainer)
        private int _nextRun;          // next run number to hand out (1-based)
        private int _completedEvals;   // evals in finished runs; guarded by _lock
        private int _width;
        private int _height;
        private int _population;
        private int _cycles;
        private int _maxSteps;
        private int _patience;
        private double _crossoverRate = CrossoverRate;
        private int _tournamentRounds = TournamentRounds;
        private int _chunkEvals = DefaultChunkEvals;
        private int _workers = 1;
        private bool _publishPerEval = true;

        // ---- readouts ------------------------------------------------------------
        private int _evalsPerMinute;
        private double _rateWindowStart;
        private int _rateEvalsAtWindowStart;

        private bool _running;
        private int _savedSleepTimeout = SleepTimeout.SystemSetting;
        private Func<double> _clock = () => Time.realtimeSinceStartup;

        /// <summary>
        /// Where the finds log is kept. Defaults to the platform's store; the tests
        /// and the app's storage redirect hand it their own through
        /// <see cref="SetStore"/>.
        /// </summary>
        private IAppStore Store => _store ?? (_store = AppStore.CreateDefault());

        /// <summary>
        /// Seconds-since-start clock, injectable so tests can drive the minute-long
        /// rate window and the autosave throttle without waiting for either.
        /// </summary>
        public Func<double> Clock
        {
            get => _clock;
            set => _clock = value ?? (() => Time.realtimeSinceStartup);
        }

        /// <summary>
        /// The chunk runner this search uses. Left null, <see cref="Start"/> asks
        /// <see cref="EvolveScheduler.CreateRunner"/> for the platform's; the tests
        /// set it to prove both runners replay the same GA.
        /// </summary>
        public ChunkRunner Runner { get; set; }

        /// <summary>
        /// Worker threads one evaluation may use, or 0 to size to the machine
        /// (<see cref="EvolveScheduler.DefaultWorkers"/>). Results are identical for
        /// any value — the objective's cycle runs parallelize bit-exactly.
        /// </summary>
        public int Workers { get; set; }

        /// <summary>
        /// Point the finds log at another store and reload from it: the app's
        /// storage redirect, so tests never touch the user's real finds.
        /// </summary>
        public void SetStore(IAppStore store)
        {
            lock (_lock)
            {
                _store = store;
                _loaded = false;
                _dirty = false;
                _discoveries.Clear();
                _seen.Clear();
                _deleted.Clear();
                _totalFound = 0;
                foreach (Lane lane in _lanes)
                    lane.Discovery = null;
            }
        }

        /// <summary>
        /// True while the search is in flight. Setting it also scopes
        /// <c>Application.runInBackground</c>: the project default is off so an idle
        /// unfocused app does not redraw at 60 fps, but with it off a desktop player
        /// pauses the moment it loses focus — and a GA search is minutes of chunks
        /// pumped from <see cref="Tick"/>, so clicking another window would stall it
        /// at the next chunk boundary. It scopes the mobile screen timeout for the
        /// same reason: a phone that dims and sleeps stops ticking. Routing every
        /// entry and exit through this setter keeps both flags correct on the
        /// faulted path too.
        /// </summary>
        public bool Running
        {
            get => _running;
            private set
            {
                if (_running == value)
                    return;
                _running = value;
                Application.runInBackground = value;
                ScopeScreenSleep(value);
            }
        }

        /// <summary>Concurrent lanes in this search (fixed at <see cref="Start"/>).</summary>
        public int Lanes => _lanes.Count;

        /// <summary>Evaluations per chunk in this search (fixed at <see cref="Start"/>).</summary>
        public int ChunkEvals => _chunkEvals;

        /// <summary>True while any lane has work in flight — nothing may be restarted under it.</summary>
        public bool Busy
        {
            get
            {
                foreach (Lane lane in _lanes)
                {
                    if (lane.Active && lane.ChunkInFlight)
                        return true;
                }
                return false;
            }
        }

        /// <summary>True while <see cref="Suspend"/> is holding new chunks back.</summary>
        public bool Suspended => _suspended;

        /// <summary>
        /// Set by the WebGL focus hook when the browser tab goes to the background.
        /// The browser has already stopped ticking us by then, so this changes no
        /// scheduling — it only lets the screen say why the counters stopped.
        /// </summary>
        public bool BrowserPaused
        {
            get => _browserPaused;
            set => _browserPaused = value;
        }

        /// <summary>Evaluations per minute as of the last full window; 0 for the first minute.</summary>
        public int EvalsPerMinute => _evalsPerMinute;

        /// <summary>What to tell the user the search is doing.</summary>
        public EvolveStatus Status
        {
            get
            {
                lock (_lock)
                {
                    Snapshot composed = ComposeLocked();
                    return composed?.Status ?? StatusFor(false);
                }
            }
        }

        /// <summary>
        /// The composed view of the search: the leader lane's run (its best, its
        /// population — what the preview and leaderboard show) plus the aggregate
        /// eval count and the runs-in-flight range. Null until a lane publishes.
        /// </summary>
        public Snapshot Latest
        {
            get
            {
                lock (_lock)
                {
                    return ComposeLocked();
                }
            }
        }

        /// <summary>
        /// Score at or above which a rule is worth keeping. Live-adjustable: raising
        /// it mid-run narrows what is admitted next but never retracts the log — a
        /// log you can retroactively empty by nudging a slider is not a log. Use
        /// <see cref="ClearDiscoveries"/> to start the catalog over.
        /// </summary>
        public double Threshold
        {
            get { lock (_lock) { return _threshold; } }
            set { lock (_lock) { _threshold = value; } }
        }

        /// <summary>
        /// The finds, best score first. Deep copies — the live entries upgrade in
        /// place while their run improves, so callers get immutable snapshots (a
        /// built card shows what the entry said when the page was built).
        /// </summary>
        public List<Discovery> Discoveries
        {
            get
            {
                lock (_lock)
                {
                    EnsureLoadedLocked();
                    var copy = new List<Discovery>(_discoveries.Count);
                    foreach (Discovery find in _discoveries)
                    {
                        copy.Add(new Discovery
                        {
                            Rule = find.Rule,
                            Score = find.Score,
                            FoundAtEval = find.FoundAtEval,
                            Run = find.Run,
                        });
                    }
                    copy.Sort((a, b) => b.Score.CompareTo(a.Score));
                    return copy;
                }
            }
        }

        /// <summary>Finds currently kept (at most <see cref="MaxDiscoveries"/>).</summary>
        public int DiscoveryCount
        {
            get { lock (_lock) { EnsureLoadedLocked(); return _discoveries.Count; } }
        }

        /// <summary>Every distinct rule that ever cleared the threshold, dropped ones included.</summary>
        public int TotalFound
        {
            get { lock (_lock) { EnsureLoadedLocked(); return _totalFound; } }
        }

        /// <summary>
        /// Searches until <see cref="RequestStop"/>: run after run on
        /// <paramref name="lanes"/> concurrent lanes (0 = size to the machine), each
        /// restarted from a fresh random population once it stalls for
        /// <paramref name="patience"/> evaluations. Run number N always draws the
        /// same seed (run 1 is <paramref name="seed"/> itself; later runs come from a
        /// PCG32 stream over it, drawn in run order), so every individual run replays
        /// exactly; only the wall-clock order of their finds varies. The finds log
        /// deliberately survives across runs AND across Start presses — it is the
        /// user's collection, not one run's output;
        /// <see cref="ClearDiscoveries"/> is the only thing that empties it.
        /// </summary>
        /// <param name="width">Lattice width each candidate is evaluated on.</param>
        /// <param name="height">Lattice height each candidate is evaluated on.</param>
        /// <param name="populationSize">Genomes bred at once, per run.</param>
        /// <param name="evalCycles">Evaluation cycles per candidate.</param>
        /// <param name="maxSteps">Step ceiling for one evaluation cycle.</param>
        /// <param name="seed">Run 1's seed, and the seed of the per-run seed stream.</param>
        /// <param name="patience">Stalled evaluations before a run restarts.</param>
        /// <param name="lanes">Concurrent runs, or 0 to size to the machine.</param>
        /// <param name="chunkEvals">Evaluations between stop points.</param>
        /// <param name="crossoverRate">Probability an admission crosses rather than mutates.</param>
        /// <param name="tournamentRounds">Tournament size for selection and eviction.</param>
        public void Start(
            int width,
            int height,
            int populationSize,
            int evalCycles,
            int maxSteps,
            ulong seed,
            int patience = DefaultPatience,
            int lanes = 0,
            int chunkEvals = DefaultChunkEvals,
            double crossoverRate = CrossoverRate,
            int tournamentRounds = TournamentRounds)
        {
            if (Running)
                return;
            _width = width;
            _height = height;
            _population = populationSize;
            _cycles = evalCycles;
            _maxSteps = maxSteps;
            _patience = patience;
            _crossoverRate = crossoverRate;
            _tournamentRounds = tournamentRounds;
            _chunkEvals = Mathf.Max(1, chunkEvals);
            _workers = Workers > 0 ? Workers : EvolveScheduler.DefaultWorkers();
            if (Runner == null)
                Runner = EvolveScheduler.CreateRunner();
            _publishPerEval = Runner.PublishesPerEval;
            if (lanes <= 0)
                lanes = EvolveScheduler.DefaultLanes(_workers, evalCycles);
            _seedRng = new Pcg32(seed, 2);
            _nextRun = 1;
            lock (_lock)
            {
                // Load before any worker can race the lazy loader.
                EnsureLoadedLocked();
                _completedEvals = 0;
                _lanes.Clear();
                for (int i = 0; i < lanes; i++)
                {
                    int run = _nextRun++;
                    var lane = new Lane { Run = run, Active = true };
                    lane.Evolver = CreateEvolver(lane, run == 1 ? seed : NextRunSeed());
                    _lanes.Add(lane);
                }
            }
            _stopRequested = false;
            _suspended = false;
            _everStarted = true;
            _evalsPerMinute = 0;
            _rateEvalsAtWindowStart = 0;
            _rateWindowStart = _clock();
            _lastSave = _rateWindowStart;
            Running = true;
            Runner.BeginTick();
            foreach (Lane lane in _lanes)
                LaunchChunk(lane);
        }

        /// <summary>
        /// The search HeatonCA 1.x ran: a 50x50 lattice, 100 genomes, five cycles of
        /// up to a thousand steps, restarted after 250 stalled evaluations.
        /// </summary>
        public void StartDefault(ulong seed) =>
            Start(
                EvolveGridSize,
                EvolveGridSize,
                DefaultPopulation,
                DefaultEvalCycles,
                DefaultMaxSteps,
                seed,
                DefaultPatience);

        /// <summary>Stops after each lane's in-flight chunk; results stay in <see cref="Latest"/>.</summary>
        public void RequestStop()
        {
            _stopRequested = true;
            PersistFinds(); // Stop and leaving-the-screen both end sessions: bank the log
        }

        /// <summary>
        /// Hold new chunks back without ending the search: the in-flight chunk
        /// finishes and publishes, then the lanes idle until <see cref="Resume"/>.
        /// This is what leaving the evolve screen and backgrounding the player do —
        /// the run keeps its population, its best, and its run number.
        /// </summary>
        public void Suspend()
        {
            if (_suspended)
                return;
            _suspended = true;
            PersistFinds(); // a suspend is usually the last thing before a quit
        }

        /// <summary>Let the lanes launch chunks again after <see cref="Suspend"/>.</summary>
        public void Resume() => _suspended = false;

        /// <summary>
        /// Empty the catalog: finds, tombstones, and the total-found odometer. The
        /// only thing that does — raising <see cref="Threshold"/> never retracts a
        /// find.
        /// </summary>
        public void ClearDiscoveries()
        {
            lock (_lock)
            {
                EnsureLoadedLocked();
                _discoveries.Clear();
                _seen.Clear();
                _deleted.Clear(); // "start the catalog over" clears tombstones too
                _totalFound = 0;
                foreach (Lane lane in _lanes)
                    lane.Discovery = null;
                _dirty = true;
            }
        }

        /// <summary>
        /// Drop one find from the log. The rule stays in the seen-set as a tombstone
        /// on purpose: it is usually still alive in the breeding population, and
        /// without the tombstone the very next publish would re-admit what the user
        /// just deleted. <see cref="TotalFound"/> keeps counting it — it did clear
        /// the threshold once.
        /// </summary>
        public bool RemoveDiscovery(string rule)
        {
            lock (_lock)
            {
                EnsureLoadedLocked();
                for (int i = 0; i < _discoveries.Count; i++)
                {
                    if (_discoveries[i].Rule != rule)
                        continue;
                    // Detach the live entry so later upgrades of its run cannot
                    // resurrect what the user just deleted; the tombstone persists so
                    // it stays deleted across app launches too.
                    foreach (Lane lane in _lanes)
                    {
                        if (lane.Discovery == _discoveries[i])
                            lane.Discovery = null;
                    }
                    _discoveries.RemoveAt(i);
                    _deleted.Add(rule);
                    _dirty = true;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Write the finds log now. Called throttled from <see cref="Tick"/>, on
        /// <see cref="RequestStop"/> and <see cref="Suspend"/>, and by the app's
        /// quit/pause hooks — every way a session ends lands here, so the gallery
        /// survives all of them.
        /// </summary>
        public void PersistFinds()
        {
            string json;
            lock (_lock)
            {
                EnsureLoadedLocked();
                var finds = new List<object>(_discoveries.Count);
                foreach (Discovery find in _discoveries)
                {
                    finds.Add(new Dictionary<string, object>
                    {
                        ["rule"] = find.Rule,
                        ["score"] = find.Score,
                        ["foundAtEval"] = find.FoundAtEval,
                        ["run"] = find.Run,
                    });
                }
                var deleted = new List<object>(_deleted.Count);
                foreach (string rule in _deleted)
                    deleted.Add(rule);
                json = AppJson.Write(new Dictionary<string, object>
                {
                    ["schema"] = Schema,
                    ["totalFound"] = _totalFound,
                    ["finds"] = finds,
                    ["deleted"] = deleted,
                });
                _dirty = false;
            }
            try
            {
                IAppStore store = Store;
                store.WriteText(StoreKey, json);
                store.Flush();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[HeatonCA] could not save the finds log: {exception}");
            }
            _lastSave = _clock();
        }

        /// <summary>
        /// Main-thread pump: reap finished chunks, publish, restart stalled runs,
        /// launch the next chunk. On the inline runner this is also where the
        /// evaluations themselves happen, inside the runner's frame budget.
        /// </summary>
        public void Tick()
        {
            if (!Running)
                return;
            ChunkRunner runner = Runner;
            runner.BeginTick();
            bool anyInFlight = false;
            foreach (Lane lane in _lanes)
            {
                if (!lane.Active)
                    continue; // idled after a stop request
                if (!runner.Poll(lane, out Exception fault))
                {
                    anyInFlight = true;
                    continue;
                }
                if (fault != null)
                {
                    Debug.LogException(fault);
                    lane.Active = false;
                    Running = false;
                    PersistFinds();
                    return;
                }
                // Between chunks nothing is mid-admission on this lane, so this
                // publish shows its settled population (the per-eval callback fires
                // before the scored candidate is appended, so per-eval publishes run
                // one entry short).
                PublishLane(lane);
                if (_stopRequested)
                {
                    lane.Active = false;
                    continue;
                }
                if (_suspended)
                {
                    anyInFlight = true; // the lane is alive, just not launching
                    continue;
                }
                // A run that has stalled past its patience is converged: the engine's
                // Run() has gone inert, so discard the population and hand the lane
                // the next run — the PyQt trainer's outer Worker loop, one lane among
                // several.
                if (lane.Evolver.NoImprovement > lane.Evolver.Patience)
                {
                    lock (_lock)
                    {
                        _completedEvals += lane.Evolver.Evals;
                        lane.Discovery = null; // the finished run's entry is sealed
                        lane.LaneState = null; // its view leaves the aggregate
                        lane.Run = _nextRun++;
                    }
                    lane.Evolver = CreateEvolver(lane, NextRunSeed());
                }
                LaunchChunk(lane);
                anyInFlight = true;
            }
            if (_stopRequested && !anyInFlight)
                Running = false;
            UpdateRate();
            AutoSave();
        }

        /// <summary>
        /// Evaluations per minute the way ml_evolve.py reported it: one number per
        /// full minute of wall clock, never a rolling estimate, so the readout is
        /// steady enough to read. Zero until the first minute is up.
        /// </summary>
        private void UpdateRate()
        {
            double now = _clock();
            double elapsed = now - _rateWindowStart;
            if (elapsed <= RateWindowSeconds)
                return;
            int evals;
            lock (_lock)
            {
                evals = AggregateEvalsLocked();
            }
            double perSecond = (evals - _rateEvalsAtWindowStart) / elapsed;
            _evalsPerMinute = (int)(perSecond * 60.0);
            _rateEvalsAtWindowStart = evals;
            _rateWindowStart = now;
        }

        /// <summary>
        /// Throttled autosave: new finds reach the store within seconds, so even a
        /// crash or force-quit loses at most a moment of the gallery.
        /// </summary>
        private void AutoSave()
        {
            bool dirty;
            lock (_lock)
            {
                dirty = _dirty;
            }
            if (dirty && _clock() - _lastSave > SaveIntervalSeconds)
                PersistFinds();
        }

        private void LaunchChunk(Lane lane)
        {
            int target = lane.Evolver.Evals + _chunkEvals;
            Runner.Launch(lane, target);
            if (!_publishPerEval)
                PublishLane(lane); // an inline runner has no other publish point
        }

        private Evolver CreateEvolver(Lane lane, ulong seed) =>
            new Evolver(
                _width,
                _height,
                _population,
                crossoverRate: _crossoverRate,
                tournamentRounds: _tournamentRounds,
                evalCycles: _cycles,
                patience: _patience,
                maxSteps: _maxSteps,
                seed: seed,
                objective: null,
                // The callback captures the lane so a publish reads its own evolver
                // only — other lanes are mid-run on other threads.
                onProgress: _ =>
                {
                    if (_publishPerEval)
                        PublishLane(lane);
                },
                // The objective's cycle runs parallelize bit-exactly (spec/evolve.md
                // "Parallel evaluation"); reuse the platform's worker budget.
                workers: _workers);

        private ulong NextRunSeed() =>
            ((ulong)_seedRng.NextU32() << 32) | _seedRng.NextU32();

        /// <summary>
        /// Publish one lane's state (worker thread per evaluation on the threaded
        /// runner, main thread per launch on the inline one, main thread between
        /// chunks either way). Reads only the lane's own evolver — other lanes'
        /// evolvers are mid-run on other threads and must never be touched here.
        /// </summary>
        private void PublishLane(Lane lane)
        {
            Evolver evolver = lane.Evolver;
            var population = new List<(string Genome, double Score)>(evolver.Population.Count);
            foreach (Candidate candidate in evolver.Population)
                population.Add((candidate.Genome, candidate.Score));
            var state = new Snapshot
            {
                Evals = evolver.Evals,
                Run = lane.Run,
                NoImprovement = evolver.NoImprovement,
                Patience = evolver.Patience,
                BestScore = evolver.Best?.Score ?? double.NegativeInfinity,
                BestGenome = evolver.Best?.Genome,
                Population = population,
                SeedingProgress = (population.Count, evolver.PopulationSize),
            };
            lock (_lock)
            {
                lane.LaneState = state;
                // One log entry per run: its champion, upgraded in place as the run
                // improves. Best is tracked outside the population, so nothing that
                // matters can slip past; a new best lands one publish after it is
                // scored, and publishes happen at least once per evaluation.
                if (evolver.Best != null)
                    RecordRunBest(lane, evolver.Best.Genome, evolver.Best.Score, state.Evals);
            }
        }

        /// <summary>Compose the lanes into one view. Caller holds the lock.</summary>
        private Snapshot ComposeLocked()
        {
            Snapshot leader = null;
            int evals = _completedEvals;
            int lo = int.MaxValue;
            int hi = 0;
            foreach (Lane lane in _lanes)
            {
                Snapshot state = lane.LaneState;
                if (state == null)
                    continue;
                evals += state.Evals;
                lo = Math.Min(lo, state.Run);
                hi = Math.Max(hi, state.Run);
                if (leader == null || state.BestScore > leader.BestScore)
                    leader = state;
            }
            if (leader == null)
                return null;
            bool seeding = leader.SeedingProgress.Done < leader.SeedingProgress.Total;
            return new Snapshot
            {
                Evals = evals,
                EvalsPerMinute = _evalsPerMinute,
                Run = leader.Run,
                RunLow = lo,
                RunHigh = hi,
                TotalRunsStarted = _nextRun - 1,
                NoImprovement = leader.NoImprovement,
                Patience = leader.Patience,
                BestScore = leader.BestScore,
                BestGenome = leader.BestGenome,
                Population = leader.Population,
                SeedingProgress = leader.SeedingProgress,
                TotalFound = _totalFound,
                Status = StatusFor(seeding),
            };
        }

        /// <summary>Aggregate evaluations across the whole search. Caller holds the lock.</summary>
        private int AggregateEvalsLocked()
        {
            int evals = _completedEvals;
            foreach (Lane lane in _lanes)
            {
                Snapshot state = lane.LaneState;
                if (state != null)
                    evals += state.Evals;
            }
            return evals;
        }

        private EvolveStatus StatusFor(bool seeding)
        {
            if (!Running)
                return _everStarted ? EvolveStatus.Stopped : EvolveStatus.Idle;
            if (_stopRequested)
                return EvolveStatus.Stopping;
            if (_browserPaused)
                return EvolveStatus.BrowserPaused;
            if (_suspended)
                return EvolveStatus.Suspended;
            return seeding ? EvolveStatus.Seeding : EvolveStatus.Running;
        }

        /// <summary>Keep a phone awake for the length of a search, and only that long.</summary>
        private void ScopeScreenSleep(bool keepAwake)
        {
            if (!Application.isMobilePlatform)
                return;
            if (keepAwake)
            {
                _savedSleepTimeout = Screen.sleepTimeout;
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }
            else
            {
                Screen.sleepTimeout = _savedSleepTimeout;
            }
        }

        /// <summary>Load the persisted finds log. Caller holds the lock.</summary>
        private void EnsureLoadedLocked()
        {
            if (_loaded)
                return;
            _loaded = true;
            try
            {
                IAppStore store = Store;
                if (!store.Exists(StoreKey))
                    return;
                string text = store.ReadText(StoreKey);
                if (string.IsNullOrEmpty(text))
                    return;
                var root = AppJson.Obj(AppJson.Parse(text));
                if (!root.TryGetValue("schema", out object schema) || AppJson.Int(schema) != Schema)
                {
                    Debug.LogWarning("[HeatonCA] ignoring a finds log from a different schema");
                    return;
                }
                _totalFound = AppJson.Int(root["totalFound"]);
                foreach (object entry in AppJson.Arr(root["finds"]))
                {
                    var o = AppJson.Obj(entry);
                    var find = new Discovery
                    {
                        Rule = AppJson.Str(o["rule"]),
                        Score = AppJson.Num(o["score"]),
                        FoundAtEval = AppJson.Int(o["foundAtEval"]),
                        Run = o.TryGetValue("run", out object run) ? AppJson.Int(run) : 0,
                    };
                    _discoveries.Add(find);
                    _seen.Add(find.Rule);
                }
                foreach (object rule in AppJson.Arr(root["deleted"]))
                {
                    string tombstone = AppJson.Str(rule);
                    _deleted.Add(tombstone);
                    _seen.Add(tombstone);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[HeatonCA] could not load the finds log: {exception}");
            }
        }

        /// <summary>
        /// Log or upgrade one lane's run champion. Caller holds the lock. Every rule
        /// string that ever headlined an entry is tombstoned in the seen-set, so a
        /// later run that re-breeds the exact same string cannot duplicate it.
        /// </summary>
        private void RecordRunBest(Lane lane, string rule, double score, int evals)
        {
            if (score < _threshold || rule == null)
                return;
            if (lane.Discovery != null)
            {
                if (lane.Discovery.Rule == rule)
                    return; // same champion, nothing new
                // The run improved on its own entry: upgrade in place.
                _seen.Add(rule);
                lane.Discovery.Rule = rule;
                lane.Discovery.Score = score;
                lane.Discovery.FoundAtEval = evals;
                lane.Discovery.Run = lane.Run;
                _dirty = true;
                return;
            }
            if (!_seen.Add(rule))
                return; // an earlier run already owns this exact string
            _totalFound++;
            _dirty = true;
            lane.Discovery = new Discovery
            {
                Rule = rule,
                Score = score,
                FoundAtEval = evals,
                Run = lane.Run,
            };
            _discoveries.Add(lane.Discovery);
            if (_discoveries.Count <= MaxDiscoveries)
                return;
            int worst = 0;
            for (int i = 1; i < _discoveries.Count; i++)
            {
                if (_discoveries[i].Score < _discoveries[worst].Score)
                    worst = i;
            }
            // The dropped rule stays in the seen-set as a tombstone; if the evicted
            // entry is some lane's live one, detach it so upgrades cannot resurrect it.
            foreach (Lane other in _lanes)
            {
                if (other.Discovery == _discoveries[worst])
                    other.Discovery = null;
            }
            _discoveries.RemoveAt(worst);
        }
    }
}
