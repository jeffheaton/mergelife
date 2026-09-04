using System;
using System.Collections.Generic;
using System.Text;
using HeatonCA.Engine;

namespace HeatonCA.SelfCheck
{
    /// <summary>
    /// File-free determinism gate for player builds. HeatonCA's engine is a
    /// bit-exact port: its MergeLife lattices must be byte-identical with the
    /// reference Python/JavaScript/Java/C engines, and its objective statistics
    /// must reproduce the shared evolve vectors double for double. IL2CPP
    /// compiles C# to C++, so stable integer math, floor semantics, and plain
    /// IEEE-754 double arithmetic must be *verified* on every new target
    /// (Android, iOS, WebGL), not assumed. Run this once per platform/backend;
    /// all five checks must pass before trusting simulations or evolution on
    /// that device. Everything is embedded, so no vector files ship in the
    /// player: the PCG32 known answers (spec/rng.md), two upstream cross-engine
    /// digests (conformance/vectors.txt lines 1 and 2), the soup-seeded
    /// redworld-48 step-50 checkpoint digest, and the first run row of the
    /// objective-redworld-48 vector.
    /// </summary>
    public static class DeterminismSelfCheck
    {
        /// <summary>The outcome of one named check.</summary>
        public readonly struct Result
        {
            public Result(string name, bool passed, string detail)
            {
                Name = name;
                Passed = passed;
                Detail = detail;
            }

            /// <summary>Stable check name (e.g. "pcg32"); also the token in the report line.</summary>
            public string Name { get; }

            /// <summary>True when the check reproduced its pinned value.</summary>
            public bool Passed { get; }

            /// <summary>Mismatch description; empty when the check passed.</summary>
            public string Detail { get; }
        }

        /// <summary>"Red World" (Heaton 2017), the rule every embedded vector uses.</summary>
        private const string RedWorld = "e542-5f79-9341-f31e-6c6b-7f08-8773-7068";

        // --- conformance/vectors.txt (upstream cross-engine contract) -------------
        // Each line is "rule rows cols seed steps fnv1a64". The lattice is filled
        // from a 32-bit LCG (state = state * 1664525 + 1013904223; byte = state >> 24),
        // row-major, RGB innermost; the digest is FNV-1a-64 over the same byte order.

        /// <summary>vectors.txt line 1: rule e542, 37 rows x 53 cols, LCG seed 1000, 1 step.</summary>
        internal const ulong Upstream1Digest = 0xe5aa457bd88722b4UL;

        /// <summary>vectors.txt line 2: rule e542, 53 rows x 37 cols, LCG seed 1001, 60 steps.</summary>
        internal const ulong Upstream60Digest = 0x84f61656af078adeUL;

        // --- heaton-life vectors/mergelife/redworld-48 -----------------------------
        // params.json: genome e542, 48x48, init "soup", seed 5; checkpoint
        // state_00050.png is the lattice at step 50. Soup50Digest was derived from
        // the vendored PNG with:
        //   cd heaton-life/vectors/mergelife/redworld-48 && python3 -c "
        //   from PIL import Image
        //   h = 0xcbf29ce484222325
        //   for b in Image.open('state_00050.png').convert('RGB').tobytes():
        //       h = ((h ^ b) * 0x100000001b3) & 0xFFFFFFFFFFFFFFFF
        //   print('0x%016x' % h)"
        //   -> 0x41482e9f3384b19b

        /// <summary>params.json "seed": the PCG32 soup seed of the redworld-48 vector.</summary>
        internal const ulong Soup50Seed = 5;

        /// <summary>FNV-1a-64 of the RGB bytes of state_00050.png (see derivation above).</summary>
        internal const ulong Soup50Digest = 0x41482e9f3384b19bUL;

        // --- heaton-life vectors/evolve/objective-redworld-48 ----------------------
        // params.json: genome e542, 48x48, seed 11, max_steps 500, cycles 3, the
        // paper objective. runs.f64 is little-endian float64, shape [3, 6], columns
        // steps, foreground, active, rect, mage, score; run i is seeded from
        // seed + i, so row 0 is RunOnce(e542, 48, 48, 11, 500). Row 0 bit patterns
        // were derived with:
        //   cd heaton-life/vectors/evolve/objective-redworld-48 && python3 -c "
        //   import struct
        //   row = struct.unpack('<18d', open('runs.f64', 'rb').read())[:6]
        //   print([hex(struct.unpack('<Q', struct.pack('<d', x))[0]) for x in row])"
        //   -> ['0x407f500000000000', '0x3f3c71c71c71c71c', '0x3fb21c71c71c71c7',
        //       '0x3fd4000000000000', '0x407f000000000000', '0x4008509605c1968a']
        //   i.e. steps 501.0, foreground 0.00043402777777777775, active
        //   0.07074652777777778, rect 0.3125, mage 496.0, score 3.0393486451819784.

