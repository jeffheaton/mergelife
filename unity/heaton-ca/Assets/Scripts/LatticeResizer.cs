using System;
using HeatonCA.Engine;

namespace HeatonCAApp
{
    /// <summary>
    /// Re-cuts a MergeLife lattice to a new shape when the canvas resizes or the
    /// device rotates.
    ///
    /// PyQt re-seeded the whole world on every resize, so a rotation threw away
    /// whatever the user was watching. Here the overlapping cells survive and
    /// only the freshly exposed ones are seeded, which is what makes a rotation
    /// feel like widening a window rather than pressing Reset. The generation
    /// count belongs to the caller: the world continues, so it keeps counting.
    /// </summary>
    public static class LatticeResizer
    {
        /// <summary>
        /// The <paramref name="oldRgb"/> lattice re-cut to
        /// <paramref name="newW"/> x <paramref name="newH"/>, returned as fresh
        /// row-major RGB bytes (newW*newH*3).
        ///
        /// The overlap — the top-left min(oldW,newW) x min(oldH,newH) block — is
        /// copied byte for byte, so a shrink is a pure crop and consumes no
        /// randomness at all.
        ///
        /// Everything outside the overlap is soup from a fresh
        /// <c>Pcg32(soupSeed, 0)</c>. Draw order, exactly: walk the NEW lattice
        /// row-major (y = 0..newH-1, then x = 0..newW-1); for a cell inside the
        /// overlap copy its three bytes and draw nothing; for a cell outside it
        /// take three draws, R then G then B, each byte being
        /// <c>(byte)(NextU32() &amp; 0xFF)</c>. That is
        /// <see cref="MergeLife.SeedSoup"/>'s derivation and ordering, restricted
        /// to the new cells, so a grow-from-nothing (oldW = oldH = 0) reproduces
        /// a plain soup of the new size bit for bit.
        /// </summary>
        public static byte[] Recut(
            ReadOnlySpan<byte> oldRgb, int oldW, int oldH, int newW, int newH, ulong soupSeed)
        {
            if (newW < 0 || newH < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(newW), $"the new lattice cannot be negative ({newW}x{newH})");
            }
            if (oldW < 0 || oldH < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(oldW), $"the old lattice cannot be negative ({oldW}x{oldH})");
            }
            if (oldRgb.Length < oldW * oldH * 3)
            {
                throw new ArgumentException(
                    $"oldRgb holds {oldRgb.Length} bytes, short of the {oldW * oldH * 3} an {oldW}x{oldH} lattice needs",
                    nameof(oldRgb));
            }
            var result = new byte[newW * newH * 3];
            int overlapCols = Math.Min(oldW, newW);
            int overlapRows = Math.Min(oldH, newH);
            // A pure crop or an exact match keeps every byte and never touches
            // the RNG, so repeated resizes down and back up stay cheap.
            bool isCrop = newW <= oldW && newH <= oldH;
            Pcg32 rng = isCrop ? null : new Pcg32(soupSeed);
            for (int y = 0; y < newH; y++)
            {
                bool rowInOverlap = y < overlapRows;
                int destRow = y * newW * 3;
                int sourceRow = y * oldW * 3;
                for (int x = 0; x < newW; x++)
                {
                    int d = destRow + x * 3;
                    if (rowInOverlap && x < overlapCols)
                    {
                        int s = sourceRow + x * 3;
                        result[d] = oldRgb[s];
                        result[d + 1] = oldRgb[s + 1];
                        result[d + 2] = oldRgb[s + 2];
                    }
                    else
                    {
                        result[d] = (byte)(rng.NextU32() & 0xFF);
                        result[d + 1] = (byte)(rng.NextU32() & 0xFF);
                        result[d + 2] = (byte)(rng.NextU32() & 0xFF);
                    }
                }
            }
            return result;
        }
    }
}
