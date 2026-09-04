// HeatonCA.Engine: copied verbatim from HeatonLife.Core (github.com/jeffheaton/heaton-life, dotnet/src/HeatonLife.Core/Parallelism.cs, commit 46a117baf33a0f916d39e2c05991604ae8a1b424).
// Copyright Jeff Heaton. Licensed under the Apache License, Version 2.0; see THIRD_PARTY_NOTICES.md.
// Modifications: namespace HeatonLife -> HeatonCA.Engine.
// Do not edit: re-run tools/import-engine.sh to sync.

using System;
using System.Threading.Tasks;

namespace HeatonCA.Engine
{
    /// <summary>
    /// The one parallel primitive the library uses (spec/fractals.md "Parallel
    /// rendering", spec/evolve.md "Parallel evaluation"): an indexed loop whose
    /// iterations are independent and write disjoint slots, so the output is
    /// bit-identical for any worker count or schedule. workers &lt;= 1 runs the
    /// plain serial loop.
    /// </summary>
    public static class Parallelism
    {
        public static void For(int count, int workers, Action<int> body)
        {
            if (workers <= 1 || count <= 1)
            {
                for (int i = 0; i < count; i++)
                    body(i);
                return;
            }
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(workers, count),
            };
            Parallel.For(0, count, options, body);
        }
    }
}
