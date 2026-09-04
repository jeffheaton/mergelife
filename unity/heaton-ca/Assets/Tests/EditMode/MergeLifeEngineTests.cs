using System;
using System.Linq;
using HeatonCA.Engine;
using NUnit.Framework;

namespace HeatonCA.Tests
{
    /// <summary>
    /// Unit pins for the MergeLife engine: the heaton-life MergeLifeTests.cs suite
    /// (canonicalization, rejected rules, compilation details, soup determinism,
    /// the decoded rule table, RandomRule) ported to NUnit, plus the convergence
    /// pins of python/mergelife-lib/tests/test_engine.py driven through the
    /// objective's internal RunEvaluator with the same hand-built lattices. The
    /// vector replays live in the conformance suites; these hold the edges of the
    /// contract that no vector file exercises.
    /// </summary>
    public class MergeLifeEngineTests
    {
        /// <summary>"Red World", the paper's rule and the engine default.</summary>
        private const string RedWorld = "e542-5f79-9341-f31e-6c6b-7f08-8773-7068";

        /// <summary>"Pen and Ink", the genome of the objective-a07f-48 vector.</summary>
        private const string PenAndInk = "a07f-c000-0000-0000-0000-0000-ff80-807f";

        /// <summary>
        /// Every range octet is 0, so every limit is 0 and no neighbor count is ever
        /// below it: the rule matches no cell, ever, and the lattice never changes
        /// (test_engine.py STATIC_RULE).
        /// </summary>
        private const string StaticRule = "0000-0000-0000-0000-0000-0000-0000-0000";

        /// <summary>The canonical rule shape: eight dash-separated groups of four lowercase hex digits.</summary>
        private const string RulePattern = "^[0-9a-f]{4}(-[0-9a-f]{4}){7}$";

        // --- parsing and canonicalization ------------------------------------------

        [Test]
        public void GenomeCanonicalization()
        {
            const string mixed = "E542-5F79-9341-F31E-6C6B-7F08-8773-7068";
            Assert.AreEqual(RedWorld, MergeLife.CanonicalRule(mixed));
            Assert.AreEqual(RedWorld, MergeLife.CanonicalRule(mixed.Replace("-", "")));
            Assert.AreEqual(RedWorld, MergeLife.CanonicalRule("  " + mixed + "\n"));
            Assert.AreEqual(RedWorld, MergeLife.CanonicalRule(RedWorld));
            Assert.IsNull(MergeLife.RuleError(mixed));
            Assert.IsNull(MergeLife.RuleError(mixed.Replace("-", "")));
            // The world stores the canonical form whatever spelling it was given.
            Assert.AreEqual(RedWorld, new MergeLife(mixed.Replace("-", ""), 8, 8).Rule);
        }

        [TestCase("", TestName = "InvalidGenomesAreRejected(empty)")]
        [TestCase("e542", TestName = "InvalidGenomesAreRejected(four digits)")]
        [TestCase("e542-5f79-9341-f31e-6c6b-7f08-8773", TestName = "InvalidGenomesAreRejected(28 digits)")]
        [TestCase("e542-5f79-9341-f31e-6c6b-7f08-8773-706", TestName = "InvalidGenomesAreRejected(31 digits)")]
        [TestCase("e542-5f79-9341-f31e-6c6b-7f08-8773-70688", TestName = "InvalidGenomesAreRejected(33 digits)")]
        [TestCase("zz42-zz42-zz42-zz42-zz42-zz42-zz42-zz42-", TestName = "InvalidGenomesAreRejected(non-hex groups)")]
        [TestCase("e542-5f79-9341-f31e-6c6b-7f08-8773-706g", TestName = "InvalidGenomesAreRejected(one non-hex digit)")]
        public void InvalidGenomesAreRejected(string bad)
        {
            Assert.IsNotNull(MergeLife.RuleError(bad));
            Assert.Throws<ArgumentException>(() => new MergeLife(bad, 8, 8));
            Assert.Throws<ArgumentException>(() => MergeLife.CanonicalRule(bad));
            Assert.Throws<ArgumentException>(() => MergeLife.ParseRule(bad));
            Assert.Throws<ArgumentException>(() => MergeLife.CompileRule(bad));
            Assert.Throws<ArgumentException>(() => MergeLife.DecodeRule(bad));
        }

