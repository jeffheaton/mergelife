using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HeatonCAApp
{
    /// <summary>
    /// The Home screen, and the back stack's root: the PyQt splash tab rebuilt for
    /// touch. The title art fills the page above a block of five buttons -- Gallery,
    /// Simulator (PyQt's "Rule Viewer"), Evolve, Settings, About -- with the version
    /// badge in the top-right corner and, while a search is running somewhere behind
    /// the screens, an "Evolving..." chip in the top-left.
    ///
    /// The button block is sized in canvas units rather than fractions of the page,
    /// so a control is never smaller than a fingertip on a short canvas: the rows
    /// keep their height and the art gives up the space. Only when even the floor
    /// (<see cref="MinButtonHeight"/>) would not fit does the block shrink.
    /// </summary>
    public sealed class HomeView : AppViewBase
    {
        /// <summary>Resources path of the title art (the PyQt splash image).</summary>
        public const string TitleArtResource = "Art/heaton_ca_title";

        /// <summary>Button row height on a phone canvas, in canvas units.</summary>
        public const float PhoneButtonHeight = 64f;

        /// <summary>Button row height on a tablet or desktop canvas, in canvas units.</summary>
        public const float ButtonHeight = 72f;

        /// <summary>
        /// Shortest a button may ever get. 44 units is the platform touch-target
        /// floor on both mobile platforms, and the PlayMode phone check pins it.
        /// </summary>
        public const float MinButtonHeight = 44f;

        /// <summary>Gap between button rows and columns, in canvas units.</summary>
        public const float ButtonGap = 14f;

        /// <summary>Height of the version badge and the evolving chip.</summary>
        public const float ChipHeight = 34f;

        /// <summary>Buttons on the page.</summary>
        private const int ButtonCount = 5;

        /// <summary>
        /// Most of the page the button block may take. Below this the art still gets
        /// a third of the page; above it the rows shrink toward
        /// <see cref="MinButtonHeight"/>.
        /// </summary>
        private const float ButtonBlockShare = 0.62f;

        private readonly List<Button> _buttons = new List<Button>(ButtonCount);
        private readonly List<Text> _labels = new List<Text>(ButtonCount);

        private RectTransform _artArea;
        private Text _fallbackTitle;
        private RectTransform _chip;

        /// <summary>
        /// Build the page into <paramref name="parent"/> (the safe-area rect). The
        /// fixed view constructor: every screen takes the same three arguments so the
        /// controller can create them uniformly.
        /// </summary>
        /// <param name="parent">The safe-area rect this screen builds into.</param>
        /// <param name="nav">The navigator its five buttons act through.</param>
        /// <param name="services">The shared services (the evolve host drives the chip).</param>
        public HomeView(RectTransform parent, INavigator nav, AppServices services)
            : base(parent, nav, services)
        {
            RectTransform root = UIBuilder.CreatePanel(parent, "HomeView");
            UIBuilder.Stretch(root);
            Root = root.gameObject;

            BuildTitleArt(root);
            BuildVersionBadge(root);
            BuildEvolvingChip(root);
            AddButton(root, AppStrings.HomeGallery, () => Nav.ShowGallery());
            AddButton(root, AppStrings.HomeSimulator, () => Nav.ShowSimulator());
            AddButton(root, AppStrings.HomeEvolve, () => Nav.ShowEvolve());
            AddButton(root, AppStrings.HomeSettings, () => Nav.ShowSettings());
            AddButton(root, AppStrings.HomeAbout, () => Nav.ShowAbout());

            ApplyLayout(Screen.height > Screen.width, UIBuilder.IsPhoneLayout);
            Root.SetActive(false); // the controller shows the root screen at boot
        }

        /// <summary>The corner badge: "v" + <c>Application.version</c>.</summary>
        public Text VersionBadge { get; private set; }

        /// <summary>The title art, or an empty image when the resource is missing.</summary>
        public RawImage TitleArt { get; private set; }

        /// <summary>
        /// The five buttons in page order: Gallery, Simulator, Evolve, Settings,
        /// About. The PlayMode suite measures these.
        /// </summary>
        public IReadOnlyList<Button> Buttons => _buttons;

        /// <summary>True while the "Evolving..." chip is up.</summary>
        public bool EvolvingChipVisible => _chip != null && _chip.gameObject.activeSelf;

        /// <summary>
        /// Show the chip exactly while a search is running. Leaving the Evolve
        /// screen never stops the GA, so Home is where the user is told the CPU is
        /// still busy.
        /// </summary>
        /// <param name="dt">Unscaled seconds since the previous frame (unused).</param>
        public override void Tick(float dt)
        {
            bool evolving = Services != null && Services.Evolve != null && Services.Evolve.Running;
            if (_chip != null && _chip.gameObject.activeSelf != evolving)
            {
                _chip.gameObject.SetActive(evolving);
            }
        }

        /// <summary>
        /// Lay the page out for this orientation and form factor: one column of five
        /// on a phone held upright, a two-column grid of three rows everywhere else
        /// (a phone in landscape has room for three rows, not five). The art takes
        /// whatever the button block leaves.
        /// </summary>
        /// <param name="portrait">True when the screen is taller than it is wide.</param>
        /// <param name="phone">True for phone-sized screens (<c>UIBuilder.IsPhoneLayout</c>).</param>
        public override void ApplyLayout(bool portrait, bool phone)
        {
            float margin = phone ? 20f : 40f;
            int columns = phone && portrait ? 1 : 2;
            int rows = (ButtonCount + columns - 1) / columns;
            float rowHeight = phone ? PhoneButtonHeight : ButtonHeight;
            float gaps = (rows - 1) * ButtonGap;
            float block = rows * rowHeight + gaps;

            // Rect is zero until the canvas has been laid out once; the nominal
            // heights are right for every canvas the app actually runs on, and the
            // controller re-applies this on the first resize pass.
            float pageHeight = Parent != null ? Parent.rect.height : 0f;
            float budget = pageHeight > 0f ? pageHeight * ButtonBlockShare : block;
            if (block > budget)
            {
                rowHeight = Mathf.Max(MinButtonHeight, (budget - gaps) / rows);
                block = rows * rowHeight + gaps;
            }

            for (int i = 0; i < _buttons.Count; i++)
            {
                int row = i / columns;
                int column = i % columns;
                bool alone = columns > 1 && i == _buttons.Count - 1 && column == 0;
                float first = alone ? 0f : column / (float)columns;
                float last = alone ? 1f : (column + 1) / (float)columns;
                float left = column == 0 ? margin : ButtonGap * 0.5f;
                float right = alone || column == columns - 1 ? margin : ButtonGap * 0.5f;
                float bottom = margin + (rows - 1 - row) * (rowHeight + ButtonGap);
                var rt = (RectTransform)_buttons[i].transform;
                rt.anchorMin = new Vector2(first, 0f);
                rt.anchorMax = new Vector2(last, 0f);
                rt.offsetMin = new Vector2(left, bottom);
                rt.offsetMax = new Vector2(-right, bottom + rowHeight);
                _labels[i].fontSize = phone ? 22 : 26;
            }

            if (_artArea != null)
            {
                UIBuilder.Stretch(
                    _artArea, margin, margin + block + ButtonGap * 2f, margin,
                    margin + ChipHeight + 8f);
            }
        }

        private void BuildTitleArt(RectTransform root)
        {
            _artArea = UIBuilder.CreatePanel(root, "TitleArt");
            var texture = Resources.Load<Texture2D>(TitleArtResource);
            TitleArt = UIBuilder.CreateRawImage(_artArea, "Art");
            TitleArt.raycastTarget = false;
            TitleArt.texture = texture;
            var artRt = (RectTransform)TitleArt.transform;
            UIBuilder.Stretch(artRt);
            if (texture != null && texture.height > 0)
            {
                // Aspect-fitted, never stretched: the fitter drives the rect inside
                // whatever the art area turns out to be.
                var fitter = TitleArt.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = texture.width / (float)texture.height;
            }
            // Fallback for a build whose Resources folder lost the art: the PyQt
            // splash heading, so Home is never a blank page.
            _fallbackTitle = UIBuilder.CreateText(
                _artArea, "FallbackTitle", AppStrings.HomeTitle, 40, UIBuilder.TextColor,
                TextAnchor.MiddleCenter);
            _fallbackTitle.fontStyle = FontStyle.Bold;
            _fallbackTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIBuilder.Stretch((RectTransform)_fallbackTitle.transform);
            TitleArt.gameObject.SetActive(texture != null);
            _fallbackTitle.gameObject.SetActive(texture == null);
        }

        private void BuildVersionBadge(RectTransform root)
        {
            RectTransform badge = UIBuilder.CreatePanel(root, "VersionBadge", Color.black);
            badge.anchorMin = new Vector2(1f, 1f);
            badge.anchorMax = new Vector2(1f, 1f);
            badge.pivot = new Vector2(1f, 1f);
            badge.anchoredPosition = new Vector2(-12f, -12f);
            badge.sizeDelta = new Vector2(110f, ChipHeight);
            VersionBadge = UIBuilder.CreateText(
                badge, "Label", AppStrings.VersionBadgePrefix + Application.version, 18,
                Color.white, TextAnchor.MiddleCenter);
            UIBuilder.Stretch((RectTransform)VersionBadge.transform);
        }

        private void BuildEvolvingChip(RectTransform root)
        {
            _chip = UIBuilder.CreatePanel(root, "EvolvingChip", UIBuilder.PanelColor);
            _chip.anchorMin = new Vector2(0f, 1f);
            _chip.anchorMax = new Vector2(0f, 1f);
            _chip.pivot = new Vector2(0f, 1f);
            _chip.anchoredPosition = new Vector2(12f, -12f);
            _chip.sizeDelta = new Vector2(160f, ChipHeight);
            Text label = UIBuilder.CreateText(
                _chip, "Label", AppStrings.HomeEvolvingChip, 18, UIBuilder.AccentColor,
                TextAnchor.MiddleCenter);
            UIBuilder.Stretch((RectTransform)label.transform);
            _chip.gameObject.SetActive(false);
        }

        private void AddButton(RectTransform root, string label, UnityEngine.Events.UnityAction click)
        {
            Button button = UIBuilder.CreateButton(root, label, label, 26, click);
            _buttons.Add(button);
            _labels.Add(button.GetComponentInChildren<Text>());
        }
    }
}
