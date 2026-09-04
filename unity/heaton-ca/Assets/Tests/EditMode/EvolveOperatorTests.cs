using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HeatonCA.Engine;
using NUnit.Framework;

namespace HeatonCA.Tests
{
    /// <summary>
    /// Unit pins for the evolve side of the engine, ported from heaton-life's
    /// EvolveTests.cs: the largest-rectangle statistic, the three GA operators
    /// (paper Sec. 5), the deterministic objective, and the reference defaults
    /// of the evolve entry points. The seeded operator replays and the mini run
    /// are vector-gated in EvolveConformanceTests; these hold the properties the
    /// vectors cannot state.
    /// </summary>
    public class EvolveOperatorTests
    {
        private const string RedWorld = "e542-5f79-9341-f31e-6c6b-7f08-8773-7068";
        private const string PenAndInk = "a07f-c000-0000-0000-0000-0000-ff80-807f";

        // --- reference defaults -----------------------------------------------------

        /// <summary>
        /// The evolve entry points must offer the same defaults as the Python
        /// reference, where Evolver() and score_genome(genome) both work with no
        /// further arguments (evolve/ga.py, evolve/objective.py). Checked by
        /// reflection rather than by running an evaluation: the declared default
        /// is the contract, and a full scoring run at the default 100x100 x 5
        /// cycles x 1000 steps is far too slow for a unit test. Parameter ORDER is
        /// deliberately not aligned with Python (that would move `objective` to
        /// first and break every caller for cosmetics).
        /// </summary>
        [Test]
        public void EvolverConstructorCarriesTheReferenceDefaults()
        {
            ConstructorInfo[] ctors = typeof(Evolver).GetConstructors();
            Assert.AreEqual(1, ctors.Length, "one public constructor");
            ParameterInfo[] parameters = ctors[0].GetParameters();
            foreach (ParameterInfo p in parameters)
                Assert.IsTrue(p.IsOptional, $"Evolver({p.Name}) should have a default");
            AssertDefaults(parameters, new Dictionary<string, object>
            {
                ["width"] = 100,
                ["height"] = 100, // python size=(100, 100)
                ["populationSize"] = 100,
                ["crossoverRate"] = 0.75,
                ["tournamentRounds"] = 5,
                ["evalCycles"] = 5,
                ["patience"] = 1000,
                ["maxSteps"] = 1000,
                ["seed"] = 0UL,
                ["workers"] = 1,
            }, "Evolver(..)");

            // And the constructed object reports them (construction runs nothing).
            var evolver = new Evolver();
            Assert.AreEqual(100, evolver.Width);
            Assert.AreEqual(100, evolver.Height);
            Assert.AreEqual(100, evolver.PopulationSize);
            Assert.AreEqual(0.75, evolver.CrossoverRate);
            Assert.AreEqual(5, evolver.TournamentRounds);
            Assert.AreEqual(5, evolver.EvalCycles);
            Assert.AreEqual(1000, evolver.Patience);
            Assert.AreEqual(1000, evolver.MaxSteps);
            Assert.AreEqual(0UL, evolver.Seed);
            Assert.AreEqual(1, evolver.Workers);
            Assert.AreEqual(0, evolver.Evals);
            Assert.AreEqual(0, evolver.Population.Count);
            Assert.IsNull(evolver.Best);
        }

        [Test]
        public void ScoreGenomeCarriesTheReferenceDefaults()
        {
            MethodInfo score = typeof(MergeLifeObjective).GetMethod(nameof(MergeLifeObjective.ScoreGenome));
            Assert.IsNotNull(score);
            ParameterInfo[] parameters = score.GetParameters();
            AssertDefaults(parameters, new Dictionary<string, object>
            {
                ["cycles"] = 5,
                ["width"] = 100,
                ["height"] = 100,
                ["seed"] = 0UL,
                ["maxSteps"] = 1000,
                ["workers"] = 1,
            }, "ScoreGenome(..)");

            // Only the genome is required, exactly as in Python.
            foreach (ParameterInfo p in parameters)
            {
                if (p.Name == "genome")
                    Assert.IsFalse(p.IsOptional, "genome is required");
                else
                    Assert.IsTrue(p.IsOptional, $"ScoreGenome({p.Name}) should be optional");
            }
            Assert.AreEqual(1000, MergeLifeObjective.MaxSteps, "the default cap constant");
        }

