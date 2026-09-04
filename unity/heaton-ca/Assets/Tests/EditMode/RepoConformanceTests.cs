using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HeatonCA.Engine;
using NUnit.Framework;

namespace HeatonCA.Tests
{
    /// <summary>
    /// Replays this repository's cross-engine conformance contract (conformance/README.md,
    /// conformance/vectors.txt) against the Unity engine copy: an RGB lattice filled by the
    /// contract's 32-bit LCG, run for the given number of steps, must hash (FNV-1a 64) to
    /// the recorded digest byte for byte. The Python, JavaScript, Java, and C engines in
    /// this repository pass the same twelve lines. Per the contract, the LCG and the hash
    /// are written out here in the harness rather than borrowed from the engine (or from
    /// HeatonCA.SelfCheck, which carries its own embedded copies): only the engine under
    /// test is shared code. The negative and harness tests below keep the gate honest — a
    /// comparison that could not fail, or a helper that drifted from the contract, would
    /// turn twelve green rows into decoration.
    /// </summary>
    public class RepoConformanceTests
    {
        /// <summary>One parsed line of vectors.txt: rule rows cols seed steps fnv1a64.</summary>
        private readonly struct Vector
        {
            public Vector(string rule, int rows, int cols, uint seed, int steps, string digest)
            {
                Rule = rule;
                Rows = rows;
                Cols = cols;
                Seed = seed;
                Steps = steps;
                Digest = digest;
            }

            public string Rule { get; }
            public int Rows { get; }
            public int Cols { get; }
            public uint Seed { get; }
            public int Steps { get; }
            public string Digest { get; }
        }

        /// <summary>The twelve vectors, one NUnit case each, named Upstream(rule,rows,cols,seed,steps).</summary>
        public static IEnumerable<TestCaseData> Cases()
        {
            foreach (Vector v in ReadVectors())
            {
                yield return new TestCaseData(v.Rule, v.Rows, v.Cols, v.Seed, v.Steps, v.Digest)
                    .SetName($"Upstream({v.Rule},{v.Rows},{v.Cols},{v.Seed},{v.Steps})");
            }
        }

        [TestCaseSource(nameof(Cases))]
        public void Upstream(string rule, int rows, int cols, uint seed, int steps, string digest)
        {
            AssertReplays(rule, rows, cols, seed, steps, digest);
        }

        /// <summary>
        /// gen_vectors.py writes six rules times two shapes. A different count means the
        /// file was regenerated with a changed generator (update this pin with it) or was
        /// truncated, which the twelve per-line cases alone would not notice.
        /// </summary>
        [Test]
        public void VectorFileCarriesTwelveCases()
        {
            Assert.AreEqual(12, ReadVectors().Count, $"unexpected vector count in {VectorPaths.RepoConformanceVectors()}");
        }

        /// <summary>
        /// The harness must be able to fail: vector 1 run against a deliberately corrupted
        /// digest, through the same comparison the twelve cases use, has to be rejected. If
        /// this test ever passes trivially, the engine comparison above proves nothing.
        /// </summary>
        [Test]
        public void CorruptedDigestIsRejected()
        {
            Vector v = ReadVectors()[0];
            string corrupted = CorruptDigest(v.Digest);
            Assert.AreNotEqual(v.Digest, corrupted, "the corruption must change the digest");

            AssertionException failure = Assert.Throws<AssertionException>(
                () => AssertReplays(v.Rule, v.Rows, v.Cols, v.Seed, v.Steps, corrupted),
                "a wrong expected digest must fail the comparison");
            StringAssert.Contains(corrupted, failure.Message);
            StringAssert.Contains(v.Digest, failure.Message, "the failure names the digest the engine produced");
        }

        /// <summary>
        /// A vectors file that cannot be found fails with a message naming
        /// HEATONCA_CONFORMANCE_VECTORS, whether the override pointed at the missing path
        /// or the repository fallback did, so a CI layout without the repo checkout learns
        /// the fix from the failure itself.
        /// </summary>
        [Test]
        public void MissingVectorFileExplainsTheEnvOverride()
        {
            string missing = Path.Combine(
                Path.GetTempPath(), "heatonca-missing-" + Guid.NewGuid().ToString("N"), "vectors.txt");
            Assert.IsFalse(File.Exists(missing));

            FileNotFoundException viaOverride = Assert.Throws<FileNotFoundException>(
                () => VectorPaths.ResolveConformanceVectors(missing, missing));
            StringAssert.Contains(VectorPaths.ConformanceEnvVar, viaOverride.Message);
            StringAssert.Contains("HEATONCA_CONFORMANCE_VECTORS", viaOverride.Message);
            StringAssert.Contains(missing, viaOverride.Message);

            FileNotFoundException viaFallback = Assert.Throws<FileNotFoundException>(
                () => VectorPaths.ResolveConformanceVectors(null, missing));
            StringAssert.Contains("HEATONCA_CONFORMANCE_VECTORS", viaFallback.Message);
            StringAssert.Contains(missing, viaFallback.Message);
        }

        /// <summary>
        /// The override, when it names an existing file, is used as given (the fallback is
        /// not even checked); an empty override is the same as none and falls back.
        /// </summary>
        [Test]
        public void EnvOverrideWinsWhenItNamesAFile()
        {
            string configured = Path.Combine(
                Path.GetTempPath(), "heatonca-vectors-" + Guid.NewGuid().ToString("N") + ".txt");
            string absent = Path.Combine(Path.GetTempPath(), "heatonca-absent-" + Guid.NewGuid().ToString("N"), "vectors.txt");
            File.WriteAllText(configured, "# override\n");
            try
            {
                Assert.AreEqual(configured, VectorPaths.ResolveConformanceVectors(configured, absent));
                Assert.AreEqual(configured, VectorPaths.ResolveConformanceVectors("", configured));
                Assert.Throws<FileNotFoundException>(() => VectorPaths.ResolveConformanceVectors("", absent));
            }
            finally
            {
                File.Delete(configured);
            }
        }

