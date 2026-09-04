// HeatonCA.Engine: copied verbatim from HeatonLife.Core (github.com/jeffheaton/heaton-life, dotnet/src/HeatonLife.Core/MergeLifeObjective.cs, commit 46a117baf33a0f916d39e2c05991604ae8a1b424).
// Copyright Jeff Heaton. Licensed under the Apache License, Version 2.0; see THIRD_PARTY_NOTICES.md.
// Modifications: namespace HeatonLife -> HeatonCA.Engine.
// Do not edit: re-run tools/import-engine.sh to sync.

using System;
using System.Collections.Generic;

namespace HeatonCA.Engine
{
    /// <summary>One objective rule: scores a single run statistic against [Min, Max].</summary>
    public sealed class ObjectiveRule
    {
        public string Stat { get; }
        public double Min { get; }
        public double Max { get; }
        public double Weight { get; }
        public double MinWeight { get; }
        public double MaxWeight { get; }

        public ObjectiveRule(
            string stat, double min, double max, double weight, double minWeight, double maxWeight)
        {
            Stat = stat;
            Min = min;
            Max = max;
            Weight = weight;
            MinWeight = minWeight;
            MaxWeight = maxWeight;
        }
    }

    /// <summary>
    /// The MergeLife objective function, Heaton 2017 Sec. 4 (spec/evolve.md) — a
    /// faithful port of the reference engine's statistics, including the merged-
    /// lattice bookkeeping and its one-generation lag. Convergence follows the
    /// 2018 reference trainer (dead world / frozen background / cap), the
    /// semantics every published score came from — see the spec for why the
    /// paper Sec. 4.1 text is deliberately not used. Integer statistics plus
    /// plain-double scoring: bit-exact across languages, gated by vectors/evolve/.
    /// </summary>
    public static class MergeLifeObjective
    {
        /// <summary>Default step cap; a capped run records MaxSteps + 1 steps.</summary>
        public const int MaxSteps = 1000;

        /// <summary>examples/paperObjective.json from the reference repository.</summary>
        public static readonly IReadOnlyList<ObjectiveRule> PaperObjective = new[]
        {
            new ObjectiveRule("steps", 300, 1000, 1, -1, 1),
            new ObjectiveRule("foreground", 0.001, 0.1, 1, -0.1, -1),
            new ObjectiveRule("active", 0.001, 0.1, 1, -1, -1),
            new ObjectiveRule("rect", 0.02, 0.25, 2, -2, 2),
            new ObjectiveRule("mage", 5, 10, 0, -5, 0),
        };

        /// <summary>The five statistics of one convergence run (paper Sec. 4).</summary>
        public readonly struct RunStats
        {
            public RunStats(double steps, double foreground, double active, double rect, double mage)
            {
                Steps = steps;
                Foreground = foreground;
                Active = active;
                Rect = rect;
                Mage = mage;
            }

            public double Steps { get; }
            public double Foreground { get; }
            public double Active { get; }
            public double Rect { get; }
            public double Mage { get; }

            public double Get(string stat) => stat switch
            {
                "steps" => Steps,
                "foreground" => Foreground,
                "active" => Active,
                "rect" => Rect,
                "mage" => Mage,
                _ => throw new ArgumentException($"unknown stat '{stat}'"),
            };
        }

        /// <summary>One convergence run: the reference's calc_objective_function body.</summary>
        public static RunStats RunOnce(string genome, int width, int height, ulong seed, int maxSteps)
        {
            var sim = new MergeLife(genome, width, height);
            sim.SeedSoup(seed);
            var ev = new RunEvaluator(height, width);
            Stats stats;
            while (true)
            {
                ev.Step(sim);
                stats = ev.ComputeStats();
                if (ev.IsStable(stats, maxSteps))
                    break;
            }
            return new RunStats(
                ev.TimeStep, stats.Fg, stats.Act, ev.LargestRectFraction(stats), stats.Mage);
        }

        /// <summary>Score one run's statistics against an objective (tent function per rule).</summary>
        public static double ScoreStats(in RunStats stats, IReadOnlyList<ObjectiveRule> objective)
        {
            double score = 0.0;
            foreach (var rule in objective)
            {
                double actual = stats.Get(rule.Stat);
                double span = rule.Max - rule.Min;
                // Verbatim from the reference: the in-range peak sits at `span/2`, NOT at
                // the interval midpoint (min + span/2). Faithfulness beats aesthetics here;
                // scores must be comparable with the reference trainer's.
                double ideal = span / 2;
                double adjust;
                if (actual < rule.Min)
                    adjust = rule.MinWeight;
                else if (actual > rule.Max)
                    adjust = rule.MaxWeight;
                else
                    adjust = ((span / 2) - Math.Abs(actual - ideal)) / (span / 2) * rule.Weight;
                score += adjust;
            }
            return score;
        }

