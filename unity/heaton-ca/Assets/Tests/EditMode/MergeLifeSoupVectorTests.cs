using System;
using System.Collections.Generic;
using System.IO;
using HeatonCA.Engine;
using NUnit.Framework;

namespace HeatonCA.Tests
{
    /// <summary>
    /// mergelife/redworld-48 vector (spec/mergelife.md, bit-exact tier): a PCG32 soup
    /// of Red World stepped to each checkpoint must reproduce the vendored state PNGs
    /// byte for byte. One test per checkpoint (0, 1, 10, 50), each from a fresh world,
    /// so a failure names the first generation that diverged. Expected pixels come
    /// through the suite's independent PngReader, never through the engine.
    /// </summary>
    public class MergeLifeSoupVectorTests
    {
        private const string CaseName = "redworld-48";

        private static string CaseDir() => VectorPaths.Vendored("mergelife", CaseName);

        public static IEnumerable<TestCaseData> Checkpoints()
        {
            var root = J.LoadCase(CaseDir());
            foreach (object entry in J.Arr(root["checkpoints"]))
            {
                var checkpoint = J.Obj(entry);
                int step = J.Int(checkpoint["step"]);
                yield return new TestCaseData(step, J.Str(checkpoint["file"]))
                    .SetName($"Soup({CaseName} step {step})");
            }
        }

        [TestCaseSource(nameof(Checkpoints))]
        public void Checkpoint(int step, string file)
        {
            string caseDir = CaseDir();
            var root = J.LoadCase(caseDir);
            Assert.AreEqual("bit-exact", J.Str(root["tier"]));
            Assert.AreEqual("mergelife", J.Str(root["family"]));
            var p = J.Obj(root["params"]);
            Assert.AreEqual("soup", J.Str(p["init"]));
            // "genome" is the vectors' frozen cross-language wire name for the rule.
            string rule = J.Str(p["genome"]);
            int width = J.Int(p["width"]);
            int height = J.Int(p["height"]);
            ulong seed = (ulong)J.Num(p["seed"]);

            var sim = new MergeLife(rule, width, height);
            sim.SeedSoup(seed);
            sim.Step(step);
            Assert.AreEqual(step, sim.Generation, "generation after stepping");

            var (pngWidth, pngHeight, channels, expected) = PngReader.Read(Path.Combine(caseDir, file));
            Assert.AreEqual(width, pngWidth, $"{file} width");
            Assert.AreEqual(height, pngHeight, $"{file} height");
            Assert.AreEqual(3, channels, $"{file} channels");
            Assert.AreEqual(expected.Length, sim.State.Length, "state byte count");

            int mismatch = FirstMismatch(sim.State, expected);
            Assert.IsTrue(
                mismatch < 0,
                $"mergelife/{CaseName}: state mismatch at step {step} ({file}); first differing byte "
                + $"index {mismatch} (x={mismatch / 3 % width}, y={mismatch / 3 / width}, channel={mismatch % 3})");
        }

        /// <summary>Index of the first byte that differs, or -1 when the spans are identical.</summary>
        private static int FirstMismatch(ReadOnlySpan<byte> actual, ReadOnlySpan<byte> expected)
        {
            int length = Math.Min(actual.Length, expected.Length);
            for (int i = 0; i < length; i++)
            {
                if (actual[i] != expected[i])
                    return i;
            }
            return actual.Length == expected.Length ? -1 : length;
        }
    }
}