        /// <summary>params.json "seed"; row 0 is the run at seed + 0.</summary>
        internal const ulong ObjectiveSeed = 11;

        /// <summary>params.json "max_steps"; a capped run records max_steps + 1 steps.</summary>
        internal const int ObjectiveMaxSteps = 500;

        /// <summary>Row 0 "steps" (501.0): the run stayed alive to the cap.</summary>
        internal const int ObjectiveSteps = 501;

        /// <summary>DoubleToInt64Bits of row 0 "steps" (501.0).</summary>
        internal const long ObjectiveStepsBits = 0x407f500000000000L;

        /// <summary>DoubleToInt64Bits of row 0 "foreground" (0.00043402777777777775).</summary>
        internal const long ObjectiveForegroundBits = 0x3f3c71c71c71c71cL;

        /// <summary>DoubleToInt64Bits of row 0 "active" (0.07074652777777778).</summary>
        internal const long ObjectiveActiveBits = 0x3fb21c71c71c71c7L;

        /// <summary>DoubleToInt64Bits of row 0 "rect" (0.3125).</summary>
        internal const long ObjectiveRectBits = 0x3fd4000000000000L;

        /// <summary>DoubleToInt64Bits of row 0 "mage" (496.0).</summary>
        internal const long ObjectiveMageBits = 0x407f000000000000L;

        /// <summary>DoubleToInt64Bits of row 0 "score" (3.0393486451819784) under the paper objective.</summary>
        internal const long ObjectiveScoreBits = 0x4008509605c1968aL;

        /// <summary>
        /// Run every check and render the report: a "HeatonCA determinism self-check:"
        /// header, then one line per check, "PASS &lt;name&gt;" or
        /// "FAIL &lt;name&gt;: &lt;detail&gt;". Returns true only if every check passed.
        /// </summary>
        public static bool Run(out string report)
        {
            var results = RunAll();
            var sb = new StringBuilder("HeatonCA determinism self-check:\n");
            bool all = true;
            foreach (var r in results)
            {
                all &= r.Passed;
                sb.Append(r.Passed ? "PASS " : "FAIL ").Append(r.Name);
                if (!r.Passed)
                    sb.Append(": ").Append(r.Detail);
                sb.Append('\n');
            }
            report = sb.ToString();
            return all;
        }

        /// <summary>The five checks, in report order.</summary>
        public static List<Result> RunAll() => new List<Result>
        {
            CheckPcg32(),
            CheckMergeLifeUpstream1(),
            CheckMergeLifeUpstream60(),
            CheckMergeLifeSoup50(),
            CheckObjectiveRedworld48(),
        };

        /// <summary>spec/rng.md known-answer test: seed(42, 54) produces six pinned values.</summary>
        private static Result CheckPcg32()
        {
            uint[] expected = { 0xA15C02B7, 0x7B47F409, 0xBA1D3330, 0x83D2F293, 0xBFA4784B, 0xCBED606E };
            var rng = new Pcg32(42, 54);
            for (int i = 0; i < expected.Length; i++)
            {
                uint got = rng.NextU32();
                if (got != expected[i])
                    return new Result("pcg32", false, $"draw {i}: 0x{got:X8} != 0x{expected[i]:X8}");
            }
            return new Result("pcg32", true, "");
        }

        /// <summary>
        /// conformance/vectors.txt line 1: LCG-seeded 37x53 lattice, 1 step. The
        /// one-step vector isolates the update rule itself (merge, mode padding,
        /// stable sub-rule sort, floor) from any drift that only compounds later.
        /// </summary>
        private static Result CheckMergeLifeUpstream1() =>
            CheckUpstream("mergelife-upstream-1", rows: 37, cols: 53, seed: 1000, steps: 1, expected: Upstream1Digest);

