// HeatonCA.Engine: copied verbatim from HeatonLife.Core (github.com/jeffheaton/heaton-life, dotnet/src/HeatonLife.Core/Pcg32.cs, commit 46a117baf33a0f916d39e2c05991604ae8a1b424).
// Copyright Jeff Heaton. Licensed under the Apache License, Version 2.0; see THIRD_PARTY_NOTICES.md.
// Modifications: namespace HeatonLife -> HeatonCA.Engine.
// Do not edit: re-run tools/import-engine.sh to sync.

namespace HeatonCA.Engine
{
    /// <summary>
    /// PCG32 (XSH-RR) — the spec-pinned RNG (spec/rng.md). Matches pcg_basic and the
    /// Python implementation exactly; simulation state must only be seeded through this.
    /// </summary>
    public sealed class Pcg32
    {
        private const ulong Mult = 6364136223846793005UL;

        private ulong _state;
        private readonly ulong _inc;

        public Pcg32(ulong seed, ulong seq = 0)
        {
            _state = 0;
            _inc = (seq << 1) | 1UL;
            NextU32();
            _state += seed;
            NextU32();
        }

        public uint NextU32()
        {
            ulong old = _state;
            _state = unchecked(old * Mult + _inc);
            uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorshifted >> rot) | (xorshifted << ((32 - rot) & 31));
        }
    }
}
