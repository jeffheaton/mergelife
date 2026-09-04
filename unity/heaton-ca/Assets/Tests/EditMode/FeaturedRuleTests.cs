using System.Collections.Generic;
using HeatonCA.Engine;
using NUnit.Framework;

namespace HeatonCA.Tests
{
    /// <summary>
    /// Pins for the featured MergeLife rules (heaton-life spec/mergelife.md
    /// "Featured rules"), ported from MergeLifeGalleryTests.cs. The catalog is a
    /// cross-implementation contract with no vector files behind it, so the suite
    /// is what holds it: every rule must parse, already be canonical, and actually
    /// run; the presentation text must be present and unambiguous; and the order,
    /// which the Simulator's preset list shows, is part of the set.
    /// </summary>
    public class FeaturedRuleTests
    {
        private const int GallerySize = 15;

        [Test]
        public void GalleryHasFifteenEntriesInSpecOrder()
        {
            Assert.AreEqual(GallerySize, MergeLifeGallery.All.Length);
            // Entry 1 is the paper's rule and the family default; entry 2 is the
            // engineered sibling that must sit beside its parent.
            Assert.AreEqual("Red World (paper)", MergeLifeGallery.All[0].Name);
            Assert.AreEqual("e542-5f79-9341-f31e-6c6b-7f08-8773-7068", MergeLifeGallery.All[0].Rule);
            Assert.AreEqual("Cobalt Reef", MergeLifeGallery.All[1].Name);
            Assert.AreEqual("Pen and Ink", MergeLifeGallery.All[2].Name);
            Assert.AreEqual("a07f-c000-0000-0000-0000-0000-ff80-807f", MergeLifeGallery.All[2].Rule);
            Assert.AreEqual("Mood Ring", MergeLifeGallery.All[GallerySize - 1].Name);
        }

        [Test]
        public void TheDefaultRuleIsTheFirstEntry()
        {
            Assert.AreEqual(MergeLifeGallery.All[0].Rule, MergeLife.CanonicalRule(MergeLife.DefaultRule));
        }

        [Test]
        public void EveryRuleIsValidAndAlreadyCanonical()
        {
            foreach (FeaturedRule entry in MergeLifeGallery.All)
            {
                Assert.IsNull(MergeLife.RuleError(entry.Rule), entry.Name);
                // The spec requires the stored form to be canonical, so a host can
                // compare a world's rule against the gallery without normalizing.
                Assert.AreEqual(entry.Rule, MergeLife.CanonicalRule(entry.Rule), entry.Name);
                Assert.AreEqual(8, MergeLife.CompileRule(entry.Rule).Length, entry.Name);
            }
        }

        [Test]
        public void EveryEntryCarriesPresentationText()
        {
            foreach (FeaturedRule entry in MergeLifeGallery.All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.Name), entry.Rule);
                Assert.AreEqual(entry.Name.Trim(), entry.Name, entry.Rule);
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.Description), entry.Rule);
                Assert.AreEqual(entry.Description.Trim(), entry.Description, entry.Rule);
                StringAssert.EndsWith(".", entry.Description, entry.Rule);
            }
        }

        [Test]
        public void RulesAndNamesAreUnique()
        {
            var rules = new HashSet<string>();
            var names = new HashSet<string>();
            foreach (FeaturedRule entry in MergeLifeGallery.All)
            {
                Assert.IsTrue(rules.Add(entry.Rule), $"duplicate rule: {entry.Rule}");
                Assert.IsTrue(names.Add(entry.Name), $"duplicate name: {entry.Name}");
            }
            Assert.AreEqual(GallerySize, rules.Count);
            Assert.AreEqual(GallerySize, names.Count);
        }

        [Test]
        public void CobaltReefIsAPermutationOfRedWorld()
        {
            // Its provenance claim in the spec: same octets, reordered.
            string[] red = MergeLifeGallery.All[0].Rule.Split('-');
            string[] cobalt = MergeLifeGallery.All[1].Rule.Split('-');
            System.Array.Sort(red);
            System.Array.Sort(cobalt);
            CollectionAssert.AreEqual(red, cobalt);
            Assert.AreNotEqual(MergeLifeGallery.All[0].Rule, MergeLifeGallery.All[1].Rule);
        }

        [Test]
        public void EveryRuleActuallyRuns()
        {
            // A gallery entry that cannot drive a world is not a featured rule.
            var rgb = new byte[32 * 32 * 3];
            foreach (FeaturedRule entry in MergeLifeGallery.All)
            {
                var world = new MergeLife(entry.Rule, 32, 32);
                Assert.AreEqual(entry.Rule, world.Rule, entry.Name);
                world.SeedSoup(7);
                world.Step(10);
                world.WriteFrame(rgb);
                Assert.AreEqual(10, world.Generation, entry.Name);
            }
        }
    }
}
