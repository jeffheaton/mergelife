// HeatonCA.Engine: copied verbatim from HeatonLife.Core (github.com/jeffheaton/heaton-life, dotnet/src/HeatonLife.Core/Evolver.cs, commit 46a117baf33a0f916d39e2c05991604ae8a1b424).
// Copyright Jeff Heaton. Licensed under the Apache License, Version 2.0; see THIRD_PARTY_NOTICES.md.
// Modifications: namespace HeatonLife -> HeatonCA.Engine.
// Do not edit: re-run tools/import-engine.sh to sync.

using System;
using System.Collections.Generic;

namespace HeatonCA.Engine
{
    /// <summary>A scored MergeLife genome.</summary>
    public sealed class Candidate
    {
        public Candidate(string genome, double score)
        {
            Genome = genome;
            Score = score;
        }

        public string Genome { get; }
        public double Score { get; }
    }

    /// <summary>
    /// GA operators for MergeLife genomes, paper Sec. 5 (spec/evolve.md). Verbatim
    /// ports of the reference: mutation exchanges two distinct hex digits (dashes
    /// untouched); crossover cuts the dashed string at two points CutLength apart
    /// and swaps the middle; selection is best-of-N tournament. Draw order from the
    /// supplied PCG32 is part of the contract.
    /// </summary>
    public static class GaOperators
    {
        /// <summary>Paper Sec. 5.2: one 4-digit sub-rule plus a dash.</summary>
        public const int CutLength = 5;

        /// <summary>Exchange two random, distinct, non-dash characters (paper Sec. 5.3).</summary>
        public static string Mutate(string genome, Pcg32 rng)
        {
            var distinct = new HashSet<char>();
            foreach (char c in genome)
                if (c != '-')
                    distinct.Add(c);
            if (distinct.Count < 2)
                return genome; // all digits identical: no swap can change anything
            while (true)
            {
                int i = (int)(rng.NextU32() % (uint)genome.Length);
                int j = (int)(rng.NextU32() % (uint)genome.Length);
                if (genome[i] != '-' && genome[j] != '-' && genome[i] != genome[j])
                {
                    var chars = genome.ToCharArray();
                    (chars[i], chars[j]) = (chars[j], chars[i]);
                    return new string(chars);
                }
            }
        }

        /// <summary>Two complementary children; the middle splice swaps parents (paper Sec. 5.2).</summary>
        public static (string First, string Second) Crossover(
            string genome1, string genome2, Pcg32 rng, int cutLength = CutLength)
        {
            int cut1 = (int)(rng.NextU32() % (uint)(genome1.Length - cutLength));
            int cut2 = cut1 + cutLength;
            string first = genome1.Substring(0, cut1)
                + genome2.Substring(cut1, cutLength)
                + genome1.Substring(cut2);
            string second = genome2.Substring(0, cut1)
                + genome1.Substring(cut1, cutLength)
                + genome2.Substring(cut2);
            return (first, second);
        }

        /// <summary>Best-of-<paramref name="rounds"/> tournament over indices (or worst-of, for eviction).</summary>
        public static int TournamentSelect(
            IReadOnlyList<double> scores, int rounds, Pcg32 rng, bool worst = false)
        {
            int winner = -1;
            for (int r = 0; r < rounds; r++)
            {
                int challenger = (int)(rng.NextU32() % (uint)scores.Count);
                if (winner < 0)
                    winner = challenger;
                else if (worst ? scores[challenger] < scores[winner]
                               : scores[challenger] > scores[winner])
                    winner = challenger;
            }
            return winner;
        }
    }

    /// <summary>
    /// Steady-state GA over MergeLife genomes, deterministic from its seed
    /// (spec/evolve.md): every random decision draws from one PCG32 stream
    /// (sequence 1 — distinct from lattice seeding), so a run replays exactly,
    /// cross-language, gated by vectors/evolve/mini-run-24.
    /// </summary>
    public sealed class Evolver
    {
        private readonly IReadOnlyList<ObjectiveRule> _objective;
        private readonly Pcg32 _rng;
        private readonly Action<Evolver>? _onProgress;
        private readonly List<double> _scoresScratch = new List<double>();