        /// <summary>
        /// Best score over <paramref name="cycles"/> seeded runs (the reference takes the
        /// max too). Deterministic: cycle i runs from seed + i.
        /// </summary>
        public static (double Score, double TimeStep) ScoreGenome(
            string genome,
            IReadOnlyList<ObjectiveRule>? objective = null,
            int cycles = 5,
            int width = 100,
            int height = 100,
            ulong seed = 0,
            int maxSteps = MaxSteps,
            int workers = 1)
        {
            // Defaults mirror the Python reference's score_genome, where only the
            // genome is required (evolve/objective.py). Parameter ORDER is kept as
            // it was — matching Python's would move `objective` to first, breaking
            // every caller for a cosmetic win.
            objective = objective ?? PaperObjective;
            // Cycle runs are independent (cycle i seeds from seed + i), so they
            // parallelize into per-index slots; the reduction below reads the
            // slots in index order — output is identical for any worker count
            // (spec/evolve.md "Parallel evaluation").
            var scores = new double[cycles];
            var steps = new int[cycles];
            Parallelism.For(cycles, workers, i =>
            {
                var stats = RunOnce(genome, width, height, seed + (ulong)i, maxSteps);
                scores[i] = ScoreStats(stats, objective);
                steps[i] = (int)stats.Steps;
            });
            double best = double.NegativeInfinity;
            int total = 0;
            for (int i = 0; i < cycles; i++)
            {
                best = Math.Max(best, scores[i]);
                total += steps[i];
            }
            return (best, total);
        }

        /// <summary>Area of the largest axis-aligned all-true rectangle (histogram-stack method).</summary>
        public static int LargestRectangleArea(ReadOnlySpan<bool> mask, int width, int height)
        {
            var heights = new int[width];
            // Array-backed stack, reused across rows: at most `width` open runs.
            var stackStart = new int[width];
            var stackHeight = new int[width];
            int best = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    heights[x] = mask[y * width + x] ? heights[x] + 1 : 0;
                best = Math.Max(best, MaxHistogramArea(heights, stackStart, stackHeight));
            }
            return best;
        }

        private static int MaxHistogramArea(int[] heights, int[] stackStart, int[] stackHeight)
        {
            int top = 0; // stack pointer
            int best = 0;
            int pos;
            for (pos = 0; pos < heights.Length; pos++)
            {
                int height = heights[pos];
                int start = pos;
                while (top > 0 && height < stackHeight[top - 1])
                {
                    top--;
                    best = Math.Max(best, stackHeight[top] * (pos - stackStart[top]));
                    start = stackStart[top];
                }
                if (top == 0 || height > stackHeight[top - 1])
                {
                    stackStart[top] = start;
                    stackHeight[top] = height;
                    top++;
                }
            }
            int end = heights.Length > 0 ? pos : 0;
            for (int s = 0; s < top; s++)
                best = Math.Max(best, stackHeight[s] * (end - stackStart[s]));
            return best;
        }

        internal readonly struct Stats
        {
            public Stats(int mage, int mode, int mc, double bg, double fg, double act)
            {
                Mage = mage;
                Mode = mode;
                Mc = mc;
                Bg = bg;
                Fg = fg;
                Act = act;
            }

            public int Mage { get; }
            public int Mode { get; }
            public int Mc { get; }
            public double Bg { get; }
            public double Fg { get; }
            public double Act { get; }
        }

        /// <summary>
        /// Streams the reference's per-step statistics over one MergeLife run.
        /// Mirrors calc_objective_stats/is_lattice_stable: 'e1' is the merged lattice of
        /// the state *before* the latest step, 'e2' one step earlier (the reference
        /// stores evals with exactly this lag).
        /// </summary>
        internal sealed class RunEvaluator
        {
            private readonly int _height;
            private readonly int _width;
            private readonly int[] _hist = new int[256];
            // Two pooled merged-lattice buffers: only e1 and e2 are ever alive, so the
            // buffer falling off the end of the lag window is recycled as the next target.
            private readonly int[][] _avgPool;
            private int _nextPoolIndex;
            private int[]? _e1Avg;
            private int _e1Mode;
            private int[]? _e2Avg;
            private int _e2Mode;
            private readonly int[] _modeCnt;
            private readonly int[] _sameCnt;
            private readonly int[] _lastMode;
            private int _modeAge;
            private int _lastModeVal = -1; // merged values are 0..255; -1 = unset
            private int _mcNochange;
            private int _lastMc;