        [Test]
        public void ParseRuleDecodesSignedPercentOctets()
        {
            // test_engine.py test_hex_roundtrip_code: the percent octet is a two's-
            // complement byte, so 0xfb is -5, 0x80 is -128, and 0xff is -1.
            var expected = new (int Range, int Percent)[]
            {
                (0x12, -5), (0xff, 127), (0x00, -128), (0x80, 0),
                (0x40, 64), (0xc0, -64), (0x01, 1), (0xfe, -1),
            };
            var pairs = MergeLife.ParseRule("12fb-ff7f-0080-8000-4040-c0c0-0101-feff");
            CollectionAssert.AreEqual(expected, pairs);
        }

        // --- rule compilation ---------------------------------------------------------

        [Test]
        public void CompileRuleLimitIsRangeOctetTimesEight()
        {
            var raw = MergeLife.ParseRule(RedWorld);
            var compiled = MergeLife.CompileRule(RedWorld);
            Assert.AreEqual(8, compiled.Length);
            foreach (var (limit, _, colorIndex) in compiled)
                Assert.AreEqual(raw[colorIndex].Range * 8, limit, $"sub-rule {colorIndex}");
            // Red World's lowest limit is 0x5f * 8 = 760, from the second sub-rule (Red).
            Assert.AreEqual(760, compiled[0].Limit);
            Assert.AreEqual(1, compiled[0].ColorIndex);
        }

        [Test]
        public void CompileRulePromotesFullRangeTo2048()
        {
            // 0xff * 8 = 2040 is promoted to 2048 so the top sub-rule catches every
            // neighbor count: eight neighbors of 255 sum to exactly 2040, which a
            // strict `cnt < 2040` test would miss.
            var rule = MergeLife.CompileRule("ff7f-0080-0000-0000-0000-0000-0000-0000");
            Assert.AreEqual(2048, rule.Max(e => e.Limit));
            Assert.AreEqual(0, rule.Min(e => e.Limit));
            var top = rule.Single(e => e.Limit == 2048);
            Assert.AreEqual(0, top.ColorIndex);
            Assert.AreEqual(127 / 127.0, top.Percent, 1e-12);
            // Only the exact full-range octet is promoted: 0xfe * 8 stays 2032.
            Assert.AreEqual(2032,
                MergeLife.CompileRule("fe00-0000-0000-0000-0000-0000-0000-0000").Max(e => e.Limit));
            // The raw octet survives promotion where it is shown: ParseRule still says 0xff.
            Assert.AreEqual(0xff, MergeLife.ParseRule("ff7f-0080-0000-0000-0000-0000-0000-0000")[0].Range);
        }

        [Test]
        public void CompileRuleScalesPercentBy127OrBy128()
        {
            // Positive percents divide by 127 (0x7f is exactly 1.0); zero and negative
            // percents divide by 128 (0x80 is exactly -1.0), mapping the octet onto
            // [-1.0, 1.0]. All limits are 0 here, so the stable sort keeps rule order
            // and rule[i] is the i-th sub-rule.
            var rule = MergeLife.CompileRule("007f-0080-0040-00c0-0000-0001-00ff-0002");
            for (int i = 0; i < 8; i++)
                Assert.AreEqual(i, rule[i].ColorIndex, $"rule order at {i}");
            Assert.AreEqual(1.0, rule[0].Percent);
            Assert.AreEqual(-1.0, rule[1].Percent);
            Assert.AreEqual(64 / 127.0, rule[2].Percent);
            Assert.AreNotEqual(64 / 128.0, rule[2].Percent);
            Assert.AreEqual(-0.5, rule[3].Percent);
            Assert.AreEqual(0.0, rule[4].Percent);
            Assert.AreEqual(1 / 127.0, rule[5].Percent);
            Assert.AreEqual(-1 / 128.0, rule[6].Percent);
            Assert.AreEqual(2 / 127.0, rule[7].Percent);
        }

