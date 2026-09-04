using System.Collections;
using System.Threading.Tasks;
using HeatonCA.Engine;
using HeatonCAApp;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace HeatonCA.PlayTests
{
    /// <summary>
    /// Parity checks for <see cref="RuleDecoderView"/>, the port of the PyQt
    /// "Rule" tab. The expected tables below are the ones
    /// <c>Assets/Tests/Vectors~/mergelife-decode/*/params.json</c> describes,
    /// written out through PyQt's own formatting rules, so a change in either
    /// the decode or the formatting shows up here as a string mismatch rather
    /// than as a subtly different screen.
    ///
    /// The screen is built into a fixed-size host rect rather than into the
    /// whole canvas, so the phone check measures a real ~492-unit phone canvas
    /// no matter what size the test runner's game view happens to be.
    /// </summary>
    public class RuleDecoderViewTests
    {
        /// <summary>The 2017 paper's rule; the app's default.</summary>
        private const string RedWorldRule = "e542-5f79-9341-f31e-6c6b-7f08-8773-7068";

        /// <summary>
        /// The decode vector whose top sub-rule is promoted (2040 -&gt; 2048) and
        /// whose first, second, and seventh sub-rules carry negative percents.
        /// </summary>
        private const string PromotedRule = "ff40-00c0-8020-407f-2081-6001-a0ff-e080";

        /// <summary>The decode vector whose eight sub-rules all share limit 128.</summary>
        private const string TiedLimitsRule = "1010-1020-1030-1040-1050-1060-1070-1080";

        /// <summary>A canvas the size of a phone in portrait, in canvas units.</summary>
        private const float PhoneWidth = 492f;

        /// <summary>Height of that phone canvas, in canvas units.</summary>
        private const float PhoneHeight = 1040f;

        /// <summary>The card width the shipped heaton-life rule lab hard-codes.</summary>
        private const float HouseCardWidth = 1010f;

        /// <summary>Red World, row by row and column by column, as the table prints it.</summary>
        private static readonly string[][] RedWorldTable =
        {
            new[] { "760", "0-759", "Red", "95%", "1(Red)", "0x5f (95)", "0x79 (121)" },
            new[] { "864", "760-863", "Blue", "84%", "4(Blue)", "0x6c (108)", "0x6b (107)" },
            new[] { "896", "864-895", "White", "81%", "7(White)", "0x70 (112)", "0x68 (104)" },
            new[] { "1016", "896-1015", "Purple", "6%", "5(Purple)", "0x7f (127)", "0x08 (8)" },
            new[] { "1080", "1016-1079", "Cyan", "90%", "6(Cyan)", "0x87 (135)", "0x73 (115)" },
            new[] { "1176", "1080-1175", "Green", "51%", "2(Green)", "0x93 (147)", "0x41 (65)" },
            new[] { "1832", "1176-1831", "Black", "51%", "0(Black)", "0xe5 (229)", "0x42 (66)" },
            new[] { "1944", "1832-1943", "Yellow", "23%", "3(Yellow)", "0xf3 (243)", "0x1e (30)" },
        };

        /// <summary>The promoted-and-negative vector as the table prints it.</summary>
        private static readonly string[][] PromotedTable =
        {
            new[] { "0", "0--1", "Green", "-50%", "1(Red)", "0x00 (0)", "0xc0 (-64)" },
            new[] { "256", "0-255", "Purple", "-99%", "4(Blue)", "0x20 (32)", "0x81 (-127)" },
            new[] { "512", "256-511", "Yellow", "100%", "3(Yellow)", "0x40 (64)", "0x7f (127)" },
            new[] { "768", "512-767", "Purple", "0%", "5(Purple)", "0x60 (96)", "0x01 (1)" },
            new[] { "1024", "768-1023", "Green", "25%", "2(Green)", "0x80 (128)", "0x20 (32)" },
            new[] { "1280", "1024-1279", "White", "0%", "6(Cyan)", "0xa0 (160)", "0xff (-1)" },
            new[] { "1792", "1280-1791", "Black", "-100%", "7(White)", "0xe0 (224)", "0x80 (-128)" },
            new[] { "2048", "1792-2047", "Black", "50%", "0(Black)", "0xff (255)", "0x40 (64)" },
        };

        [UnityTest]
        public IEnumerator HeadersMatchThePyQtLabels()
        {
            yield return TestBoot.Boot();
            RuleDecoderView view = BuildDesktop(RedWorldRule, out RectTransform _, out RecordingNavigator _);
            yield return null;

            bool greek = RuleDecoderView.FontHasGreekHeaders(UIBuilder.DefaultFont);
            string[] expected = RuleDecoderView.HeaderLabels(greek);
            Assert.AreEqual(RuleDecoderView.ColumnCount, expected.Length, "column count");
            CollectionAssert.AreEqual(expected, view.RenderedHeaders, "rendered headings");

            // tab_rule.py: ["High (a)", "Range", "Key Color", "Percent (b)",
            // "Index (g)", "Octet-1", "Octet-2"], with the Greek letters where
            // this comment writes Latin ones.
            Assert.AreEqual(AppStrings.DecoderHeaderRange, view.HeaderCell(1).text, "Range heading");
            Assert.AreEqual(
                AppStrings.DecoderHeaderKeyColor, view.HeaderCell(2).text, "Key Color heading");
            Assert.AreEqual(AppStrings.DecoderHeaderOctet1, view.HeaderCell(5).text, "Octet-1 heading");
            Assert.AreEqual(AppStrings.DecoderHeaderOctet2, view.HeaderCell(6).text, "Octet-2 heading");
            AssertHeaderIsOneOf(
                view.HeaderCell(0).text,
                AppStrings.DecoderHeaderHigh,
                AppStrings.DecoderHeaderHighAscii);
            AssertHeaderIsOneOf(
                view.HeaderCell(3).text,
                AppStrings.DecoderHeaderPercent,
                AppStrings.DecoderHeaderPercentAscii);
            AssertHeaderIsOneOf(
                view.HeaderCell(4).text,
                AppStrings.DecoderHeaderIndex,
                AppStrings.DecoderHeaderIndexAscii);

            // The phone cards label their rows with the same headings, minus the
            // key-color column, which is the card's colored strip.
            Assert.IsNull(view.CardLabel(0, RuleDecoderView.SwatchColumn), "swatch has no card label");
            for (int column = 0; column < RuleDecoderView.ColumnCount; column++)
            {
                if (column == RuleDecoderView.SwatchColumn)
                {
                    continue;
                }
                Assert.AreEqual(
                    expected[column],
                    view.CardLabel(3, column).text,
                    "card label for column " + column);
            }
        }

        [UnityTest]
        public IEnumerator RowsMatchDecodeRuleForRedWorld()
        {
            yield return TestBoot.Boot();
            RuleDecoderView view = BuildDesktop(RedWorldRule, out RectTransform _, out RecordingNavigator _);
            yield return null;

            Assert.AreEqual(RedWorldRule, view.Rule, "canonical rule");
            Assert.AreEqual(MergeLife.DefaultRule, view.Rule, "Red World is the engine default");
            Assert.AreEqual(
                "Rule: " + RedWorldRule, view.RuleLabel.text, "the Rule: <rule> heading");

            AssertTable(view, RedWorldTable);

            // The screen must format the engine's decode, not a second decode of
            // its own: every cell also matches FormatRow of the same row.
            MergeLife.DecodedSubRule[] rows = MergeLife.DecodeRule(RedWorldRule);
            Assert.AreEqual(RuleDecoderView.RowCount, rows.Length, "decoded rows");
            for (int row = 0; row < rows.Length; row++)
            {
                string[] formatted = RuleDecoderView.FormatRow(rows[row]);
                for (int column = 0; column < RuleDecoderView.ColumnCount; column++)
                {
                    Assert.AreEqual(
                        formatted[column],
                        view.RenderedCell(row, column),
                        "row " + row + " column " + column + " follows DecodeRule");
                }
            }

            // Row 0 merges toward Red; row 6 toward Black, the one target whose
            // name is drawn in white.
            AssertColor(new Color32(255, 0, 0, 255), view.RenderedSwatchColor(0), "row 0 swatch");
            AssertColor(Color.black, view.RenderedSwatchTextColor(0), "row 0 swatch ink");
            AssertColor(new Color32(0, 0, 0, 255), view.RenderedSwatchColor(6), "row 6 swatch");
            AssertColor(Color.white, view.RenderedSwatchTextColor(6), "row 6 swatch ink");
        }

        [UnityTest]
        public IEnumerator PromotedRowShowsRawOctetAndLimit2048()
        {
            yield return TestBoot.Boot();
            RuleDecoderView view = BuildDesktop(PromotedRule, out RectTransform _, out RecordingNavigator _);
            yield return null;

            AssertTable(view, PromotedTable);

            // The full-range sub-rule: its limit is promoted from 2040 to 2048,
            // but the rule string still carries 0xff. PyQt divided the promoted
            // limit by 8 and printed 0x100 -- a value no octet can hold.
            Assert.AreEqual("2048", view.RenderedCell(7, 0), "promoted limit");
            Assert.AreEqual("0xff (255)", view.RenderedCell(7, 5), "raw octet-1");
            StringAssert.DoesNotContain("0x100", view.RenderedCell(7, 5), "no re-derived octet");
            Assert.AreEqual("1792-2047", view.RenderedCell(7, 1), "promoted range");

            // The divergence is disclosed on screen.
            Assert.AreEqual(
                AppStrings.RuleDecoderFootnote, view.FootnoteLabel.text, "raw-octet footnote");
            Assert.IsTrue(
                view.FootnoteLabel.gameObject.activeInHierarchy, "the footnote is visible");
        }

        [UnityTest]
        public IEnumerator NegativePercentSwapsTargetColor()
        {
            yield return TestBoot.Boot();
            RuleDecoderView view = BuildDesktop(PromotedRule, out RectTransform _, out RecordingNavigator _);
            yield return null;

            // Row 0: percent -0.5, so the sub-rule's own color is Red (index 1)
            // and the color it merges toward is the next one, Green.
            Assert.AreEqual("1(Red)", view.RenderedCell(0, 4), "row 0 keeps its own index");
            Assert.AreEqual("Green", view.RenderedCell(0, RuleDecoderView.SwatchColumn), "row 0 target");
            Assert.AreEqual("-50%", view.RenderedCell(0, 3), "row 0 percent stays negative");
            AssertColor(new Color32(0, 255, 0, 255), view.RenderedSwatchColor(0), "row 0 swatch");
            AssertColor(Color.black, view.RenderedSwatchTextColor(0), "row 0 swatch ink");

            // Row 6: White (index 7) wraps around to Black (index 0), the only
            // target whose label is drawn in white.
            Assert.AreEqual("7(White)", view.RenderedCell(6, 4), "row 6 keeps its own index");
            Assert.AreEqual("Black", view.RenderedCell(6, RuleDecoderView.SwatchColumn), "row 6 wraps");
            AssertColor(new Color32(0, 0, 0, 255), view.RenderedSwatchColor(6), "row 6 swatch");
            AssertColor(Color.white, view.RenderedSwatchTextColor(6), "row 6 swatch ink");

            // A positive percent never swaps.
            Assert.AreEqual("3(Yellow)", view.RenderedCell(2, 4), "row 2 index");
            Assert.AreEqual("Yellow", view.RenderedCell(2, RuleDecoderView.SwatchColumn), "row 2 target");
            AssertColor(new Color32(255, 255, 0, 255), view.RenderedSwatchColor(2), "row 2 swatch");
        }

        [UnityTest]
        public IEnumerator PercentTruncatesTowardZero()
        {
            yield return TestBoot.Boot();
            RuleDecoderView red = BuildDesktop(RedWorldRule, out RectTransform _, out RecordingNavigator _);
            yield return null;

            // 30/127 = 0.23622...; PyQt's int() truncates, so 23%, and rounding
            // to 24% would be wrong.
            Assert.AreEqual("23%", red.RenderedCell(7, 3), "Red World row 7 percent");
            Assert.AreNotEqual("24%", red.RenderedCell(7, 3), "percent must not round up");
            // 8/127 = 0.0629...; 6%, not 6.3% and not 7%.
            Assert.AreEqual("6%", red.RenderedCell(3, 3), "Red World row 3 percent");
            // 65/127 = 0.5118...; 51%, not 52%.
            Assert.AreEqual("51%", red.RenderedCell(5, 3), "Red World row 5 percent");

            RuleDecoderView promoted =
                BuildDesktop(PromotedRule, out RectTransform _, out RecordingNavigator _);
            yield return null;

            // Truncation is toward zero on the negative side too: -127/128 is
            // -0.9921875, which truncates to -99%, never to -100%.
            Assert.AreEqual("-99%", promoted.RenderedCell(1, 3), "negative percent truncates toward zero");
            Assert.AreEqual("0%", promoted.RenderedCell(3, 3), "1/127 truncates to zero");
            Assert.AreEqual("0%", promoted.RenderedCell(5, 3), "-1/128 truncates to zero");
        }

        [UnityTest]
        public IEnumerator DegenerateRangeStringsAreKeptVerbatim()
        {
            yield return TestBoot.Boot();
            RuleDecoderView promoted =
                BuildDesktop(PromotedRule, out RectTransform _, out RecordingNavigator _);
            yield return null;

            // A zero limit gives "high = limit - 1 = -1", which PyQt printed as
            // a hyphen joining 0 to -1. Clamping it would hide a dead sub-rule.
            Assert.AreEqual("0--1", promoted.RenderedCell(0, 1), "the empty first range");

            RuleDecoderView tied =
                BuildDesktop(TiedLimitsRule, out RectTransform _, out RecordingNavigator _);
            yield return null;

            Assert.AreEqual("0-127", tied.RenderedCell(0, 1), "the one live range");
            for (int row = 1; row < RuleDecoderView.RowCount; row++)
            {
                // Every sub-rule tied with the one above it starts where the
                // previous ended and ends one lower: low 128, high 127.
                Assert.AreEqual("128-127", tied.RenderedCell(row, 1), "tied range on row " + row);
                Assert.AreEqual("128", tied.RenderedCell(row, 0), "tied limit on row " + row);
            }

            // Plain ASCII hyphen-minus, never an en dash or an em dash.
            for (int row = 0; row < RuleDecoderView.RowCount; row++)
            {
                string range = promoted.RenderedCell(row, 1);
                StringAssert.Contains("-", range, "row " + row + " uses a hyphen");
                StringAssert.DoesNotContain("\u2013", range, "row " + row + " has no en dash");
                StringAssert.DoesNotContain("\u2014", range, "row " + row + " has no em dash");
            }
        }

        [UnityTest]
        public IEnumerator PhoneLayoutBuildsEightCardsThatFitTheCanvas()
        {
            yield return TestBoot.Boot();
            RectTransform host = CreateHost(PhoneWidth, PhoneHeight);
            RuleDecoderView view = Build(
                host, RedWorldRule, true, true, out RecordingNavigator _);
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.IsTrue(view.PhoneLayout, "phone layout is active");
            Assert.IsTrue(view.CardScroll.gameObject.activeSelf, "the card list is shown");
            Assert.IsFalse(view.TableScroll.gameObject.activeSelf, "the table is put away");
            Assert.AreEqual(RuleDecoderView.RowCount, view.Cards.Count, "one card per sub-rule");

            var hostCorners = new Vector3[4];
            host.GetWorldCorners(hostCorners);
            float cardsHeight = 0f;
            for (int row = 0; row < view.Cards.Count; row++)
            {
                RectTransform card = view.Cards[row];
                Assert.IsTrue(card.gameObject.activeInHierarchy, "card " + row + " is built");
                Assert.Greater(card.rect.width, 0f, "card " + row + " has a width");
                Assert.LessOrEqual(
                    card.rect.width,
                    host.rect.width + 0.5f,
                    "card " + row + " must fit the phone canvas");
                Assert.Less(
                    card.rect.width,
                    HouseCardWidth,
                    "card " + row + " must not use the house rule lab's fixed 1010-unit card");
                var cardCorners = new Vector3[4];
                card.GetWorldCorners(cardCorners);
                Assert.GreaterOrEqual(
                    cardCorners[0].x, hostCorners[0].x - 0.5f, "card " + row + " left edge");
                Assert.LessOrEqual(
                    cardCorners[2].x, hostCorners[2].x + 0.5f, "card " + row + " right edge");
                cardsHeight += card.rect.height;
            }

            // Everything the table shows is still on the cards.
            AssertTable(view, RedWorldTable);
            AssertColor(new Color32(255, 0, 0, 255), view.RenderedSwatchColor(0), "card 0 swatch");
            AssertColor(Color.white, view.RenderedSwatchTextColor(6), "card 6 swatch ink");

            // The stack is taller than the viewport, which is what the scroll
            // view is for; it must not have been squeezed to fit.
            var viewport = (RectTransform)view.CardScroll.transform;
            Assert.Greater(cardsHeight, viewport.rect.height, "the card stack scrolls");
            Assert.LessOrEqual(
                view.CardScroll.content.rect.width,
                host.rect.width + 0.5f,
                "the scroll content fits the canvas");
        }

        [UnityTest]
        public IEnumerator GreekHeadersFallBackWhenTheFontLacksThem()
        {
            yield return TestBoot.Boot();

            string[] greek = RuleDecoderView.HeaderLabels(true);
            string[] ascii = RuleDecoderView.HeaderLabels(false);

            Assert.AreEqual(AppStrings.DecoderHeaderHigh, greek[0], "Greek High");
            Assert.AreEqual(AppStrings.DecoderHeaderPercent, greek[3], "Greek Percent");
            Assert.AreEqual(AppStrings.DecoderHeaderIndex, greek[4], "Greek Index");
            Assert.AreEqual('\u03b1', greek[0][greek[0].Length - 2], "alpha in the High heading");
            Assert.AreEqual('\u03b2', greek[3][greek[3].Length - 2], "beta in the Percent heading");
            Assert.AreEqual('\u03b3', greek[4][greek[4].Length - 2], "gamma in the Index heading");

            Assert.AreEqual(AppStrings.DecoderHeaderHighAscii, ascii[0], "ASCII High");
            Assert.AreEqual(AppStrings.DecoderHeaderPercentAscii, ascii[3], "ASCII Percent");
            Assert.AreEqual(AppStrings.DecoderHeaderIndexAscii, ascii[4], "ASCII Index");
            foreach (string heading in ascii)
            {
                foreach (char c in heading)
                {
                    Assert.Less((int)c, 128, "the fallback headings stay ASCII: " + heading);
                }
            }

            // The four headings without a Greek letter never change.
            for (int column = 0; column < RuleDecoderView.ColumnCount; column++)
            {
                if (column == 0 || column == 3 || column == 4)
                {
                    Assert.AreNotEqual(greek[column], ascii[column], "column " + column + " swaps");
                    continue;
                }
                Assert.AreEqual(greek[column], ascii[column], "column " + column + " is shared");
            }

            // A font that cannot be asked counts as lacking the glyphs.
            Assert.IsFalse(RuleDecoderView.FontHasGreekHeaders(null), "no font, no Greek");

            // Whichever branch this platform's font takes, the screen renders it.
            RuleDecoderView view = BuildDesktop(RedWorldRule, out RectTransform _, out RecordingNavigator _);
            yield return null;
            string[] expected =
                RuleDecoderView.HeaderLabels(RuleDecoderView.FontHasGreekHeaders(UIBuilder.DefaultFont));
            for (int column = 0; column < RuleDecoderView.ColumnCount; column++)
            {
                Assert.AreEqual(expected[column], view.HeaderCell(column).text, "heading " + column);
            }
        }

        [UnityTest]
        public IEnumerator BackAndOpenInSimulatorRouteThroughTheNavigator()
        {
            yield return TestBoot.Boot();
            RuleDecoderView view = BuildDesktop(PromotedRule, out RectTransform _, out RecordingNavigator nav);
            yield return null;

            Assert.AreEqual(
                AppStrings.RuleDecoderOpenInSimulator,
                view.OpenInSimulatorButton.GetComponentInChildren<Text>().text,
                "Open in Simulator label");
            Assert.AreEqual(
                AppStrings.RuleDecoderCopyRule,
                view.CopyRuleButton.GetComponentInChildren<Text>().text,
                "Copy rule label");
            Assert.AreEqual(
                AppStrings.Back,
                view.BackButton.GetComponentInChildren<Text>().text,
                "Back label");

            view.OpenInSimulatorButton.onClick.Invoke();
            Assert.AreEqual(PromotedRule, nav.SimulatorRule, "the decoded rule goes to the simulator");
            Assert.IsTrue(nav.SimulatorAutoStart, "and it starts running");

            view.BackButton.onClick.Invoke();
            Assert.AreEqual(1, nav.BackCount, "Back pops the stack");
        }

        private static void AssertHeaderIsOneOf(string actual, string greek, string ascii)
        {
            Assert.IsTrue(
                actual == greek || actual == ascii,
                "heading '" + actual + "' must be '" + greek + "' or '" + ascii + "'");
        }

        private static void AssertTable(RuleDecoderView view, string[][] expected)
        {
            for (int row = 0; row < RuleDecoderView.RowCount; row++)
            {
                for (int column = 0; column < RuleDecoderView.ColumnCount; column++)
                {
                    Assert.AreEqual(
                        expected[row][column],
                        view.RenderedCell(row, column),
                        "row " + row + " column " + column);
                }
            }
        }

        private static void AssertColor(Color expected, Color actual, string message)
        {
            Assert.AreEqual(expected.r, actual.r, 0.01f, message + " (red)");
            Assert.AreEqual(expected.g, actual.g, 0.01f, message + " (green)");
            Assert.AreEqual(expected.b, actual.b, 0.01f, message + " (blue)");
            Assert.AreEqual(expected.a, actual.a, 0.01f, message + " (alpha)");
        }

        private static RectTransform CreateHost(float width, float height)
        {
            RectTransform host = UIBuilder.CreatePanel(
                AppController.Instance.SafeArea, "RuleDecoderTestHost");
            host.anchorMin = new Vector2(0.5f, 0.5f);
            host.anchorMax = new Vector2(0.5f, 0.5f);
            host.pivot = new Vector2(0.5f, 0.5f);
            host.anchoredPosition = Vector2.zero;
            host.sizeDelta = new Vector2(width, height);
            return host;
        }

        private static RuleDecoderView BuildDesktop(
            string rule, out RectTransform host, out RecordingNavigator nav)
        {
            host = CreateHost(1400f, 800f);
            return Build(host, rule, false, false, out nav);
        }

        private static RuleDecoderView Build(
            RectTransform host, string rule, bool portrait, bool phone, out RecordingNavigator nav)
        {
            nav = new RecordingNavigator();
            var view = new RuleDecoderView(host, nav, new AppServices());
            view.Show();
            view.OpenRule(rule);
            view.ApplyLayout(portrait, phone);
            Canvas.ForceUpdateCanvases();
            return view;
        }

        /// <summary>
        /// A navigator that records what the screen asked for instead of
        /// navigating, so the decoder can be exercised without the rest of the
        /// app on screen.
        /// </summary>
        private sealed class RecordingNavigator : INavigator
        {
            /// <summary>The rule handed to <see cref="ShowSimulator"/>, or null.</summary>
            public string SimulatorRule { get; private set; }

            /// <summary>The auto-start flag of the last <see cref="ShowSimulator"/> call.</summary>
            public bool SimulatorAutoStart { get; private set; }

            /// <summary>How many times <see cref="Back"/> was called.</summary>
            public int BackCount { get; private set; }

            /// <summary>The last toast text.</summary>
            public string LastStatus { get; private set; }

            /// <inheritdoc/>
            public void ShowHome()
            {
            }

            /// <inheritdoc/>
            public void ShowGallery()
            {
            }

            /// <inheritdoc/>
            public void ShowSimulator(string rule = null, bool autoStart = false)
            {
                SimulatorRule = rule;
                SimulatorAutoStart = autoStart;
            }

            /// <inheritdoc/>
            public void ShowRuleDecoder(string rule)
            {
            }

            /// <inheritdoc/>
            public void ShowEvolve()
            {
            }

            /// <inheritdoc/>
            public void ShowSettings()
            {
            }

            /// <inheritdoc/>
            public void ShowAbout()
            {
            }

            /// <inheritdoc/>
            public void Back()
            {
                BackCount++;
            }

            /// <inheritdoc/>
            public Task ShowAlertAsync(string title, string message) => Task.CompletedTask;

            /// <inheritdoc/>
            public Task<bool> ShowConfirmAsync(
                string title,
                string message,
                string ok = AppStrings.Ok,
                string cancel = AppStrings.Cancel) => Task.FromResult(false);

            /// <inheritdoc/>
            public void Status(string text)
            {
                LastStatus = text;
            }

            /// <inheritdoc/>
            public void OpenUrl(string url)
            {
            }
        }
    }
}