        private static void AssertDefaults(
            ParameterInfo[] parameters, Dictionary<string, object> expected, string what)
        {
            foreach (ParameterInfo p in parameters)
            {
                if (!expected.TryGetValue(p.Name, out object want))
                    continue;
                Assert.IsTrue(p.IsOptional, $"{what}: {p.Name} should have a default");
                Assert.AreEqual(want, p.DefaultValue, $"{what}: {p.Name}");
                Assert.AreEqual(want.GetType(), p.ParameterType, $"{what}: {p.Name} type");
                expected.Remove(p.Name);
            }
            Assert.AreEqual(0, expected.Count, $"{what}: never saw {string.Join(", ", expected.Keys)}");
        }

        // --- largest rectangle ------------------------------------------------------

        [Test]
        public void LargestRectangleKnownCases()
        {
            bool[] mask =
            {
                true, true, true,
                true, true, false,
                true, false, false,
            };
            Assert.AreEqual(4, MergeLifeObjective.LargestRectangleArea(mask, 3, 3)); // 2x2 top-left
            Assert.AreEqual(15, MergeLifeObjective.LargestRectangleArea(
                Enumerable.Repeat(true, 15).ToArray(), 5, 3));
            Assert.AreEqual(0, MergeLifeObjective.LargestRectangleArea(new bool[15], 5, 3));
            bool[] ragged =
            {
                true, false, true, true, true,
                true, true, true, true, false,
                false, true, true, true, false,
            };
            Assert.AreEqual(6, MergeLifeObjective.LargestRectangleArea(ragged, 5, 3));
            // A single column and a single row are rectangles too.
            Assert.AreEqual(3, MergeLifeObjective.LargestRectangleArea(new[] { true, true, true }, 1, 3));
            Assert.AreEqual(3, MergeLifeObjective.LargestRectangleArea(new[] { true, true, true }, 3, 1));
        }

        [Test]
        public void LargestRectangleMatchesBruteForce()
        {
            var rng = new Pcg32(1);
            const int h = 7, w = 9;
            for (int trial = 0; trial < 20; trial++)
            {
                var mask = new bool[h * w];
                for (int i = 0; i < mask.Length; i++)
                    mask[i] = rng.NextU32() / 4294967296.0 < 0.6;

                int brute = 0;
                for (int y0 = 0; y0 < h; y0++)
                    for (int y1 = y0; y1 < h; y1++)
                        for (int x0 = 0; x0 < w; x0++)
                            for (int x1 = x0; x1 < w; x1++)
                            {
                                bool all = true;
                                for (int y = y0; y <= y1 && all; y++)
                                    for (int x = x0; x <= x1 && all; x++)
                                        all = mask[y * w + x];
                                if (all)
                                    brute = Math.Max(brute, (y1 - y0 + 1) * (x1 - x0 + 1));
                            }
                Assert.AreEqual(brute, MergeLifeObjective.LargestRectangleArea(mask, w, h), $"trial {trial}");
            }
        }

        // --- GA operators -----------------------------------------------------------

        [Test]
        public void MutateIsADigitSwap()
        {
            // Paper Sec. 5.3: exchange two random, distinct, non-dash characters. The
            // child is a permutation of the parent that differs in exactly two
            // positions, neither of them a dash, and is still a valid rule.
            for (uint seed = 0; seed < 20; seed++)
            {
                var rng = new Pcg32(seed);
                string child = GaOperators.Mutate(RedWorld, rng);
                Assert.AreNotEqual(RedWorld, child, $"seed {seed}");
                Assert.AreEqual(RedWorld.Length, child.Length, $"seed {seed}");
                CollectionAssert.AreEqual(
                    RedWorld.OrderBy(c => c).ToArray(),
                    child.OrderBy(c => c).ToArray(),
                    $"seed {seed}: permutation");
                for (int i = 0; i < RedWorld.Length; i++)
                    Assert.AreEqual(RedWorld[i] == '-', child[i] == '-', $"seed {seed}: dashes must not move");

                int[] changed = Enumerable.Range(0, RedWorld.Length)
                    .Where(i => RedWorld[i] != child[i])
                    .ToArray();
                Assert.AreEqual(2, changed.Length, $"seed {seed}: exactly two digits exchanged");
                Assert.AreEqual(RedWorld[changed[0]], child[changed[1]], $"seed {seed}: swap");
                Assert.AreEqual(RedWorld[changed[1]], child[changed[0]], $"seed {seed}: swap");
                Assert.IsNull(MergeLife.RuleError(child), $"seed {seed}: child is a valid rule");
            }
        }

        [Test]
        public void MutateDegenerateGenomeUnchanged()
        {
            // With fewer than two distinct digits no swap can change anything; the
            // operator must return rather than loop forever hunting for one.
            const string flat = "0000-0000-0000-0000-0000-0000-0000-0000";
            Assert.AreEqual(flat, GaOperators.Mutate(flat, new Pcg32(1)));
            const string white = "ffff-ffff-ffff-ffff-ffff-ffff-ffff-ffff";
            Assert.AreEqual(white, GaOperators.Mutate(white, new Pcg32(1)));
        }