        [Test]
        public void CompileRuleStableSortKeepsRuleOrderAmongTies()
        {
            // Sub-rules sort by limit alone; equal limits keep their hex-string order
            // (the Python reference's stable sorted(key=alpha)). The percent must NOT
            // break ties: rule position is the only tie-breaker.
            var rule = MergeLife.CompileRule("2000-1000-2000-1000-2000-1000-2000-1000");
            CollectionAssert.AreEqual(
                new[] { 128, 128, 128, 128, 256, 256, 256, 256 },
                rule.Select(e => e.Limit).ToArray());
            CollectionAssert.AreEqual(
                new[] { 1, 3, 5, 7, 0, 2, 4, 6 },
                rule.Select(e => e.ColorIndex).ToArray());

            // Eight ties with wildly different percents stay in rule order.
            var tied = MergeLife.CompileRule("107f-1001-1080-10c0-1000-1040-10ff-1002");
            CollectionAssert.AreEqual(
                Enumerable.Range(0, 8).ToArray(),
                tied.Select(e => e.ColorIndex).ToArray());

            // heaton-life's original pin: the promoted sub-rule sorts last and the
            // first zero-limit sub-rule in rule order (index 1) leads.
            var promoted = MergeLife.CompileRule("ff7f-0080-0000-0000-0000-0000-0000-0000");
            Assert.AreEqual(0, promoted[0].Limit);
            Assert.AreEqual(1, promoted[0].ColorIndex);
            Assert.AreEqual(0, promoted[7].ColorIndex);
        }

        [Test]
        public void CompileRuleIsSortedAndComplete()
        {
            // test_engine.py test_parse_update_rule_sorted_and_complete, over the
            // paper rule and a spread of random rules: limits ascend, every color
            // index appears exactly once, and every percent lies in [-1, 1].
            var rules = new[] { RedWorld, PenAndInk, StaticRule }
                .Concat(Enumerable.Range(0, 20).Select(seed => MergeLife.RandomRule((uint)seed)));
            foreach (string text in rules)
            {
                var compiled = MergeLife.CompileRule(text);
                Assert.AreEqual(8, compiled.Length, text);
                for (int i = 1; i < compiled.Length; i++)
                    Assert.LessOrEqual(compiled[i - 1].Limit, compiled[i].Limit, $"{text}: limits ascend");
                CollectionAssert.AreEquivalent(
                    Enumerable.Range(0, 8).ToArray(),
                    compiled.Select(e => e.ColorIndex).ToArray(),
                    $"{text}: every color index once");
                foreach (var entry in compiled)
                {
                    Assert.GreaterOrEqual(entry.Percent, -1.0, text);
                    Assert.LessOrEqual(entry.Percent, 1.0, text);
                }
            }
        }

        // --- soup and stepping ------------------------------------------------------

        [Test]
        public void SoupDeterminismAndShapes()
        {
            var a = new MergeLife(MergeLife.DefaultRule, 32, 24);
            var b = new MergeLife(MergeLife.DefaultRule, 32, 24);
            Assert.AreEqual(RedWorld, a.Rule);
            Assert.AreEqual(32, a.Width);
            Assert.AreEqual(24, a.Height);
            Assert.AreEqual(0, a.Generation);
            a.SeedSoup(11);
            b.SeedSoup(11);
            Assert.AreEqual(24 * 32 * 3, a.State.Length);
            CollectionAssert.AreEqual(a.State.ToArray(), b.State.ToArray());
            a.Step(5);
            b.Step(5);
            Assert.AreEqual(5, a.Generation);
            CollectionAssert.AreEqual(a.State.ToArray(), b.State.ToArray());

            // A different seed is a different soup.
            var c = new MergeLife(MergeLife.DefaultRule, 32, 24);
            c.SeedSoup(12);
            b.SeedSoup(11);
            Assert.AreEqual(0, b.Generation);
            CollectionAssert.AreNotEqual(b.State.ToArray(), c.State.ToArray());
        }

        [Test]
        public void StepChangesGrid()
        {
            var sim = new MergeLife(MergeLife.DefaultRule, 32, 32);
            sim.SeedSoup(1);
            byte[] before = sim.State.ToArray();
            sim.Step();
            Assert.AreEqual(1, sim.Generation);
            CollectionAssert.AreNotEqual(before, sim.State.ToArray());
        }

        // --- decoded rule table -----------------------------------------------------

