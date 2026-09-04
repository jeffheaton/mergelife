using System;
using System.Threading;
using HeatonCA.Engine;
using HeatonCAApp;
using NUnit.Framework;
using UnityEngine;

namespace HeatonCA.Tests
{
    /// <summary>
    /// The evolve platform seam: the inline runner's frame budget (it must always
    /// make progress, and must never blow past its cap), the worker runner's
    /// hand-off, and the arithmetic that sizes a search to the machine.
    /// </summary>
    public class EvolveSchedulerTests
    {
        /// <summary>A budget no single evaluation of this size can exhaust.</summary>
        private const double UnlimitedBudgetMs = 60000;

        private const double PollTimeoutSeconds = 30;

        /// <summary>The cap is a hard ceiling on the work one launch does.</summary>
        [Test]
        public void MainThreadRunnerStopsAtMaxEvalsPerTick()
        {
            var runner = new MainThreadChunkRunner { MaxEvalsPerTick = 3, BudgetMs = UnlimitedBudgetMs };
            Lane lane = NewLane();
            runner.BeginTick();
            runner.Launch(lane, 100);
            Assert.AreEqual(3, lane.Evolver.Evals);
        }

        /// <summary>
        /// A machine too slow to finish one evaluation inside a frame must still
        /// finish evaluations: an exhausted budget costs a long frame, never a
        /// livelocked search.
        /// </summary>
        [Test]
        public void MainThreadRunnerAlwaysMakesProgress()
        {
            var runner = new MainThreadChunkRunner { BudgetMs = 0 };
            Lane lane = NewLane();
            runner.BeginTick();
            runner.Launch(lane, 100);
            Assert.AreEqual(1, lane.Evolver.Evals, "a spent budget still buys one evaluation");

            var capped = new MainThreadChunkRunner { MaxEvalsPerTick = 0, BudgetMs = UnlimitedBudgetMs };
            Lane other = NewLane();
            capped.BeginTick();
            capped.Launch(other, 100);
            Assert.AreEqual(1, other.Evolver.Evals, "so does a cap of zero");
        }

        /// <summary>An inline launch is already finished by the time it returns.</summary>
        [Test]
        public void MainThreadRunnerPollCompletesImmediately()
        {
            var runner = new MainThreadChunkRunner { MaxEvalsPerTick = 1, BudgetMs = UnlimitedBudgetMs };
            Lane lane = NewLane();
            runner.BeginTick();
            runner.Launch(lane, 100);
            Assert.IsTrue(runner.Poll(lane, out Exception fault));
            Assert.IsNull(fault);
            Assert.IsFalse(lane.ChunkInFlight);
            Assert.IsFalse(runner.PublishesPerEval, "an inline runner publishes per launch, not per eval");
        }

        /// <summary>A launch that never reaches its target does nothing at all.</summary>
        [Test]
        public void MainThreadRunnerIgnoresATargetAlreadyMet()
        {
            var runner = new MainThreadChunkRunner { BudgetMs = UnlimitedBudgetMs };
            Lane lane = NewLane();
            runner.BeginTick();
            runner.Launch(lane, 2);
            Assert.AreEqual(2, lane.Evolver.Evals);
            runner.Launch(lane, 2);
            Assert.AreEqual(2, lane.Evolver.Evals);
        }

        /// <summary>The worker runner reaches its target and reports the lane idle afterwards.</summary>
        [Test]
        public void ThreadedRunnerReachesItsTarget()
        {
            var runner = new ThreadedChunkRunner();
            Lane lane = NewLane();
            runner.Launch(lane, ThreadedChunkRunner.ChunkEvals);
            Assert.IsTrue(lane.ChunkInFlight);
            DateTime deadline = DateTime.UtcNow.AddSeconds(PollTimeoutSeconds);
            Exception fault;
            while (!runner.Poll(lane, out fault))
            {
                if (DateTime.UtcNow > deadline)
                    Assert.Fail("the worker chunk never completed");
                Thread.Sleep(1);
            }
            Assert.IsNull(fault);
            Assert.IsFalse(lane.ChunkInFlight);
            Assert.AreEqual(ThreadedChunkRunner.ChunkEvals, lane.Evolver.Evals);
            Assert.IsTrue(runner.PublishesPerEval);
        }

        /// <summary>
        /// Lanes per the house formula: two per cycle-budget's worth of workers,
        /// never fewer than one and never more than the worker count.
        /// </summary>
        [Test]
        public void DefaultLanesFollowsTheWorkerBudget()
        {
            Assert.AreEqual(1, EvolveScheduler.DefaultLanes(1, 5), "a browser's single worker gets one lane");
            Assert.AreEqual(3, EvolveScheduler.DefaultLanes(8, 5));
            Assert.AreEqual(6, EvolveScheduler.DefaultLanes(16, 5));
            Assert.AreEqual(4, EvolveScheduler.DefaultLanes(4, 1), "lanes never outnumber workers");
            Assert.AreEqual(1, EvolveScheduler.DefaultLanes(0, 5), "there is always at least one lane");
            Assert.AreEqual(8, EvolveScheduler.DefaultLanes(8, 0), "a zero cycle budget divides by one");
        }

        /// <summary>The editor is a desktop: every core but one, and never fewer than one.</summary>
        [Test]
        public void DefaultWorkersLeavesTheMachineACore()
        {
            int workers = EvolveScheduler.DefaultWorkers();
            Assert.GreaterOrEqual(workers, 1);
            Assert.AreEqual(Mathf.Max(1, SystemInfo.processorCount - 1), workers);
        }

        /// <summary>The editor runs the worker runner; only a WebGL player runs inline.</summary>
        [Test]
        public void CreateRunnerPicksTheThreadedRunnerInTheEditor()
        {
            Assert.IsInstanceOf<ThreadedChunkRunner>(EvolveScheduler.CreateRunner());
        }

        private static Lane NewLane() =>
            new Lane
            {
                Run = 1,
                Active = true,
                Evolver = new Evolver(
                    width: 24,
                    height: 24,
                    populationSize: 8,
                    crossoverRate: EvolveHost.CrossoverRate,
                    tournamentRounds: 3,
                    evalCycles: 1,
                    patience: 1000,
                    maxSteps: 120,
                    seed: 123,
                    workers: 1),
            };
    }
}