        [Test]
        public void CrossoverChildrenAreComplementarySplices()
        {
            // Paper Sec. 5.2: cut the dashed string at two points CutLength apart
            // (one 4-digit sub-rule plus a dash) and swap the middle. Each position
            // comes from one parent in the first child and the other parent in the
            // second, and a single cut explains both children.
            Assert.AreEqual(5, GaOperators.CutLength);
            for (uint seed = 0; seed < 20; seed++)
            {
                var (first, second) = GaOperators.Crossover(RedWorld, PenAndInk, new Pcg32(seed));
                Assert.AreEqual(RedWorld.Length, first.Length, $"seed {seed}");
                Assert.AreEqual(RedWorld.Length, second.Length, $"seed {seed}");
                for (int i = 0; i < first.Length; i++)
                {
                    bool fromP1 = first[i] == RedWorld[i];
                    bool fromP2 = second[i] == PenAndInk[i];
                    Assert.IsTrue(fromP1 == fromP2 || RedWorld[i] == PenAndInk[i], $"seed {seed}: position {i}");
                }

                int cuts = 0;
                for (int cut = 0; cut + GaOperators.CutLength <= RedWorld.Length; cut++)
                {
                    string spliced1 = RedWorld.Substring(0, cut)
                        + PenAndInk.Substring(cut, GaOperators.CutLength)
                        + RedWorld.Substring(cut + GaOperators.CutLength);
                    string spliced2 = PenAndInk.Substring(0, cut)
                        + RedWorld.Substring(cut, GaOperators.CutLength)
                        + PenAndInk.Substring(cut + GaOperators.CutLength);
                    if (spliced1 == first && spliced2 == second)
                        cuts++;
                }
                Assert.GreaterOrEqual(cuts, 1, $"seed {seed}: one splice of length {GaOperators.CutLength} explains both children");
                Assert.IsNull(MergeLife.RuleError(first), $"seed {seed}: first child is a valid rule");
                Assert.IsNull(MergeLife.RuleError(second), $"seed {seed}: second child is a valid rule");
            }
        }

        [Test]
        public void TournamentPrefersExtremes()
        {
            var scores = new double[] { -5.0, 10.0, 0.0 };
            var rng = new Pcg32(2);
            int bestWins = 0, worstWins = 0;
            for (int i = 0; i < 50; i++)
            {
                if (GaOperators.TournamentSelect(scores, 3, rng) == 1)
                    bestWins++;
                if (GaOperators.TournamentSelect(scores, 3, rng, worst: true) == 0)
                    worstWins++;
            }
            // P(extreme wins best-of-3 uniform draws) = 1 - (2/3)^3, about 0.70.
            Assert.Greater(bestWins, 25, $"best-of-3 should usually pick the max ({bestWins}/50)");
            Assert.Greater(worstWins, 25, $"worst-of-3 should usually pick the min ({worstWins}/50)");
            // A one-round tournament is a plain uniform draw, always in range.
            for (int i = 0; i < 20; i++)
            {
                int pick = GaOperators.TournamentSelect(scores, 1, rng);
                Assert.GreaterOrEqual(pick, 0);
                Assert.Less(pick, scores.Length);
            }
        }

        // --- objective ---------------------------------------------------------------

        [Test]
        public void ScoreGenomeIsDeterministic()
        {
            var a = MergeLifeObjective.ScoreGenome(
                RedWorld, MergeLifeObjective.PaperObjective, 2, 32, 32, 5, 200);
            var b = MergeLifeObjective.ScoreGenome(
                RedWorld, MergeLifeObjective.PaperObjective, 2, 32, 32, 5, 200);
            Assert.AreEqual(a.Score, b.Score);
            Assert.AreEqual(a.TimeStep, b.TimeStep);

            // Cycle i runs from seed + i; the score is the best cycle and TimeStep
            // the sum of their generations.
            var run0 = MergeLifeObjective.RunOnce(RedWorld, 32, 32, 5, 200);
            var run1 = MergeLifeObjective.RunOnce(RedWorld, 32, 32, 6, 200);
            double score0 = MergeLifeObjective.ScoreStats(run0, MergeLifeObjective.PaperObjective);
            double score1 = MergeLifeObjective.ScoreStats(run1, MergeLifeObjective.PaperObjective);
            Assert.AreEqual(Math.Max(score0, score1), a.Score);
            Assert.AreEqual(run0.Steps + run1.Steps, a.TimeStep);
        }
    }
}
