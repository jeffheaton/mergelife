using System.Collections.Generic;
using HeatonCA.Engine;
using NUnit.Framework;

namespace HeatonCA.Tests
{
    /// <summary>
    /// heaton-life spec/rng.md: PCG32 (XSH-RR, the pcg_basic reference), the one generator
    /// allowed to touch simulation state, pinned so seeded worlds replay identically across
    /// languages. Every implementation asserts the spec's six known answers for seed(42, 54);
    /// the default-stream answers below were worked from the spec's pseudocode independently
    /// of the engine, because the app seeds soups through that stream (initseq 0).
    /// </summary>
    public class Pcg32Tests
    {
        /// <summary>spec/rng.md "Known-answer test": seed(42, 54), in order.</summary>
        private static readonly uint[] KnownAnswersSeed42Seq54 =
        {
            0xA15C02B7, 0x7B47F409, 0xBA1D3330, 0x83D2F293, 0xBFA4784B, 0xCBED606E,
        };

        /// <summary>seed(5, 0), the soup seed the determinism self-check replays.</summary>
        private static readonly uint[] KnownAnswersSeed5DefaultStream =
        {
            0x0F5DEBA9, 0xFB55AB28, 0x5980A57F, 0x7C7AFF85, 0xD5E488E8, 0xF2FE585D,
        };

        /// <summary>seed(0, 0): the all-zero seed still produces the spec's stream, not zeros.</summary>
        private static readonly uint[] KnownAnswersSeed0DefaultStream =
        {
            0xE4C14788, 0x379C6516, 0x5C4AB3BB, 0x601D23E0, 0x1C382B8C, 0xD1FAAB16,
        };

        [Test]
        public void KnownAnswerSeed42Seq54()
        {
            CollectionAssert.AreEqual(KnownAnswersSeed42Seq54, Draw(new Pcg32(42, 54), KnownAnswersSeed42Seq54.Length));
        }

        [Test]
        public void KnownAnswersOnTheDefaultStream()
        {
            CollectionAssert.AreEqual(KnownAnswersSeed5DefaultStream, Draw(new Pcg32(5), KnownAnswersSeed5DefaultStream.Length));
            CollectionAssert.AreEqual(KnownAnswersSeed0DefaultStream, Draw(new Pcg32(0), KnownAnswersSeed0DefaultStream.Length));
        }

        /// <summary>spec/rng.md: initseq defaults to 0, so Pcg32(seed) is Pcg32(seed, 0).</summary>
        [Test]
        public void SequenceDefaultsToZero()
        {
            CollectionAssert.AreEqual(Draw(new Pcg32(42, 0), 64), Draw(new Pcg32(42), 64));
            CollectionAssert.AreEqual(Draw(new Pcg32(5, 0), 64), Draw(new Pcg32(5), 64));
        }

        [Test]
        public void SameSeedAndSequenceReplay()
        {
            CollectionAssert.AreEqual(Draw(new Pcg32(42, 54), 256), Draw(new Pcg32(42, 54), 256));
            CollectionAssert.AreEqual(Draw(new Pcg32(ulong.MaxValue, 7), 256), Draw(new Pcg32(ulong.MaxValue, 7), 256));
        }

        /// <summary>Pcg32(seed, 0) and Pcg32(seed, 1) are distinct streams for the same seed.</summary>
        [Test]
        public void DifferentSequencesGiveDifferentStreams()
        {
            foreach (ulong seed in new ulong[] { 0, 1, 5, 42, 0x9E3779B97F4A7C15UL })
            {
                uint[] stream0 = Draw(new Pcg32(seed, 0), 16);
                uint[] stream1 = Draw(new Pcg32(seed, 1), 16);
                CollectionAssert.AreNotEqual(stream0, stream1, $"seed {seed}");
                Assert.AreNotEqual(stream0[0], stream1[0], $"seed {seed}: first draws");
            }
        }

        [Test]
        public void DifferentSeedsGiveDifferentStreams()
        {
            CollectionAssert.AreNotEqual(Draw(new Pcg32(1), 16), Draw(new Pcg32(2), 16));
            CollectionAssert.AreNotEqual(Draw(new Pcg32(42, 54), 16), Draw(new Pcg32(43, 54), 16));
        }

        private static uint[] Draw(Pcg32 rng, int count)
        {
            var values = new uint[count];
            for (int i = 0; i < count; i++)
                values[i] = rng.NextU32();
            return values;
        }
    }
}