        [Test]
        public void DecodeRuleMatchesTheRuleTabTable()
        {
            var rows = MergeLife.DecodeRule(MergeLife.DefaultRule);
            Assert.AreEqual(8, rows.Length);
            var first = rows[0]; // the HeatonCA Rule-tab top row
            Assert.AreEqual(760, first.Limit);
            Assert.AreEqual(0, first.RangeLow);
            Assert.AreEqual(759, first.RangeHigh);
            Assert.AreEqual(1, first.ColorIndex);
            Assert.AreEqual("Red", first.ColorName);
            Assert.AreEqual(1, first.TargetIndex);
            Assert.AreEqual("Red", first.TargetName);
            Assert.AreEqual((byte)255, first.TargetR);
            Assert.AreEqual((byte)0, first.TargetG);
            Assert.AreEqual((byte)0, first.TargetB);
            Assert.AreEqual((byte)0x5F, first.RangeByte);
            Assert.AreEqual((sbyte)0x79, first.PercentByte);
            Assert.AreEqual(95, (int)(first.Percent * 100));
            Assert.AreEqual(23, (int)(rows[7].Percent * 100)); // truncation, not rounding

            // Rows tile the neighbor-count axis in compiled order: each row starts
            // where the previous one ended and the last row ends at its limit.
            for (int i = 1; i < rows.Length; i++)
                Assert.AreEqual(rows[i - 1].Limit, rows[i].RangeLow, $"row {i} starts at the previous limit");
            for (int i = 0; i < rows.Length; i++)
                Assert.AreEqual(rows[i].Limit - 1, rows[i].RangeHigh, $"row {i} ends one below its limit");
        }

        [Test]
        public void DecodeRuleNegativeSwapsTargetAndKeepsRawOctets()
        {
            var rows = MergeLife.DecodeRule("ff40-00c0-8020-407f-2081-6001-a0ff-e080");
            Assert.AreEqual(8, rows.Length);

            var black = rows.Single(r => r.ColorIndex == 0);
            Assert.AreEqual(2048, black.Limit);
            Assert.AreEqual(0xFF, black.RangeByte); // raw, not limit/8
            Assert.AreEqual(0, black.TargetIndex); // positive percent: no swap

            var red = rows.Single(r => r.ColorIndex == 1);
            Assert.AreEqual(-0.5, red.Percent);
            Assert.AreEqual("Red", red.ColorName);
            Assert.AreEqual("Green", red.TargetName);
            Assert.AreEqual(2, red.TargetIndex);
            Assert.AreEqual((byte)0, red.TargetR);
            Assert.AreEqual((byte)255, red.TargetG);
            Assert.AreEqual((byte)0, red.TargetB);
            Assert.AreEqual(-64, red.PercentByte);

            var white = rows.Single(r => r.ColorIndex == 7);
            Assert.AreEqual(-1.0, white.Percent);
            Assert.AreEqual(0, white.TargetIndex); // wraps past White to Black
            Assert.AreEqual("Black", white.TargetName);
            Assert.AreEqual(-128, white.PercentByte);
        }

        [Test]
        public void RandomRuleIsValidAndDeterministic()
        {
            string g1 = MergeLife.RandomRule(42);
            string g2 = MergeLife.RandomRule(42);
            Assert.AreEqual(g1, g2);
            Assert.IsNull(MergeLife.RuleError(g1));
            Assert.AreNotEqual(g1, MergeLife.RandomRule(43));

            // test_engine.py test_random_update_rule_format: already canonical.
            for (uint seed = 0; seed < 20; seed++)
            {
                string rule = MergeLife.RandomRule(seed);
                Assert.That(rule, Does.Match(RulePattern), $"seed {seed}");
                Assert.AreEqual(rule, MergeLife.CanonicalRule(rule), $"seed {seed}");
                Assert.AreEqual(8, MergeLife.CompileRule(rule).Length, $"seed {seed}");
            }
        }

        // --- convergence pins (test_engine.py) ----------------------------------------

        /// <summary>
        /// The trainer's loop shape: step, then ask whether that generation ended the
        /// run. Returns the generation count and the statistics the exit was judged on.
        /// </summary>
        private static (int Steps, MergeLifeObjective.Stats Last) RunToConvergence(MergeLife sim, int maxSteps)
        {
            var evaluator = new MergeLifeObjective.RunEvaluator(sim.Height, sim.Width);
            while (true)
            {
                evaluator.Step(sim);
                var stats = evaluator.ComputeStats();
                if (evaluator.IsStable(stats, maxSteps))
                    return (evaluator.TimeStep, stats);
            }
        }

