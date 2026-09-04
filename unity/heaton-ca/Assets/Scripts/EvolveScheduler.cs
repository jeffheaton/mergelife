using System;
using System.Threading.Tasks;
using HeatonCA.Engine;
using UnityEngine;

namespace HeatonCAApp
{
    /// <summary>
    /// One concurrent restart-run of the evolver: its own <see cref="Engine.Evolver"/>
    /// (one PCG32 stream, so the run replays exactly), whatever the chunk runner
    /// needs to track its in-flight work, and the run's entry in the finds log.
    /// <see cref="EvolveHost"/> owns the lane; the <see cref="ChunkRunner"/> owns
    /// <see cref="Chunk"/>, <see cref="ChunkInFlight"/>, and <see cref="Fault"/>.
    /// </summary>
    public sealed class Lane
    {
        /// <summary>This lane's run. Replaced wholesale when the run converges.</summary>
        public Evolver Evolver;

        /// <summary>The worker task advancing this lane, or null (threaded runner only).</summary>
        public Task Chunk;

        /// <summary>True between <see cref="ChunkRunner.Launch"/> and the poll that reaps it.</summary>
        public bool ChunkInFlight;

        /// <summary>What the chunk threw, handed to the host by the next poll.</summary>
        public Exception Fault;

        /// <summary>Run number (1-based, unique across the whole search).</summary>
        public int Run;

        /// <summary>False once the host has idled the lane for good (a stop, or a fault).</summary>
        public bool Active;

        /// <summary>This run's entry in the finds log, upgraded in place. Guarded by the host's lock.</summary>
        public EvolveHost.Discovery Discovery;

        /// <summary>This lane's own published view. Guarded by the host's lock.</summary>
        public EvolveHost.Snapshot LaneState;
    }

    /// <summary>
    /// How a lane's evaluations actually run. The GA is deterministic AND resumable
    /// — <c>Run(n)</c> then <c>Run(n + k)</c> continues the same PCG32 stream — so
    /// evolution advances in small chunks with clean stop points in between, and
    /// where those chunks execute is a platform decision, not a GA one. Desktop and
    /// mobile hand each chunk to a worker task; WebGL has no threads it can block
    /// on, so it runs evaluations inline under a frame budget.
    /// </summary>
    public abstract class ChunkRunner
    {
        /// <summary>
        /// Advance <paramref name="lane"/> toward <paramref name="targetEvals"/>
        /// total evaluations. May return before the target is reached; the host
        /// simply launches again from wherever the evolver got to.
        /// </summary>
        public abstract void Launch(Lane lane, int targetEvals);

        /// <summary>
        /// True when the lane has no work in flight and may be launched again.
        /// <paramref name="fault"/> carries what the chunk threw, or null.
        /// </summary>
        public abstract bool Poll(Lane lane, out Exception fault);

        /// <summary>
        /// Called once at the top of every <see cref="EvolveHost.Tick"/>, before any
        /// lane is polled — where a runner with a per-frame budget resets it.
        /// </summary>
        public virtual void BeginTick()
        {
        }

        /// <summary>
        /// True when the evolver's per-evaluation callback is a sensible publish
        /// point. Worker chunks publish per evaluation (the main thread reads the
        /// snapshot whenever it likes); an inline runner is already on the main
        /// thread and holding it, so it publishes once per <see cref="Launch"/>
        /// instead of once per evaluation.
        /// </summary>
        public virtual bool PublishesPerEval => true;
    }

    /// <summary>
    /// Chunks on worker tasks: the desktop and mobile runner. This is the one place
    /// in the app assembly that calls <c>Task.Run</c> — everything else that wants
    /// work off the main thread goes through a lane.
    /// </summary>
    public sealed class ThreadedChunkRunner : ChunkRunner
    {
        /// <summary>
        /// Evaluations per chunk. Small: a chunk is the granularity at which the
        /// host can stop, restart a converged run, or notice a fault, and four
        /// evaluations is short enough that Stop feels immediate even when a
        /// treasure is taking 1001 steps to die.
        /// </summary>
        public const int ChunkEvals = 4;

        /// <inheritdoc />
        public override void Launch(Lane lane, int targetEvals)
        {
            Evolver evolver = lane.Evolver;
            lane.ChunkInFlight = true;
            lane.Chunk = Task.Run(() => evolver.Run(targetEvals));
        }

        /// <inheritdoc />
        public override bool Poll(Lane lane, out Exception fault)
        {
            fault = lane.Fault;
            lane.Fault = null;
            Task chunk = lane.Chunk;
            if (chunk == null)
            {
                lane.ChunkInFlight = false;
                return true;
            }
            if (!chunk.IsCompleted)
                return false;
            lane.Chunk = null;
            lane.ChunkInFlight = false;
            if (chunk.IsFaulted)
                fault = chunk.Exception;
            return true;
        }
    }