        public int Width { get; }
        public int Height { get; }
        public int PopulationSize { get; }
        public double CrossoverRate { get; }
        public int TournamentRounds { get; }
        public int EvalCycles { get; }
        public int Patience { get; }
        public int MaxSteps { get; }
        public ulong Seed { get; }
        public List<Candidate> Population { get; } = new List<Candidate>();
        public Candidate? Best { get; private set; }

        /// <summary>Worker count for objective evaluation; results are identical for any value.</summary>
        public int Workers { get; }
        public int Evals { get; private set; }
        public int NoImprovement { get; private set; }

        public Evolver(
            int width = 100,
            int height = 100,
            int populationSize = 100,
            double crossoverRate = 0.75,
            int tournamentRounds = 5,
            int evalCycles = 5,
            int patience = 1000,
            int maxSteps = 1000,
            ulong seed = 0,
            IReadOnlyList<ObjectiveRule>? objective = null,
            Action<Evolver>? onProgress = null,
            int workers = 1)
        {
            Workers = workers;
            Width = width;
            Height = height;
            PopulationSize = populationSize;
            CrossoverRate = crossoverRate;
            TournamentRounds = tournamentRounds;
            EvalCycles = evalCycles;
            Patience = patience;
            MaxSteps = maxSteps;
            Seed = seed;
            _objective = objective ?? MergeLifeObjective.PaperObjective;
            _rng = new Pcg32(seed, 1); // seq 1: distinct stream from sim seeding
            _onProgress = onProgress;
        }

        /// <summary>Seed a random population, then evolve until maxEvals or patience runs out.</summary>
        public Candidate Run(int maxEvals)
        {
            while (Population.Count < PopulationSize && Evals < maxEvals)
                Admit(MergeLife.RandomRule(_rng.NextU32()));
            while (Evals < maxEvals && NoImprovement <= Patience)
            {
                var scores = CurrentScores();
                if (scores.Count > 1 && _rng.NextU32() / 4294967296.0 < CrossoverRate)
                {
                    int first = GaOperators.TournamentSelect(scores, TournamentRounds, _rng);
                    int second = first;
                    while (second == first)
                        second = GaOperators.TournamentSelect(scores, TournamentRounds, _rng);
                    string parent1 = Population[first].Genome;
                    string parent2 = Population[second].Genome;
                    if (parent1 != parent2)
                    {
                        var (childA, childB) = GaOperators.Crossover(parent1, parent2, _rng);
                        Admit(childA);
                        Admit(childB);
                    }
                }
                else
                {
                    int parent = GaOperators.TournamentSelect(scores, TournamentRounds, _rng);
                    Admit(GaOperators.Mutate(Population[parent].Genome, _rng));
                }
            }
            if (Best == null)
                throw new InvalidOperationException("run produced no candidates");
            return Best;
        }

        private List<double> CurrentScores()
        {
            _scoresScratch.Clear();
            foreach (var candidate in Population)
                _scoresScratch.Add(candidate.Score);
            return _scoresScratch;
        }

        private void Admit(string genome)
        {
            while (Population.Count + 1 > PopulationSize)
            {
                int evict = GaOperators.TournamentSelect(
                    CurrentScores(), TournamentRounds, _rng, worst: true);
                Population.RemoveAt(evict);
            }
            var candidate = Score(genome);
            Population.Add(candidate);
            if (Best == null || candidate.Score > Best.Score)
            {
                Best = candidate;
                NoImprovement = 0;
            }
            else
            {
                NoImprovement++;
            }
        }

        private Candidate Score(string genome)
        {
            var (score, _) = MergeLifeObjective.ScoreGenome(
                genome,
                _objective,
                EvalCycles,
                Width,
                Height,
                Seed + (ulong)(Evals * EvalCycles),
                MaxSteps,
                Workers);
            Evals++;
            var candidate = new Candidate(genome, score);
            _onProgress?.Invoke(this);
            return candidate;
        }
    }
}