        /// <summary>
        /// The harness's FNV-1a 64 against published values (offset basis for the empty
        /// input; "a" and "abc" from the reference test vectors), so a typo in the prime or
        /// the basis cannot masquerade as an engine bug.
        /// </summary>
        [Test]
        public void HarnessHashMatchesTheContract()
        {
            Assert.AreEqual("cbf29ce484222325", Fnv1a64(Array.Empty<byte>()));
            Assert.AreEqual("af63dc4c8601ec8c", Fnv1a64(Encoding.ASCII.GetBytes("a")));
            Assert.AreEqual("e71fa2190541574b", Fnv1a64(Encoding.ASCII.GetBytes("abc")));
        }

        /// <summary>
        /// The harness's LCG against values worked from the contract's recurrence by hand
        /// (seed 1000 is vector 1's seed): state = state * 1664525 + 1013904223 mod 2^32,
        /// byte = state &gt;&gt; 24. The lattice is rows * cols * 3 bytes, filled in a
        /// single pass, which is the contract's row-major, channels-innermost order. The
        /// lattice digest was computed by an independent Python transcription of the
        /// same two paragraphs of the README.
        /// </summary>
        [Test]
        public void HarnessLatticeMatchesTheContract()
        {
            byte[] lattice = LcgLattice(1000, 37, 53);
            Assert.AreEqual(37 * 53 * 3, lattice.Length);
            CollectionAssert.AreEqual(
                new byte[] { 0x9f, 0xfb, 0x87, 0xaf, 0xd9, 0x55, 0x61, 0xcb },
                new ArraySegment<byte>(lattice, 0, 8));
            Assert.AreEqual("c861959fbb84ad0e", Fnv1a64(lattice));

            CollectionAssert.AreEqual(
                new byte[] { 0x3c, 0x47, 0xd1, 0xaa },
                new ArraySegment<byte>(LcgLattice(0, 1, 2), 0, 4));
        }

        // --- the harness -----------------------------------------------------------

        /// <summary>The comparison every conformance case goes through, negative test included.</summary>
        private static void AssertReplays(string rule, int rows, int cols, uint seed, int steps, string expectedDigest)
        {
            string actual = ReplayDigest(rule, rows, cols, seed, steps);
            Assert.AreEqual(expectedDigest, actual, $"rule {rule}, {rows}x{cols}, seed {seed}, {steps} step(s)");
        }

        /// <summary>Contract steps 1-3: LCG lattice, run, FNV-1a 64 of the current lattice.</summary>
        private static string ReplayDigest(string rule, int rows, int cols, uint seed, int steps)
        {
            // MergeLife's constructor takes (rule, width, height); vectors.txt says rows then cols.
            var sim = new MergeLife(rule, cols, rows);
            sim.SetState(LcgLattice(seed, rows, cols));
            sim.Step(steps);
            return Fnv1a64(sim.State);
        }

        /// <summary>
        /// Contract PRNG: 32-bit LCG, one byte per advance, filling rows * cols * 3 bytes in
        /// row-major order with the RGB channels innermost.
        /// </summary>
        private static byte[] LcgLattice(uint seed, int rows, int cols)
        {
            uint state = seed;
            var lattice = new byte[rows * cols * 3];
            for (int i = 0; i < lattice.Length; i++)
            {
                state = unchecked(state * 1664525u + 1013904223u);
                lattice[i] = (byte)(state >> 24);
            }
            return lattice;
        }

        /// <summary>Contract hash: FNV-1a 64 as 16 lowercase hex digits.</summary>
        private static string Fnv1a64(ReadOnlySpan<byte> data)
        {
            ulong hash = 0xCBF29CE484222325UL;
            foreach (byte b in data)
            {
                hash ^= b;
                hash = unchecked(hash * 0x100000001B3UL);
            }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        /// <summary>Flips the last hex digit so the result is a well-formed but wrong digest.</summary>
        private static string CorruptDigest(string digest)
        {
            char last = digest[digest.Length - 1];
            char flipped = last == '0' ? '1' : '0';
            return digest.Substring(0, digest.Length - 1) + flipped;
        }

        /// <summary>Parses vectors.txt: blank lines and # comments skipped, six fields per line.</summary>
        private static List<Vector> ReadVectors()
        {
            string path = VectorPaths.RepoConformanceVectors();
            var vectors = new List<Vector>();
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;
                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 6)
                    throw new InvalidDataException($"{path}:{i + 1}: expected 6 fields, got {parts.Length}");
                string digest = parts[5];
                if (digest.Length != 16 || digest.Trim("0123456789abcdef".ToCharArray()).Length != 0)
                    throw new InvalidDataException($"{path}:{i + 1}: digest must be 16 lowercase hex digits, got '{digest}'");
                vectors.Add(new Vector(
                    parts[0],
                    int.Parse(parts[1], CultureInfo.InvariantCulture),
                    int.Parse(parts[2], CultureInfo.InvariantCulture),
                    uint.Parse(parts[3], CultureInfo.InvariantCulture),
                    int.Parse(parts[4], CultureInfo.InvariantCulture),
                    digest));
            }
            return vectors;
        }
    }
}
