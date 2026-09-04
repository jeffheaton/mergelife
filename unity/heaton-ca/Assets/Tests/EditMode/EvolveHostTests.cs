using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using HeatonCAApp;
using NUnit.Framework;

namespace HeatonCA.Tests
{
    /// <summary>
    /// <see cref="EvolveHost"/>: that chunking the GA does not change it (the
    /// mini-run vector replays through the host on either chunk runner), that the
    /// finds log admits, upgrades, evicts, forgets, and survives a reload the way
    /// the catalog promises, and that the evaluations-per-minute readout follows
    /// ml_evolve.py's once-a-minute rule.
    /// </summary>
    public class EvolveHostTests
    {
        /// <summary>Wall-clock ceiling for a pumped run; a stalled host fails, it does not hang.</summary>
        private const double PumpTimeoutSeconds = 30;

        /// <summary>A small lattice the log tests can breed on in a few milliseconds.</summary>
        private const int TinyEdge = 24;

        private const int TinyPopulation = 8;
        private const int TinyMaxSteps = 120;

        /// <summary>Below every possible objective score: the log keeps whatever the run finds.</summary>
        private const double KeepEverything = -100;

        /// <summary>Above every possible objective score: the log keeps nothing.</summary>
        private const double KeepNothing = 100;

        // ---- the vector replay ---------------------------------------------------

        /// <summary>
        /// The evolve mini-run vector, replayed through the host rather than through
        /// a bare Evolver: the host breeds in chunks (Run(n), then Run(n + k)), on a
        /// worker task or inline, and the GA must not notice. After the vector's 20
        /// evaluations the host's snapshot has to carry the vector's champion, its
        /// score, and its population, in order.
        /// </summary>
        [TestCase("threaded")]
        [TestCase("main-thread")]
        public void MiniRunReplaysThroughHostOnBothRunners(string runnerKind)
        {
            string caseDir = VectorPaths.Vendored("evolve", "mini-run-24");
            var root = J.LoadCase(caseDir);
            var p = J.Obj(root["params"]);
            var expected = J.Obj(root["expected"]);
            int evalTarget = J.Int(expected["evals"]);

            var host = new EvolveHost();
            host.SetStore(new MemoryStore());
            host.Runner = NewRunner(runnerKind);
            host.Workers = 1;
            host.Threshold = KeepEverything;
            host.Start(
                J.Int(p["width"]),
                J.Int(p["height"]),
                J.Int(p["population_size"]),
                J.Int(p["eval_cycles"]),
                J.Int(p["max_steps"]),
                (ulong)J.Num(p["seed"]),
                J.Int(p["patience"]),
                lanes: 1,
                chunkEvals: 4,
                crossoverRate: J.Num(p["crossover_rate"]),
                tournamentRounds: J.Int(p["tournament_rounds"]));
            try
            {
                PumpTo(host, evalTarget);
                EvolveHost.Snapshot latest = host.Latest;
                Assert.AreEqual(evalTarget, latest.Evals, "the host overshot the vector's eval count");
                Assert.AreEqual(1, latest.Run, "run 1 must use the seed itself");
                Assert.AreEqual(J.Str(expected["best_genome"]), latest.BestGenome);
                double[] expectedBest = VectorPaths.ReadF64(
                    Path.Combine(caseDir, J.Str(J.Obj(expected["best_score"])["file"])));
                Assert.AreEqual(expectedBest[0], latest.BestScore);
                var expectedPopulation = J.Arr(expected["population"]);
                Assert.AreEqual(expectedPopulation.Count, latest.Population.Count);
                for (int i = 0; i < expectedPopulation.Count; i++)
                    Assert.AreEqual(J.Str(expectedPopulation[i]), latest.Population[i].Genome, $"population[{i}]");
            }
            finally
            {
                StopHost(host);
            }
        }

        // ---- the finds log -------------------------------------------------------

