using System;
using System.Diagnostics;
using HeatonCA.Engine;
using NUnit.Framework;

namespace HeatonCA.Tests
{
    /// <summary>
    /// Engine throughput figures for the README: milliseconds per step of a 512x512
    /// soup, and milliseconds per 50x50 convergence run (one objective evaluation).
    /// The fixture is Explicit, so a plain EditMode run skips it; select it by name in
    /// the Test Runner or with <c>-testFilter HeatonCA.Tests.EngineBenchmarkTests</c>
    /// and read the numbers from the test output. Timings depend on the machine, so
    /// nothing here asserts on time; the only assertions are that the work happened.
    /// </summary>
    [Explicit("Timing report for the README; run by hand, never as part of a gate.")]
    [Category("Benchmark")]
    public class EngineBenchmarkTests
    {
        /// <summary>Lattice edge of the step benchmark (a large desktop simulator canvas).</summary>
        private const int SoupSize = 512;

        /// <summary>Steps timed after one untimed warm-up step.</summary>
        private const int SoupSteps = 30;

        /// <summary>Lattice edge of the evolve objective's convergence runs.</summary>
        private const int RunSize = 50;

        /// <summary>Soup seed of the timed convergence runs.</summary>
        private const ulong RunSeed = 11;

        /// <summary>Step cap of the timed convergence runs (the objective's default).</summary>
        private const int RunMaxSteps = MergeLifeObjective.MaxSteps;

        /// <summary>How many convergence runs to time.</summary>
        private const int RunRepeats = 3;

        [Test]
        public void StepThroughput512()
        {
            var sim = new MergeLife(MergeLife.DefaultRule, SoupSize, SoupSize);
            sim.SeedSoup(RunSeed);
            sim.Step(); // warm-up: first-touch page faults and JIT are not the engine's cost

            var watch = Stopwatch.StartNew();
            sim.Step(SoupSteps);
            watch.Stop();

            double totalMs = watch.Elapsed.TotalMilliseconds;
            double msPerStep = totalMs / SoupSteps;
            double stepsPerSecond = 1000.0 / msPerStep;
            TestContext.WriteLine(FormattableString.Invariant(
                $"MergeLife {SoupSize}x{SoupSize} (rule {MergeLife.DefaultRule}): {SoupSteps} steps in {totalMs:F1} ms -> {msPerStep:F2} ms/step ({stepsPerSecond:F1} steps/s)"));

            Assert.AreEqual(SoupSteps + 1, sim.Generation);
        }

        [Test]
        public void RunOnceThroughput50()
        {
            double totalMs = 0;
            for (int i = 0; i < RunRepeats; i++)
            {
                var watch = Stopwatch.StartNew();
                var stats = MergeLifeObjective.RunOnce(MergeLife.DefaultRule, RunSize, RunSize, RunSeed, RunMaxSteps);
                watch.Stop();

                double ms = watch.Elapsed.TotalMilliseconds;
                totalMs += ms;
                double msPerStep = ms / stats.Steps;
                TestContext.WriteLine(FormattableString.Invariant(
                    $"RunOnce {RunSize}x{RunSize} seed {RunSeed} cap {RunMaxSteps}, repeat {i + 1}/{RunRepeats}: {stats.Steps:R} steps in {ms:F1} ms ({msPerStep:F3} ms/step)"));

                Assert.Greater(stats.Steps, 0);
                Assert.LessOrEqual(stats.Steps, RunMaxSteps + 1);
            }

            double msPerEval = totalMs / RunRepeats;
            double evalsPerMinute = 60000.0 / msPerEval;
            TestContext.WriteLine(FormattableString.Invariant(
                $"RunOnce {RunSize}x{RunSize}: mean {msPerEval:F1} ms/eval over {RunRepeats} repeats ({evalsPerMinute:F1} evals/min single-threaded)"));
        }
    }
}
