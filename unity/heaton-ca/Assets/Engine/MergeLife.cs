// HeatonCA.Engine: copied verbatim from HeatonLife.Core (github.com/jeffheaton/heaton-life, dotnet/src/HeatonLife.Core/MergeLife.cs, commit 46a117baf33a0f916d39e2c05991604ae8a1b424).
// Copyright Jeff Heaton. Licensed under the Apache License, Version 2.0; see THIRD_PARTY_NOTICES.md.
// Modifications: namespace HeatonLife -> HeatonCA.Engine; MergeLife.ToPng removed (PngGrid/DeflateStream not carried).
// Do not edit: re-run tools/import-engine.sh to sync.

using System;
using System.Text;

namespace HeatonCA.Engine
{
    /// <summary>
    /// MergeLife cellular automaton, Heaton 2017, arXiv:1712.03019 (spec/mergelife.md).
    /// Faithful port of the reference engine (github.com/jeffheaton/mergelife),
    /// byte-identical per the upstream cross-engine conformance vectors. Do not
    /// "clean up" the numerics: the stable sort by range limit alone, the mode-padded
    /// neighbor sum, the 127/128 percent scaling, and the floor semantics are all part
    /// of the contract. State is flat row-major RGB bytes (Height*Width*3). Bit-exact tier.
    /// </summary>
    public sealed class MergeLife : IRgbFrameSource
    {
        /// <summary>"Red World", the paper's showcase rule — the Python default.</summary>
        public const string DefaultRule = "e542-5f79-9341-f31e-6c6b-7f08-8773-7068";

        // The eight key colors: corners of the RGB cube, in rule order
        // (black, red, green, yellow, blue, purple, cyan, white).
        private static readonly int[][] ColorTable =
        {
            new[] { 0, 0, 0 },
            new[] { 255, 0, 0 },
            new[] { 0, 255, 0 },
            new[] { 255, 255, 0 },
            new[] { 0, 0, 255 },
            new[] { 255, 0, 255 },
            new[] { 0, 255, 255 },
            new[] { 255, 255, 255 },
        };

        private readonly (int Limit, double Percent, int ColorIndex)[] _rule;
        private byte[] _cells;
        private byte[] _scratch;
        private readonly int[] _avg;
        private readonly int[] _hist = new int[256];

        public string Rule { get; }
        public int Width { get; }
        public int Height { get; }
        public int Generation { get; private set; }

        /// <summary>Row-major RGB bytes, index = (y * Width + x) * 3 + channel.</summary>
        public ReadOnlySpan<byte> State => _cells;

        public MergeLife(string rule, int width, int height)
        {
            Rule = CanonicalRule(rule);
            Width = width;
            Height = height;
            // Resolve the negative-percent flip once: the reference applies it at every
            // update (|pct| toward the *next* key color); pre-resolving is equivalent.
            var compiled = CompileRule(Rule);
            _rule = new (int, double, int)[compiled.Length];
            for (int i = 0; i < compiled.Length; i++)
            {
                var (limit, percent, colorIndex) = compiled[i];
                if (percent < 0)
                    _rule[i] = (limit, -percent, (colorIndex + 1) % ColorTable.Length);
                else
                    _rule[i] = (limit, percent, colorIndex);
            }
            _cells = new byte[width * height * 3];
            _scratch = new byte[width * height * 3];
            _avg = new int[width * height];
            SeedSoup(0); // the Python constructor's default init
        }

        /// <summary>Hex rule -> eight (range_byte, percent_byte) pairs; percent is a signed byte.</summary>
        public static (int Range, int Percent)[] ParseRule(string rule)
        {
            string text = Normalize(rule);
            if (!IsHex32(text))
                throw new ArgumentException(
                    $"invalid MergeLife rule '{rule}' (expected 8 dash-separated groups of 4 hex digits)");
            var pairs = new (int, int)[8];
            for (int i = 0; i < 8; i++)
            {
                int range = Convert.ToInt32(text.Substring(i * 4, 2), 16);
                int pct = Convert.ToInt32(text.Substring(i * 4 + 2, 2), 16);
                if (pct >= 128) // two's complement
                    pct -= 256;
                pairs[i] = (range, pct);
            }
            return pairs;
        }