        /// <summary>A threshold no candidate can clear keeps the log empty; one below them all fills it.</summary>
        [Test]
        public void ThresholdGatesWhatTheLogKeeps()
        {
            var strict = new EvolveHost();
            strict.SetStore(new MemoryStore());
            strict.Threshold = KeepNothing;
            RunTiny(strict, seed: 5, evals: 12);
            Assert.AreEqual(0, strict.DiscoveryCount, "nothing may clear an impossible threshold");
            Assert.AreEqual(0, strict.TotalFound);

            var open = new EvolveHost();
            open.SetStore(new MemoryStore());
            open.Threshold = KeepEverything;
            RunTiny(open, seed: 5, evals: 12);
            Assert.AreEqual(1, open.DiscoveryCount, "a run contributes exactly one entry");
            Assert.AreEqual(1, open.TotalFound);
        }

        /// <summary>
        /// A run improves on itself many times over twelve evaluations, and the log
        /// still holds one entry for it — its current champion, upgraded in place,
        /// not a trail of every high-water mark.
        /// </summary>
        [Test]
        public void RunChampionUpgradesInPlace()
        {
            var host = new EvolveHost();
            host.SetStore(new MemoryStore());
            host.Threshold = KeepEverything;
            RunTiny(host, seed: 9, evals: 12);
            List<EvolveHost.Discovery> finds = host.Discoveries;
            Assert.AreEqual(1, finds.Count);
            Assert.AreEqual(host.Latest.BestGenome, finds[0].Rule);
            Assert.AreEqual(host.Latest.BestScore, finds[0].Score);
            Assert.AreEqual(1, finds[0].Run, "the entry names the run that bred it");
        }

