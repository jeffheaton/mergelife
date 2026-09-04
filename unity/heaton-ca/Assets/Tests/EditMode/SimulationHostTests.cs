using HeatonCA.Engine;
using HeatonCAApp;
using NUnit.Framework;

namespace HeatonCA.Tests
{
    /// <summary>
    /// The transport. What matters is that simulated time tracks real time
    /// (PyQt ran one generation per timer tick, so its speed was really the
    /// frame rate), that a lattice too big to keep up degrades to slower
    /// playback instead of a frozen frame, and that the backlog is dropped
    /// rather than carried — a host that owed 300 steps after a stall would
    /// spend the next second catching up instead of animating.
    ///
    /// Driven by a counting fake rather than a real MergeLife world: this is
    /// scheduling arithmetic, and a fake makes the step counts exact.
    /// </summary>
    public class SimulationHostTests
    {
        /// <summary>Counts what the host asks for; the engine's own behavior is tested elsewhere.</summary>
        private sealed class FakeSim : ISimulation
        {
            public FakeSim(int width, int height)
            {
                Width = width;
                Height = height;
            }

            public int Width { get; }

            public int Height { get; }

            public int Generation { get; private set; }

            public int StepCalls { get; private set; }

            public int ResetCalls { get; private set; }

            public void Step(int n = 1)
            {
                StepCalls++;
                Generation += n;
            }

            public void Reset(uint? seed = null)
            {
                ResetCalls++;
                Generation = 0;
            }
        }

        /// <summary>A host playing a small world at 30 gen/s, the app defaults.</summary>
        private static SimulationHost Playing(int width = 40, int height = 30)
        {
            var host = new SimulationHost();
            host.Load(new FakeSim(width, height));
            return host;
        }

        [Test]
        public void AFreshHostIsEmptyAndStopped()
        {
            var host = new SimulationHost();
            Assert.IsNull(host.Sim);
            Assert.IsFalse(host.Playing);
            Assert.IsFalse(host.Dirty);
            Assert.AreEqual(0, host.Generation);
            Assert.AreEqual(SimulationHost.DefaultStepsPerSecond, host.StepsPerSecond, 1e-9);
            // Ticking an empty host is a no-op, not a crash.
            host.Tick(1.0);
            host.StepOnce();
            Assert.AreEqual(0, host.Generation);
        }

        [Test]
        public void LoadStartsPlayingAndMarksTheFrameDirty()
        {
            var host = new SimulationHost();
            var sim = new FakeSim(10, 10);
            host.Load(sim);
            Assert.AreSame(sim, host.Sim);
            Assert.IsTrue(host.Playing);
            Assert.IsTrue(host.Dirty);
            // Loading paused is how the Simulator opens without auto-starting.
            host.Load(new FakeSim(10, 10), false);
            Assert.IsFalse(host.Playing);
            Assert.IsTrue(host.Dirty);
        }

        [Test]
        public void UnloadDropsTheWorldAndTheTransport()
        {
            SimulationHost host = Playing();
            host.Unload();
            Assert.IsNull(host.Sim);
            Assert.IsFalse(host.Playing);
            Assert.IsFalse(host.Dirty);
            Assert.AreEqual(0, host.Generation);
        }

        [Test]
        public void SimulatedTimeTracksRealTime()
        {
            SimulationHost host = Playing();
            host.StepsPerSecond = 30;
            host.Tick(0.1);
            Assert.AreEqual(3, host.Generation);
            host.Tick(0.1);
            Assert.AreEqual(6, host.Generation);
            // A second of real time is 30 generations however it is sliced.
            for (int i = 0; i < 20; i++)
            {
                host.Tick(0.05);
            }
            Assert.AreEqual(36, host.Generation);
        }

        [Test]
        public void FractionalStepsAccumulateInsteadOfBeingLost()
        {
            SimulationHost host = Playing();
            host.StepsPerSecond = 10;
            host.Dirty = false; // the load's first blit is already accounted for
            // 0.05 s is half a step: nothing runs, but the remainder is kept.
            host.Tick(0.05);
            Assert.AreEqual(0, host.Generation);
            Assert.IsFalse(host.Dirty, "no step means no re-blit");
            host.Tick(0.05);
            Assert.AreEqual(1, host.Generation);
            Assert.IsTrue(host.Dirty);
        }

        [Test]
        public void PausedAndNonPositiveTicksDoNothing()
        {
            SimulationHost host = Playing();
            host.Playing = false;
            host.Tick(1.0);
            Assert.AreEqual(0, host.Generation);
            host.Playing = true;
            host.Tick(0.0);
            host.Tick(-1.0);
            Assert.AreEqual(0, host.Generation);
        }

        [Test]
        public void StepOnceAdvancesOneGenerationEvenWhilePaused()
        {
            SimulationHost host = Playing();
            host.Playing = false;
            host.Dirty = false;
            host.StepOnce();
            Assert.AreEqual(1, host.Generation);
            Assert.IsTrue(host.Dirty);
            host.StepOnce();
            Assert.AreEqual(2, host.Generation);
        }