        /// <summary>Validation helper: the parse error message, or null if the rule is valid.</summary>
        public static string? RuleError(string rule)
        {
            try
            {
                ParseRule(rule);
            }
            catch (ArgumentException exc)
            {
                return exc.Message;
            }
            return null;
        }

        /// <summary>Normalize to the canonical lowercase dashed form.</summary>
        public static string CanonicalRule(string rule)
        {
            ParseRule(rule);
            string text = Normalize(rule);
            var groups = new string[8];
            for (int i = 0; i < 8; i++)
                groups[i] = text.Substring(i * 4, 4);
            return string.Join("-", groups);
        }

        /// <summary>
        /// Rule -> sub-rules (limit, percent, color_index), stably sorted by limit alone
        /// (ties keep rule order). limit = range_byte * 8, with the full-range value 2040
        /// promoted to 2048 so the top sub-rule catches every neighbor count. percent =
        /// pct/127 if positive else pct/128, giving [-1.0, 1.0]; negative percents are
        /// preserved here and resolved at application time, matching the Python compile_rule.
        /// </summary>
        public static (int Limit, double Percent, int ColorIndex)[] CompileRule(string rule)
        {
            var pairs = ParseRule(rule);
            var entries = new (int Limit, double Percent, int ColorIndex)[8];
            for (int i = 0; i < 8; i++)
            {
                var (range, pct) = pairs[i];
                int limit = range * 8;
                if (limit == 2040)
                    limit = 2048;
                double percent = pct > 0 ? pct / 127.0 : pct / 128.0;
                entries[i] = (limit, percent, i);
            }
            // Stable sort by limit alone: ColorIndex is the rule position, so it breaks
            // ties exactly the way Python's stable sorted() does.
            Array.Sort(entries, (a, b) =>
                a.Limit != b.Limit ? a.Limit.CompareTo(b.Limit) : a.ColorIndex.CompareTo(b.ColorIndex));
            return entries;
        }

        /// <summary>Key-color names in rule order — spec/mergelife.md "Decoded rule table".</summary>
        public static readonly string[] ColorNames =
        {
            "Black", "Red", "Green", "Yellow", "Blue", "Purple", "Cyan", "White",
        };

        /// <summary>One row of the rule-lab table — spec/mergelife.md "Decoded rule table".</summary>
        public readonly struct DecodedSubRule
        {
            public readonly int Limit;
            public readonly int RangeLow;
            public readonly int RangeHigh;
            public readonly double Percent;
            public readonly int ColorIndex;
            public readonly string ColorName;
            public readonly int TargetIndex;
            public readonly string TargetName;
            public readonly byte TargetR;
            public readonly byte TargetG;
            public readonly byte TargetB;
            public readonly byte RangeByte;
            public readonly sbyte PercentByte;

            public DecodedSubRule(
                int limit, int rangeLow, int rangeHigh, double percent,
                int colorIndex, int targetIndex, byte rangeByte, sbyte percentByte)
            {
                Limit = limit;
                RangeLow = rangeLow;
                RangeHigh = rangeHigh;
                Percent = percent;
                ColorIndex = colorIndex;
                ColorName = ColorNames[colorIndex];
                TargetIndex = targetIndex;
                TargetName = ColorNames[targetIndex];
                TargetR = (byte)ColorTable[targetIndex][0];
                TargetG = (byte)ColorTable[targetIndex][1];
                TargetB = (byte)ColorTable[targetIndex][2];
                RangeByte = rangeByte;
                PercentByte = percentByte;
            }
        }