        /// <summary>
        /// A deleted rule stays deleted across an app launch: the tombstone is
        /// written with the log and reloaded with it, and the total-found odometer
        /// keeps counting a rule the user threw away — it did clear the bar once.
        /// </summary>
        [Test]
        public void TombstoneSurvivesReload()
        {
            string root = Path.Combine(Path.GetTempPath(), "heatonca-finds-" + Guid.NewGuid().ToString("N"));
            try
            {
                string doomed = Synthetic(0);
                WriteLog(new FileStore(root), totalFound: 2, finds: new[] { (doomed, 4.5), (Synthetic(1), 4.0) });

                var host = new EvolveHost();
                host.SetStore(new FileStore(root));
                Assert.AreEqual(2, host.DiscoveryCount);
                Assert.IsTrue(host.RemoveDiscovery(doomed));
                Assert.IsFalse(host.RemoveDiscovery(doomed), "a rule can only be deleted once");
                Assert.IsFalse(host.RemoveDiscovery("no-such-rule"));
                host.PersistFinds();

                var reloaded = new EvolveHost();
                reloaded.SetStore(new FileStore(root));
                Assert.AreEqual(1, reloaded.DiscoveryCount);
                Assert.AreEqual(2, reloaded.TotalFound, "a deleted rule still counts as found");
                reloaded.PersistFinds();
                Assert.Contains(doomed, Tombstones(new FileStore(root)), "the tombstone must round-trip");

                reloaded.ClearDiscoveries();
                reloaded.PersistFinds();
                Assert.AreEqual(0, reloaded.DiscoveryCount);
                Assert.AreEqual(0, reloaded.TotalFound);
                Assert.AreEqual(0, Tombstones(new FileStore(root)).Count, "clearing the catalog clears tombstones");
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        /// <summary>
        /// The log is a best-of: the sixty-first find pushes out the weakest entry,
        /// so a longer search can only improve the catalog.
        /// </summary>
        [Test]
        public void EvictionKeepsTheBestSixty()
        {
            var store = new MemoryStore();
            var seeded = new List<(string Rule, double Score)>();
            for (int i = 0; i < EvolveHost.MaxDiscoveries; i++)
                seeded.Add((Synthetic(i), -1000.0 + i)); // entry 0 is the weakest
            WriteLog(store, EvolveHost.MaxDiscoveries, seeded);

            var host = new EvolveHost();
            host.SetStore(store);
            host.Threshold = KeepEverything;
            Assert.AreEqual(EvolveHost.MaxDiscoveries, host.DiscoveryCount);
            RunTiny(host, seed: 11, evals: 12);

            Assert.AreEqual(EvolveHost.MaxDiscoveries, host.DiscoveryCount, "the log never grows past its cap");
            Assert.AreEqual(EvolveHost.MaxDiscoveries + 1, host.TotalFound);
            var rules = new List<string>();
            foreach (EvolveHost.Discovery find in host.Discoveries)
                rules.Add(find.Rule);
            Assert.IsFalse(rules.Contains(Synthetic(0)), "the weakest entry is the one that goes");
            Assert.IsTrue(rules.Contains(host.Latest.BestGenome), "the new find is kept");
        }

        // ---- readouts ------------------------------------------------------------

        /// <summary>
        /// Evaluations per minute the way ml_evolve.py reported it: nothing for the
        /// first minute, then one number per completed window, computed as
        /// (int)(perSecond * 60).
        /// </summary>
        [Test]
        public void EvalsPerMinuteReportsOncePerMinute()
        {
            double now = 0;
            var host = new EvolveHost();
            host.Clock = () => now;
            host.SetStore(new MemoryStore());
            host.Runner = new MainThreadChunkRunner { MaxEvalsPerTick = 2, BudgetMs = 60000 };
            host.Workers = 1;
            host.Threshold = KeepNothing;
            host.Start(TinyEdge, TinyEdge, TinyPopulation, 1, TinyMaxSteps, 3, 1000, lanes: 1, chunkEvals: 2);
            try
            {
                now = 30;
                host.Tick();
                Assert.AreEqual(0, host.Latest.EvalsPerMinute, "no rate until a full window is up");
                Assert.AreEqual(0, host.EvalsPerMinute);

                now = 60.5;
                host.Tick();
                int evals = host.Latest.Evals;
                int expected = (int)(evals / 60.5 * 60.0);
                Assert.AreEqual(expected, host.EvalsPerMinute);
                Assert.AreEqual(expected, host.Latest.EvalsPerMinute);

                // A second window measures only what happened inside it.
                int atWindowEnd = evals;
                now = 121.5;
                host.Tick();
                int second = (int)((host.Latest.Evals - atWindowEnd) / 61.0 * 60.0);
                Assert.AreEqual(second, host.EvalsPerMinute);
            }
            finally
            {
                StopHost(host);
            }
        }

        /// <summary>
        /// The status readout follows the session: idle before the first Start,
        /// suspended while the app holds chunks back, stopped once the lanes drain.
        /// </summary>
        [Test]
        public void StatusTracksTheSession()
        {
            var host = new EvolveHost();
            host.SetStore(new MemoryStore());
            host.Runner = new MainThreadChunkRunner { MaxEvalsPerTick = 2, BudgetMs = 60000 };
            host.Workers = 1;
            host.Threshold = KeepNothing;
            Assert.AreEqual(EvolveHost.EvolveStatus.Idle, host.Status);
            host.Start(TinyEdge, TinyEdge, TinyPopulation, 1, TinyMaxSteps, 4, 1000, lanes: 1, chunkEvals: 2);
            try
            {
                Assert.AreEqual(EvolveHost.EvolveStatus.Seeding, host.Status, "a population of 8 is not seeded in 2 evals");
                host.Suspend();
                Assert.AreEqual(EvolveHost.EvolveStatus.Suspended, host.Status);
                host.BrowserPaused = true;
                Assert.AreEqual(EvolveHost.EvolveStatus.BrowserPaused, host.Status);
                host.BrowserPaused = false;
                host.Resume();
                PumpTo(host, TinyPopulation + 2);
                Assert.AreEqual(EvolveHost.EvolveStatus.Suspended, host.Status, "the pump leaves the host held");
                host.Resume();
                Assert.AreEqual(EvolveHost.EvolveStatus.Running, host.Status, "seeding is over once the population is full");
            }
            finally
            {
                StopHost(host);
            }
            Assert.AreEqual(EvolveHost.EvolveStatus.Stopped, host.Status);
        }

        // ---- helpers -------------------------------------------------------------

        private static ChunkRunner NewRunner(string kind) =>
            kind == "threaded"
                ? (ChunkRunner)new ThreadedChunkRunner()
                // A budget far past any plausible evaluation, so MaxEvalsPerTick and
                // not the wall clock decides where a chunk ends: the vector is only
                // reproducible at an exact evaluation count.
                : new MainThreadChunkRunner { MaxEvalsPerTick = 4, BudgetMs = 60000 };

        /// <summary>Breed a short run on the inline runner, then leave the host held.</summary>
        private static void RunTiny(EvolveHost host, ulong seed, int evals)
        {
            host.Runner = new MainThreadChunkRunner { MaxEvalsPerTick = 4, BudgetMs = 60000 };
            host.Workers = 1;
            host.Start(TinyEdge, TinyEdge, TinyPopulation, 1, TinyMaxSteps, seed, 1000, lanes: 1, chunkEvals: 4);
            try
            {
                PumpTo(host, evals);
            }
            finally
            {
                StopHost(host);
            }
        }

        /// <summary>
        /// Drive the host to exactly <paramref name="targetEvals"/> evaluations.
        /// The host is held suspended throughout, so a chunk is launched only from
        /// a boundary the pump has already observed — which is what makes the count
        /// exact even though a worker chunk publishes on its own thread.
        /// </summary>
        private static void PumpTo(EvolveHost host, int targetEvals)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(PumpTimeoutSeconds);
            host.Suspend();
            while (true)
            {
                host.Tick(); // held: reaps and publishes, launches nothing
                int evals = host.Latest?.Evals ?? 0;
                if (!host.Busy)
                {
                    // Nothing is in flight, so the last publish was the settled
                    // between-chunks one: `evals` is a real chunk boundary, and the
                    // population it published is the whole population (a per-eval
                    // publish from a worker runs one entry short).
                    if (evals >= targetEvals)
                        return;
                    host.Resume();
                    host.Tick();
                    host.Suspend();
                }
                if (DateTime.UtcNow > deadline)
                    Assert.Fail($"the host stalled at {evals} of {targetEvals} evaluations");
                Thread.Sleep(2);
            }
        }

        private static void StopHost(EvolveHost host)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(PumpTimeoutSeconds);
            host.RequestStop();
            while (host.Running)
            {
                host.Tick();
                if (DateTime.UtcNow > deadline)
                    Assert.Fail("the host never came to a stop");
                Thread.Sleep(1);
            }
        }