            public int TimeStep { get; private set; }

            public RunEvaluator(int height, int width)
            {
                _height = height;
                _width = width;
                _avgPool = new[] { new int[height * width], new int[height * width] };
                _modeCnt = new int[height * width];
                _sameCnt = new int[height * width];
                _lastMode = new int[height * width];
            }

            public void Step(MergeLife sim)
            {
                int cells = _height * _width;
                int[] avg = _avgPool[_nextPoolIndex];
                _nextPoolIndex ^= 1;
                Array.Clear(_hist, 0, 256);
                var state = sim.State;
                for (int i = 0; i < cells; i++)
                {
                    int merged = (state[i * 3] + state[i * 3 + 1] + state[i * 3 + 2]) / 3;
                    avg[i] = merged;
                    _hist[merged]++;
                }
                int mode = 0, best = -1;
                for (int v = 0; v < 256; v++)
                {
                    if (_hist[v] > best)
                    {
                        best = _hist[v];
                        mode = v;
                    }
                }
                sim.Step(1);
                _e2Avg = _e1Avg;
                _e2Mode = _e1Mode;
                _e1Avg = avg;
                _e1Mode = mode;
                TimeStep++;
            }

            public Stats ComputeStats()
            {
                int size = _height * _width;
                if (_e1Avg == null || _e2Avg == null)
                    return new Stats(0, 0, 0, 0, 0, 0);
                int[] d1 = _e1Avg;
                int[] d2 = _e2Avg;
                int md2 = _e2Mode;

                int mc = 0;
                for (int i = 0; i < size; i++)
                {
                    bool modeMask = d2[i] == md2;
                    _modeCnt[i] = modeMask ? _modeCnt[i] + 1 : 0;
                    // 2018 trainer threshold: >50 consecutive generations makes a
                    // cell stable background (spec/evolve.md "Objective statistics";
                    // the paper's 100 leaves the count structurally zero long enough
                    // that the freeze exit ends every run at ~101 generations).
                    if (_modeCnt[i] > 50)
                        mc++;
                }
                int sc = 0;
                for (int i = 0; i < size; i++)
                {
                    bool sameMask = d1[i] == d2[i] && d2[i] != md2;
                    _sameCnt[i] = sameMask ? _sameCnt[i] + 1 : 0;
                    if (_sameCnt[i] > 5)
                        sc++;
                }

                if (_lastModeVal < 0 || _lastModeVal != md2)
                {
                    _lastModeVal = md2;
                    _modeAge = 0;
                }
                else
                {
                    _modeAge++;
                }

                for (int i = 0; i < size; i++)
                    if (d2[i] == md2)
                        _lastMode[i] = TimeStep;

                int active = 0;
                if (TimeStep >= 25)
                {
                    for (int i = 0; i < size; i++)
                    {
                        int since = TimeStep - _lastMode[i];
                        if (since > 5 && since < 25)
                            active++;
                    }
                }

                return new Stats(
                    _modeAge, md2, mc,
                    (double)mc / size, (double)sc / size, (double)active / size);
            }

            /// <summary>
            /// The 2018 trainer's convergence test (spec/evolve.md): dead world,
            /// frozen background, or the cap. A capped run records maxSteps + 1
            /// steps — above the steps rule's band, scoring MaxWeight; staying
            /// alive to the cap is the treasure signature every published score
            /// came from.
            /// </summary>
            public bool IsStable(in Stats stats, int maxSteps)
            {
                if (TimeStep > 100 && stats.Bg < 0.01)
                    return true; // dead world: background exploded away
                if (_lastMc == stats.Mc)
                {
                    _mcNochange++;
                    if (_mcNochange > 100)
                        return true; // frozen background
                }
                else
                {
                    _mcNochange = 0;
                    _lastMc = stats.Mc;
                }
                return TimeStep > maxSteps;
            }

            public double LargestRectFraction(in Stats stats)
            {
                int size = _height * _width;
                var mask = new bool[size];
                for (int i = 0; i < size; i++)
                    mask[i] = _e2Avg![i] == stats.Mode;
                return (double)LargestRectangleArea(mask, _width, _height) / size;
            }
        }
    }
}