        /// <summary>
        /// conformance/vectors.txt line 2: LCG-seeded 53x37 lattice, 60 steps. Sixty
        /// generations of the same rule on the transposed lattice, so a single wrong
        /// byte anywhere in the pipeline propagates into the digest.
        /// </summary>
        private static Result CheckMergeLifeUpstream60() =>
            CheckUpstream("mergelife-upstream-60", rows: 53, cols: 37, seed: 1001, steps: 60, expected: Upstream60Digest);

        /// <summary>
        /// Replays one upstream cross-engine vector: fill a rows x cols RGB lattice
        /// from the 32-bit LCG (row-major, channels innermost), run the steps, and
        /// FNV-1a-64 the resulting lattice in the same byte order. The LCG is written
        /// out here rather than shared with the engine, as the contract requires.
        /// </summary>
        private static Result CheckUpstream(string name, int rows, int cols, uint seed, int steps, ulong expected)
        {
            uint state = seed;
            var lattice = new byte[rows * cols * 3];
            for (int i = 0; i < lattice.Length; i++)
            {
                state = unchecked(state * 1664525u + 1013904223u);
                lattice[i] = (byte)(state >> 24);
            }
            var sim = new MergeLife(RedWorld, cols, rows);
            sim.SetState(lattice);
            for (int s = 0; s < steps; s++)
                sim.Step();
            ulong digest = Fnv1a64(sim.State);
            return digest == expected
                ? new Result(name, true, "")
                : new Result(name, false, $"digest {digest:x16} != {expected:x16}");
        }

        /// <summary>
        /// heaton-life vectors/mergelife/redworld-48: a 48x48 soup seeded through PCG32
        /// (seed 5) and stepped to generation 50 must digest to the vendored
        /// state_00050.png. This is the only check that exercises SeedSoup, the draw
        /// order every catalog and evolution run depends on.
        /// </summary>
        private static Result CheckMergeLifeSoup50()
        {
            var sim = new MergeLife(RedWorld, 48, 48);
            sim.SeedSoup(Soup50Seed);
            for (int s = 0; s < 50; s++)
                sim.Step();
            ulong digest = Fnv1a64(sim.State);
            return digest == Soup50Digest
                ? new Result("mergelife-soup50", true, "")
                : new Result("mergelife-soup50", false, $"digest {digest:x16} != {Soup50Digest:x16}");
        }

        /// <summary>
        /// heaton-life vectors/evolve/objective-redworld-48, run row 0: one convergence
        /// run of the paper objective on a 48x48 soup (seed 11, cap 500) must record
        /// 501 steps and reproduce foreground, active, rect, mage, and the paper score
        /// bit for bit. This is the check that guards evolution on a new backend: the
        /// statistics are integer counts divided by the cell count, and the score is
        /// plain double arithmetic, so any fused or extended-precision contraction
        /// shows up here as a flipped low bit.
        /// </summary>
        private static Result CheckObjectiveRedworld48()
        {
            const string name = "objective-redworld48";
            var stats = MergeLifeObjective.RunOnce(RedWorld, 48, 48, ObjectiveSeed, ObjectiveMaxSteps);
            if (stats.Steps != ObjectiveSteps)
                return new Result(name, false, $"steps {stats.Steps:R} != {ObjectiveSteps}");
            double score = MergeLifeObjective.ScoreStats(stats, MergeLifeObjective.PaperObjective);
            (string Stat, double Value, long Expected)[] columns =
            {
                ("steps", stats.Steps, ObjectiveStepsBits),
                ("foreground", stats.Foreground, ObjectiveForegroundBits),
                ("active", stats.Active, ObjectiveActiveBits),
                ("rect", stats.Rect, ObjectiveRectBits),
                ("mage", stats.Mage, ObjectiveMageBits),
                ("score", score, ObjectiveScoreBits),
            };
            foreach (var (stat, value, expected) in columns)
            {
                long got = BitConverter.DoubleToInt64Bits(value);
                if (got != expected)
                    return new Result(name, false, $"{stat} {value:R} = 0x{got:x16} != 0x{expected:x16}");
            }
            return new Result(name, true, "");
        }

        /// <summary>FNV-1a, 64-bit: offset basis 0xcbf29ce484222325, prime 0x100000001b3.</summary>
        internal static ulong Fnv1a64(ReadOnlySpan<byte> data)
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
