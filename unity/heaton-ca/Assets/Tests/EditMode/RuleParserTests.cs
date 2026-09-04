using HeatonCA.Engine;
using HeatonCAApp;
using NUnit.Framework;

namespace HeatonCA.Tests
{
    /// <summary>
    /// What the Simulator's rule box accepts. The tolerated forms are the ones
    /// rules are actually written in — papers and READMEs use uppercase, the
    /// command line writes <c>hex;name</c>, and a copy out of a table arrives
    /// with spaces or no dashes at all — and everything accepted normalizes to
    /// the one canonical spelling the rest of the app compares against.
    /// </summary>
    public class RuleParserTests
    {
        private const string RedWorld = "e542-5f79-9341-f31e-6c6b-7f08-8773-7068";

        private static void AssertParses(string input, string expected = RedWorld)
        {
            Assert.IsTrue(RuleParser.TryParse(input, out string canonical, out string error), input);
            Assert.AreEqual(expected, canonical, input);
            Assert.IsNull(error, input);
        }

        private static void AssertRejects(string input)
        {
            Assert.IsFalse(RuleParser.TryParse(input, out string canonical, out string error), input);
            Assert.IsNull(canonical, input);
            Assert.AreEqual(AppStrings.SimInvalidRule, error, input);
            Assert.IsNotEmpty(error, input);
        }

        [Test]
        public void CanonicalInputPassesThroughUnchanged()
        {
            AssertParses(RedWorld);
        }

        [Test]
        public void UppercaseIsLowercased()
        {
            AssertParses("E542-5F79-9341-F31E-6C6B-7F08-8773-7068");
            AssertParses("E542-5f79-9341-F31e-6C6B-7F08-8773-7068");
        }

        [Test]
        public void UndashedInputIsRegrouped()
        {
            AssertParses("e5425f799341f31e6c6b7f0887737068");
            AssertParses("E5425F799341F31E6C6B7F0887737068");
        }

        [Test]
        public void DashesInOddPlacesAreIgnored()
        {
            AssertParses("e5-42-5f-79-9341f31e-6c6b-7f08-8773-7068");
            AssertParses("-e542-5f79-9341-f31e-6c6b-7f08-8773-7068-");
        }

        [Test]
        public void SurroundingAndEmbeddedWhitespaceIsIgnored()
        {
            AssertParses("  e542-5f79-9341-f31e-6c6b-7f08-8773-7068  ");
            AssertParses("e542 5f79 9341 f31e 6c6b 7f08 8773 7068");
            AssertParses("e542\t5f79\n9341 f31e-6c6b-7f08-8773-7068");
        }

        [Test]
        public void ATrailingCommentIsDropped()
        {
            // The form the MergeLife command line writes its found rules in.
            AssertParses("e542-5f79-9341-f31e-6c6b-7f08-8773-7068;Red World");
            AssertParses("e542-5f79-9341-f31e-6c6b-7f08-8773-7068 ; score 2.31");
            // Everything after the first semicolon goes, semicolons included.
            AssertParses("e542-5f79-9341-f31e-6c6b-7f08-8773-7068;a;b;c");
        }

        [Test]
        public void TooFewOrTooManyDigitsAreRejected()
        {
            AssertRejects("e542-5f79-9341-f31e-6c6b-7f08-8773-706");   // 31
            AssertRejects("e542-5f79-9341-f31e-6c6b-7f08-8773-70680"); // 33
            AssertRejects("e542-5f79");
        }

        [Test]
        public void NonHexCharactersAreRejected()
        {
            AssertRejects("g542-5f79-9341-f31e-6c6b-7f08-8773-7068");
            AssertRejects("e542-5f79-9341-f31e-6c6b-7f08-8773-706z");
            AssertRejects("e542_5f79_9341_f31e_6c6b_7f08_8773_7068");
            AssertRejects("Red World");
        }

        [Test]
        public void NullAndEmptyAreRejected()
        {
            AssertRejects(null);
            AssertRejects(string.Empty);
            AssertRejects("   ");
            AssertRejects(";just a comment");
        }

        [Test]
        public void EveryGalleryAndFeaturedRuleRoundTrips()
        {
            foreach (string rule in GalleryCatalog.Rules)
            {
                AssertParses(rule, rule);
                AssertParses(rule.ToUpperInvariant(), rule);
                AssertParses(rule.Replace("-", string.Empty), rule);
            }
            foreach (FeaturedRule entry in MergeLifeGallery.All)
            {
                AssertParses(entry.Rule, entry.Rule);
            }
        }

        [Test]
        public void OutputAlwaysMatchesTheEngineCanonicalForm()
        {
            // The parser is a front door onto MergeLife.CanonicalRule, never a
            // second normalizer that could drift from it.
            const string Messy = " A07F c000-0000-0000-0000-0000-FF80-807f ;pen and ink ";
            Assert.IsTrue(RuleParser.TryParse(Messy, out string canonical, out _));
            Assert.AreEqual(MergeLife.CanonicalRule(canonical), canonical);
            Assert.AreEqual("a07f-c000-0000-0000-0000-0000-ff80-807f", canonical);
        }
    }
}
