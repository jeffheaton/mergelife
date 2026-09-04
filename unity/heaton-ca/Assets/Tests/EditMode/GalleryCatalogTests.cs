using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using HeatonCA.Engine;
using HeatonCAApp;
using NUnit.Framework;
using UnityEngine;

namespace HeatonCA.Tests
{
    /// <summary>
    /// The shipped rule lists. <see cref="GalleryCatalog"/> transcribes them from
    /// the PyQt app, and the pre-rendered previews under Resources/Gallery are
    /// keyed by those exact strings, so the transcription is re-read from the
    /// Python source at test time rather than trusted: if someone edits either
    /// side, this fails instead of the gallery quietly losing a tile.
    ///
    /// The names come from the engine's featured catalog, which is a
    /// cross-implementation contract of its own; what is pinned here is which of
    /// them the gallery surfaces and in what order.
    /// </summary>
    public class GalleryCatalogTests
    {
        private const int GallerySize = 30;
        private const int NamedCount = 13;
        private const int PresetCount = 3;

        /// <summary>The PyQt sources, three directories above Assets/ in the mergelife checkout.</summary>
        private static string PyQtFile(string name) =>
            Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..", "..", "python", "application", "pyqt", name));

        /// <summary>The quoted rules of a named Python list literal, in source order.</summary>
        private static List<string> PythonRuleList(string fileName, string listName)
        {
            string path = PyQtFile(fileName);
            Assert.IsTrue(File.Exists(path), $"expected the PyQt source at {path}");
            string source = File.ReadAllText(path);
            Match block = Regex.Match(
                source, @"(?<![A-Za-z0-9_])" + listName + @"\s*=\s*\[(.*?)\]", RegexOptions.Singleline);
            Assert.IsTrue(block.Success, $"{fileName} has no {listName} list literal");
            var rules = new List<string>();
            foreach (Match m in Regex.Matches(block.Groups[1].Value, "\"([^\"]*)\""))
            {
                rules.Add(m.Groups[1].Value);
            }
            return rules;
        }

        [Test]
        public void GalleryHasThirtyUniqueCanonicalRules()
        {
            Assert.AreEqual(GallerySize, GalleryCatalog.Rules.Count);
            var seen = new HashSet<string>();
            foreach (string rule in GalleryCatalog.Rules)
            {
                Assert.IsNull(MergeLife.RuleError(rule), rule);
                Assert.AreEqual(rule, MergeLife.CanonicalRule(rule), $"{rule} is not canonical");
                Assert.IsTrue(seen.Add(rule), $"{rule} appears twice");
            }
        }

        [Test]
        public void GalleryMatchesThePyQtSourceExactly()
        {
            List<string> python = PythonRuleList("tab_gallery.py", "GALLERY_RULES");
            CollectionAssert.AreEqual(python, GalleryCatalog.Rules);
        }

        [Test]
        public void PresetsMatchThePyQtSourceExactly()
        {
            List<string> python = PythonRuleList("tab_simulate.py", "RULES");
            Assert.AreEqual(PresetCount, python.Count);
            CollectionAssert.AreEqual(python, GalleryCatalog.Presets);
        }

        [Test]
        public void PresetsAreValidAndInTheGallery()
        {
            Assert.AreEqual(PresetCount, GalleryCatalog.Presets.Count);
            foreach (string rule in GalleryCatalog.Presets)
            {
                Assert.IsNull(MergeLife.RuleError(rule), rule);
                Assert.AreEqual(rule, MergeLife.CanonicalRule(rule), $"{rule} is not canonical");
                CollectionAssert.Contains(GalleryCatalog.Rules, rule);
            }
            // The first preset is the paper's rule, as the dropdown opens on it.
            Assert.AreEqual(GalleryCatalog.DefaultRule, GalleryCatalog.Presets[0]);
        }

        [Test]
        public void DefaultRuleIsRedWorld()
        {
            Assert.AreEqual("e542-5f79-9341-f31e-6c6b-7f08-8773-7068", GalleryCatalog.DefaultRule);
            Assert.AreEqual(MergeLife.DefaultRule, GalleryCatalog.DefaultRule);
            Assert.AreEqual(GalleryCatalog.Rules[0], GalleryCatalog.DefaultRule);
            Assert.AreEqual("Red World (paper)", GalleryCatalog.Name(GalleryCatalog.DefaultRule));
        }

