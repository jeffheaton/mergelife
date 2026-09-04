using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HeatonCA.Engine;
using HeatonCA.SelfCheck;
using NUnit.Framework;

namespace HeatonCA.Tests
{
    /// <summary>
    /// The same file-free checks a player build runs (<see cref="DeterminismSelfCheck"/>),
    /// executed in the editor, plus cross-checks of every constant the self-check
    /// embeds against the vector file it was derived from. If the checks pass here but
    /// fail in a player build, the difference is the scripting backend, which is exactly
    /// what the self-check exists to catch. If a cross-check fails, the embedded constant
    /// has drifted from the vendored vectors (or the repo's conformance/vectors.txt), and
    /// the self-check is no longer guarding the contract it claims to.
    /// </summary>
    public class SelfCheckTests
    {
        /// <summary>The five check names, in the order RunAll reports them.</summary>
        private static readonly string[] CheckNames =
        {
            "pcg32",
            "mergelife-upstream-1",
            "mergelife-upstream-60",
            "mergelife-soup50",
            "objective-redworld48",
        };

        public static IEnumerable<TestCaseData> Checks()
        {
            foreach (var result in DeterminismSelfCheck.RunAll())
                yield return new TestCaseData(result).SetName($"SelfCheck({result.Name})");
        }

        [TestCaseSource(nameof(Checks))]
        public void Check(DeterminismSelfCheck.Result result)
        {
            Assert.IsTrue(result.Passed, $"{result.Name}: {result.Detail}");
        }

        [Test]
        public void RunReportsPassAndListsFiveChecks()
        {
            bool passed = DeterminismSelfCheck.Run(out string report);
            Assert.IsTrue(passed, report);
            StringAssert.StartsWith("HeatonCA determinism self-check:", report);
            StringAssert.DoesNotContain("FAIL ", report);

            var resultLines = new List<string>();
            foreach (string line in report.Split('\n'))
            {
                if (line.StartsWith("PASS ", StringComparison.Ordinal) || line.StartsWith("FAIL ", StringComparison.Ordinal))
                    resultLines.Add(line);
            }
            Assert.AreEqual(CheckNames.Length, resultLines.Count, $"expected one line per check in:\n{report}");
            for (int i = 0; i < CheckNames.Length; i++)
                Assert.AreEqual($"PASS {CheckNames[i]}", resultLines[i], $"line {i} of:\n{report}");
        }

        [Test]
        public void Soup50ConstantMatchesVendoredVector()
        {
            string caseDir = VectorPaths.Vendored("mergelife", "redworld-48");
            var root = J.LoadCase(caseDir);
            Assert.AreEqual("bit-exact", J.Str(root["tier"]));
            var p = J.Obj(root["params"]);

            // The self-check runs Red World on a 48x48 soup seeded with Soup50Seed; the
            // vector's params must describe that same world, or the digest below is a
            // coincidence at best.
            Assert.AreEqual(MergeLife.DefaultRule, J.Str(p["genome"]));
            Assert.AreEqual("soup", J.Str(p["init"]));
            Assert.AreEqual(48, J.Int(p["width"]));
            Assert.AreEqual(48, J.Int(p["height"]));
            Assert.AreEqual(DeterminismSelfCheck.Soup50Seed, (ulong)J.Num(p["seed"]));

            string file = CheckpointFile(J.Arr(root["checkpoints"]), step: 50);
            var (width, height, channels, pixels) = PngReader.Read(Path.Combine(caseDir, file));
            Assert.AreEqual(48, width);
            Assert.AreEqual(48, height);
            Assert.AreEqual(3, channels, "mergelife checkpoints are RGB");
            Assert.AreEqual(48 * 48 * 3, pixels.Length);

            // Recompute with the hash written out below, independently of the self-check,
            // then confirm the self-check's own FNV-1a-64 agrees with it.
            ulong digest = Fnv1a64(pixels);
            Assert.AreEqual(
                DeterminismSelfCheck.Soup50Digest, digest,
                $"state_00050.png digests to {digest:x16}; Soup50Digest is {DeterminismSelfCheck.Soup50Digest:x16}");
            Assert.AreEqual(digest, DeterminismSelfCheck.Fnv1a64(pixels), "self-check FNV-1a-64 disagrees with the test's copy");
        }