        [Test]
        public void StaticWorldFreezesAt153()
        {
            // test_engine.py test_static_world_converges_on_frozen_background. The
            // 2018 trainer's frozen-background exit: the stable background count has
            // not moved for more than 100 CA generations. StaticRule matches no cell,
            // so the lattice never changes; a uniform lattice makes every cell
            // background, which qualifies as stable at generation 52 and then never
            // moves again -- 101 further generations puts the exit at 153.
            var sim = new MergeLife(StaticRule, 20, 20);
            var uniform = new byte[20 * 20 * 3];
            for (int i = 0; i < uniform.Length; i++)
                uniform[i] = 7;
            sim.SetState(uniform);

            var (steps, last) = RunToConvergence(sim, MergeLifeObjective.MaxSteps);
            Assert.AreEqual(153, steps);
            Assert.AreEqual(153, sim.Generation);
            // Every cell is stable background, so this was the frozen-background
            // exit, not the dead-world exit (bg < 1%) and not the cap.
            Assert.AreEqual(400, last.Mc);
            Assert.AreEqual(1.0, last.Bg);
            Assert.AreEqual(7, last.Mode);
            CollectionAssert.AreEqual(uniform, sim.State.ToArray(), "the static rule never touches a cell");
        }

        [Test]
        public void ExplodedWorldDiesAt101()
        {
            // test_engine.py test_exploded_world_converges_on_dead_world_exit. The
            // 2018 trainer's dead-world exit: past generation 100, less than 1% of
            // the lattice is stable background. Giving every cell a distinct merged
            // value leaves the mode holding a single cell (1/256), so the exit fires
            // the first generation it is allowed to, at 101 -- well before the
            // frozen-background counter above could reach 153.
            var sim = new MergeLife(StaticRule, 16, 16);
            var distinct = new byte[16 * 16 * 3];
            for (int cell = 0; cell < 256; cell++)
            {
                distinct[cell * 3] = (byte)cell;
                distinct[cell * 3 + 1] = (byte)cell;
                distinct[cell * 3 + 2] = (byte)cell;
            }
            sim.SetState(distinct);

            var (steps, last) = RunToConvergence(sim, MergeLifeObjective.MaxSteps);
            Assert.AreEqual(101, steps);
            Assert.AreEqual(101, sim.Generation);
            Assert.AreEqual(0, last.Mode, "a 256-way tie breaks toward the lowest merged value");
            Assert.AreEqual(1, last.Mc);
            Assert.AreEqual(1 / 256.0, last.Bg);
            Assert.Less(last.Bg, 0.01);
            CollectionAssert.AreEqual(distinct, sim.State.ToArray(), "the static rule never touches a cell");
        }

        [TestCase(RedWorld, 11UL, TestName = "CappedRunRecordsMaxStepsPlusOne(Red World, seed 11)")]
        [TestCase(PenAndInk, 21UL, TestName = "CappedRunRecordsMaxStepsPlusOne(Pen and Ink, seed 21)")]
        public void CappedRunRecordsMaxStepsPlusOne(string genome, ulong seed)
        {
            // test_engine.py test_long_running_rule_records_a_step_above_the_objective_max:
            // the cap is a strict `>`, so a run that never converges stops at
            // maxSteps + 1. These are row 0 of the objective-redworld-48 and
            // objective-a07f-48 vectors (48x48, max_steps 500), both of which stay
            // alive to the cap.
            var stats = MergeLifeObjective.RunOnce(genome, 48, 48, seed, 500);
            Assert.AreEqual(501.0, stats.Steps);
        }

        [Test]
        public void CappedStepsEarnTheMaxWeightNotTheInRangeValue()
        {
            // Why the off-by-one matters: 1001 steps sits above the steps rule's
            // [300, 1000] band and earns max_weight (+1), where 1000 steps is still
            // in range and scores the steeply negative tent value. Every published
            // score depends on surviving to the cap being rewarded this way.
            var atCap = new MergeLifeObjective.RunStats(1001, 0.05, 0.05, 0.1, 7);
            var inBand = new MergeLifeObjective.RunStats(1000, 0.05, 0.05, 0.1, 7);
            double capped = MergeLifeObjective.ScoreStats(atCap, MergeLifeObjective.PaperObjective);
            double banded = MergeLifeObjective.ScoreStats(inBand, MergeLifeObjective.PaperObjective);
            // Only the steps term differs: +1 versus ((350 - |1000 - 350|) / 350) * 1.
            double stepsInBand = (350.0 - Math.Abs(1000.0 - 350.0)) / 350.0;
            Assert.AreEqual(1.0 - stepsInBand, capped - banded, 1e-12);
            Assert.Greater(capped, banded);
        }
    }
}
