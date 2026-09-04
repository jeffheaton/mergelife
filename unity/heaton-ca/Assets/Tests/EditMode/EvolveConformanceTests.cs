using System;
using System.Collections.Generic;
using System.IO;
using HeatonCA.Engine;
using NUnit.Framework;

namespace HeatonCA.Tests
{
    /// <summary>
    /// Evolve vectors: objective stats, GA operators, and the mini run (bit-exact).
    /// Ported from heaton-life-unity's EditMode suite; replays the vendored copies
    /// under Assets/Tests/Vectors~/evolve (objective-a07f-48, objective-redworld-48,
    /// operators-seeded, mini-run-24).
    /// </summary>
    public class EvolveConformanceTests
    {
        public static IEnumerable<TestCaseData> Cases()
        {
            foreach (string dir in Directory.GetDirectories(VectorPaths.Vendored("evolve")))
                yield return new TestCaseData(Path.GetFileName(dir))
                    .SetName($"Evolve({Path.GetFileName(dir)})");
        }

        [TestCaseSource(nameof(Cases))]
        public void Vector(string caseName)
        {
            string caseDir = VectorPaths.Vendored("evolve", caseName);
            var root = J.LoadCase(caseDir);
            Assert.AreEqual("bit-exact", J.Str(root["tier"]));
            var p = J.Obj(root["params"]);
            switch (J.Str(root["kind"]))
            {
                case "objective":
                    RunObjective(caseDir, root, p);
                    break;
                case "operators":
                    RunOperators(root, p);
                    break;
                case "run":
                    RunMiniEvolution(caseDir, root, p);
                    break;
                default:
                    Assert.Fail($"unknown evolve kind in {caseName}");
                    break;
            }
        }

        private static void RunObjective(
            string caseDir, Dictionary<string, object> root, Dictionary<string, object> p)
        {
            Assert.AreEqual("paper", J.Str(p["objective"]));
            string genome = J.Str(p["genome"]);
            int width = J.Int(p["width"]);
            int height = J.Int(p["height"]);
            int cycles = J.Int(p["cycles"]);
            ulong seed = (ulong)J.Num(p["seed"]);
            int maxSteps = J.Int(p["max_steps"]);

            var outputs = J.Obj(root["outputs"]);
            double[] expectedRuns = VectorPaths.ReadF64(
                Path.Combine(caseDir, J.Str(J.Obj(outputs["runs"])["file"])));
            Assert.AreEqual(cycles * 6, expectedRuns.Length);

            double maxScore = double.NegativeInfinity;
            double totalSteps = 0;
            for (int i = 0; i < cycles; i++)
            {
                var stats = MergeLifeObjective.RunOnce(genome, width, height, seed + (ulong)i, maxSteps);
                double score = MergeLifeObjective.ScoreStats(stats, MergeLifeObjective.PaperObjective);
                double[] row =
                {
                    stats.Steps, stats.Foreground, stats.Active, stats.Rect, stats.Mage, score,
                };
                for (int c = 0; c < 6; c++)
                    Assert.IsTrue(
                        row[c].Equals(expectedRuns[i * 6 + c]),
                        $"cycle {i} column {c}: {row[c]:R} != {expectedRuns[i * 6 + c]:R}");
                maxScore = Math.Max(maxScore, score);
                totalSteps += stats.Steps;
            }
            double[] expectedScore = VectorPaths.ReadF64(
                Path.Combine(caseDir, J.Str(J.Obj(outputs["score"])["file"])));
            Assert.AreEqual(expectedScore[0], maxScore);
            Assert.AreEqual(expectedScore[1], totalSteps);
        }

        private static void RunOperators(Dictionary<string, object> root, Dictionary<string, object> p)
        {
            var expected = J.Obj(root["expected"]);
            string genome = J.Str(p["genome"]);

            var rng = new Pcg32((ulong)J.Num(p["mutate_seed"]));
            string current = genome;
            foreach (object m in J.Arr(expected["mutations"]))
            {
                current = GaOperators.Mutate(current, rng);
                Assert.AreEqual(J.Str(m), current);
            }

            rng = new Pcg32((ulong)J.Num(p["crossover_seed"]));
            string parent2 = J.Str(p["parent2"]);
            foreach (object pair in J.Arr(expected["crossovers"]))
            {
                var (first, second) = GaOperators.Crossover(genome, parent2, rng);
                Assert.AreEqual(J.Str(J.Arr(pair)[0]), first);
                Assert.AreEqual(J.Str(J.Arr(pair)[1]), second);
            }

            rng = new Pcg32((ulong)J.Num(p["tournament_seed"]));
            int rounds = J.Int(p["tournament_rounds"]);
            var scores = new List<double>();
            foreach (object s in J.Arr(p["tournament_scores"]))
                scores.Add(J.Num(s));
            foreach (object winner in J.Arr(expected["winners_best"]))
                Assert.AreEqual(J.Int(winner), GaOperators.TournamentSelect(scores, rounds, rng));
            foreach (object winner in J.Arr(expected["winners_worst"]))
                Assert.AreEqual(
                    J.Int(winner), GaOperators.TournamentSelect(scores, rounds, rng, worst: true));
        }

        private static void RunMiniEvolution(
            string caseDir, Dictionary<string, object> root, Dictionary<string, object> p)
        {
            Assert.AreEqual("paper", J.Str(p["objective"]));
            // Serial and parallel evaluation must both match the vectors
            // (spec/evolve.md "Parallel evaluation") — proven under Unity's runtime.
            foreach (int workers in new[] { 1, 5 })
            {
                var evolver = new Evolver(
                    J.Int(p["width"]),
                    J.Int(p["height"]),
                    J.Int(p["population_size"]),
                    J.Num(p["crossover_rate"]),
                    J.Int(p["tournament_rounds"]),
                    J.Int(p["eval_cycles"]),
                    J.Int(p["patience"]),
                    J.Int(p["max_steps"]),
                    (ulong)J.Num(p["seed"]),
                    workers: workers);
                var best = evolver.Run(J.Int(p["max_evals"]));

                var expected = J.Obj(root["expected"]);
                Assert.AreEqual(J.Str(expected["best_genome"]), best.Genome);
                Assert.AreEqual(J.Int(expected["evals"]), evolver.Evals);
                var expectedPopulation = J.Arr(expected["population"]);
                Assert.AreEqual(expectedPopulation.Count, evolver.Population.Count);
                for (int i = 0; i < evolver.Population.Count; i++)
                    Assert.AreEqual(J.Str(expectedPopulation[i]), evolver.Population[i].Genome);
                double[] expectedBest = VectorPaths.ReadF64(
                    Path.Combine(caseDir, J.Str(J.Obj(expected["best_score"])["file"])));
                Assert.AreEqual(expectedBest[0], best.Score);
            }
        }
    }
}