        /// <summary>
        /// The 8 sub-rules as display-ready rows, in compiled (sorted) order —
        /// spec/mergelife.md "Decoded rule table". Carries the raw octets alongside
        /// the compiled values: HeatonCA re-derives octet 1 from the promoted limit
        /// (2048/8 = 0x100) — the raw byte is 0xff.
        /// </summary>
        public static DecodedSubRule[] DecodeRule(string rule)
        {
            var raw = ParseRule(rule);
            var compiled = CompileRule(rule);
            var rows = new DecodedSubRule[8];
            int low = 0;
            for (int i = 0; i < 8; i++)
            {
                var (limit, percent, index) = compiled[i];
                var (range, pct) = raw[index];
                int target = percent < 0 ? (index + 1) % ColorNames.Length : index;
                rows[i] = new DecodedSubRule(
                    limit, low, limit - 1, percent, index, target, (byte)range, (sbyte)pct);
                low = limit;
            }
            return rows;
        }

        /// <summary>A random rule from PCG32 (UI convenience; not part of the upstream contract).</summary>
        public static string RandomRule(uint seed)
        {
            var rng = new Pcg32(seed);
            var sb = new StringBuilder();
            for (int i = 0; i < 8; i++)
            {
                if (i > 0)
                    sb.Append('-');
                uint range = rng.NextU32() & 0xFF;
                uint pct = rng.NextU32() & 0xFF;
                sb.Append(range.ToString("x2")).Append(pct.ToString("x2"));
            }
            return sb.ToString();
        }

        private static string Normalize(string rule) =>
            rule.Trim().ToLowerInvariant().Replace("-", "");

        private static bool IsHex32(string text)
        {
            if (text.Length != 32)
                return false;
            foreach (char c in text)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                    return false;
            return true;
        }

        /// <summary>
        /// Soup init per spec: one PCG32 draw per byte (draw &amp; 0xFF), row-major,
        /// RGB innermost.
        ///
        /// The seed is a <c>ulong</c> where the other families take a <c>uint</c>,
        /// and that is deliberate, not an oversight: the GA walks seeds
        /// arithmetically — evaluation i of a run scores from <c>seed + i *
        /// eval_cycles</c> and cycle j from <c>seed + j</c>
        /// (<see cref="MergeLifeObjective.ScoreGenome"/>, spec/evolve.md
        /// "Determinism") — so a long search runs off the end of 32 bits. Narrowing
        /// it would silently wrap those seeds and break run reproducibility. Callers
        /// with a uint need no cast; it widens implicitly.
        /// </summary>
        public void SeedSoup(ulong seed)
        {
            var rng = new Pcg32(seed);
            for (int i = 0; i < _cells.Length; i++)
                _cells[i] = (byte)(rng.NextU32() & 0xFF);
            Generation = 0;
            // Replays through SeedSoup itself rather than a copy of the loop above:
            // this family is bit-exact, so the draw order must live in exactly one place.
            Remember(seed, s => SeedSoup(s));
        }

        // --- reset ---------------------------------------------------------------
        // How this world was last seeded, so Reset can replay it. The Python
        // reference keeps init/density/seed on its params object and re-dispatches
        // in reset(); a captured delegate is the same idea without a params type.
        // This family's seeds are ulong (SeedSoup takes one), so the stored seed is
        // wider than the interface's uint and Reset simply widens the value it is given.
        private Action<ulong> _replayInit = _ => { };
        private ulong _seed;

        private void Remember(ulong seed, Action<ulong> replay)
        {
            _seed = seed;
            _replayInit = replay;
        }

        /// <inheritdoc />
        public void Reset(uint? seed = null)
        {
            if (seed.HasValue)
                _seed = seed.Value;
            _replayInit(_seed);
        }

        /// <summary>Load an explicit RGB lattice (row-major, Height*Width*3 bytes).</summary>
        public void SetState(ReadOnlySpan<byte> cells)
        {
            if (cells.Length != _cells.Length)
                throw new ArgumentException($"expected {_cells.Length} bytes, got {cells.Length}");
            cells.CopyTo(_cells);
            Generation = 0;
            var snapshot = _cells.Clone() as byte[];
            Remember(_seed, _ =>
            {
                Array.Copy(snapshot!, _cells, _cells.Length);
                Generation = 0;
            });
        }

