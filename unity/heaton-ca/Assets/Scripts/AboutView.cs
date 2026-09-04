using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace HeatonCAApp
{
    /// <summary>
    /// The About screen: what this build is, who owns it, the paper it
    /// implements, the four links the store listings promise, the determinism
    /// verdict from boot, and the third-party notices.
    ///
    /// It replaces the PyQt About tab, which had been broken since a refactor
    /// deleted the module it imported. Two deliberate differences from that
    /// screen: there is no log path (meaningless on iOS, Android, and WebGL,
    /// where the user cannot open a file by path), and every link goes through
    /// <see cref="INavigator.OpenUrl"/> rather than
    /// <c>Application.OpenURL</c> — on WebGL the navigator opens a tab from
    /// inside the click handler so the popup blocker treats it as user
    /// initiated.
    ///
    /// Every row is text or a button in one vertical list inside a ScrollRect,
    /// so the same build fits a 64-point title on a desktop page and a 40-point
    /// title on a phone without a second layout.
    /// </summary>
    public sealed class AboutView : AppViewBase
    {
        /// <summary>Resources path (no extension) of the notices text asset.</summary>
        public const string NoticesResource = "ThirdPartyNotices";

        /// <summary>
        /// Longest run of notices text one legacy <c>Text</c> may hold. uGUI
        /// meshes four vertices per character and refuses to draw past 65,000, so
        /// the notices are split into blocks well under that rather than being
        /// silently truncated two thirds of the way through the Apache license.
        /// </summary>
        public const int NoticesBlockChars = 6000;

        /// <summary>Height of the top bar, in canvas units.</summary>
        private const float BarHeight = 64f;

        /// <summary>Left and right margin of the About list.</summary>
        private const float PadX = 16f;

        /// <summary>Height of the notices panel when it is expanded.</summary>
        private const float NoticesPanelHeight = 300f;

        private readonly RectTransform _root;
        private readonly RectTransform _listContent;
        private readonly Text _backLabel;
        private readonly Text _introText;
        private readonly LayoutElement _doiLayout;
        private readonly RectTransform _linksRow;
        private readonly LayoutElement _linksLayout;
        private readonly RectTransform _noticesPanel;
        private readonly LayoutElement _noticesHeaderLayout;
        private readonly LayoutElement _noticesPanelLayout;
        private readonly List<Text> _noticesBlocks = new List<Text>();
        private readonly List<Text> _buttonLabels = new List<Text>();

        /// <summary>
        /// Builds the screen into <paramref name="parent"/>. Everything shown is
        /// read once here except the self-check verdict, which
        /// <see cref="Show"/> refreshes so a report captured after this view was
        /// constructed is still the one on screen.
        /// </summary>
        /// <param name="parent">The safe-area rect (or a test's stand-in page).</param>
        /// <param name="nav">The navigator every link and the Back button call.</param>
        /// <param name="services">
        /// The shared services; this screen reads only
        /// <see cref="AppServices.SelfCheckPassed"/> and
        /// <see cref="AppServices.SelfCheckReport"/>.
        /// </param>
        public AboutView(RectTransform parent, INavigator nav, AppServices services)
            : base(parent, nav, services)
        {
            _root = UIBuilder.CreatePanel(parent, "AboutView");
            UIBuilder.Stretch(_root);
            Root = _root.gameObject;

            RectTransform bar = UIBuilder.CreatePanel(_root, "TopBar", UIBuilder.PanelColor);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(0f, -BarHeight);
            bar.offsetMax = Vector2.zero;

            BackButton = UIBuilder.CreateButton(bar, "Back", AppStrings.Back, 20, () => Nav?.Back());
            var backRect = (RectTransform)BackButton.transform;
            backRect.anchorMin = new Vector2(0f, 0.5f);
            backRect.anchorMax = new Vector2(0f, 0.5f);
            backRect.pivot = new Vector2(0f, 0.5f);
            backRect.sizeDelta = new Vector2(140f, 44f);
            backRect.anchoredPosition = new Vector2(12f, 0f);
            _backLabel = BackButton.GetComponentInChildren<Text>();

            TitleText = UIBuilder.CreateText(
                bar, "Title", AppStrings.AboutTitle, 26, UIBuilder.TextColor,
                TextAnchor.MiddleCenter);
            TitleText.fontStyle = FontStyle.Bold;
            UIBuilder.PlaceBarTitle(TitleText);

            ScrollRect scroll = UIBuilder.CreateScrollView(_root, "Body", out _listContent);
            var scrollRect = (RectTransform)scroll.transform;
            UIBuilder.Stretch(scrollRect, 0f, 0f, 0f, BarHeight);
            var layout = _listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 10f;
            layout.padding = new RectOffset((int)PadX, (int)PadX, 18, 22);
            _listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            ProductNameText = CreateRow(
                "ProductName", Application.productName, 64, UIBuilder.TextColor);
            ProductNameText.fontStyle = FontStyle.Bold;

            VersionText = CreateRow(
                "Version",
                string.Format(
                    CultureInfo.InvariantCulture, AppStrings.AboutVersionFormat,
                    Application.version, BuildInfo.BuildNumber),
                30,
                UIBuilder.TextColor);

            BuiltText = CreateRow(
                "Built",
                string.Format(
                    CultureInfo.InvariantCulture, AppStrings.AboutBuiltFormat, BuildInfo.BuildTimeUtc),
                15,
                UIBuilder.TextDimColor);

            CopyrightText = CreateRow("Copyright", AppLinks.CopyrightLine, 18, UIBuilder.TextDimColor);
            _introText = CreateRow("PaperIntro", AppStrings.AboutPaperIntro, 17, UIBuilder.TextColor);
            CitationText = CreateRow("Citation", AppLinks.Citation, 16, UIBuilder.TextDimColor);

            DoiButton = CreateLinkButton(_listContent, "Doi", AppStrings.AboutDoi, AppLinks.PaperDoiUrl);
            _doiLayout = DoiButton.gameObject.AddComponent<LayoutElement>();

            _linksRow = UIBuilder.CreatePanel(_listContent, "Links");
            _linksLayout = _linksRow.gameObject.AddComponent<LayoutElement>();
            TutorialButton = CreateLinkButton(
                _linksRow, "Tutorial", AppStrings.AboutTutorial, AppLinks.TutorialUrl);
            ManualButton = CreateLinkButton(
                _linksRow, "Manual", AppStrings.AboutManual, AppLinks.ManualUrl);
            PrivacyButton = CreateLinkButton(
                _linksRow, "Privacy", AppStrings.AboutPrivacy, AppLinks.PrivacyUrl);
            SourceCodeButton = CreateLinkButton(
                _linksRow, "SourceCode", AppStrings.AboutSourceCode, AppLinks.RepoUrl);

            SelfCheckText = CreateRow("SelfCheck", string.Empty, 18, UIBuilder.TextColor);
            SelfCheckDetailText = CreateRow("SelfCheckDetail", string.Empty, 14, UIBuilder.TextDimColor);
            SelfCheckDetailText.alignment = TextAnchor.UpperLeft;
            SelfCheckDetailText.gameObject.SetActive(false);

            ThirdPartyButton = UIBuilder.CreateButton(
                _listContent, "ThirdPartyHeader", AppStrings.AboutThirdPartyNotices, 20,
                ToggleThirdPartyNotices);
            _noticesHeaderLayout = ThirdPartyButton.gameObject.AddComponent<LayoutElement>();
            _buttonLabels.Add(ThirdPartyButton.GetComponentInChildren<Text>());

            _noticesPanel = UIBuilder.CreatePanel(_listContent, "ThirdPartyPanel", UIBuilder.PanelColor);
            _noticesPanelLayout = _noticesPanel.gameObject.AddComponent<LayoutElement>();
            BuildNoticesPanel(_noticesPanel);
            _noticesPanel.gameObject.SetActive(false);

            RefreshSelfCheck();
            ApplyLayout(Screen.height > Screen.width, UIBuilder.IsPhoneLayout);
        }

        /// <summary>The bar's Back button.</summary>
        public Button BackButton { get; }

        /// <summary>
        /// The scrolling list every row lives in. Exposed so the layout suite can
        /// measure the page's content without also measuring the top bar, whose
        /// title rect follows <c>UIBuilder.PlaceBarTitle</c>'s desktop insets in
        /// the Editor whatever canvas a test builds.
        /// </summary>
        public RectTransform ListContent => _listContent;

        /// <summary>The bar title ("About").</summary>
        public Text TitleText { get; }

        /// <summary>The product name, in the page's largest type.</summary>
        public Text ProductNameText { get; }

        /// <summary>"Version {version} (build {build})".</summary>
        public Text VersionText { get; }

        /// <summary>"Built {UTC stamp}", from <see cref="BuildInfo"/>.</summary>
        public Text BuiltText { get; }

        /// <summary>The <see cref="AppLinks.CopyrightLine"/> row.</summary>
        public Text CopyrightText { get; }

        /// <summary>The <see cref="AppLinks.Citation"/> row.</summary>
        public Text CitationText { get; }

        /// <summary>"Determinism self-check: PASS" (or FAIL), from the boot report.</summary>
        public Text SelfCheckText { get; }

        /// <summary>
        /// The full self-check report. Hidden while the check passes — it is a
        /// wall of digests nobody needs — and shown under the verdict when it
        /// fails, so a screenshot from a user names the check that broke.
        /// </summary>
        public Text SelfCheckDetailText { get; }

        /// <summary>Opens <see cref="AppLinks.PaperDoiUrl"/>.</summary>
        public Button DoiButton { get; }

        /// <summary>Opens <see cref="AppLinks.TutorialUrl"/>.</summary>
        public Button TutorialButton { get; }

        /// <summary>Opens <see cref="AppLinks.ManualUrl"/>.</summary>
        public Button ManualButton { get; }

        /// <summary>Opens <see cref="AppLinks.PrivacyUrl"/>.</summary>
        public Button PrivacyButton { get; }

        /// <summary>Opens <see cref="AppLinks.RepoUrl"/>.</summary>
        public Button SourceCodeButton { get; }

        /// <summary>Expands and collapses the third-party notices.</summary>
        public Button ThirdPartyButton { get; }

        /// <summary>The whole notices document, as loaded from Resources (empty if absent).</summary>
        public string ThirdPartyNoticesText { get; private set; } = string.Empty;

        /// <summary>How many Text blocks the notices were split across (see <see cref="NoticesBlockChars"/>).</summary>
        public int ThirdPartyBlockCount => _noticesBlocks.Count;

        /// <summary>True while the notices block is open.</summary>
        public bool ThirdPartyExpanded => _noticesPanel != null && _noticesPanel.gameObject.activeSelf;

        /// <summary>
        /// The product name's point size: 64 fills a desktop or tablet page, and
        /// 40 is what fits a phone's narrower one. Pure and static so the
        /// PlayMode suite can pin both without building a screen.
        /// </summary>
        /// <param name="phone">True for the phone layout.</param>
        /// <returns>Point size for <see cref="ProductNameText"/>.</returns>
        public static int TitleSize(bool phone) => phone ? 40 : 64;

        /// <summary>Brings the screen up and re-reads the self-check verdict.</summary>
        public override void Show()
        {
            base.Show();
            RefreshSelfCheck();
        }

        /// <summary>
        /// Re-lays the page for a new form factor: type sizes shrink on a phone,
        /// the four link buttons drop from one row of four to two rows of two,
        /// and the product name follows <see cref="TitleSize"/>.
        /// </summary>
        /// <param name="portrait">True when the screen is taller than it is wide.</param>
        /// <param name="phone">True for phone-sized screens (<c>UIBuilder.IsPhoneLayout</c>).</param>
        public override void ApplyLayout(bool portrait, bool phone)
        {
            TitleText.fontSize = phone ? 22 : 26;
            UIBuilder.PlaceBarTitle(TitleText);
            _backLabel.fontSize = phone ? 18 : 20;

            ProductNameText.fontSize = TitleSize(phone);
            VersionText.fontSize = phone ? 22 : 30;
            BuiltText.fontSize = phone ? 14 : 15;
            CopyrightText.fontSize = phone ? 16 : 18;
            _introText.fontSize = phone ? 16 : 17;
            CitationText.fontSize = phone ? 15 : 16;
            SelfCheckText.fontSize = phone ? 16 : 18;
            SelfCheckDetailText.fontSize = phone ? 13 : 14;

            float buttonHeight = phone ? 52f : 48f;
            _doiLayout.preferredHeight = buttonHeight;
            _noticesHeaderLayout.preferredHeight = buttonHeight;
            _noticesPanelLayout.preferredHeight = NoticesPanelHeight;
            _linksLayout.preferredHeight = phone ? buttonHeight * 2f + 8f : buttonHeight;
            foreach (Text label in _buttonLabels)
            {
                label.fontSize = phone ? 18 : 20;
            }

            if (phone)
            {
                // Four link buttons across a phone page leave about 90 units each,
                // which "Privacy policy" cannot use; two rows of two can.
                PlaceLink(TutorialButton, 0f, 0.5f, 0.5f, 1f);
                PlaceLink(ManualButton, 0.5f, 1f, 0.5f, 1f);
                PlaceLink(PrivacyButton, 0f, 0.5f, 0f, 0.5f);
                PlaceLink(SourceCodeButton, 0.5f, 1f, 0f, 0.5f);
            }
            else
            {
                PlaceLink(TutorialButton, 0f, 0.25f, 0f, 1f);
                PlaceLink(ManualButton, 0.25f, 0.5f, 0f, 1f);
                PlaceLink(PrivacyButton, 0.5f, 0.75f, 0f, 1f);
                PlaceLink(SourceCodeButton, 0.75f, 1f, 0f, 1f);
            }

            foreach (Text block in _noticesBlocks)
            {
                block.fontSize = phone ? 12 : 13;
            }
        }

        /// <summary>Opens the notices when they are closed, and closes them when they are open.</summary>
        public void ToggleThirdPartyNotices()
        {
            _noticesPanel.gameObject.SetActive(!_noticesPanel.gameObject.activeSelf);
        }

        /// <summary>Re-reads the boot self-check verdict into the verdict row.</summary>
        private void RefreshSelfCheck()
        {
            bool passed = Services != null
                && Services.SelfCheckPassed != null
                && Services.SelfCheckPassed();
            SelfCheckText.text = string.Format(
                CultureInfo.InvariantCulture,
                AppStrings.AboutSelfCheckFormat,
                passed ? AppStrings.AboutSelfCheckPassWord : AppStrings.AboutSelfCheckFailWord);
            SelfCheckText.color = passed ? UIBuilder.TextColor : new Color32(0xFF, 0x8A, 0x80, 0xFF);
            string report = !passed && Services != null && Services.SelfCheckReport != null
                ? Services.SelfCheckReport()
                : string.Empty;
            SelfCheckDetailText.text = report ?? string.Empty;
            SelfCheckDetailText.gameObject.SetActive(!string.IsNullOrEmpty(SelfCheckDetailText.text));
        }

        /// <summary>One centered, wrapping text row of the page.</summary>
        private Text CreateRow(string name, string content, int size, Color color)
        {
            Text text = UIBuilder.CreateText(
                _listContent, name, content, size, color, TextAnchor.MiddleCenter);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>A button that hands one <see cref="AppLinks"/> address to the navigator.</summary>
        private Button CreateLinkButton(RectTransform parent, string name, string label, string url)
        {
            Button button = UIBuilder.CreateButton(parent, name, label, 20, () => Nav?.OpenUrl(url));
            _buttonLabels.Add(button.GetComponentInChildren<Text>());
            return button;
        }

        /// <summary>Anchors one link button to a fraction of the links row.</summary>
        private static void PlaceLink(Button button, float xMin, float xMax, float yMin, float yMax)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(4f, 4f);
            rect.offsetMax = new Vector2(-4f, -4f);
        }

        /// <summary>
        /// Fills the collapsible panel: a scroll view holding the notices text,
        /// split into blocks no legacy Text mesh can overflow.
        /// </summary>
        private void BuildNoticesPanel(RectTransform panel)
        {
            var asset = Resources.Load<TextAsset>(NoticesResource);
            ThirdPartyNoticesText = asset != null && asset.text != null ? asset.text : string.Empty;

            ScrollRect scroll = UIBuilder.CreateScrollView(panel, "NoticesScroll", out RectTransform content);
            UIBuilder.Stretch((RectTransform)scroll.transform, 10f, 10f, 10f, 10f);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 2f;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            foreach (string block in SplitNotices(ThirdPartyNoticesText, NoticesBlockChars))
            {
                Text text = UIBuilder.CreateText(
                    content, "Notices", block, 13, UIBuilder.TextDimColor, TextAnchor.UpperLeft);
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.supportRichText = false; // license text is not markup
                _noticesBlocks.Add(text);
            }
        }

        /// <summary>
        /// Splits <paramref name="text"/> into blocks of at most
        /// <paramref name="maxChars"/> characters, breaking on line boundaries so
        /// no license paragraph is cut mid-word. Pure; an empty document yields
        /// no blocks at all.
        /// </summary>
        /// <param name="text">The whole document.</param>
        /// <param name="maxChars">Longest block to emit; values below 1 are treated as 1.</param>
        /// <returns>The blocks, in order.</returns>
        public static List<string> SplitNotices(string text, int maxChars)
        {
            var blocks = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                return blocks;
            }
            if (maxChars < 1)
            {
                maxChars = 1;
            }
            int start = 0;
            while (start < text.Length)
            {
                if (text.Length - start <= maxChars)
                {
                    blocks.Add(text.Substring(start));
                    break;
                }
                int end = text.LastIndexOf('\n', start + maxChars - 1, maxChars);
                int length = end >= start ? end - start + 1 : maxChars;
                blocks.Add(text.Substring(start, length));
                start += length;
            }
            return blocks;
        }
    }
}