    /// <summary>
    /// Chunks inline, under a frame budget: the WebGL runner, where the player is
    /// single-threaded and a blocked main thread is a frozen page. Each launch runs
    /// evaluations until the budget is spent — but always at least one, so the
    /// search cannot livelock on a machine too slow to finish an evaluation inside
    /// a frame. One MergeLife evaluation is atomic (the GA resumes only at
    /// evaluation boundaries), so a "treasure" — <c>evalCycles</c> cycles of up to
    /// <c>maxSteps</c> steps — costs one long frame no budget can subdivide.
    /// </summary>
    public sealed class MainThreadChunkRunner : ChunkRunner
    {
        /// <summary>Frame budget in milliseconds; the default leaves room for the UI at 60 fps.</summary>
        public const double DefaultBudgetMs = 8;

        private readonly System.Diagnostics.Stopwatch _budget = new System.Diagnostics.Stopwatch();

        /// <summary>Milliseconds of evaluation allowed per tick, across all lanes.</summary>
        public double BudgetMs { get; set; } = DefaultBudgetMs;

        /// <summary>
        /// Cap on evaluations per launch, whatever the budget says. Unlimited by
        /// default — the budget is the real governor; the cap exists so a test can
        /// pin the work a launch does. A crossover admission scores two children in
        /// one step, so a launch can land one evaluation past the cap; it can never
        /// keep going after it.
        /// </summary>
        public int MaxEvalsPerTick { get; set; } = int.MaxValue;

        /// <inheritdoc />
        public override bool PublishesPerEval => false;

        /// <inheritdoc />
        public override void BeginTick() => _budget.Restart();

        /// <inheritdoc />
        public override void Launch(Lane lane, int targetEvals)
        {
            Evolver evolver = lane.Evolver;
            if (evolver.Evals >= targetEvals)
                return;
            if (!_budget.IsRunning)
                _budget.Restart();
            lane.ChunkInFlight = true;
            int done = 0;
            try
            {
                do
                {
                    // One step of the GA's admission loop, which is what Run(n + 1)
                    // is: the same iterations, in the same order, off the same PCG32
                    // stream a worker chunk would have run.
                    int before = evolver.Evals;
                    evolver.Run(before + 1);
                    if (evolver.Evals == before)
                        break; // the run has gone inert: patience is spent
                    done += evolver.Evals - before;
                }
                while (evolver.Evals < targetEvals
                       && done < MaxEvalsPerTick
                       && _budget.Elapsed.TotalMilliseconds < BudgetMs);
            }
            catch (Exception exception)
            {
                lane.Fault = exception;
            }
        }

        /// <inheritdoc />
        public override bool Poll(Lane lane, out Exception fault)
        {
            // Launch ran to completion inline, so a lane is never in flight across
            // ticks; a fault it caught surfaces here, one tick later.
            fault = lane.Fault;
            lane.Fault = null;
            lane.ChunkInFlight = false;
            return true;
        }
    }

    /// <summary>
    /// Platform seam for the evolver: which runner executes chunks, how many worker
    /// threads an evaluation may use, and how many restart-runs breed at once. The
    /// only <c>#if UNITY_WEBGL</c> in the evolve stack lives here.
    /// </summary>
    public static class EvolveScheduler
    {
        /// <summary>The runner this platform can actually use.</summary>
        public static ChunkRunner CreateRunner()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new MainThreadChunkRunner();
#else
            return new ThreadedChunkRunner();
#endif
        }

        /// <summary>
        /// Worker threads one evaluation may fan out over. The objective's cycle
        /// runs parallelize bit-exactly (spec/evolve.md "Parallel evaluation"), so
        /// this only trades heat for latency: the browser gets one (no threads),
        /// a phone gets half its cores capped at four (thermals and the UI), a
        /// desktop keeps one core free for everything else.
        /// </summary>
        public static int DefaultWorkers()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return 1;
#else
            return Application.isMobilePlatform
                ? Mathf.Clamp(SystemInfo.processorCount / 2, 1, 4)
                : Mathf.Max(1, SystemInfo.processorCount - 1);
#endif
        }

        /// <summary>
        /// Concurrent restart-runs for a machine with <paramref name="workers"/>
        /// workers evaluating <paramref name="evalCycles"/> cycles per candidate.
        /// A run's evaluation can use at most <paramref name="evalCycles"/> threads,
        /// and cycle lengths are skewed (duds die in ~101 steps, treasures run
        /// 1001), so a lane averages roughly half its cycle budget: two lanes per
        /// cycle-budget's worth of workers fills the machine without drowning it,
        /// and a phone's small worker budget keeps lanes at one or two. WebGL needs
        /// no special case: its single worker collapses this to one lane, which is
        /// what a single-threaded player wants anyway.
        /// </summary>
        public static int DefaultLanes(int workers, int evalCycles) =>
            Mathf.Clamp(2 * workers / Mathf.Max(1, evalCycles), 1, Mathf.Max(1, workers));
    }
}