        /// <summary>Restore a saved state at a given generation (catalog loads).</summary>
        public void SetState(ReadOnlySpan<byte> cells, int generation)
        {
            SetState(cells);
            Generation = generation;
        }

        /// <summary>Paint one cell's RGB in place; does not reset the generation.</summary>
        public void SetCell(int x, int y, byte r, byte g, byte b)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                throw new ArgumentOutOfRangeException($"cell ({x}, {y}) outside {Width}x{Height}");
            int index = (y * Width + x) * 3;
            _cells[index] = r;
            _cells[index + 1] = g;
            _cells[index + 2] = b;
        }

        /// <summary>Restore a rectangle (inclusive corners) to the family blank, black (spec/patterns.md "Clear").</summary>
        public void ClearRect(int x0, int y0, int x1, int y1)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    SetCell(x, y, 0, 0, 0);
        }

        public void Step(int n = 1)
        {
            for (int s = 0; s < n; s++)
                StepOnce();
            Generation += n;
        }

        /// <summary>Frame per spec/render.md: the state itself, raw RGB bytes.</summary>
        public void WriteFrame(byte[] rgb)
        {
            if (rgb.Length != _cells.Length)
                throw new ArgumentException($"expected {_cells.Length} bytes, got {rgb.Length}");
            Array.Copy(_cells, rgb, _cells.Length);
        }

        /// <summary>Allocating convenience for <see cref="WriteFrame"/>.</summary>
        public byte[] Frame()
        {
            var rgb = new byte[_cells.Length];
            WriteFrame(rgb);
            return rgb;
        }

        private void StepOnce()
        {
            int w = Width, h = Height, cells = w * h;

            // Merged grid: integer floor((r+g+b)/3), and its histogram for the mode.
            Array.Clear(_hist, 0, 256);
            for (int i = 0; i < cells; i++)
            {
                int avg = (_cells[i * 3] + _cells[i * 3 + 1] + _cells[i * 3 + 2]) / 3;
                _avg[i] = avg;
                _hist[avg]++;
            }
            // Grid mode; first maximum wins, i.e. ties break toward the lowest value,
            // same as the reference's bincount().argmax().
            int mode = 0, best = -1;
            for (int v = 0; v < 256; v++)
            {
                if (_hist[v] > best)
                {
                    best = _hist[v];
                    mode = v;
                }
            }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // 3x3 neighbor sum of the merged grid (center excluded),
                    // out-of-bounds cells padded with the mode.
                    int cnt = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dy == 0 && dx == 0)
                                continue;
                            int ny = y + dy, nx = x + dx;
                            cnt += (ny < 0 || ny >= h || nx < 0 || nx >= w)
                                ? mode
                                : _avg[ny * w + nx];
                        }

                    // First sub-rule (in sorted order) with cnt < limit wins;
                    // unmatched cells keep their value.
                    int baseIndex = (y * w + x) * 3;
                    bool matched = false;
                    foreach (var (limit, percent, colorIndex) in _rule)
                    {
                        if (cnt >= limit)
                            continue;
                        int[] color = ColorTable[colorIndex];
                        for (int c = 0; c < 3; c++)
                        {
                            int value = _cells[baseIndex + c];
                            int moved = value + (int)Math.Floor((color[c] - value) * percent);
                            _scratch[baseIndex + c] = (byte)moved;
                        }
                        matched = true;
                        break;
                    }
                    if (!matched)
                    {
                        _scratch[baseIndex] = _cells[baseIndex];
                        _scratch[baseIndex + 1] = _cells[baseIndex + 1];
                        _scratch[baseIndex + 2] = _cells[baseIndex + 2];
                    }
                }
            }
            (_cells, _scratch) = (_scratch, _cells);
        }
    }
}