        [Test]
        public void ObjectiveConstantsMatchVendoredVector()
        {
            string caseDir = VectorPaths.Vendored("evolve", "objective-redworld-48");
            var root = J.LoadCase(caseDir);
            Assert.AreEqual("bit-exact", J.Str(root["tier"]));
            Assert.AreEqual("objective", J.Str(root["kind"]));
            var p = J.Obj(root["params"]);

            // RunOnce(e542, 48, 48, ObjectiveSeed, ObjectiveMaxSteps) scored with the
            // paper objective is run 0 of this vector; pin every parameter it depends on.
            Assert.AreEqual("paper", J.Str(p["objective"]));
            Assert.AreEqual(MergeLife.DefaultRule, J.Str(p["genome"]));
            Assert.AreEqual(48, J.Int(p["width"]));
            Assert.AreEqual(48, J.Int(p["height"]));
            Assert.AreEqual(DeterminismSelfCheck.ObjectiveSeed, (ulong)J.Num(p["seed"]));
            Assert.AreEqual(DeterminismSelfCheck.ObjectiveMaxSteps, J.Int(p["max_steps"]));

            // runs.f64 is row-major [cycles, columns]; params.json names the columns, so
            // the row layout is read from the vector rather than assumed.
            var runs = J.Obj(J.Obj(root["outputs"])["runs"]);
            var columns = J.Arr(runs["columns"]);
            var shape = J.Arr(runs["shape"]);
            Assert.AreEqual(2, shape.Count);
            int rowCount = J.Int(shape[0]);
            int columnCount = J.Int(shape[1]);
            Assert.AreEqual(J.Int(p["cycles"]), rowCount);
            Assert.AreEqual(columns.Count, columnCount);
            double[] values = VectorPaths.ReadF64(Path.Combine(caseDir, J.Str(runs["file"])));
            Assert.AreEqual(rowCount * columnCount, values.Length);

            (string Column, long Bits)[] expected =
            {
                ("steps", DeterminismSelfCheck.ObjectiveStepsBits),
                ("foreground", DeterminismSelfCheck.ObjectiveForegroundBits),
                ("active", DeterminismSelfCheck.ObjectiveActiveBits),
                ("rect", DeterminismSelfCheck.ObjectiveRectBits),
                ("mage", DeterminismSelfCheck.ObjectiveMageBits),
                ("score", DeterminismSelfCheck.ObjectiveScoreBits),
            };
            Assert.AreEqual(expected.Length, columnCount, "the self-check pins every column of run 0");
            foreach (var (column, bits) in expected)
            {
                int c = IndexOf(columns, column);
                Assert.GreaterOrEqual(c, 0, $"runs.f64 has no '{column}' column");
                double value = values[c]; // row 0
                long got = BitConverter.DoubleToInt64Bits(value);
                Assert.AreEqual(bits, got, $"{column}: row 0 is {value:R} (0x{got:x16}); the constant is 0x{bits:x16}");
            }

            // ObjectiveSteps is the integer form of the same "steps" cell (a capped run
            // records max_steps + 1).
            double steps = values[IndexOf(columns, "steps")];
            Assert.AreEqual((double)DeterminismSelfCheck.ObjectiveSteps, steps);
            Assert.AreEqual(DeterminismSelfCheck.ObjectiveMaxSteps + 1, DeterminismSelfCheck.ObjectiveSteps);
        }

        [Test]
        public void UpstreamDigestsMatchRepoVectors()
        {
            string path = VectorPaths.RepoConformanceVectors();
            Assert.IsTrue(
                File.Exists(path),
                $"conformance vectors not found at {path}; run from the mergelife checkout or set HEATONCA_CONFORMANCE_VECTORS");

            var lines = new List<string[]>();
            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;
                lines.Add(line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            }
            Assert.GreaterOrEqual(lines.Count, 2, "vectors.txt has fewer than two data lines");

            // "rule rows cols seed steps fnv1a64": the self-check hard-codes the
            // parameters of lines 1 and 2 alongside their digests, so all six fields
            // of each line are pinned here.
            AssertVectorLine(lines[0], 1, rows: 37, cols: 53, seed: 1000, steps: 1, DeterminismSelfCheck.Upstream1Digest);
            AssertVectorLine(lines[1], 2, rows: 53, cols: 37, seed: 1001, steps: 60, DeterminismSelfCheck.Upstream60Digest);
        }

        private static void AssertVectorLine(
            string[] parts, int lineNumber, int rows, int cols, uint seed, int steps, ulong digest)
        {
            Assert.AreEqual(6, parts.Length, $"vectors.txt data line {lineNumber} has {parts.Length} fields");
            Assert.AreEqual(MergeLife.DefaultRule, parts[0], $"line {lineNumber} rule");
            Assert.AreEqual(rows, int.Parse(parts[1], CultureInfo.InvariantCulture), $"line {lineNumber} rows");
            Assert.AreEqual(cols, int.Parse(parts[2], CultureInfo.InvariantCulture), $"line {lineNumber} cols");
            Assert.AreEqual(seed, uint.Parse(parts[3], CultureInfo.InvariantCulture), $"line {lineNumber} seed");
            Assert.AreEqual(steps, int.Parse(parts[4], CultureInfo.InvariantCulture), $"line {lineNumber} steps");
            Assert.AreEqual(
                digest, ulong.Parse(parts[5], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                $"line {lineNumber} digest {parts[5]} != embedded {digest:x16}");
        }

        /// <summary>The "file" of the params.json checkpoint recorded at <paramref name="step"/>.</summary>
        private static string CheckpointFile(List<object> checkpoints, int step)
        {
            foreach (object checkpoint in checkpoints)
            {
                var cp = J.Obj(checkpoint);
                if (J.Int(cp["step"]) == step)
                    return J.Str(cp["file"]);
            }
            throw new AssertionException($"the vector has no step-{step} checkpoint");
        }

        private static int IndexOf(List<object> columns, string name)
        {
            for (int i = 0; i < columns.Count; i++)
            {
                if (J.Str(columns[i]) == name)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// FNV-1a, 64-bit (offset basis 0xcbf29ce484222325, prime 0x100000001b3), written
        /// out here so the vendored digest is recomputed independently of the self-check.
        /// </summary>
        private static ulong Fnv1a64(ReadOnlySpan<byte> data)
        {
            ulong hash = 0xCBF29CE484222325UL;
            foreach (byte b in data)
            {
                hash ^= b;
                hash = unchecked(hash * 0x100000001B3UL);
            }
            return hash;
        }
    }
}