        [Test]
        public void StepsPerSecondIsClampedToTheSettingsRange()
        {
            var host = new SimulationHost();
            host.StepsPerSecond = 0;
            Assert.AreEqual(SimulationHost.MinStepsPerSecond, host.StepsPerSecond, 1e-9);
            host.StepsPerSecond = -100;
            Assert.AreEqual(SimulationHost.MinStepsPerSecond, host.StepsPerSecond, 1e-9);
            host.StepsPerSecond = 0.5;
            Assert.AreEqual(SimulationHost.MinStepsPerSecond, host.StepsPerSecond, 1e-9);
            host.StepsPerSecond = 1000;
            Assert.AreEqual(SimulationHost.MaxStepsPerSecond, host.StepsPerSecond, 1e-9);
            host.StepsPerSecond = 45;
            Assert.AreEqual(45, host.StepsPerSecond, 1e-9);
            // A NaN (a corrupt setting, a divide that went wrong upstream) must
            // not poison the accumulator into never stepping again.
            host.StepsPerSecond = double.NaN;
            Assert.AreEqual(SimulationHost.DefaultStepsPerSecond, host.StepsPerSecond, 1e-9);
        }

        [Test]
        public void StepsPerTickCapFollowsTheCellBudget()
        {
            // 1,000,000 cells: one step is already most of the budget.
            Assert.AreEqual(1, Playing(1000, 1000).StepsPerTickCap);
            // 250,000 cells: 1,500,000 / 250,000 = 6.
            Assert.AreEqual(6, Playing(500, 500).StepsPerTickCap);
            // A small lattice is capped by the flat ceiling, not the budget.
            Assert.AreEqual(SimulationHost.MaxStepsPerTick, Playing(40, 30).StepsPerTickCap);
            // A lattice larger than the whole budget still advances.
            Assert.AreEqual(1, Playing(4000, 4000).StepsPerTickCap);
            // No world loaded: the cap is meaningless but must be sane.
            Assert.AreEqual(1, new SimulationHost().StepsPerTickCap);
        }

        [Test]
        public void ABigLatticePlaysSlowerRatherThanFreezing()
        {
            // 1M cells caps at one step per tick, so a full second of owed work
            // becomes one step and the rest is dropped.
            SimulationHost host = Playing(1000, 1000);
            host.StepsPerSecond = 60;
            host.Tick(1.0);
            Assert.AreEqual(1, host.Generation);
            // The backlog is gone: the next tick starts from zero, so playback
            // is merely slow instead of spending every frame catching up.
            host.Tick(0.01);
            Assert.AreEqual(1, host.Generation);
            host.Tick(1.0);
            Assert.AreEqual(2, host.Generation);
        }

        [Test]
        public void ALongStallDropsTheBacklogInOneCappedTick()
        {
            // A garbage collection, a rotation, a resumed app: one huge delta.
            SimulationHost host = Playing(40, 30);
            host.StepsPerSecond = 60;
            host.Tick(5.0); // 300 steps owed, 12 allowed
            Assert.AreEqual(SimulationHost.MaxStepsPerTick, host.Generation);
            var sim = (FakeSim)host.Sim;
            Assert.AreEqual(1, sim.StepCalls, "the capped steps run as one batched call");
            // Nothing carried over: the next ordinary tick runs its own steps only.
            host.Tick(0.1);
            Assert.AreEqual(SimulationHost.MaxStepsPerTick + 6, host.Generation);
        }

        [Test]
        public void LoadingANewWorldClearsTheAccumulator()
        {
            SimulationHost host = Playing();
            host.StepsPerSecond = 10;
            host.Tick(0.09); // 0.9 of a step banked
            Assert.AreEqual(0, host.Generation);
            host.Load(new FakeSim(40, 30));
            host.Tick(0.09); // would step if the 0.9 had carried over
            Assert.AreEqual(0, host.Generation);
        }

        [Test]
        public void GenerationReportsTheLoadedWorld()
        {
            SimulationHost host = Playing();
            host.StepOnce();
            host.StepOnce();
            Assert.AreEqual(host.Sim.Generation, host.Generation);
            Assert.AreEqual(2, host.Generation);
            host.Unload();
            Assert.AreEqual(0, host.Generation);
        }

        [Test]
        public void DirtyIsSetByTheHostAndClearedByTheConsumer()
        {
            SimulationHost host = Playing();
            Assert.IsTrue(host.Dirty, "a freshly loaded world needs its first blit");
            host.Dirty = false;
            host.Tick(0.5);
            Assert.IsTrue(host.Dirty);
            host.Dirty = false;
            host.Tick(0.0);
            Assert.IsFalse(host.Dirty);
        }
    }
}
