using System;
using System.Collections.Generic;
using System.Globalization;
using HeatonCA.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace HeatonCAApp
{
    /// <summary>
    /// The rule decoder: the port of the PyQt "Rule" tab
    /// (python/application/pyqt/tab_rule.py). It shows the eight sub-rules of a
    /// MergeLife rule in compiled order under the seven original column
    /// headings, so a rule string can be read as behavior rather than as hex.
    ///
    /// Every number on this screen comes from
    /// <see cref="MergeLife.DecodeRule"/>; this view only formats. The
    /// formatting is deliberately PyQt-exact, including the two degenerate
    /// range strings the original produced — the first sub-rule of a rule whose
    /// lowest limit is zero renders "0--1", and every sub-rule tied with the one
    /// above it renders "128-127" — because a clamped range would hide the tie
    /// that produced it. Percentages truncate toward zero (Python's
    /// <c>int(pct * 100)</c>), never round.
    ///
    /// The one deliberate divergence is the octet pair, and
    /// <see cref="AppStrings.RuleDecoderFootnote"/> says so on screen: this port
    /// prints the raw octets the rule string carries, where PyQt re-derived
    /// octet 1 from the promoted limit (printing "0x100" for the top sub-rule of
    /// a full-range rule) and printed the magnitude of octet 2 rather than its
    /// signed byte.
    ///
    /// Two layouts, chosen by <see cref="ApplyLayout"/>: a real seven-column
    /// table on desktop and tablet, and one card per sub-rule inside a vertical
    /// scroll view on phones. Both are laid out from fractions of the parent
    /// rect and never from a fixed canvas width — a table that assumes a wide
    /// canvas is exactly what overflows a phone.
    /// </summary>
    public sealed class RuleDecoderView : AppViewBase, IRuleDecoderScreen
    {
        /// <summary>Sub-rules in a MergeLife rule, and so rows in the table.</summary>
        public const int RowCount = 8;

        /// <summary>Decoder columns: the seven PyQt headings.</summary>
        public const int ColumnCount = 7;

        /// <summary>Column index of the key-color swatch (the one colored cell).</summary>
        public const int SwatchColumn = 2;

        // The three Greek letters are written as escapes so this source file
        // stays pure ASCII; the strings that carry them live in AppStrings.

        /// <summary>Greek small letter alpha, in the "High" heading.</summary>
        private const char Alpha = '\u03b1';

        /// <summary>Greek small letter beta, in the "Percent" heading.</summary>
        private const char Beta = '\u03b2';

        /// <summary>Greek small letter gamma, in the "Index" heading.</summary>
        private const char Gamma = '\u03b3';

        /// <summary>
        /// Column boundaries as fractions of the table width, so the table
        /// scales with the canvas instead of assuming one. Eight stops bound the
        /// seven columns: 10% High, 15% Range, 16% Key Color, 12% Percent, 15%
        /// Index, 16% Octet-1, 16% Octet-2.
        /// </summary>
        private static readonly float[] ColumnStops =
        {
            0f, 0.10f, 0.25f, 0.41f, 0.53f, 0.68f, 0.84f, 1f,
        };

        /// <summary>
        /// The six columns a phone card lists as label/value rows. Column
        /// <see cref="SwatchColumn"/> is missing on purpose: it is the card's
        /// colored header strip rather than a row.
        /// </summary>
        private static readonly int[] CardColumns = { 0, 1, 3, 4, 5, 6 };

        // Phone card metrics. Cards only ever appear on phone canvases, whose
        // width the density model keeps between roughly 420 and 1080 units, so
        // these are constants; the card's WIDTH always comes from the layout
        // group, which is what keeps it inside the canvas.
        private const float CardPadding = 8f;
        private const float CardSwatchHeight = 34f;
        private const float CardGap = 6f;
        private const float CardRowHeight = 26f;

        private readonly Text[,] _tableCells = new Text[RowCount, ColumnCount];
        private readonly Image[] _tableSwatches = new Image[RowCount];
        private readonly Text[] _headerCells = new Text[ColumnCount];
        private readonly LayoutElement[] _rowElements = new LayoutElement[RowCount];
        private readonly Text[,] _cardTexts = new Text[RowCount, ColumnCount];
        private readonly Text[,] _cardLabels = new Text[RowCount, ColumnCount];
        private readonly Image[] _cardSwatches = new Image[RowCount];
        private readonly RectTransform[] _cards = new RectTransform[RowCount];
        private readonly string[] _headerLabels;

        private RectTransform _bar;
        private Text _barTitle;
        private Button _backButton;
        private Text _ruleLabel;
        private RectTransform _body;
        private ScrollRect _tableScroll;
        private RectTransform _tableContent;
        private LayoutElement _headerElement;
        private ScrollRect _cardScroll;
        private RectTransform _cardContent;
        private Text _footnote;
        private RectTransform _buttonRow;
        private Button _openButton;
        private LayoutElement _openElement;
        private Button _copyButton;
        private LayoutElement _copyElement;

        private string _rule;
        private MergeLife.DecodedSubRule[] _rows;
        private bool _phone;
        private bool _portrait;

        /// <summary>
        /// Builds both layouts (table and cards) once and shows the decode of
        /// <see cref="GalleryCatalog.DefaultRule"/>; the navigator calls
        /// <see cref="Show(string)"/> with the rule the user actually asked for.
        /// </summary>
        /// <param name="parent">The safe-area rect this screen builds into.</param>
        /// <param name="nav">The navigator its buttons talk to.</param>
        /// <param name="services">The shared service bag (unused by this screen).</param>
        public RuleDecoderView(RectTransform parent, INavigator nav, AppServices services)
            : base(parent, nav, services)
        {
            _headerLabels = HeaderLabels(FontHasGreekHeaders(UIBuilder.DefaultFont));

            RectTransform root = UIBuilder.CreatePanel(
                parent, "RuleDecoderView", UIBuilder.BackgroundColor);
            UIBuilder.Stretch(root);
            Root = root.gameObject;

            BuildBar(root);
            BuildRuleLabel(root);
            BuildFooter(root);
            _body = UIBuilder.CreatePanel(root, "Body");
            BuildTable(_body);
            BuildCards(_body);

            SetRule(GalleryCatalog.DefaultRule);
            ApplyLayout(Screen.height > Screen.width, UIBuilder.IsPhoneLayout);
            Root.SetActive(false);
        }

        /// <summary>The canonical rule this screen is decoding.</summary>
        public string Rule => _rule;

        /// <summary>
        /// The seven column headings as rendered — the Greek forms, or the ASCII
        /// fallbacks when the runtime font has no alpha, beta, or gamma.
        /// </summary>
        public IReadOnlyList<string> RenderedHeaders => _headerLabels;

        /// <summary>True while the phone card list is the active layout.</summary>
        public bool PhoneLayout => _phone;

        /// <summary>True while the screen is laid out for a portrait canvas.</summary>
        public bool PortraitLayout => _portrait;

        /// <summary>The scroll view holding the desktop and tablet table.</summary>
        public ScrollRect TableScroll => _tableScroll;

        /// <summary>The scroll view holding the phone cards.</summary>
        public ScrollRect CardScroll => _cardScroll;

        /// <summary>The eight phone cards, one per sub-rule, in compiled order.</summary>
        public IReadOnlyList<RectTransform> Cards => _cards;

        /// <summary>The area between the rule label and the footer that the two layouts share.</summary>
        public RectTransform Body => _body;

        /// <summary>The top bar's Back button.</summary>
        public Button BackButton => _backButton;

        /// <summary>The "Open in Simulator" button.</summary>
        public Button OpenInSimulatorButton => _openButton;

        /// <summary>The "Copy rule" button.</summary>
        public Button CopyRuleButton => _copyButton;

        /// <summary>The "Rule: &lt;rule&gt;" heading.</summary>
        public Text RuleLabel => _ruleLabel;

        /// <summary>The raw-octet footnote under the table.</summary>
        public Text FootnoteLabel => _footnote;

        /// <summary>
        /// Does <paramref name="font"/> carry the three Greek letters the PyQt
        /// headings use? A font that cannot answer counts as lacking them: the
        /// ASCII headings render everywhere, so falling back is never wrong,
        /// while a missing glyph would draw an empty box.
        /// </summary>
        /// <param name="font">The font the headings will be drawn in.</param>
        /// <returns>True when alpha, beta, and gamma are all available.</returns>
        public static bool FontHasGreekHeaders(Font font)
        {
            if (font == null)
            {
                return false;
            }
            try
            {
                return font.HasCharacter(Alpha)
                    && font.HasCharacter(Beta)
                    && font.HasCharacter(Gamma);
            }
            catch (Exception)
            {
                // Some font assets refuse the question rather than answering it.
                return false;
            }
        }

        /// <summary>
        /// The seven column headings, in column order.
        /// </summary>
        /// <param name="greek">
        /// True for the PyQt headings with their Greek letters; false for the
        /// ASCII stand-ins ("High (alpha)", "Percent (beta)", "Index (gamma)").
        /// </param>
        /// <returns>A fresh array of seven headings.</returns>
        public static string[] HeaderLabels(bool greek)
        {
            return new[]
            {
                greek ? AppStrings.DecoderHeaderHigh : AppStrings.DecoderHeaderHighAscii,
                AppStrings.DecoderHeaderRange,
                AppStrings.DecoderHeaderKeyColor,
                greek ? AppStrings.DecoderHeaderPercent : AppStrings.DecoderHeaderPercentAscii,
                greek ? AppStrings.DecoderHeaderIndex : AppStrings.DecoderHeaderIndexAscii,
                AppStrings.DecoderHeaderOctet1,
                AppStrings.DecoderHeaderOctet2,
            };
        }

        /// <summary>
        /// One decoded sub-rule as the seven strings the table prints, in column
        /// order. Pure, so the parity tests can read it without a canvas.
        ///
        /// The range string is whatever the decode says, hyphen-joined and
        /// unclamped: "0--1" for a zero limit and "128-127" for a tie are the
        /// PyQt output, and reproducing them is the point. The percent truncates
        /// toward zero, matching Python's <c>int()</c>. The octets are raw — see
        /// <see cref="AppStrings.RuleDecoderFootnote"/>.
        /// </summary>
        /// <param name="row">A row of <see cref="MergeLife.DecodeRule"/>.</param>
        /// <returns>Seven strings: High, Range, Key Color, Percent, Index, Octet-1, Octet-2.</returns>
        public static string[] FormatRow(MergeLife.DecodedSubRule row)
        {
            return new[]
            {
                row.Limit.ToString(CultureInfo.InvariantCulture),
                string.Concat(
                    row.RangeLow.ToString(CultureInfo.InvariantCulture),
                    "-",
                    row.RangeHigh.ToString(CultureInfo.InvariantCulture)),
                AppStrings.ColorNames[row.TargetIndex],
                string.Concat(
                    ((int)(row.Percent * 100d)).ToString(CultureInfo.InvariantCulture), "%"),
                string.Concat(
                    row.ColorIndex.ToString(CultureInfo.InvariantCulture),
                    "(",
                    AppStrings.ColorNames[row.ColorIndex],
                    ")"),
                FormatOctet(row.RangeByte, row.RangeByte),
                FormatOctet((byte)row.PercentByte, row.PercentByte),
            };
        }

        /// <summary>
        /// The swatch fill: the RGB the simulation actually merges toward, which
        /// for a negative percent is the color one past the sub-rule's own index.
        /// </summary>
        /// <param name="row">A row of <see cref="MergeLife.DecodeRule"/>.</param>
        /// <returns>The target color, fully opaque.</returns>
        public static Color32 SwatchColor(MergeLife.DecodedSubRule row) =>
            new Color32(row.TargetR, row.TargetG, row.TargetB, 255);

        /// <summary>
        /// The swatch label color: white on Black (target index 0), black on
        /// every other target. PyQt's exact rule, kept even where a luminance
        /// test would disagree (Blue reads its name in black there too).
        /// </summary>
        /// <param name="row">A row of <see cref="MergeLife.DecodeRule"/>.</param>
        /// <returns>White for index 0, otherwise black.</returns>
        public static Color SwatchTextColor(MergeLife.DecodedSubRule row) =>
            row.TargetIndex == 0 ? Color.white : Color.black;

        /// <summary>
        /// Decode <paramref name="rule"/> and refill both layouts. Any form
        /// <see cref="RuleParser"/> accepts works; text that is not a rule falls
        /// back to <see cref="GalleryCatalog.DefaultRule"/> rather than throwing,
        /// because arriving here with a half-typed rule is a navigation
        /// accident, not an error the user must acknowledge.
        /// </summary>
        /// <param name="rule">The rule to decode.</param>
        public void SetRule(string rule)
        {
            _rule = RuleParser.TryParse(rule, out string canonical, out _)
                ? canonical
                : GalleryCatalog.DefaultRule;
            _rows = MergeLife.DecodeRule(_rule);
            _ruleLabel.text = string.Format(
                CultureInfo.InvariantCulture, AppStrings.RuleDecoderRuleFormat, _rule);
            FillCells();
        }

        /// <summary>
        /// The <see cref="IRuleDecoderScreen"/> entry point: the controller shows
        /// this screen and then hands it the rule to decode.
        /// </summary>
        /// <param name="rule">Rule in any form <see cref="RuleParser"/> accepts.</param>
        public void OpenRule(string rule)
        {
            SetRule(rule);
        }

        /// <summary>Decode <paramref name="rule"/> and bring the screen up.</summary>
        /// <param name="rule">The rule to decode.</param>
        public void Show(string rule)
        {
            SetRule(rule);
            Show();
        }

        /// <summary>
        /// The text column <paramref name="column"/> of row
        /// <paramref name="row"/> is showing in the layout currently on screen —
        /// the table cell on desktop and tablet, the card's value (or its swatch
        /// label, for <see cref="SwatchColumn"/>) on a phone.
        /// </summary>
        /// <param name="row">Sub-rule index, 0 to <see cref="RowCount"/> - 1.</param>
        /// <param name="column">Column index, 0 to <see cref="ColumnCount"/> - 1.</param>
        /// <returns>The rendered string.</returns>
        public string RenderedCell(int row, int column)
        {
            CheckCell(row, column);
            Text text = _phone ? _cardTexts[row, column] : _tableCells[row, column];
            return text == null ? null : text.text;
        }

        /// <summary>The fill color of row <paramref name="row"/>'s swatch as rendered.</summary>
        /// <param name="row">Sub-rule index, 0 to <see cref="RowCount"/> - 1.</param>
        /// <returns>The swatch's current color.</returns>
        public Color RenderedSwatchColor(int row)
        {
            CheckCell(row, SwatchColumn);
            return (_phone ? _cardSwatches[row] : _tableSwatches[row]).color;
        }

        /// <summary>The label color of row <paramref name="row"/>'s swatch as rendered.</summary>
        /// <param name="row">Sub-rule index, 0 to <see cref="RowCount"/> - 1.</param>
        /// <returns>The swatch label's current color.</returns>
        public Color RenderedSwatchTextColor(int row)
        {
            CheckCell(row, SwatchColumn);
            return (_phone ? _cardTexts[row, SwatchColumn] : _tableCells[row, SwatchColumn]).color;
        }

        /// <summary>The table's heading cell for <paramref name="column"/>.</summary>
        /// <param name="column">Column index, 0 to <see cref="ColumnCount"/> - 1.</param>
        /// <returns>The heading Text component.</returns>
        public Text HeaderCell(int column)
        {
            CheckCell(0, column);
            return _headerCells[column];
        }

        /// <summary>
        /// The heading a phone card prints beside its value for
        /// <paramref name="column"/>, or null for <see cref="SwatchColumn"/>,
        /// which a card shows as its colored header strip rather than a row.
        /// </summary>
        /// <param name="row">Sub-rule index, 0 to <see cref="RowCount"/> - 1.</param>
        /// <param name="column">Column index, 0 to <see cref="ColumnCount"/> - 1.</param>
        /// <returns>The card's label Text, or null.</returns>
        public Text CardLabel(int row, int column)
        {
            CheckCell(row, column);
            return _cardLabels[row, column];
        }

        /// <inheritdoc/>
        public override void ApplyLayout(bool portrait, bool phone)
        {
            _portrait = portrait;
            _phone = phone;

            float pad = phone ? 8f : 14f;
            float barHeight = phone ? 52f : 60f;
            int titleSize = phone ? 20 : 26;
            int ruleSize = phone ? 15 : 20;
            float ruleHeight = phone ? 28f : 34f;
            float buttonHeight = phone ? 44f : 48f;
            float footnoteHeight = phone ? 58f : 40f;
            int footnoteSize = phone ? 12 : 14;

            _bar.anchorMin = new Vector2(0f, 1f);
            _bar.anchorMax = Vector2.one;
            _bar.pivot = new Vector2(0.5f, 1f);
            _bar.anchoredPosition = Vector2.zero;
            _bar.sizeDelta = new Vector2(0f, barHeight);

            var backRt = (RectTransform)_backButton.transform;
            backRt.anchorMin = new Vector2(0f, 0.5f);
            backRt.anchorMax = new Vector2(0f, 0.5f);
            backRt.pivot = new Vector2(0f, 0.5f);
            backRt.anchoredPosition = new Vector2(10f, 0f);
            backRt.sizeDelta = new Vector2(phone ? 92f : 120f, barHeight - 16f);

            // PlaceBarTitle copies the point size into its shrink-to-fit ceiling,
            // so the size has to be set first.
            _barTitle.fontSize = titleSize;
            UIBuilder.PlaceBarTitle(_barTitle, 16f);

            var ruleRt = (RectTransform)_ruleLabel.transform;
            ruleRt.anchorMin = new Vector2(0f, 1f);
            ruleRt.anchorMax = Vector2.one;
            ruleRt.pivot = new Vector2(0.5f, 1f);
            ruleRt.anchoredPosition = new Vector2(0f, -(barHeight + pad * 0.5f));
            ruleRt.sizeDelta = new Vector2(-2f * pad, ruleHeight);
            _ruleLabel.fontSize = ruleSize;
            _ruleLabel.resizeTextMaxSize = ruleSize;

            _footnote.fontSize = footnoteSize;
            var footnoteRt = (RectTransform)_footnote.transform;
            footnoteRt.anchorMin = Vector2.zero;
            footnoteRt.anchorMax = new Vector2(1f, 0f);
            footnoteRt.pivot = new Vector2(0.5f, 0f);
            footnoteRt.anchoredPosition = new Vector2(0f, buttonHeight + pad);
            footnoteRt.sizeDelta = new Vector2(-2f * pad, footnoteHeight);

            _buttonRow.anchorMin = Vector2.zero;
            _buttonRow.anchorMax = new Vector2(1f, 0f);
            _buttonRow.pivot = new Vector2(0.5f, 0f);
            _buttonRow.anchoredPosition = new Vector2(0f, pad * 0.5f);
            _buttonRow.sizeDelta = new Vector2(-2f * pad, buttonHeight);
            _openElement.preferredWidth = phone ? 186f : 240f;
            _copyElement.preferredWidth = phone ? 132f : 170f;

            UIBuilder.Stretch(
                _body,
                pad,
                buttonHeight + footnoteHeight + 2f * pad,
                pad,
                barHeight + ruleHeight + 1.5f * pad);

            _headerElement.preferredHeight = 38f;
            for (int row = 0; row < RowCount; row++)
            {
                _rowElements[row].preferredHeight = 44f;
            }

            _tableScroll.gameObject.SetActive(!phone);
            _cardScroll.gameObject.SetActive(phone);

            RectTransform content = phone ? _cardContent : _tableContent;
            if (content != null && content.gameObject.activeInHierarchy)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            }
        }

        private static string FormatOctet(byte raw, int signed)
        {
            return string.Concat(
                "0x",
                raw.ToString("x2", CultureInfo.InvariantCulture),
                " (",
                signed.ToString(CultureInfo.InvariantCulture),
                ")");
        }

        private static void CheckCell(int row, int column)
        {
            if (row < 0 || row >= RowCount)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }
            if (column < 0 || column >= ColumnCount)
            {
                throw new ArgumentOutOfRangeException(nameof(column));
            }
        }

        private void BuildBar(RectTransform root)
        {
            _bar = UIBuilder.CreatePanel(root, "Bar", UIBuilder.PanelColor);
            _backButton = UIBuilder.CreateButton(
                _bar, "Back", AppStrings.Back, 18, () => Nav?.Back());
            _barTitle = UIBuilder.CreateText(
                _bar, "Title", AppStrings.RuleDecoderTitle, 26, UIBuilder.TextColor,
                TextAnchor.MiddleCenter);
            _barTitle.fontStyle = FontStyle.Bold;
        }

        private void BuildRuleLabel(RectTransform root)
        {
            _ruleLabel = UIBuilder.CreateText(
                root, "RuleLabel", string.Empty, 20, UIBuilder.TextColor, TextAnchor.MiddleCenter);
            // A rule is 39 characters wide; on the narrowest phone canvas it has
            // to shrink rather than run off both edges.
            _ruleLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _ruleLabel.verticalOverflow = VerticalWrapMode.Truncate;
            _ruleLabel.resizeTextForBestFit = true;
            _ruleLabel.resizeTextMaxSize = 20;
            _ruleLabel.resizeTextMinSize = 10;
        }

        private void BuildFooter(RectTransform root)
        {
            _footnote = UIBuilder.CreateText(
                root, "Footnote", AppStrings.RuleDecoderFootnote, 14, UIBuilder.TextDimColor,
                TextAnchor.LowerCenter);
            _footnote.horizontalOverflow = HorizontalWrapMode.Wrap;
            _footnote.verticalOverflow = VerticalWrapMode.Overflow;

            _buttonRow = UIBuilder.CreatePanel(root, "Buttons");
            var layout = _buttonRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.spacing = 12f;

            _openButton = UIBuilder.CreateButton(
                _buttonRow, "OpenInSimulator", AppStrings.RuleDecoderOpenInSimulator, 18,
                OpenInSimulator);
            _openElement = _openButton.gameObject.AddComponent<LayoutElement>();
            _openElement.flexibleWidth = 0f;

            _copyButton = UIBuilder.CreateButton(
                _buttonRow, "CopyRule", AppStrings.RuleDecoderCopyRule, 18, CopyRule);
            _copyElement = _copyButton.gameObject.AddComponent<LayoutElement>();
            _copyElement.flexibleWidth = 0f;
        }

        private void BuildTable(RectTransform body)
        {
            _tableScroll = UIBuilder.CreateScrollView(body, "Table", out _tableContent);
            UIBuilder.Stretch((RectTransform)_tableScroll.transform);
            var layout = _tableContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 3f;
            layout.padding = new RectOffset(2, 2, 0, 6);
            _tableContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            RectTransform header = CreateTableRow("Header", out _headerElement);
            for (int column = 0; column < ColumnCount; column++)
            {
                _headerCells[column] = CreateTableCell(
                    header, column, _headerLabels[column], true, out Image _);
            }

            for (int row = 0; row < RowCount; row++)
            {
                RectTransform rowRt = CreateTableRow(
                    "Row" + row.ToString(CultureInfo.InvariantCulture), out _rowElements[row]);
                for (int column = 0; column < ColumnCount; column++)
                {
                    _tableCells[row, column] = CreateTableCell(
                        rowRt, column, string.Empty, false, out Image background);
                    if (column == SwatchColumn)
                    {
                        _tableSwatches[row] = background;
                    }
                }
            }
        }

        private RectTransform CreateTableRow(string name, out LayoutElement element)
        {
            RectTransform row = UIBuilder.CreatePanel(_tableContent, name);
            element = row.gameObject.AddComponent<LayoutElement>();
            element.flexibleHeight = 0f;
            return row;
        }

        private Text CreateTableCell(
            RectTransform row, int column, string value, bool header, out Image background)
        {
            var cellGo = new GameObject(
                "Cell" + column.ToString(CultureInfo.InvariantCulture),
                typeof(RectTransform), typeof(Image));
            var cellRt = (RectTransform)cellGo.transform;
            cellRt.SetParent(row, false);
            cellRt.anchorMin = new Vector2(ColumnStops[column], 0f);
            cellRt.anchorMax = new Vector2(ColumnStops[column + 1], 1f);
            cellRt.offsetMin = new Vector2(2f, 1f);
            cellRt.offsetMax = new Vector2(-2f, -1f);
            background = cellGo.GetComponent<Image>();
            background.color = header ? Color.clear : new Color(0f, 0f, 0f, 0.28f);
            background.raycastTarget = false;

            Text text = UIBuilder.CreateText(
                cellRt, "Value", value, 16,
                header ? UIBuilder.TextDimColor : UIBuilder.TextColor, TextAnchor.MiddleCenter);
            text.fontStyle = header ? FontStyle.Bold : FontStyle.Normal;
            // Shrink rather than spill into the neighboring column: the fractions
            // above hold on every canvas the app runs on, but a heading with a
            // long ASCII fallback ("Percent (beta)") is close on a narrow tablet.
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = 16;
            text.resizeTextMinSize = 11;
            UIBuilder.Stretch((RectTransform)text.transform, 4f, 0f, 4f, 0f);
            return text;
        }

        private void BuildCards(RectTransform body)
        {
            _cardScroll = UIBuilder.CreateScrollView(body, "Cards", out _cardContent);
            UIBuilder.Stretch((RectTransform)_cardScroll.transform);
            var layout = _cardContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 8f;
            layout.padding = new RectOffset(4, 4, 0, 8);
            _cardContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            float cardHeight = 2f * CardPadding + CardSwatchHeight + CardGap
                + CardColumns.Length * CardRowHeight;
            for (int row = 0; row < RowCount; row++)
            {
                _cards[row] = BuildCard(row, cardHeight);
            }
        }

        private RectTransform BuildCard(int row, float cardHeight)
        {
            RectTransform card = UIBuilder.CreatePanel(
                _cardContent,
                "Card" + row.ToString(CultureInfo.InvariantCulture),
                UIBuilder.PanelColor);
            var element = card.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = cardHeight;
            element.flexibleHeight = 0f;

            var swatchGo = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
            var swatchRt = (RectTransform)swatchGo.transform;
            swatchRt.SetParent(card, false);
            swatchRt.anchorMin = new Vector2(0f, 1f);
            swatchRt.anchorMax = Vector2.one;
            swatchRt.pivot = new Vector2(0.5f, 1f);
            swatchRt.anchoredPosition = new Vector2(0f, -CardPadding);
            swatchRt.sizeDelta = new Vector2(-2f * CardPadding, CardSwatchHeight);
            Image swatch = swatchGo.GetComponent<Image>();
            swatch.raycastTarget = false;
            _cardSwatches[row] = swatch;
            Text swatchText = UIBuilder.CreateText(
                swatchRt, "Value", string.Empty, 17, Color.black, TextAnchor.MiddleCenter);
            swatchText.fontStyle = FontStyle.Bold;
            swatchText.horizontalOverflow = HorizontalWrapMode.Wrap;
            swatchText.verticalOverflow = VerticalWrapMode.Truncate;
            UIBuilder.Stretch((RectTransform)swatchText.transform, 8f, 0f, 8f, 0f);
            _cardTexts[row, SwatchColumn] = swatchText;

            for (int i = 0; i < CardColumns.Length; i++)
            {
                int column = CardColumns[i];
                RectTransform line = UIBuilder.CreatePanel(
                    card, "Line" + column.ToString(CultureInfo.InvariantCulture));
                line.anchorMin = new Vector2(0f, 1f);
                line.anchorMax = Vector2.one;
                line.pivot = new Vector2(0.5f, 1f);
                line.anchoredPosition = new Vector2(
                    0f, -(CardPadding + CardSwatchHeight + CardGap + i * CardRowHeight));
                line.sizeDelta = new Vector2(-2f * CardPadding, CardRowHeight);

                Text label = UIBuilder.CreateText(
                    line, "Label", _headerLabels[column], 15, UIBuilder.TextDimColor);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                var labelRt = (RectTransform)label.transform;
                labelRt.anchorMin = new Vector2(0f, 0f);
                labelRt.anchorMax = new Vector2(0.46f, 1f);
                labelRt.offsetMin = new Vector2(6f, 0f);
                labelRt.offsetMax = new Vector2(-4f, 0f);
                _cardLabels[row, column] = label;

                Text value = UIBuilder.CreateText(line, "Value", string.Empty, 15, UIBuilder.TextColor);
                value.horizontalOverflow = HorizontalWrapMode.Wrap;
                value.verticalOverflow = VerticalWrapMode.Truncate;
                var valueRt = (RectTransform)value.transform;
                valueRt.anchorMin = new Vector2(0.46f, 0f);
                valueRt.anchorMax = Vector2.one;
                valueRt.offsetMin = new Vector2(4f, 0f);
                valueRt.offsetMax = new Vector2(-6f, 0f);
                _cardTexts[row, column] = value;
            }
            return card;
        }

        private void FillCells()
        {
            for (int row = 0; row < RowCount; row++)
            {
                MergeLife.DecodedSubRule decoded = _rows[row];
                string[] values = FormatRow(decoded);
                Color32 fill = SwatchColor(decoded);
                Color ink = SwatchTextColor(decoded);
                for (int column = 0; column < ColumnCount; column++)
                {
                    _tableCells[row, column].text = values[column];
                    _cardTexts[row, column].text = values[column];
                }
                _tableCells[row, SwatchColumn].color = ink;
                _cardTexts[row, SwatchColumn].color = ink;
                _tableSwatches[row].color = fill;
                _cardSwatches[row].color = fill;
            }
        }

        private void OpenInSimulator()
        {
            Nav?.ShowSimulator(_rule, true);
        }

        private void CopyRule()
        {
            // WebGlBridge routes to the browser clipboard API in a WebGL build
            // and to GUIUtility.systemCopyBuffer everywhere else, which is the
            // one seam this screen needs.
            WebGlBridge.CopyText(_rule);
            Nav?.Status(AppStrings.SimStatusCopied);
        }
    }
}