        [Test]
        public void ThirteenGalleryRulesAreNamedInGalleryOrder()
        {
            Assert.AreEqual(NamedCount, GalleryCatalog.NamedRules.Count);
            int previous = -1;
            var names = new HashSet<string>();
            foreach ((string name, string rule) in GalleryCatalog.NamedRules)
            {
                Assert.IsNotEmpty(name);
                Assert.AreEqual(name, GalleryCatalog.Name(rule));
                Assert.IsTrue(names.Add(name), $"{name} appears twice");
                int index = IndexInGallery(rule);
                Assert.Greater(index, previous, $"{name} is out of gallery order");
                previous = index;
            }
            Assert.AreEqual("Red World (paper)", GalleryCatalog.NamedRules[0].Name);
            Assert.AreEqual("Mood Ring", GalleryCatalog.NamedRules[NamedCount - 1].Name);
        }

        [Test]
        public void NamedRulesAreExactlyTheFeaturedRulesTheGalleryCarries()
        {
            var featured = new Dictionary<string, string>();
            foreach (FeaturedRule entry in MergeLifeGallery.All)
            {
                featured[entry.Rule] = entry.Name;
            }
            var expected = new List<string>();
            foreach (string rule in GalleryCatalog.Rules)
            {
                if (featured.ContainsKey(rule))
                {
                    expected.Add(rule);
                }
            }
            var actual = new List<string>();
            foreach ((string _, string rule) in GalleryCatalog.NamedRules)
            {
                actual.Add(rule);
            }
            CollectionAssert.AreEqual(expected, actual);
            // Cobalt Reef and Diamond Mine are featured but were never in the
            // PyQt gallery, so they carry names without carrying tiles.
            Assert.AreEqual(2, MergeLifeGallery.All.Length - NamedCount);
            Assert.AreEqual("Cobalt Reef", GalleryCatalog.Name("e542-9341-6c6b-f31e-5f79-7f08-8773-7068"));
            CollectionAssert.DoesNotContain(
                GalleryCatalog.Rules, "e542-9341-6c6b-f31e-5f79-7f08-8773-7068");
        }

        [Test]
        public void UnnamedRulesReturnNullAndBadInputDoesNotThrow()
        {
            // Seventeen of the thirty show their hex alone, as PyQt showed all of them.
            int unnamed = 0;
            foreach (string rule in GalleryCatalog.Rules)
            {
                if (GalleryCatalog.Name(rule) == null)
                {
                    unnamed++;
                }
            }
            Assert.AreEqual(GallerySize - NamedCount, unnamed);
            Assert.IsNull(GalleryCatalog.Name(null));
            Assert.IsNull(GalleryCatalog.Name(string.Empty));
            Assert.IsNull(GalleryCatalog.Name("not a rule"));
            Assert.IsNull(GalleryCatalog.Name("e542-5f79-9341-f31e-6c6b-7f08-8773-706"));
        }

        [Test]
        public void NameAcceptsAnyFormTheRuleCanBeWrittenIn()
        {
            Assert.AreEqual("Pen and Ink", GalleryCatalog.Name("A07F-C000-0000-0000-0000-0000-FF80-807F"));
            Assert.AreEqual("Pen and Ink", GalleryCatalog.Name("a07fc0000000000000000000ff80807f"));
        }

        [Test]
        public void BothNearDuplicatePairsSurvive()
        {
            // Two pairs in the shipped gallery differ by a couple of octets and
            // look nothing alike. They are the reason the gallery is worth
            // browsing, so a de-duplication pass must never eat them.
            AssertNearDuplicatePair(
                "7b58-f7b4-c5b4-fd87-22fa-eb10-6de8-107c",  // High Noon
                "7b58-f7b4-a5b4-fd87-22fa-eb12-6de8-107f");
            AssertNearDuplicatePair(
                "2152-9b71-abb7-162a-45ff-dd03-fe15-957e",  // Mood Ring
                "2152-9b71-bbc7-162a-c5ff-ad03-fd65-957e");
        }

        private static void AssertNearDuplicatePair(string named, string sibling)
        {
            CollectionAssert.Contains(GalleryCatalog.Rules, named);
            CollectionAssert.Contains(GalleryCatalog.Rules, sibling);
            Assert.AreNotEqual(named, sibling);
            Assert.IsNotNull(GalleryCatalog.Name(named), $"{named} should be a featured rule");
            Assert.IsNull(GalleryCatalog.Name(sibling), $"{sibling} is the unnamed sibling");
            Assert.AreEqual(named.Length, sibling.Length);
            int differing = 0;
            for (int i = 0; i < named.Length; i++)
            {
                if (named[i] != sibling[i])
                {
                    differing++;
                }
            }
            Assert.Greater(differing, 0);
            Assert.LessOrEqual(differing, 8, $"{named} and {sibling} are not near-duplicates");
        }

        private static int IndexInGallery(string rule)
        {
            for (int i = 0; i < GalleryCatalog.Rules.Count; i++)
            {
                if (GalleryCatalog.Rules[i] == rule)
                {
                    return i;
                }
            }
            Assert.Fail($"{rule} is not in the gallery");
            return -1;
        }
    }
}
