using System;
using HeatonCA.Engine;
using HeatonCAApp;
using NUnit.Framework;

namespace HeatonCA.Tests
{
    /// <summary>
    /// The resize/rotation re-cut. Three properties carry the whole feature:
    /// overlapping cells survive byte for byte (a rotation is not a Reset), the
    /// newly exposed cells are ordinary MergeLife soup drawn in a documented
    /// order, and shrinking touches the RNG not at all.
    ///
    /// The soup half is checked against an independent <see cref="Pcg32"/>
    /// replay written in the test, not against the resizer's own output, so a
    /// change to the draw order fails here instead of silently redefining it.
    /// </summary>
    public class LatticeResizerTests
    {
        private const ulong Seed = 12345;

        /// <summary>A recognizable old lattice: every byte derived from its position.</summary>
        private static byte[] MakeLattice(int width, int height)
        {
            var rgb = new byte[width * height * 3];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = (y * width + x) * 3;
                    rgb[i] = (byte)(x * 7 + 1);
                    rgb[i + 1] = (byte)(y * 11 + 2);
                    rgb[i + 2] = (byte)(x * y + 3);
                }
            }
            return rgb;
        }

        [Test]
        public void OverlappingCellsAreCopiedByteForByte()
        {
            byte[] old = MakeLattice(20, 12);
            byte[] recut = LatticeResizer.Recut(old, 20, 12, 30, 18, Seed);
            Assert.AreEqual(30 * 18 * 3, recut.Length);
            for (int y = 0; y < 12; y++)
            {
                for (int x = 0; x < 20; x++)
                {
                    int s = (y * 20 + x) * 3;
                    int d = (y * 30 + x) * 3;
                    Assert.AreEqual(old[s], recut[d], $"R at ({x},{y})");
                    Assert.AreEqual(old[s + 1], recut[d + 1], $"G at ({x},{y})");
                    Assert.AreEqual(old[s + 2], recut[d + 2], $"B at ({x},{y})");
                }
            }
        }

        [Test]
        public void NewCellsMatchAnIndependentPcgReplay()
        {
            const int OldW = 20, OldH = 12, NewW = 30, NewH = 18;
            byte[] old = MakeLattice(OldW, OldH);
            byte[] recut = LatticeResizer.Recut(old, OldW, OldH, NewW, NewH, Seed);
            // The documented order: walk the new lattice row-major; a cell inside
            // the top-left overlap draws nothing, a cell outside takes three
            // draws, R then G then B, each (byte)(NextU32() & 0xFF).
            var rng = new Pcg32(Seed);
            for (int y = 0; y < NewH; y++)
            {
                for (int x = 0; x < NewW; x++)
                {
                    if (y < OldH && x < OldW)
                    {
                        continue;
                    }
                    int d = (y * NewW + x) * 3;
                    Assert.AreEqual((byte)(rng.NextU32() & 0xFF), recut[d], $"R at ({x},{y})");
                    Assert.AreEqual((byte)(rng.NextU32() & 0xFF), recut[d + 1], $"G at ({x},{y})");
                    Assert.AreEqual((byte)(rng.NextU32() & 0xFF), recut[d + 2], $"B at ({x},{y})");
                }
            }
        }

        [Test]
        public void GrowingFromNothingReproducesAPlainSoup()
        {
            // With no overlap every cell is new, so the re-cut must equal the
            // engine's own soup for the same seed — the proof that the byte
            // derivation and the draw order are MergeLife's, not a lookalike.
            const int Width = 16, Height = 9;
            byte[] recut = LatticeResizer.Recut(ReadOnlySpan<byte>.Empty, 0, 0, Width, Height, Seed);
            var world = new MergeLife(MergeLife.DefaultRule, Width, Height);
            world.SeedSoup(Seed);
            byte[] soup = world.State.ToArray();
            CollectionAssert.AreEqual(soup, recut);
        }

        [Test]
        public void ShrinkingIsAPureCrop()
        {
            byte[] old = MakeLattice(30, 18);
            byte[] recut = LatticeResizer.Recut(old, 30, 18, 20, 12, Seed);
            Assert.AreEqual(20 * 12 * 3, recut.Length);
            for (int y = 0; y < 12; y++)
            {
                for (int x = 0; x < 20; x++)
                {
                    int s = (y * 30 + x) * 3;
                    int d = (y * 20 + x) * 3;
                    Assert.AreEqual(old[s], recut[d], $"R at ({x},{y})");
                    Assert.AreEqual(old[s + 1], recut[d + 1], $"G at ({x},{y})");
                    Assert.AreEqual(old[s + 2], recut[d + 2], $"B at ({x},{y})");
                }
            }
            // A crop consumes no randomness, so the seed cannot matter.
            byte[] otherSeed = LatticeResizer.Recut(old, 30, 18, 20, 12, Seed + 999);
            CollectionAssert.AreEqual(recut, otherSeed);
        }

        [Test]
        public void SameShapeIsAnExactCopy()
        {
            byte[] old = MakeLattice(11, 7);
            byte[] recut = LatticeResizer.Recut(old, 11, 7, 11, 7, Seed);
            CollectionAssert.AreEqual(old, recut);
        }

        [Test]
        public void MixedResizeKeepsTheOverlapAndSeedsTheRest()
        {
            // Landscape to portrait: narrower and taller, so cells are lost on
            // the right and gained at the bottom in one pass.
            const int OldW = 24, OldH = 10, NewW = 16, NewH = 20;
            byte[] old = MakeLattice(OldW, OldH);
            byte[] recut = LatticeResizer.Recut(old, OldW, OldH, NewW, NewH, Seed);
            var rng = new Pcg32(Seed);
            for (int y = 0; y < NewH; y++)
            {
                for (int x = 0; x < NewW; x++)
                {
                    int d = (y * NewW + x) * 3;
                    if (y < OldH)
                    {
                        int s = (y * OldW + x) * 3;
                        Assert.AreEqual(old[s], recut[d], $"kept R at ({x},{y})");
                        Assert.AreEqual(old[s + 1], recut[d + 1], $"kept G at ({x},{y})");
                        Assert.AreEqual(old[s + 2], recut[d + 2], $"kept B at ({x},{y})");
                        continue;
                    }
                    Assert.AreEqual((byte)(rng.NextU32() & 0xFF), recut[d], $"new R at ({x},{y})");
                    Assert.AreEqual((byte)(rng.NextU32() & 0xFF), recut[d + 1], $"new G at ({x},{y})");
                    Assert.AreEqual((byte)(rng.NextU32() & 0xFF), recut[d + 2], $"new B at ({x},{y})");
                }
            }
        }

        [Test]
        public void TheSameResizeIsRepeatable()
        {
            byte[] old = MakeLattice(20, 12);
            byte[] first = LatticeResizer.Recut(old, 20, 12, 33, 21, Seed);
            byte[] second = LatticeResizer.Recut(old, 20, 12, 33, 21, Seed);
            CollectionAssert.AreEqual(first, second);
            // A different seed changes the new cells but not the kept ones.
            byte[] other = LatticeResizer.Recut(old, 20, 12, 33, 21, Seed + 1);
            CollectionAssert.AreNotEqual(first, other);
            Assert.AreEqual(first[0], other[0]);
        }
    }
}