        /// <summary>A distinct, well-formed rule string for entry <paramref name="index"/>.</summary>
        private static string Synthetic(int index) =>
            "0000-0000-0000-0000-0000-0000-0000-" + (index + 1).ToString("x4");

        private static void WriteLog(
            IAppStore store, int totalFound, IReadOnlyList<(string Rule, double Score)> finds)
        {
            var entries = new List<object>();
            foreach ((string rule, double score) in finds)
            {
                entries.Add(new Dictionary<string, object>
                {
                    ["rule"] = rule,
                    ["score"] = score,
                    ["foundAtEval"] = 1,
                    ["run"] = 1,
                });
            }
            store.WriteText(EvolveHost.StoreKey, AppJson.Write(new Dictionary<string, object>
            {
                ["schema"] = EvolveHost.Schema,
                ["totalFound"] = totalFound,
                ["finds"] = entries,
                ["deleted"] = new List<object>(),
            }));
            store.Flush();
        }

        private static List<string> Tombstones(IAppStore store)
        {
            var root = AppJson.Obj(AppJson.Parse(store.ReadText(EvolveHost.StoreKey)));
            var rules = new List<string>();
            foreach (object rule in AppJson.Arr(root["deleted"]))
                rules.Add(AppJson.Str(rule));
            return rules;
        }

        /// <summary>An <see cref="IAppStore"/> that never touches the user's disk.</summary>
        private sealed class MemoryStore : IAppStore
        {
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>();

            public bool Exists(string key) => _values.ContainsKey(key);

            public string ReadText(string key) => _values.TryGetValue(key, out string text) ? text : null;

            public void WriteText(string key, string text) => _values[key] = text;

            public void Delete(string key) => _values.Remove(key);

            public void Flush()
            {
            }

            public IEnumerable<string> Keys(string prefix)
            {
                foreach (KeyValuePair<string, string> pair in _values)
                {
                    if (pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                        yield return pair.Key;
                }
            }
        }
    }
}
