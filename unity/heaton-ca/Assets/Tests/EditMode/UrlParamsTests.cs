using HeatonCAApp;
using NUnit.Framework;

namespace HeatonCA.Tests
{
    /// <summary>
    /// UrlParams: the ?rule= / ?size= / ?controls= contract the old
    /// js/web-full-screen embeds rely on, pinned so a WebGL build keeps opening
    /// links that were written against ml-fullscreen2.js. Pure parsing, so these
    /// run in the Editor with no browser and no player.
    /// </summary>
    public class UrlParamsTests
    {
        /// <summary>The first gallery rule, in the canonical lowercase dashed form.</summary>
        private const string Canonical = "e542-5f79-9341-f31e-6c6b-7f08-8773-7068";

        /// <summary>The same rule as an embed link would carry it: uppercase, no dashes.</summary>
        private const string Undashed = "E5425F799341F31E6C6B7F0887737068";

        [Test]
        public void DashedRuleParsesToItself()
        {
            UrlParams p = UrlParams.Parse("https://example.com/ca/?rule=" + Canonical);

            Assert.AreEqual(Canonical, p.Rule);
            Assert.IsNull(p.CellSize);
            Assert.IsNull(p.Controls);
        }

        [Test]
        public void MixedCaseUndashedRuleCanonicalizes()
        {
            UrlParams p = UrlParams.Parse("?rule=" + Undashed);

            Assert.AreEqual(Canonical, p.Rule);
        }

        [Test]
        public void PercentEncodedRuleDecodes()
        {
            // %2D is '-': some embed generators escape every non-alphanumeric.
            UrlParams p = UrlParams.Parse("?rule=e542%2D5f79%2D9341%2Df31e%2D6c6b%2D7f08%2D8773%2D7068");

            Assert.AreEqual(Canonical, p.Rule);
        }

        [Test]
        public void InvalidRuleIsNull()
        {
            Assert.IsNull(UrlParams.Parse("?rule=nope").Rule);
            Assert.IsNull(UrlParams.Parse("?rule=e542-5f79").Rule);
            Assert.IsNull(UrlParams.Parse("?rule=").Rule);
            Assert.IsNull(UrlParams.Parse("?rule=zzzz-5f79-9341-f31e-6c6b-7f08-8773-7068").Rule);
        }

        [Test]
        public void SizeInRangeParses()
        {
            Assert.AreEqual(1, UrlParams.Parse("?size=1").CellSize);
            Assert.AreEqual(9, UrlParams.Parse("?size=9").CellSize);
            Assert.AreEqual(25, UrlParams.Parse("?size=25").CellSize);
        }

        [Test]
        public void SizeOutOfRangeOrJunkIsNull()
        {
            Assert.IsNull(UrlParams.Parse("?size=0").CellSize);
            Assert.IsNull(UrlParams.Parse("?size=26").CellSize);
            Assert.IsNull(UrlParams.Parse("?size=-4").CellSize);
            Assert.IsNull(UrlParams.Parse("?size=4.5").CellSize);
            Assert.IsNull(UrlParams.Parse("?size=big").CellSize);
            Assert.IsNull(UrlParams.Parse("?size=").CellSize);
        }

        [Test]
        public void ControlsAcceptsBothDirectionsInAnyCasing()
        {
            Assert.IsTrue(UrlParams.Parse("?controls=on").Controls);
            Assert.IsTrue(UrlParams.Parse("?controls=1").Controls);
            Assert.IsTrue(UrlParams.Parse("?controls=TRUE").Controls);
            Assert.IsFalse(UrlParams.Parse("?controls=off").Controls);
            Assert.IsFalse(UrlParams.Parse("?controls=0").Controls);
            Assert.IsFalse(UrlParams.Parse("?controls=False").Controls);
            Assert.IsNull(UrlParams.Parse("?controls=maybe").Controls);
            Assert.IsNull(UrlParams.Parse("?controls=").Controls);
        }

        [Test]
        public void AllThreeParametersTogether()
        {
            UrlParams p = UrlParams.Parse(
                "https://heatonresearch.com/ca/index.html?rule=" + Undashed + "&size=12&controls=off");

            Assert.AreEqual(Canonical, p.Rule);
            Assert.AreEqual(12, p.CellSize);
            Assert.IsFalse(p.Controls);
        }

        [Test]
        public void NoQueryLeavesEverythingNull()
        {
            foreach (string url in new[]
            {
                null,
                "",
                "https://heatonresearch.com/ca/index.html",
                "?",
                "&&",
                "not a url at all",
            })
            {
                UrlParams p = UrlParams.Parse(url);
                Assert.IsNotNull(p, url);
                Assert.IsNull(p.Rule, url);
                Assert.IsNull(p.CellSize, url);
                Assert.IsNull(p.Controls, url);
            }
        }

        [Test]
        public void FragmentIsIgnored()
        {
            // A '#' ends the query, and a query-looking fragment is not a query.
            UrlParams withBoth = UrlParams.Parse("?size=7#rule=" + Canonical);
            Assert.AreEqual(7, withBoth.CellSize);
            Assert.IsNull(withBoth.Rule);

            UrlParams fragmentOnly = UrlParams.Parse("https://example.com/ca#rule=" + Canonical);
            Assert.IsNull(fragmentOnly.Rule);
        }

        [Test]
        public void RepeatedKeyTakesTheLastValue()
        {
            Assert.AreEqual(20, UrlParams.Parse("?size=4&size=20").CellSize);
            Assert.IsFalse(UrlParams.Parse("?controls=on&controls=off").Controls);
            Assert.AreEqual(Canonical, UrlParams.Parse("?rule=bogus&rule=" + Canonical).Rule);

            // Last wins even when the last one is the bad one.
            Assert.IsNull(UrlParams.Parse("?rule=" + Canonical + "&rule=bogus").Rule);
            Assert.IsNull(UrlParams.Parse("?size=8&size=99").CellSize);
        }

        [Test]
        public void UnknownKeysAndBareTokensAreIgnored()
        {
            UrlParams p = UrlParams.Parse("?utm_source=blog&flag&=orphan&SIZE=6&rule=" + Canonical);

            Assert.AreEqual(Canonical, p.Rule);
            Assert.AreEqual(6, p.CellSize, "keys match case-insensitively");
            Assert.IsNull(p.Controls);
        }

        [Test]
        public void BareQueryWithoutQuestionMarkIsAccepted()
        {
            // WebGlBridge.GetQueryString() returns "?a=b", but a caller that already
            // stripped the '?' must get the same answer.
            UrlParams p = UrlParams.Parse("rule=" + Canonical + "&size=3");

            Assert.AreEqual(Canonical, p.Rule);
            Assert.AreEqual(3, p.CellSize);
        }
    }
}
