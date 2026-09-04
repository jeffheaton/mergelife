using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HeatonCAApp
{
    /// <summary>
    /// The curated gallery: the 30 rules of the PyQt app's tab_gallery.py, in
    /// that exact order, each as a pre-rendered preview the user can tap to
    /// watch it run.
    ///
    /// PyQt laid the tiles out three per row in a fixed-width desktop window and
    /// showed a blank white pixmap whenever a preview file was missing. Both
    /// survive here: desktop and WebGL keep the three-per-row look, and a rule
    /// with no art under Resources/Gallery still gets its tile, drawn as the
    /// solid white rectangle that was PyQt's "no preview" cue. Touch screens are
    /// the one departure — a phone cannot show three 300x192 previews side by
    /// side and stay legible, so the column count is counted off the screen's
    /// PHYSICAL width (<see cref="Columns"/>), which is the only measure that
    /// tells a phone from a tablet under match-width canvas scaling.
    ///
    /// The view owns no textures. Previews come from <c>Resources.Load</c>, so
    /// Unity owns them, and the blank tile is a RawImage with no texture at all,
    /// which uGUI draws as its flat <see cref="Color.white"/> — nothing to
    /// create, and so nothing to leak when the screen goes away.
    /// </summary>
    public sealed class GalleryView : AppViewBase
    {
        /// <summary>Resources folder holding the previews, one per rule: "Gallery/&lt;rule&gt;".</summary>
        public const string ArtFolder = "Gallery/";

        /// <summary>Preview width in pixels; the tile's art area keeps this aspect.</summary>
        public const int ArtPixelWidth = 300;

        /// <summary>Preview height in pixels; the tile's art area keeps this aspect.</summary>
        public const int ArtPixelHeight = 192;

        /// <summary>
        /// Physical width one tile wants on a touch screen, in millimeters: a
        /// 300x192 preview stays readable at roughly 62 mm, so a 65 mm phone
        /// gets one column, a 197 mm tablet three, and the widest tablets four.
        /// </summary>
        public const float TileWidthMm = 62f;

        /// <summary>Columns on desktop and WebGL: PyQt's three per row.</summary>
        public const int DesktopColumns = 3;

        /// <summary>The most columns any screen gets.</summary>
        public const int MaxColumns = 4;

        /// <summary>The fewest columns any screen gets.</summary>
        public const int MinColumns = 1;

        /// <summary>Height of the top bar carrying the title and Back.</summary>
        private const float BarHeight = 64f;

        /// <summary>Gap between tiles, horizontally and vertically.</summary>
        private const float TileSpacing = 12f;

        /// <summary>Inset of a tile's art and text from the tile's own edges.</summary>
        private const float TileInset = 4f;

        /// <summary>Side margin of the grid on desktop and tablet canvases.</summary>
        private const float SideMargin = 40f;

        /// <summary>Side margin on a phone, where 40 units a side is a fifth of the canvas.</summary>
        private const float PhoneSideMargin = 16f;

        /// <summary>Floor on a tile's width, so a sliver of a canvas still lays out.</summary>
        private const float MinTileWidth = 96f;

        /// <summary>Characters in a canonical rule: 32 hex digits and 7 dashes.</summary>
        private const float CaptionCharacters = 39f;

        /// <summary>
        /// Width of one caption character as a fraction of the font size. The
        /// legacy font's digits advance 0.556 em and its hyphen 0.333, so 0.6 em
        /// leaves the hex caption a comfortable margin inside its tile at every
        /// column count (pinned by GalleryFitsAPhonePortraitCanvas).
        /// </summary>
        private const float CaptionEmWidth = 0.6f;

        /// <summary>Largest caption size; wide desktop tiles stop growing text here.</summary>
        private const int CaptionMaxSize = 18;

        /// <summary>Smallest caption size; four columns on a tablet land near it.</summary>
        private const int CaptionMinSize = 9;

        /// <summary>How much smaller the name subtitle is than the hex caption.</summary>
        private const int SubtitleSizeDrop = 2;

        /// <summary>Cell width a tile built outside a grid lays itself out at.</summary>
        private const float DefaultTileWidth = 300f;

        private readonly RectTransform _panel;
        private readonly RectTransform _scrollRect;
        private readonly RectTransform _grid;
        private readonly GridLayoutGroup _layout;
        private readonly List<TileParts> _tiles = new List<TileParts>();
        private readonly List<RectTransform> _tileRects = new List<RectTransform>();

        private float _sideMargin = SideMargin;
        private float _appliedWidth = -1f;

        /// <summary>
        /// Builds the whole gallery — bar, scroll view, and all 30 tiles — into
        /// <paramref name="parent"/>. The tiles are static art, so they are built
        /// once and only ever re-measured; nothing is rebuilt on a later visit.
        /// </summary>
        /// <param name="parent">The safe-area rect (or a test's probe) to build into.</param>
        /// <param name="nav">Navigator a tapped tile calls.</param>
        /// <param name="services">Shared services; the gallery uses none of them.</param>
        public GalleryView(RectTransform parent, INavigator nav, AppServices services)
            : base(parent, nav, services)
        {
            _panel = UIBuilder.CreatePanel(parent, "GalleryView", UIBuilder.BackgroundColor);
            UIBuilder.Stretch(_panel);
            Root = _panel.gameObject;

            BuildTopBar(_panel);

            ScrollRect scroll = UIBuilder.CreateScrollView(_panel, "Scroll", out RectTransform content);
            _scrollRect = (RectTransform)scroll.transform;
            _grid = content;
            _layout = content.gameObject.AddComponent<GridLayoutGroup>();
            _layout.spacing = new Vector2(TileSpacing, TileSpacing);
            _layout.padding = new RectOffset(0, 0, 8, 8);
            _layout.childAlignment = TextAnchor.UpperCenter;
            _layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _layout.constraintCount = DesktopColumns;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            foreach (string rule in GalleryCatalog.Rules)
            {
                string captured = rule;
                TileParts tile = BuildTile(_grid, captured, () => Open(captured));
                _tiles.Add(tile);
                _tileRects.Add(tile.Root);
            }

            // The controller applies the real layout right after construction;
            // this makes the first frame right even when it does not.
            ApplyLayout(Screen.height >= Screen.width, UIBuilder.IsPhoneLayout);
        }

        /// <summary>The 30 tiles, in gallery order (the PlayMode suite reads these).</summary>
        public IReadOnlyList<RectTransform> Tiles => _tileRects;

        /// <summary>The bar's Back button (the house handle for the layout checks).</summary>
        public Button BackButton { get; private set; }

        /// <summary>The bar's title.</summary>
        public Text TitleText { get; private set; }

        /// <summary>The grid driving the tiles; its cell size follows the canvas.</summary>
        public GridLayoutGroup Layout => _layout;

        /// <summary>Columns the last <see cref="ApplyLayout(bool, bool)"/> settled on.</summary>
        public int ColumnCount => _layout.constraintCount;

        /// <summary>
        /// Tiles per row, counted off the screen's PHYSICAL width. Under
        /// match-width canvas scaling a phone and a tablet are both exactly
        /// <c>UIBuilder.ReferenceWidth</c> units wide, so canvas units cannot
        /// tell them apart; millimeters can. Desktop and WebGL keep PyQt's fixed
        /// three per row — a window can be any width, and a resizable window that
        /// re-flowed its columns would be a new behavior, not a ported one.
        /// Pure math, so the whole rule is unit-testable.
        /// </summary>
        /// <param name="widthPixels">Screen width in device pixels.</param>
        /// <param name="dpi">
        /// Screen dots per inch. Values at or below 1 mean the platform did not
        /// report it, and there is then no physical width to count in: those
        /// screens fall back to the desktop three, exactly as
        /// <c>UIBuilder.PhoneSized</c> treats an unreported dpi as a tablet.
        /// </param>
        /// <param name="isMobile">True on a phone or tablet build.</param>
        /// <returns>Column count, between <see cref="MinColumns"/> and <see cref="MaxColumns"/>.</returns>
        public static int Columns(float widthPixels, float dpi, bool isMobile)
        {
            if (!isMobile || dpi <= 1f)
            {
                return DesktopColumns;
            }
            float widthMm = widthPixels * 25.4f / dpi;
            return Mathf.Clamp(Mathf.FloorToInt(widthMm / TileWidthMm), MinColumns, MaxColumns);
        }

        /// <summary>
        /// The pre-rendered preview for <paramref name="rule"/>, or null when the
        /// build ships no art for it. The textures are import-time Point-filtered
        /// and uncompressed (Assets/Editor/ArtImportSettings.cs), so they are
        /// pixel-exact like PyQt's QPixmap and are never touched here.
        /// </summary>
        /// <param name="rule">Canonical rule string, as the file is named.</param>
        /// <returns>The preview texture, owned by Unity, or null when missing.</returns>
        public static Texture2D LoadArt(string rule)
        {
            return string.IsNullOrEmpty(rule) ? null : Resources.Load<Texture2D>(ArtFolder + rule);
        }

        /// <summary>
        /// Caption size for a tile of <paramref name="cellWidth"/> units: the
        /// largest size at which all 39 characters of a rule still fit inside the
        /// tile, clamped to the readable range. Pure, so the fit is pinned by a
        /// test rather than by a screenshot.
        /// </summary>
        /// <param name="cellWidth">Tile width in canvas units.</param>
        /// <returns>Font size for the hex caption.</returns>
        public static int CaptionFontSize(float cellWidth)
        {
            float textWidth = cellWidth - 2f * TileInset;
            int size = Mathf.FloorToInt(textWidth / (CaptionCharacters * CaptionEmWidth));
            return Mathf.Clamp(size, CaptionMinSize, CaptionMaxSize);
        }

        /// <summary>
        /// Builds one gallery tile — art, hex caption, and the name subtitle when
        /// the featured catalog names the rule — laid out for a tile
        /// <paramref name="cellWidth"/> units wide. Public because it is also the
        /// blank-tile fallback's only entry point: a rule with no shipped art
        /// builds exactly the same tile, whose art area draws solid white.
        /// </summary>
        /// <param name="parent">Rect to parent the tile under.</param>
        /// <param name="rule">Rule the tile shows and opens.</param>
        /// <param name="cellWidth">Tile width in canvas units.</param>
        /// <param name="onClick">Run when the tile (or anything on it) is tapped.</param>
        /// <returns>The tile's own rect.</returns>
        public static RectTransform CreateTile(
            RectTransform parent, string rule, float cellWidth, Action onClick)
        {
            TileParts tile = BuildTile(parent, rule, onClick);
            LayoutTile(tile, cellWidth);
            return tile.Root;
        }

        /// <summary>
        /// Re-lays the grid for a new orientation or form factor. The column
        /// count comes from the physical screen, the tile size from the canvas
        /// the view was given, so neither depends on a fixed reference width.
        /// </summary>
        /// <param name="portrait">
        /// True when the screen is taller than wide. The gallery scrolls
        /// vertically in both orientations and reads its columns off the physical
        /// width, which rotation already changes, so this only reaches the
        /// injected overload for symmetry with the other screens.
        /// </param>
        /// <param name="phone">True for phone-sized screens; they keep thinner side margins.</param>
        public override void ApplyLayout(bool portrait, bool phone)
        {
            ApplyLayout(
                portrait, phone, Columns(Screen.width, Screen.dpi, Application.isMobilePlatform));
        }

        /// <summary>
        /// Column-injected overload: the PlayMode suite drives a phone's single
        /// column and a tablet's four in the editor, where the real screen is
        /// neither.
        /// </summary>
        /// <param name="portrait">True when the screen is taller than wide.</param>
        /// <param name="phone">True for phone-sized screens.</param>
        /// <param name="columns">Tiles per row; clamped to the supported range.</param>
        public void ApplyLayout(bool portrait, bool phone, int columns)
        {
            _sideMargin = phone ? PhoneSideMargin : SideMargin;
            UIBuilder.Stretch(_scrollRect, _sideMargin, 0f, _sideMargin, BarHeight);
            _layout.constraintCount = Mathf.Clamp(columns, MinColumns, MaxColumns);
            _appliedWidth = -1f; // margins or columns changed: re-measure the cells
            ResizeCells();
        }

        /// <summary>
        /// Re-measures the tiles when the canvas has changed size. The rect is
        /// zero-width until the first layout pass, so construction can only guess
        /// from the canvas reference; this is where the guess is corrected.
        /// </summary>
        /// <param name="dt">Unscaled seconds since the previous frame; unused.</param>
        public override void Tick(float dt)
        {
            ResizeCells();
        }

        /// <summary>Shows the screen, re-measuring in case the canvas changed while it was away.</summary>
        public override void Show()
        {
            base.Show();
            ResizeCells();
        }

        /// <summary>Opens a rule in the simulator, already running (PyQt's display_rule).</summary>
        private void Open(string rule)
        {
            Nav?.ShowSimulator(rule, autoStart: true);
        }

        /// <summary>The screen's top bar: Back on the left, the title across the rest.</summary>
        private void BuildTopBar(RectTransform panel)
        {
            RectTransform bar = UIBuilder.CreatePanel(panel, "TopBar", UIBuilder.PanelColor);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(0f, -BarHeight);
            bar.offsetMax = Vector2.zero;

            BackButton = UIBuilder.CreateButton(
                bar, "Back", AppStrings.Back, 20, () => Nav?.Back());
            var backRect = (RectTransform)BackButton.transform;
            backRect.anchorMin = new Vector2(0f, 0.5f);
            backRect.anchorMax = new Vector2(0f, 0.5f);
            backRect.pivot = new Vector2(0f, 0.5f);
            backRect.sizeDelta = new Vector2(140f, 44f);
            backRect.anchoredPosition = new Vector2(12f, 0f);

            TitleText = UIBuilder.CreateText(
                bar, "Title", AppStrings.GalleryTitle, 26, UIBuilder.TextColor,
                TextAnchor.MiddleCenter);
            TitleText.fontStyle = FontStyle.Bold;
            UIBuilder.PlaceBarTitle(TitleText);
        }

        /// <summary>
        /// Fits the grid's cells to the canvas the view was given. Cheap enough
        /// to call every frame: it returns immediately unless the width moved.
        /// </summary>
        private void ResizeCells()
        {
            float available = ContentWidth();
            if (available <= 0f || Mathf.Abs(available - _appliedWidth) < 0.5f)
            {
                return;
            }
            _appliedWidth = available;
            int columns = Mathf.Max(MinColumns, _layout.constraintCount);
            float cellWidth = Mathf.Max(
                MinTileWidth, (available - TileSpacing * (columns - 1)) / columns);
            _layout.cellSize = new Vector2(cellWidth, TileHeight(cellWidth));
            foreach (TileParts tile in _tiles)
            {
                LayoutTile(tile, cellWidth);
            }
        }

        /// <summary>
        /// Width the grid has to fill: the view's own canvas less the margins.
        /// Falls back to the canvas reference width before the first layout pass,
        /// when the rect still measures zero.
        /// </summary>
        private float ContentWidth()
        {
            float width = Parent != null ? Parent.rect.width : 0f;
            if (width <= 1f)
            {
                width = UIBuilder.ReferenceWidth;
            }
            return width - 2f * _sideMargin;
        }

        /// <summary>
        /// A tile's height: the art at the preview's 300x192 aspect, then the hex
        /// caption, then the name subtitle. Every tile reserves the subtitle line
        /// whether or not it has a name, because one grid cell size has to suit
        /// all thirty.
        /// </summary>
        private static float TileHeight(float cellWidth)
        {
            int caption = CaptionFontSize(cellWidth);
            return TileInset
                + ArtHeight(cellWidth)
                + CaptionHeight(caption)
                + SubtitleHeight(caption)
                + TileInset;
        }

        /// <summary>Height of the art area for a tile of this width, at the preview's aspect.</summary>
        private static float ArtHeight(float cellWidth)
        {
            return (cellWidth - 2f * TileInset) * ArtPixelHeight / ArtPixelWidth;
        }

        /// <summary>Height of the hex caption row.</summary>
        private static float CaptionHeight(int captionSize)
        {
            return captionSize + 8f;
        }

        /// <summary>Height of the name subtitle row.</summary>
        private static float SubtitleHeight(int captionSize)
        {
            return SubtitleSize(captionSize) + 6f;
        }

        /// <summary>Font size of the name subtitle.</summary>
        private static int SubtitleSize(int captionSize)
        {
            return Mathf.Max(CaptionMinSize, captionSize - SubtitleSizeDrop);
        }

        /// <summary>
        /// Builds a tile's objects. The card itself is the only raycast target,
        /// so a tap anywhere on it — art, caption, or name — reaches the button.
        /// </summary>
        private static TileParts BuildTile(RectTransform parent, string rule, Action onClick)
        {
            var go = new GameObject(rule, typeof(RectTransform), typeof(Image), typeof(Button));
            var root = (RectTransform)go.transform;
            root.SetParent(parent, false);
            // The grid anchors its cells to the top-left corner; a tile built on
            // its own keeps the same convention, so its size is its sizeDelta.
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            var background = go.GetComponent<Image>();
            background.color = UIBuilder.PanelColor;
            var button = go.GetComponent<Button>();
            button.targetGraphic = background;
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            RawImage art = UIBuilder.CreateRawImage(root, "Art");
            art.raycastTarget = false;
            // A missing preview leaves the texture null, which uGUI draws as the
            // RawImage's flat color: the white rectangle PyQt's blank_preview put
            // in the same place. Nothing is allocated, so nothing has to be freed.
            art.texture = LoadArt(rule);
            art.color = Color.white;

            Text caption = UIBuilder.CreateText(
                root, "Caption", rule, CaptionMaxSize, UIBuilder.TextColor,
                TextAnchor.MiddleCenter);
            caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            caption.verticalOverflow = VerticalWrapMode.Truncate;

            string name = GalleryCatalog.Name(rule);
            Text subtitle = null;
            if (!string.IsNullOrEmpty(name))
            {
                subtitle = UIBuilder.CreateText(
                    root, "Name", name, CaptionMaxSize - SubtitleSizeDrop, UIBuilder.TextDimColor,
                    TextAnchor.MiddleCenter);
                subtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
                subtitle.verticalOverflow = VerticalWrapMode.Truncate;
            }

            return new TileParts(root, (RectTransform)art.transform, caption, subtitle);
        }

        /// <summary>
        /// Stacks a tile's art, caption, and name for a tile of
        /// <paramref name="cellWidth"/> units. Everything is anchored to the
        /// tile's top edge, so the grid can set the tile's size without the
        /// contents drifting.
        /// </summary>
        private static void LayoutTile(TileParts tile, float cellWidth)
        {
            float width = Mathf.Max(MinTileWidth, cellWidth);
            float artHeight = ArtHeight(width);
            int captionSize = CaptionFontSize(width);
            float captionHeight = CaptionHeight(captionSize);

            // Inside the grid this is redundant (the group drives the cell's own
            // size); outside it — a tile built on its own — it is what gives the
            // card the size its contents were laid out for.
            tile.Root.sizeDelta = new Vector2(width, TileHeight(width));
            PlaceRow(tile.Art, TileInset, artHeight);
            PlaceRow(tile.Caption.rectTransform, TileInset + artHeight, captionHeight);
            tile.Caption.fontSize = captionSize;
            if (tile.Name != null)
            {
                PlaceRow(
                    tile.Name.rectTransform,
                    TileInset + artHeight + captionHeight,
                    SubtitleHeight(captionSize));
                tile.Name.fontSize = SubtitleSize(captionSize);
            }
        }

        /// <summary>One full-width row of a tile, <paramref name="top"/> units down from its top.</summary>
        private static void PlaceRow(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-2f * TileInset, height);
            rect.anchoredPosition = new Vector2(0f, -top);
        }

        /// <summary>The pieces of one tile the layout pass has to move and resize.</summary>
        private readonly struct TileParts
        {
            /// <summary>The tile card: the button, and the only raycast target on it.</summary>
            public readonly RectTransform Root;

            /// <summary>The preview area, kept at the 300x192 aspect.</summary>
            public readonly RectTransform Art;

            /// <summary>The hex caption under the art.</summary>
            public readonly Text Caption;

            /// <summary>The name subtitle, or null for the 17 unnamed rules.</summary>
            public readonly Text Name;

            /// <summary>Records one tile's pieces.</summary>
            public TileParts(RectTransform root, RectTransform art, Text caption, Text name)
            {
                Root = root;
                Art = art;
                Caption = caption;
                Name = name;
            }
        }
    }
}
