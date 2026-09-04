using System;
using System.Collections.Generic;
using System.Globalization;
using HeatonCA.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace HeatonCAApp
{
    /// <summary>
    /// The app's core screen: a live MergeLife lattice with PyQt's transport
    /// (Start / Stop / Step / Reset), the rule controls (a canonicalizing text
    /// field, a preset dropdown, Random), and the three additions the port makes
    /// (Rule opens the decoder, Save PNG exports the frame, Copy rule puts the
    /// hex on the clipboard).
    ///
    /// Three things about it are deliberately different from
    /// <c>tab_simulate.py</c>:
    ///
    /// 1. The lattice is cut from a density-independent cell size
    ///    (<see cref="CellGeometry"/>) rather than a literal pixel count, so
    ///    "5" looks the same on a 96 dpi monitor and a 460 dpi phone.
    /// 2. A resize or a rotation re-cuts the lattice through
    ///    <see cref="LatticeResizer"/>: the overlapping cells and the generation
    ///    survive, where PyQt re-seeded the whole world. Rotating a phone is
    ///    now like widening a window, not like pressing Reset.
    /// 3. The image is drawn at whole cells and centered instead of stretched
    ///    (PyQt scaled with IgnoreAspectRatio), so a cell is always square.
    ///
    /// The view owns no simulation math: it hands worlds to
    /// <see cref="SimulationHost"/> (which <c>AppController</c> ticks) and
    /// pixels to <see cref="FrameBlitter"/>. It never touches
    /// <c>Screen.sleepTimeout</c> either; keeping the screen awake while
    /// playing is the controller's job.
    ///
    /// It answers <see cref="ISimulatorScreen"/> as well as the four-member view
    /// contract, which is how the WebGL <c>?controls=off</c> embed gets its
    /// kiosk look: chrome off, lattice across the whole page.
    /// </summary>
    public sealed class SimulatorView : AppViewBase, ISimulatorScreen
    {
        /// <summary>Toolbar buttons, in the order the plan lists them.</summary>
        private const int ToolbarButtonCount = 8;

        /// <summary>Top bar height in canvas units.</summary>
        private const float TopBarHeight = 56f;

        /// <summary>Toolbar button height in canvas units (the row pitch adds <see cref="Pad"/>).</summary>
        private const float ButtonHeight = 46f;

        /// <summary>Rule field and dropdown height in canvas units.</summary>
        private const float FieldHeight = 44f;

        /// <summary>Gap between a control and its cell edge, in canvas units.</summary>
        private const float Pad = 10f;

        /// <summary>Back button width in canvas units.</summary>
        private const float BackButtonWidth = 120f;

        /// <summary>How long the inline rule error stays up (the plan's "held 4 s").</summary>
        private const float ErrorHoldSeconds = 4f;

        /// <summary>Height of one row of the preset dropdown's list, in canvas units.</summary>
        private const float DropdownItemHeight = 40f;

        /// <summary>
        /// Joins a rule's display name to its hex in the preset dropdown
        /// ("Red World (paper) - e542-..."). The plan spells the entry as
        /// "Name - hex"; this belongs in AppStrings, which WP2.1 owns, so it is
        /// coined here and reported rather than added to that file from this
        /// work package.
        /// </summary>
        private const string PresetNameJoiner = " - ";

        private readonly Button[] _toolbarButtons = new Button[ToolbarButtonCount];
        private readonly List<string> _presetRules = new List<string>();
        private readonly FpsCounter _fps = new FpsCounter();

        private readonly RectTransform _topBar;
        private readonly RectTransform _toolbar;
        private readonly RectTransform _ruleRow;
        private readonly RectTransform _imageArea;
        private readonly RectTransform _overlayBox;
        private readonly RectTransform _errorBox;
        private readonly Text _title;
        private readonly Text _cellNote;
        private readonly Canvas _canvas;

        private string _rule;
        private ulong _soupSeed;
        private ulong _seedCounter;
        private GridSpec _spec;
        private Vector2 _areaPx;
        private bool _areaPinned;
        private bool _running;
        private bool _portrait;
        private bool _phone;
        private bool _chromeVisible = true;
        private float _chromeHeight;
        private float _toolbarHeight = ButtonHeight + Pad;
        private float _ruleRowHeight = FieldHeight + Pad;
        private float _errorSeconds;
        private int _appliedCellSize = -1;

        /// <summary>
        /// Builds the whole screen into <paramref name="parent"/> (the
        /// controller's safe-area rect) and lays it out for a landscape
        /// desktop; the controller calls <see cref="ApplyLayout"/> immediately
        /// afterwards with the real orientation, and <see cref="Open"/> loads
        /// the first world.
        /// </summary>
        /// <param name="parent">The safe-area rect this screen builds into.</param>
        /// <param name="nav">The navigator Back, Rule, and the status toasts talk to.</param>
        /// <param name="services">The shared host, blitter, and store.</param>
        public SimulatorView(RectTransform parent, INavigator nav, AppServices services)
            : base(parent, nav, services)
        {
            _canvas = parent != null ? parent.GetComponentInParent<Canvas>() : null;

            RectTransform root = UIBuilder.CreatePanel(parent, "SimulatorView");
            UIBuilder.Stretch(root);
            Root = root.gameObject;

            // --- image area (built first so the chrome draws over it) -------------
            _imageArea = UIBuilder.CreatePanel(root, "ImageArea", UIBuilder.BackgroundColor);
            LatticeImage = UIBuilder.CreateRawImage(_imageArea, "Lattice");
            LatticeImage.raycastTarget = false;
            var imageRt = (RectTransform)LatticeImage.transform;
            imageRt.anchorMin = new Vector2(0.5f, 0.5f);
            imageRt.anchorMax = new Vector2(0.5f, 0.5f);
            imageRt.pivot = new Vector2(0.5f, 0.5f);
            imageRt.anchoredPosition = Vector2.zero;
            imageRt.sizeDelta = new Vector2(16f, 16f);
            Services?.Blitter?.Attach(LatticeImage);

            // The overlay hugs its text: a VerticalLayoutGroup plus a fitter, so
            // the black box is exactly as wide as the longer of its two lines and
            // the optional cell-size note simply disappears when it is off.
            _overlayBox = UIBuilder.CreatePanel(_imageArea, "Overlay", Color.black);
            _overlayBox.anchorMin = new Vector2(1f, 1f);
            _overlayBox.anchorMax = new Vector2(1f, 1f);
            _overlayBox.pivot = new Vector2(1f, 1f);
            _overlayBox.anchoredPosition = new Vector2(-10f, -10f);
            var overlayLayout = _overlayBox.gameObject.AddComponent<VerticalLayoutGroup>();
            overlayLayout.padding = new RectOffset(10, 10, 6, 6);
            overlayLayout.spacing = 2f;
            overlayLayout.childAlignment = TextAnchor.UpperRight;
            overlayLayout.childControlWidth = true;
            overlayLayout.childControlHeight = true;
            overlayLayout.childForceExpandWidth = false;
            overlayLayout.childForceExpandHeight = false;
            var overlayFitter = _overlayBox.gameObject.AddComponent<ContentSizeFitter>();
            overlayFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            overlayFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            OverlayLabel = UIBuilder.CreateText(
                _overlayBox, "Steps", OverlayText(0, 0), 20, Color.white, TextAnchor.MiddleRight);
            OverlayLabel.fontStyle = FontStyle.Bold;
            _cellNote = UIBuilder.CreateText(
                _overlayBox, "CellNote", string.Empty, 14, Color.white, TextAnchor.MiddleRight);
            _cellNote.gameObject.SetActive(false);

            // The rule error sits at the top of the image area, immediately under
            // the field that produced it, and holds for four seconds.
            _errorBox = UIBuilder.CreatePanel(_imageArea, "RuleError", Color.black);
            _errorBox.anchorMin = new Vector2(0f, 1f);
            _errorBox.anchorMax = new Vector2(1f, 1f);
            _errorBox.pivot = new Vector2(0.5f, 1f);
            _errorBox.offsetMin = new Vector2(10f, -40f);
            _errorBox.offsetMax = new Vector2(-10f, 0f);
            ErrorLabel = UIBuilder.CreateText(
                _errorBox, "Label", AppStrings.SimInvalidRule, 18, Color.white,
                TextAnchor.MiddleCenter);
            UIBuilder.Stretch((RectTransform)ErrorLabel.transform, 10f, 0f, 10f, 0f);
            ErrorLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            ErrorLabel.verticalOverflow = VerticalWrapMode.Truncate;
            _errorBox.gameObject.SetActive(false);

            // --- top bar ----------------------------------------------------------
            _topBar = UIBuilder.CreatePanel(root, "TopBar", UIBuilder.PanelColor);
            BackButton = UIBuilder.CreateButton(
                _topBar, "Back", AppStrings.Back, 20, () => Nav?.Back());
            var backRt = (RectTransform)BackButton.transform;
            backRt.anchorMin = new Vector2(0f, 0.5f);
            backRt.anchorMax = new Vector2(0f, 0.5f);
            backRt.pivot = new Vector2(0f, 0.5f);
            backRt.anchoredPosition = new Vector2(12f, 0f);
            backRt.sizeDelta = new Vector2(BackButtonWidth, 40f);
            _title = UIBuilder.CreateText(
                _topBar, "Title", AppStrings.SimulatorTitle, 26, UIBuilder.TextColor,
                TextAnchor.MiddleCenter);
            _title.fontStyle = FontStyle.Bold;
            UIBuilder.PlaceBarTitle(_title);

            // --- toolbar ----------------------------------------------------------
            _toolbar = UIBuilder.CreatePanel(root, "Toolbar", UIBuilder.PanelColor);
            StartButton = MakeToolButton(0, "Start", AppStrings.SimStart, OnStart);
            StopButton = MakeToolButton(1, "Stop", AppStrings.SimStop, OnStop);
            StepButton = MakeToolButton(2, "Step", AppStrings.SimStep, OnStep);
            ResetButton = MakeToolButton(3, "Reset", AppStrings.SimReset, OnResetLattice);
            RuleButton = MakeToolButton(4, "Rule", AppStrings.SimRule, OnShowDecoder);
            RandomButton = MakeToolButton(5, "Random", AppStrings.SimRandom, OnRandomRule);
            SavePngButton = MakeToolButton(6, "SavePng", AppStrings.SimSavePng, OnSavePng);
            CopyRuleButton = MakeToolButton(7, "CopyRule", AppStrings.SimCopyRule, OnCopyRule);

            // --- rule row ---------------------------------------------------------
            _ruleRow = UIBuilder.CreatePanel(root, "RuleRow", UIBuilder.PanelColor);
            RuleField = UIBuilder.CreateInputField(
                _ruleRow, "RuleField", string.Empty, 19, OnRuleSubmitted);
            Text placeholder = UIBuilder.CreateText(
                RuleField.transform, "Placeholder", AppStrings.SimRulePlaceholder, 19,
                UIBuilder.TextDimColor);
            placeholder.fontStyle = FontStyle.Italic;
            UIBuilder.Stretch((RectTransform)placeholder.transform, 10f, 4f, 10f, 4f);
            RuleField.placeholder = placeholder;
            PresetDropdown = CreatePresetDropdown(_ruleRow, "Presets", 19);

            ApplyLayout(portrait: false, phone: false);
            RefreshButtons();
            RefreshOverlay();
            AppSettings.Changed += OnSettingsChanged;
        }

        /// <summary>The lattice image. Sized to whole cells and centered, never stretched.</summary>
        public RawImage LatticeImage { get; }

        /// <summary>The top bar's Back button; it pops the navigator's back stack.</summary>
        public Button BackButton { get; }

        /// <summary>Start: begins playback, disables itself and Step, enables Stop.</summary>
        public Button StartButton { get; }

        /// <summary>Stop: pauses playback and reverses Start's enable pattern.</summary>
        public Button StopButton { get; }

        /// <summary>Step: advances one generation; interactable only while stopped.</summary>
        public Button StepButton { get; }

        /// <summary>Reset: re-seeds the lattice and zeroes the step counter.</summary>
        public Button ResetButton { get; }

        /// <summary>Rule: opens the decoder table for the rule on screen.</summary>
        public Button RuleButton { get; }

        /// <summary>Random: loads a fresh <see cref="MergeLife.RandomRule"/>.</summary>
        public Button RandomButton { get; }

        /// <summary>Save PNG: exports the displayed frame through <see cref="PngExporter"/>.</summary>
        public Button SavePngButton { get; }

        /// <summary>Copy rule: puts the canonical rule on the clipboard.</summary>
        public Button CopyRuleButton { get; }

        /// <summary>The rule text field; its end-edit runs <see cref="RuleParser"/>.</summary>
        public InputField RuleField { get; }

        /// <summary>The preset picker: the three PyQt presets, then the named gallery rules.</summary>
        public Dropdown PresetDropdown { get; }

        /// <summary>The inline "Invalid rule" label; its box is active only while the error holds.</summary>
        public Text ErrorLabel { get; }

        /// <summary>The corner "Steps / FPS" readout; its box is active only when the setting is on.</summary>
        public Text OverlayLabel { get; }

        /// <summary>True while the top bar, toolbar, and rule row are on screen.</summary>
        public bool ChromeVisible => _chromeVisible;

        /// <summary>The canonical rule on screen, or null before the first <see cref="Open"/>.</summary>
        public string Rule => _rule;

        /// <summary>Lattice columns currently cut for the image area.</summary>
        public int Cols => _spec.Cols;

        /// <summary>Lattice rows currently cut for the image area.</summary>
        public int Rows => _spec.Rows;

        /// <summary>
        /// The rules behind <see cref="PresetDropdown"/>, in option order, so the
        /// selected index and the rule it applies can be checked together.
        /// </summary>
        public IReadOnlyList<string> PresetRules => _presetRules;

        /// <summary>
        /// The overlay line: "Steps: 1,234, FPS: 60". The step count carries
        /// invariant thousands separators (PyQt's <c>f"{n:,}"</c> is locale-free,
        /// and so is this), which is why the number is formatted here rather than
        /// left to the format string's own culture.
        /// </summary>
        /// <param name="steps">The lattice generation.</param>
        /// <param name="fps">Frames rendered in the last second.</param>
        /// <returns>The formatted overlay line.</returns>
        public static string OverlayText(int steps, int fps)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                AppStrings.SimOverlayFormat,
                steps.ToString("N0", CultureInfo.InvariantCulture),
                fps.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Load <paramref name="rule"/> into a lattice cut for the live image
        /// area and seed it with a fresh soup — PyQt's <c>changeRule</c>, which
        /// always re-seeded and zeroed the step count.
        /// </summary>
        /// <param name="rule">
        /// Any form <see cref="RuleParser"/> accepts; null (or unparsable) keeps
        /// the current rule, or <see cref="GalleryCatalog.DefaultRule"/> on the
        /// first visit.
        /// </param>
        /// <param name="autoStart">
        /// True starts playback immediately — what the gallery and the WebGL
        /// <c>?rule=</c> parameter do; false leaves it paused on generation 0.
        /// </param>
        public void Open(string rule, bool autoStart)
        {
            _areaPinned = false;
            OpenAt(rule, autoStart, MeasureAreaPixels());
        }

        /// <summary>
        /// <see cref="Open(string, bool)"/> for an image area of exactly
        /// <paramref name="areaWidthPx"/> x <paramref name="areaHeightPx"/>
        /// device pixels instead of the measured one, and pins that size against
        /// later measurement.
        ///
        /// The seam the PlayMode suite drives two canvas sizes through: a
        /// headless test has no window to resize, and the grid has to be exact
        /// for the re-cut assertions to mean anything. The app always calls the
        /// measuring overload.
        /// </summary>
        /// <param name="rule">As <see cref="Open(string, bool)"/>.</param>
        /// <param name="autoStart">As <see cref="Open(string, bool)"/>.</param>
        /// <param name="areaWidthPx">Image-area width in device pixels.</param>
        /// <param name="areaHeightPx">Image-area height in device pixels.</param>
        public void Open(string rule, bool autoStart, float areaWidthPx, float areaHeightPx)
        {
            _areaPinned = true;
            OpenAt(rule, autoStart, ClampArea(areaWidthPx, areaHeightPx));
        }

        /// <summary>
        /// Re-cut the lattice for an image area of exactly
        /// <paramref name="areaWidthPx"/> x <paramref name="areaHeightPx"/>
        /// device pixels, preserving the overlapping cells and the generation,
        /// and pin that size — the test counterpart of a window resize or a
        /// rotation, which the app routes through <see cref="ApplyLayout"/>.
        /// </summary>
        /// <param name="areaWidthPx">New image-area width in device pixels.</param>
        /// <param name="areaHeightPx">New image-area height in device pixels.</param>
        public void Resize(float areaWidthPx, float areaHeightPx)
        {
            _areaPinned = true;
            FitLattice(ClampArea(areaWidthPx, areaHeightPx));
        }

        /// <summary>
        /// Show or hide everything around the lattice — the kiosk look the old
        /// web viewer's <c>?controls=off</c> embeds used. Hiding the chrome hands
        /// its height back to the image area, so the lattice is re-cut to fill
        /// the whole page rather than merely growing its margins.
        /// </summary>
        /// <param name="visible">False to leave only the lattice on screen.</param>
        public void SetChromeVisible(bool visible)
        {
            if (_chromeVisible == visible)
            {
                return;
            }
            _chromeVisible = visible;
            LayoutChrome();
            FitLattice(_areaPinned ? _areaPx : MeasureAreaPixels());
        }

        /// <inheritdoc />
        public override void Show()
        {
            base.Show();
            _fps.Reset();
            Services?.Blitter?.Attach(LatticeImage);
            ApplySettings();
            if (Services?.Host != null && Services.Host.Sim != null)
            {
                // The transport intent is the view's, not the host's: Hide()
                // paused a running world, and coming back resumes it.
                Services.Host.Playing = _running;
            }
            SyncPresetSelection();
            RefreshButtons();
            RefreshOverlay();
        }

        /// <inheritdoc />
        public override void Hide()
        {
            base.Hide();
            if (Services?.Host != null)
            {
                Services.Host.Playing = false;
            }
            HideError();
        }

        /// <inheritdoc />
        public override void Tick(float dt)
        {
            _fps.Frame(Time.realtimeSinceStartupAsDouble);
            if (_errorSeconds > 0f)
            {
                _errorSeconds -= dt;
                if (_errorSeconds <= 0f)
                {
                    HideError();
                }
            }
            if (Services?.Host != null && Services.Host.Dirty)
            {
                Blit();
            }
            RefreshOverlay();
        }

        /// <inheritdoc />
        public override void ApplyLayout(bool portrait, bool phone)
        {
            _portrait = portrait;
            _phone = phone;
            LayoutChrome();
            FitLattice(_areaPinned ? _areaPx : MeasureAreaPixels());
        }

        // ---- transport ---------------------------------------------------------------

        /// <summary>PyQt <c>start_game</c>: Start and Step off, Stop on, the world running.</summary>
        private void OnStart()
        {
            _running = true;
            if (Services?.Host != null)
            {
                Services.Host.Playing = Services.Host.Sim != null;
            }
            RefreshButtons();
        }

        /// <summary>PyQt <c>stop_game</c>: the exact reverse of <see cref="OnStart"/>.</summary>
        private void OnStop()
        {
            _running = false;
            if (Services?.Host != null)
            {
                Services.Host.Playing = false;
            }
            RefreshButtons();
        }

        /// <summary>PyQt <c>step_game</c>: one generation, only reachable while stopped.</summary>
        private void OnStep()
        {
            Services?.Host?.StepOnce();
            RefreshButtons();
            RefreshOverlay();
        }

        /// <summary>
        /// PyQt <c>reset_game</c>: a fresh soup at generation 0, keeping the rule,
        /// the lattice shape, and whether the world was running.
        /// </summary>
        private void OnResetLattice()
        {
            SeedWorld(_spec, _running);
            RefreshButtons();
            RefreshOverlay();
        }

        // ---- rule controls -----------------------------------------------------------

        /// <summary>
        /// The rule field's end-edit. A parsable rule is canonicalized, written
        /// back into the field, and applied (which re-seeds); anything else
        /// raises the inline error for four seconds and leaves both the running
        /// rule and the typed text alone, so the user can fix a digit in place.
        /// </summary>
        private void OnRuleSubmitted(string typed)
        {
            if (RuleParser.TryParse(typed, out string canonical, out string error))
            {
                if (string.Equals(canonical, _rule, StringComparison.Ordinal))
                {
                    // End-edit also fires on focus loss, and re-seeding the world
                    // because the user clicked away would be a Reset nobody asked
                    // for; only canonicalize what is already on screen.
                    RuleField.SetTextWithoutNotify(_rule);
                    HideError();
                    return;
                }
                ApplyRule(canonical);
                return;
            }
            ShowError(error);
        }

        /// <summary>Preset picked: apply that entry's rule (which re-seeds, as PyQt did).</summary>
        private void OnPresetSelected(int index)
        {
            if (index < 0 || index >= _presetRules.Count)
            {
                return;
            }
            ApplyRule(_presetRules[index]);
        }

        /// <summary>Random: a fresh <see cref="MergeLife.RandomRule"/>, already canonical.</summary>
        private void OnRandomRule()
        {
            ApplyRule(MergeLife.CanonicalRule(MergeLife.RandomRule(unchecked((uint)NextSeed()))));
        }

        /// <summary>Rule: the decoder table for what is on screen.</summary>
        private void OnShowDecoder()
        {
            if (!string.IsNullOrEmpty(_rule))
            {
                Nav?.ShowRuleDecoder(_rule);
            }
        }

        /// <summary>Copy rule: the canonical hex on the clipboard, with a toast.</summary>
        private void OnCopyRule()
        {
            WebGlBridge.CopyText(_rule ?? string.Empty);
            Nav?.Status(AppStrings.SimStatusCopied);
        }

        /// <summary>
        /// Save PNG: the last blitted frame, named for the rule's first hex group
        /// ("heatonca-e542-20260902-140301-123.png"). Where it lands is
        /// <see cref="PngExporter"/>'s business, and whatever it reports becomes
        /// the toast.
        /// </summary>
        private void OnSavePng()
        {
            byte[] png = Services?.Blitter?.EncodePng();
            string file = PngExporter.SnapshotFileName(FirstGroup(_rule), DateTime.UtcNow);
            PngExporter.Save(png, file, message => Nav?.Status(message));
        }

        /// <summary>
        /// Adopt <paramref name="canonical"/>: field, dropdown, and a freshly
        /// seeded world at the current lattice shape. The transport state
        /// survives — PyQt's <c>changeRule</c> left <c>_running</c> alone.
        /// </summary>
        private void ApplyRule(string canonical)
        {
            _rule = canonical;
            RuleField.SetTextWithoutNotify(_rule);
            SyncPresetSelection();
            HideError();
            SeedWorld(_spec, _running);
            RefreshButtons();
            RefreshOverlay();
        }

        // ---- world -------------------------------------------------------------------

        /// <summary>
        /// Cut a lattice for <paramref name="areaPx"/>, adopt
        /// <paramref name="rule"/>, and seed it.
        /// </summary>
        private void OpenAt(string rule, bool autoStart, Vector2 areaPx)
        {
            _rule = ResolveRule(rule);
            RuleField.SetTextWithoutNotify(_rule);
            SyncPresetSelection();
            HideError();
            _running = autoStart;
            SeedWorld(CutGrid(areaPx), autoStart);

            // Blit the seeded lattice immediately rather than waiting for the first
            // Tick: a world opened paused would otherwise show — and, worse, export —
            // an empty texture until something advanced it. This is the house
            // OpenWorld pattern (blit, then clear Dirty).
            Blit();

            RefreshButtons();
            RefreshOverlay();
        }

        /// <summary>
        /// A new <see cref="MergeLife"/> at <paramref name="spec"/>'s shape,
        /// seeded from a fresh soup seed (kept, because
        /// <see cref="LatticeResizer"/> draws the cells a later re-cut exposes
        /// from that same seed).
        /// </summary>
        private void SeedWorld(GridSpec spec, bool play)
        {
            if (Services?.Host == null || string.IsNullOrEmpty(_rule))
            {
                return;
            }
            var sim = new MergeLife(_rule, spec.Cols, spec.Rows);
            _soupSeed = NextSeed();
            sim.SeedSoup(_soupSeed);
            Services.Host.StepsPerSecond = AppSettings.StepsPerSecond;
            Services.Host.Load(sim, play);
            Blit();
        }

        /// <summary>
        /// Re-cut for <paramref name="areaPx"/>: size the image, refresh the
        /// capped-cell note, and — when the shape actually changed — hand the
        /// host a world carrying the old lattice's overlap and its generation.
        /// </summary>
        private void FitLattice(Vector2 areaPx)
        {
            GridSpec spec = CutGrid(areaPx);
            if (Services?.Host == null || string.IsNullOrEmpty(_rule))
            {
                return;
            }
            if (!(Services.Host.Sim is MergeLife current)
                || !string.Equals(current.Rule, _rule, StringComparison.Ordinal))
            {
                // Nothing of this view's is loaded yet; Open() seeds the first world.
                return;
            }
            if (current.Width == spec.Cols && current.Height == spec.Rows)
            {
                return;
            }
            byte[] cells = LatticeResizer.Recut(
                current.State, current.Width, current.Height, spec.Cols, spec.Rows, _soupSeed);
            var next = new MergeLife(_rule, spec.Cols, spec.Rows);
            next.SetState(cells, current.Generation);
            Services.Host.Load(next, Services.Host.Playing);
            Blit();
            RefreshOverlay();
        }

        /// <summary>
        /// The lattice for <paramref name="areaPx"/>, remembered along with the
        /// area and the cell size it was cut at, and reflected in the image size
        /// and the capped-cell note.
        /// </summary>
        private GridSpec CutGrid(Vector2 areaPx)
        {
            _areaPx = areaPx;
            _appliedCellSize = AppSettings.CellSize;
            _spec = CellGeometry.GridFor(
                areaPx.x, areaPx.y, Screen.dpi, _appliedCellSize, Application.isMobilePlatform);
            SizeImage(_spec);
            UpdateCellNote(_spec);
            return _spec;
        }

        /// <summary>
        /// The image is exactly <c>Cols*CellPx x Rows*CellPx</c> device pixels,
        /// centered in the area. PyQt scaled its pixmap with IgnoreAspectRatio,
        /// which turned square cells into rectangles on every window shape but
        /// one; whole cells and a centered rect is the replacement.
        /// </summary>
        private void SizeImage(GridSpec spec)
        {
            float scale = CanvasScale();
            var rt = (RectTransform)LatticeImage.transform;
            rt.sizeDelta = new Vector2(spec.WidthPx / scale, spec.HeightPx / scale);
        }

        /// <summary>Show "cell size raised to N" only while the cell budget forced a coarser lattice.</summary>
        private void UpdateCellNote(GridSpec spec)
        {
            bool capped = spec.WasCapped(AppSettings.CellSize);
            if (capped)
            {
                _cellNote.text = string.Format(
                    CultureInfo.InvariantCulture,
                    AppStrings.SimCellSizeRaisedFormat,
                    spec.EffectiveCellSize.ToString(CultureInfo.InvariantCulture));
            }
            if (_cellNote.gameObject.activeSelf != capped)
            {
                _cellNote.gameObject.SetActive(capped);
            }
        }

        /// <summary>Upload the current frame and clear the host's dirty flag.</summary>
        private void Blit()
        {
            if (Services?.Blitter == null || Services.Host == null)
            {
                return;
            }
            if (Services.Host.Sim is IRgbFrameSource frame)
            {
                Services.Blitter.Blit(frame);
            }
            Services.Host.Dirty = false;
        }

        // ---- settings ----------------------------------------------------------------

        /// <summary>
        /// Live settings: speed retimes the transport, the overlay toggles, and a
        /// new cell size re-cuts the lattice (keeping the overlap, so changing
        /// the cell size does not throw the world away).
        ///
        /// Self-unsubscribing: a view whose root has been destroyed drops the
        /// handler instead of touching dead objects, which is what keeps the
        /// PlayMode suite's per-test views from piling up on a static event.
        /// </summary>
        private void OnSettingsChanged()
        {
            if (Root == null)
            {
                AppSettings.Changed -= OnSettingsChanged;
                return;
            }
            ApplySettings();
        }

        private void ApplySettings()
        {
            if (Services?.Host != null)
            {
                Services.Host.StepsPerSecond = AppSettings.StepsPerSecond;
            }
            if (AppSettings.CellSize != _appliedCellSize)
            {
                FitLattice(_areaPinned ? _areaPx : MeasureAreaPixels());
            }
            RefreshOverlay();
        }

        // ---- chrome ------------------------------------------------------------------

        /// <summary>
        /// The three layouts. Landscape desktop and tablet get one toolbar row
        /// and a side-by-side rule row; portrait wraps the toolbar to two rows
        /// (bigger touch targets on a narrow canvas); a phone in portrait also
        /// stacks the rule field over the preset picker, because a 39-character
        /// rule has no room to share a line there.
        /// </summary>
        private void LayoutChrome()
        {
            int rows = _portrait ? 2 : 1;
            int columns = (ToolbarButtonCount + rows - 1) / rows;
            bool stackedRuleRow = _phone && _portrait;
            int toolFont = _phone ? 17 : 20;

            _toolbarHeight = rows * (ButtonHeight + Pad);
            _ruleRowHeight = (stackedRuleRow ? 2 : 1) * (FieldHeight + Pad);

            _chromeHeight = _chromeVisible
                ? TopBarHeight + _toolbarHeight + _ruleRowHeight
                : 0f;

            _title.fontSize = _phone ? 22 : 26;
            UIBuilder.PlaceBarTitle(_title);

            PlaceStrip(_topBar, 0f, TopBarHeight);
            PlaceStrip(_toolbar, TopBarHeight, _toolbarHeight);
            PlaceStrip(_ruleRow, TopBarHeight + _toolbarHeight, _ruleRowHeight);
            SetChromeActive(_topBar);
            SetChromeActive(_toolbar);
            SetChromeActive(_ruleRow);
            UIBuilder.Stretch(_imageArea, 0f, 0f, 0f, _chromeHeight);

            for (int i = 0; i < ToolbarButtonCount; i++)
            {
                int column = i % columns;
                int row = i / columns;
                PlaceCell(
                    (RectTransform)_toolbarButtons[i].transform,
                    column / (float)columns,
                    (column + 1) / (float)columns,
                    1f - (row + 1) / (float)rows,
                    1f - row / (float)rows);
                Text label = _toolbarButtons[i].GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.fontSize = toolFont;
                    label.resizeTextForBestFit = true;
                    label.resizeTextMaxSize = toolFont;
                    label.resizeTextMinSize = 12;
                }
            }

            var fieldRt = (RectTransform)RuleField.transform;
            var presetRt = (RectTransform)PresetDropdown.transform;
            if (stackedRuleRow)
            {
                PlaceCell(fieldRt, 0f, 1f, 0.5f, 1f);
                PlaceCell(presetRt, 0f, 1f, 0f, 0.5f);
            }
            else
            {
                PlaceCell(fieldRt, 0f, 0.62f, 0f, 1f);
                PlaceCell(presetRt, 0.62f, 1f, 0f, 1f);
            }
        }

        /// <summary>
        /// Total chrome above the image area, in canvas units — zero while the
        /// kiosk flag has it hidden, which is what lets the lattice fill an
        /// embedded page edge to edge.
        /// </summary>
        private float ChromeHeight => _chromeHeight;

        /// <summary>Anchor a full-width strip <paramref name="top"/> units below the top edge.</summary>
        private static void PlaceStrip(RectTransform rt, float top, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -(top + height));
            rt.offsetMax = new Vector2(0f, -top);
        }

        /// <summary>
        /// Put a control in a normalized cell of its parent, inset by half a
        /// <see cref="Pad"/> on every side. Normalized rather than absolute so
        /// the toolbar never assumes a canvas width.
        /// </summary>
        private static void PlaceCell(
            RectTransform rt, float minX, float maxX, float minY, float maxY)
        {
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(Pad * 0.5f, Pad * 0.5f);
            rt.offsetMax = new Vector2(-Pad * 0.5f, -Pad * 0.5f);
        }

        private void SetChromeActive(RectTransform strip)
        {
            if (strip.gameObject.activeSelf != _chromeVisible)
            {
                strip.gameObject.SetActive(_chromeVisible);
            }
        }

        private Button MakeToolButton(int index, string name, string label, Action action)
        {
            Button button = UIBuilder.CreateButton(_toolbar, name, label, 20, () => action());
            _toolbarButtons[index] = button;
            return button;
        }

        /// <summary>
        /// PyQt's enable pattern, which is the whole transport contract: Start
        /// and Step are live only while stopped, Stop only while running.
        /// </summary>
        private void RefreshButtons()
        {
            StartButton.interactable = !_running;
            StopButton.interactable = _running;
            StepButton.interactable = !_running;
        }

        private void RefreshOverlay()
        {
            bool show = AppSettings.ShowOverlay;
            if (_overlayBox.gameObject.activeSelf != show)
            {
                _overlayBox.gameObject.SetActive(show);
            }
            if (!show)
            {
                return;
            }
            int steps = Services?.Host != null ? Services.Host.Generation : 0;
            OverlayLabel.text = OverlayText(steps, _fps.Fps);
        }

        private void ShowError(string message)
        {
            ErrorLabel.text = message;
            _errorBox.gameObject.SetActive(true);
            _errorSeconds = ErrorHoldSeconds;
        }

        private void HideError()
        {
            _errorSeconds = 0f;
            if (_errorBox != null && _errorBox.gameObject.activeSelf)
            {
                _errorBox.gameObject.SetActive(false);
            }
        }

        // ---- presets -----------------------------------------------------------------

        /// <summary>
        /// The preset list: PyQt's three <c>tab_simulate.RULES</c> first, then the
        /// gallery rules the engine's featured catalog names, as "Name - hex".
        ///
        /// All three presets happen to be named gallery rules, so the two lists
        /// overlap; a rule already listed is skipped rather than shown twice.
        /// </summary>
        private Dropdown CreatePresetDropdown(RectTransform parent, string name, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Dropdown));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var background = go.GetComponent<Image>();
            background.color = UIBuilder.ControlColor;

            Text caption = UIBuilder.CreateText(
                go.transform, "Label", string.Empty, fontSize, UIBuilder.TextColor);
            caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            caption.verticalOverflow = VerticalWrapMode.Truncate;
            caption.resizeTextForBestFit = true;
            caption.resizeTextMaxSize = fontSize;
            caption.resizeTextMinSize = 12;
            UIBuilder.Stretch((RectTransform)caption.transform, 12f, 4f, 22f, 4f);

            // The open-list affordance is a plain accent strip, not a triangle
            // glyph: LegacyRuntime.ttf text in this app stays inside Latin-1.
            RectTransform grip = UIBuilder.CreatePanel(go.transform, "Grip", UIBuilder.AccentColor);
            grip.anchorMin = new Vector2(1f, 0f);
            grip.anchorMax = new Vector2(1f, 1f);
            grip.pivot = new Vector2(1f, 0.5f);
            grip.offsetMin = new Vector2(-8f, 8f);
            grip.offsetMax = new Vector2(0f, -8f);

            var templateGo = new GameObject(
                "Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            var template = (RectTransform)templateGo.transform;
            template.SetParent(go.transform, false);
            template.anchorMin = new Vector2(0f, 0f);
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.anchoredPosition = new Vector2(0f, 2f);
            template.sizeDelta = new Vector2(0f, DropdownItemHeight * 6f);
            templateGo.GetComponent<Image>().color = UIBuilder.PanelColor;

            // RectMask2D rather than Mask: plain rect clipping, no stencil
            // material, and it is what UIBuilder's own scroll view uses.
            var viewportGo = new GameObject(
                "Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            var viewport = (RectTransform)viewportGo.transform;
            viewport.SetParent(template, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0f, 1f);
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().color = UIBuilder.PanelColor;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            var content = (RectTransform)contentGo.transform;
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, DropdownItemHeight);

            var itemGo = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            var item = (RectTransform)itemGo.transform;
            item.SetParent(content, false);
            item.anchorMin = new Vector2(0f, 0.5f);
            item.anchorMax = new Vector2(1f, 0.5f);
            item.pivot = new Vector2(0.5f, 0.5f);
            item.sizeDelta = new Vector2(0f, DropdownItemHeight);

            RectTransform itemBackground = UIBuilder.CreatePanel(
                item, "Item Background", UIBuilder.ControlColor);
            UIBuilder.Stretch(itemBackground);
            RectTransform itemCheckmark = UIBuilder.CreatePanel(
                item, "Item Checkmark", UIBuilder.AccentColor);
            itemCheckmark.anchorMin = new Vector2(0f, 0.5f);
            itemCheckmark.anchorMax = new Vector2(0f, 0.5f);
            itemCheckmark.pivot = new Vector2(0f, 0.5f);
            itemCheckmark.anchoredPosition = new Vector2(10f, 0f);
            itemCheckmark.sizeDelta = new Vector2(10f, 10f);
            Text itemLabel = UIBuilder.CreateText(
                item, "Item Label", string.Empty, fontSize, UIBuilder.TextColor);
            UIBuilder.Stretch((RectTransform)itemLabel.transform, 28f, 1f, 10f, 2f);

            var toggle = itemGo.GetComponent<Toggle>();
            toggle.targetGraphic = itemBackground.GetComponent<Image>();
            toggle.graphic = itemCheckmark.GetComponent<Image>();
            toggle.isOn = true;

            var scroll = templateGo.GetComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            templateGo.SetActive(false);

            var options = new List<Dropdown.OptionData>();
            foreach (string rule in GalleryCatalog.Presets)
            {
                AddPresetOption(options, rule);
            }
            foreach ((string Name, string Rule) named in GalleryCatalog.NamedRules)
            {
                AddPresetOption(options, named.Rule);
            }

            var dropdown = go.GetComponent<Dropdown>();
            dropdown.targetGraphic = background;
            dropdown.template = template;
            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            dropdown.options = options;
            dropdown.SetValueWithoutNotify(0);
            dropdown.RefreshShownValue();
            dropdown.onValueChanged.AddListener(OnPresetSelected);
            return dropdown;
        }

        private void AddPresetOption(List<Dropdown.OptionData> options, string rule)
        {
            if (_presetRules.Contains(rule))
            {
                return;
            }
            _presetRules.Add(rule);
            options.Add(new Dropdown.OptionData(PresetLabel(rule)));
        }

        /// <summary>"Name - hex" for a named rule, the bare hex for the rest.</summary>
        private static string PresetLabel(string rule)
        {
            string name = GalleryCatalog.Name(rule);
            return string.IsNullOrEmpty(name) ? rule : name + PresetNameJoiner + rule;
        }

        /// <summary>
        /// Point the dropdown at the rule on screen. A rule that is not one of
        /// the presets (a typed rule, a Random one, a gallery rule the catalog
        /// does not name) leaves the selection where it was and simply captions
        /// the control with what is actually running.
        /// </summary>
        private void SyncPresetSelection()
        {
            if (string.IsNullOrEmpty(_rule))
            {
                return;
            }
            int index = _presetRules.IndexOf(_rule);
            if (index >= 0 && PresetDropdown.value != index)
            {
                PresetDropdown.SetValueWithoutNotify(index);
            }
            if (PresetDropdown.captionText != null)
            {
                PresetDropdown.captionText.text = PresetLabel(_rule);
            }
        }

        // ---- geometry ----------------------------------------------------------------

        /// <summary>
        /// The image area in device pixels, derived from the parent rect and the
        /// chrome rather than read back from a laid-out child: a view built this
        /// frame has no layout pass behind it yet, and the first world should not
        /// be cut from a zero rect.
        /// </summary>
        private Vector2 MeasureAreaPixels()
        {
            float scale = CanvasScale();
            Rect parentRect = Parent != null ? Parent.rect : new Rect();
            float widthUnits = parentRect.width;
            float heightUnits = parentRect.height - ChromeHeight;
            if (widthUnits < 1f || heightUnits < 1f)
            {
                // No usable rect yet (pre-layout, or a collapsed panel): fall back
                // to the screen less the chrome, which CellGeometry then floors at
                // its 8x8 minimum if even that is a sliver.
                return ClampArea(Screen.width, Screen.height - ChromeHeight * scale);
            }
            return ClampArea(widthUnits * scale, heightUnits * scale);
        }

        /// <summary>Device pixels per canvas unit.</summary>
        private float CanvasScale()
        {
            return _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
        }

        private static Vector2 ClampArea(float widthPx, float heightPx)
        {
            return new Vector2(Mathf.Max(1f, widthPx), Mathf.Max(1f, heightPx));
        }

        // ---- odds and ends -----------------------------------------------------------

        private string ResolveRule(string requested)
        {
            if (!string.IsNullOrEmpty(requested)
                && RuleParser.TryParse(requested, out string canonical, out _))
            {
                return canonical;
            }
            return string.IsNullOrEmpty(_rule) ? GalleryCatalog.DefaultRule : _rule;
        }

        /// <summary>The rule's first hex group, which is the snapshot file's slug.</summary>
        private static string FirstGroup(string rule)
        {
            if (string.IsNullOrEmpty(rule))
            {
                return string.Empty;
            }
            int dash = rule.IndexOf('-');
            return dash > 0 ? rule.Substring(0, dash) : rule;
        }

        /// <summary>
        /// A soup seed nobody has used yet: the clock, mixed with a per-view
        /// counter so two seeds drawn inside one clock tick still differ. Nothing
        /// reproducible depends on it — the engine's determinism contract is about
        /// replaying a *given* seed, which is exactly what a re-cut does.
        /// </summary>
        private ulong NextSeed()
        {
            unchecked
            {
                _seedCounter++;
                return (ulong)DateTime.UtcNow.Ticks ^ (_seedCounter * 0x9E3779B97F4A7C15UL);
            }
        }
    }
}
