using System;
using System.Collections.Generic;
using System.Globalization;
using HeatonCA.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace HeatonCAApp
{
    /// <summary>
    /// The Evolve screen: the port of PyQt's <c>tab_evolve.py</c>. Two buttons
    /// (Start and Stop, with PyQt's exact enable rules), the same nine label/value
    /// readouts in the same order, a live 96x96 preview of the run's best rule, one
    /// user-adjustable GA parameter (the save threshold), and a Finds button where
    /// PyQt had an output directory -- the folder of <c>&lt;rule&gt;.png</c> files
    /// became the persisted finds gallery, which is the only real product change.
    ///
    /// Everything else about the search is the PyQt trainer's configuration
    /// (50x50, population 100, five eval cycles, a thousand steps, patience 250,
    /// crossover 0.75, tournament 5) and lives in <see cref="EvolveHost"/>; this
    /// screen only presses Start and reads the numbers back. It never steps a
    /// simulation itself either: the preview is a <see cref="SimulationHost"/> plus
    /// a <see cref="FrameBlitter"/>, exactly like the simulator screen.
    ///
    /// Leaving the screen does not end the search -- Back asks it to stop the way
    /// closing the PyQt tab did, but opening a find in the simulator deliberately
    /// leaves it breeding (Home shows an "Evolving..." chip while it does), and the
    /// finds log survives every one of those exits.
    /// </summary>
    public sealed class EvolveView : AppViewBase
    {
        /// <summary>Lattice edge of the live preview, in cells.</summary>
        public const int PreviewCells = 96;

        /// <summary>Soup seed the preview uses, so one rule always looks the same.</summary>
        public const ulong PreviewSeed = 0;

        /// <summary>Generations per second the preview plays at.</summary>
        public const double PreviewStepsPerSecond = 30;

        private const float TopBarHeight = 64f;
        private const float BottomBarHeight = 76f;
        private const float PreviewColumnWidth = 300f;
        private const float PreviewStripPhone = 150f;
        private const float PreviewStripTablet = 230f;
        private const float LabelColumnWide = 264f;
        private const float ColumnGap = 12f;
        private const float FindsButtonWidth = 132f;
        private const float FindsButtonHeight = 38f;
        private const float RowGap = 6f;
        private const float ThresholdRowHeight = 66f;
        private const float NoteHeight = 48f;
        private const int RowFontWide = 18;
        private const int RowFontPhone = 17;

        private readonly EvolveHost _host;
        private readonly FindsThumbnailer _thumbs;
        private readonly SimulationHost _previewHost = new SimulationHost();
        private readonly FrameBlitter _previewBlitter = new FrameBlitter();
        private readonly List<ReadoutRow> _rows = new List<ReadoutRow>();

        private readonly Text _title;
        private readonly RectTransform _body;
        private readonly RectTransform _previewArea;
        private readonly RawImage _previewImage;
        private readonly ScrollRect _scroll;
        private readonly RectTransform _content;
        private readonly RectTransform _thresholdRow;
        private readonly LayoutElement _thresholdElement;
        private readonly Text _thresholdCaption;
        private readonly Text _thresholdValue;
        private readonly Slider _thresholdSlider;
        private readonly Text _browserNote;
        private readonly Text _batteryNote;
        private readonly LayoutElement _browserNoteElement;
        private readonly LayoutElement _batteryNoteElement;
        private readonly RectTransform _bottomBar;
        private readonly FindsView _finds;

        private readonly ReadoutRow _runRow;
        private readonly ReadoutRow _evalRow;
        private readonly ReadoutRow _perMinuteRow;
        private readonly ReadoutRow _ruleRow;
        private readonly ReadoutRow _scoreRow;
        private readonly ReadoutRow _noImproveRow;
        private readonly ReadoutRow _foundRow;
        private readonly ReadoutRow _statusRow;
        private readonly ReadoutRow _savedFindsRow;

        private MergeLife _previewSim;
        private string _previewRule;
        private double _threshold = EvolveHost.DefaultThreshold;

        private bool _phone;
        private bool _portrait;
        private bool _findsWereVisible;

        // Last-rendered readout values, so a screen that changes nothing formats
        // nothing: this Tick runs beside a search that is using every core.
        private int _shownEvals = int.MinValue;
        private int _shownPerMinute = int.MinValue;
        private int _shownFound = int.MinValue;
        private int _shownKept = int.MinValue;
        private string _shownBest = "\0";
        private EvolveHost.EvolveStatus _shownStatus = (EvolveHost.EvolveStatus)(-1);

        /// <summary>
        /// Build the screen into <paramref name="parent"/> (the safe-area rect).
        /// </summary>
        /// <param name="parent">The rect this screen fills.</param>
        /// <param name="nav">The app's navigator.</param>
        /// <param name="services">The shared service bag; its EvolveHost is the search.</param>
        public EvolveView(RectTransform parent, INavigator nav, AppServices services)
            : base(parent, nav, services)
        {
            // The host and the thumbnailer are the controller's, shared with the
            // Home chip and every other screen. The fallbacks keep a view built
            // without a service bag (a focused test) from throwing in its
            // constructor rather than failing where the missing service is used.
            _host = services != null && services.Evolve != null ? services.Evolve : new EvolveHost();
            _thumbs = services != null && services.Thumbnails != null
                ? services.Thumbnails
                : new FindsThumbnailer();

            RectTransform root = UIBuilder.CreatePanel(parent, "EvolveView", UIBuilder.BackgroundColor);
            UIBuilder.Stretch(root);
            Root = root.gameObject;

            RectTransform topBar = UIBuilder.CreatePanel(root, "TopBar", UIBuilder.PanelColor);
            topBar.anchorMin = new Vector2(0f, 1f);
            topBar.anchorMax = Vector2.one;
            topBar.offsetMin = new Vector2(0f, -TopBarHeight);
            topBar.offsetMax = Vector2.zero;
            TopBar = topBar;

            BackButton = UIBuilder.CreateButton(topBar, "Back", AppStrings.Back, 20, OnBack);
            var backRt = (RectTransform)BackButton.transform;
            backRt.anchorMin = new Vector2(0f, 0.5f);
            backRt.anchorMax = new Vector2(0f, 0.5f);
            backRt.pivot = new Vector2(0f, 0.5f);
            backRt.sizeDelta = new Vector2(140f, 42f);
            backRt.anchoredPosition = new Vector2(16f, 0f);

            _title = UIBuilder.CreateText(
                topBar, "Title", AppStrings.EvolveTitle, 26, UIBuilder.TextColor,
                TextAnchor.MiddleCenter);
            _title.fontStyle = FontStyle.Bold;

            RectTransform bottomBar = UIBuilder.CreatePanel(root, "BottomBar", UIBuilder.PanelColor);
            bottomBar.anchorMin = Vector2.zero;
            bottomBar.anchorMax = new Vector2(1f, 0f);
            bottomBar.offsetMin = Vector2.zero;
            bottomBar.offsetMax = new Vector2(0f, BottomBarHeight);
            _bottomBar = bottomBar;

            StartButton = UIBuilder.CreateButton(
                bottomBar, "Start", AppStrings.EvolveStart, 22, OnStart);
            var startRt = (RectTransform)StartButton.transform;
            startRt.anchorMin = Vector2.zero;
            startRt.anchorMax = new Vector2(0.5f, 1f);
            startRt.offsetMin = new Vector2(16f, 12f);
            startRt.offsetMax = new Vector2(-8f, -12f);

            StopButton = UIBuilder.CreateButton(
                bottomBar, "Stop", AppStrings.EvolveStop, 22, OnStop);
            var stopRt = (RectTransform)StopButton.transform;
            stopRt.anchorMin = new Vector2(0.5f, 0f);
            stopRt.anchorMax = Vector2.one;
            stopRt.offsetMin = new Vector2(8f, 12f);
            stopRt.offsetMax = new Vector2(-16f, -12f);

            _body = UIBuilder.CreatePanel(root, "Body");
            UIBuilder.Stretch(_body, 0f, BottomBarHeight, 0f, TopBarHeight);

            _previewArea = UIBuilder.CreatePanel(_body, "PreviewArea");
            _previewImage = UIBuilder.CreateRawImage(_previewArea, "Preview");
            _previewImage.raycastTarget = false;
            // A RawImage with no texture paints solid white, which is the wrong
            // thing to show before the first rule is bred: stay hidden instead and
            // let the screen background through.
            _previewImage.enabled = false;
            UIBuilder.Stretch((RectTransform)_previewImage.transform);
            var fitter = _previewImage.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;

            _scroll = UIBuilder.CreateScrollView(_body, "Readouts", out _content);
            var layout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = RowGap;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            // PyQt's grid, row for row and label for label; only the last one
            // differs, because the finds live in the app now instead of a folder.
            _runRow = AddRow(AppStrings.EvolveRunNumberLabel);
            _evalRow = AddRow(AppStrings.EvolveEvalNumberLabel);
            _perMinuteRow = AddRow(AppStrings.EvolveEvalsPerMinuteLabel);
            _ruleRow = AddRow(AppStrings.EvolveCurrentRuleLabel);
            _scoreRow = AddRow(AppStrings.EvolveCurrentScoreLabel);
            _noImproveRow = AddRow(AppStrings.EvolveNoImproveLabel);
            _foundRow = AddRow(AppStrings.EvolveRulesFoundLabel);
            _statusRow = AddRow(AppStrings.EvolveStatusLabel);
            _savedFindsRow = AddRow(
                AppStrings.EvolveSavedFindsLabel, FindsButtonWidth + ColumnGap);

            FindsButton = UIBuilder.CreateButton(
                _savedFindsRow.Row, "Finds", AppStrings.EvolveFindsButton, 18, ShowFinds);
            var findsRt = (RectTransform)FindsButton.transform;
            findsRt.anchorMin = new Vector2(1f, 0.5f);
            findsRt.anchorMax = new Vector2(1f, 0.5f);
            findsRt.pivot = new Vector2(1f, 0.5f);
            findsRt.sizeDelta = new Vector2(FindsButtonWidth, FindsButtonHeight);
            findsRt.anchoredPosition = Vector2.zero;

            _thresholdRow = UIBuilder.CreatePanel(_content, "ThresholdRow");
            _thresholdElement = _thresholdRow.gameObject.AddComponent<LayoutElement>();
            _thresholdElement.minHeight = ThresholdRowHeight;
            _thresholdElement.preferredHeight = ThresholdRowHeight;
            _thresholdCaption = UIBuilder.CreateText(
                _thresholdRow, "Caption", AppStrings.EvolveThresholdLabel, RowFontWide,
                UIBuilder.TextDimColor);
            _thresholdValue = UIBuilder.CreateText(
                _thresholdRow, "Value", Format2(EvolveHost.DefaultThreshold), RowFontWide,
                UIBuilder.TextColor, TextAnchor.MiddleRight);
            _thresholdSlider = UIBuilder.CreateSlider(
                _thresholdRow, "Slider",
                (float)EvolveHost.MinThreshold, (float)EvolveHost.MaxThreshold,
                (float)EvolveHost.DefaultThreshold, false, OnThresholdChanged);
            _host.Threshold = EvolveHost.DefaultThreshold;

            _browserNote = AddNote("BrowserNote", AppStrings.EvolveBrowserNote, out _browserNoteElement);
            _batteryNote = AddNote("BatteryNote", AppStrings.EvolveBatteryNote, out _batteryNoteElement);
            // Runtime checks, not compile-time defines: this file is not one of the
            // plan's platform seams, and both notes are simply false elsewhere.
            _browserNote.gameObject.SetActive(Application.platform == RuntimePlatform.WebGLPlayer);
            _batteryNote.gameObject.SetActive(Application.isMobilePlatform);

            _finds = new FindsView(root, nav, _host, _thumbs, HideFinds);

            ApplyLayout(portrait: false, phone: UIBuilder.IsPhoneLayout);
            RenderReadouts(null);
            UpdateButtons();
        }

        /// <summary>
        /// True when this screen pumps the search and the thumbnail queue itself.
        /// False in the app: <c>AppController.Update</c> already ticks
        /// <see cref="EvolveHost"/> and <see cref="FindsThumbnailer"/> every frame
        /// (the search is session-long and keeps breeding while the user is on
        /// another screen), so a second pump here would advance it twice per frame.
        /// The PlayMode suite drives this screen without a controller and turns it
        /// on.
        /// </summary>
        public bool PumpsSearch { get; set; }

        /// <summary>The top bar, for the layout checks.</summary>
        public RectTransform TopBar { get; }

        /// <summary>The bottom transport bar holding Start and Stop.</summary>
        public RectTransform BottomBar => _bottomBar;

        /// <summary>Back button: asks the search to stop, then pops the screen.</summary>
        public Button BackButton { get; }

        /// <summary>Start button: disabled while a search is running.</summary>
        public Button StartButton { get; }

        /// <summary>Stop button: disabled until a search is running, and while stopping.</summary>
        public Button StopButton { get; }

        /// <summary>Finds button, in the "Saved finds:" row where PyQt named a folder.</summary>
        public Button FindsButton { get; }

        /// <summary>The one user-editable GA parameter: the score worth keeping.</summary>
        public Slider ThresholdSlider => _thresholdSlider;

        /// <summary>The threshold row's caption, for the layout checks.</summary>
        public Text ThresholdCaption => _thresholdCaption;

        /// <summary>The threshold row's live value ("3.50").</summary>
        public Text ThresholdValue => _thresholdValue;

        /// <summary>The threshold row itself, for the layout checks.</summary>
        public RectTransform ThresholdRow => _thresholdRow;

        /// <summary>The nine PyQt readout rows, in PyQt's order.</summary>
        public IReadOnlyList<ReadoutRow> Readouts => _rows;

        /// <summary>The scrolling readout column.</summary>
        public ScrollRect ReadoutScroll => _scroll;

        /// <summary>The readout column's content rect (what the scroll view moves).</summary>
        public RectTransform ReadoutContent => _content;

        /// <summary>The area the square live preview is fitted into.</summary>
        public RectTransform PreviewArea => _previewArea;

        /// <summary>The live preview of the run's best rule; disabled until there is one.</summary>
        public RawImage PreviewImage => _previewImage;

        /// <summary>The rule the preview is running, or null when there is none.</summary>
        public string PreviewRule => _previewRule;

        /// <summary>The WebGL browser-mode note; shown only in a browser player.</summary>
        public Text BrowserNote => _browserNote;

        /// <summary>The mobile battery note; shown only on a phone or tablet.</summary>
        public Text BatteryNote => _batteryNote;

        /// <summary>The finds page that covers this screen.</summary>
        public FindsView Finds => _finds;

        /// <summary>True while the finds page covers this screen.</summary>
        public bool FindsVisible => _finds != null && _finds.Visible;

        /// <summary>PyQt's "Run Number:" value, as shown.</summary>
        public string RunNumberText => _runRow.Value.text;

        /// <summary>PyQt's "Eval Number:" value, as shown.</summary>
        public string EvalNumberText => _evalRow.Value.text;

        /// <summary>PyQt's "Evals/min:" value, as shown.</summary>
        public string EvalsPerMinuteText => _perMinuteRow.Value.text;

        /// <summary>PyQt's "Current Rule:" value, as shown.</summary>
        public string CurrentRuleText => _ruleRow.Value.text;

        /// <summary>PyQt's "Current Score:" value ("score/threshold"), as shown.</summary>
        public string CurrentScoreText => _scoreRow.Value.text;

        /// <summary>PyQt's "No improve/Max allowed:" value, as shown.</summary>
        public string NoImproveText => _noImproveRow.Value.text;

        /// <summary>PyQt's "Rules found:" value, as shown.</summary>
        public string RulesFoundText => _foundRow.Value.text;

        /// <summary>PyQt's "Status:" value, as shown.</summary>
        public string StatusText => _statusRow.Value.text;

        /// <summary>The "Saved finds:" value -- how many finds this device keeps.</summary>
        public string SavedFindsText => _savedFindsRow.Value.text;

        /// <inheritdoc />
        public override void Show()
        {
            base.Show();
            if (_findsWereVisible)
            {
                // The user left this screen from the finds page (opening a find in
                // the simulator); coming back lands where they were.
                _findsWereVisible = false;
                ShowFinds();
            }
            else
            {
                _finds.Hide();
            }
            RefreshReadouts(_host != null ? _host.Latest : null, force: true);
            UpdateButtons();
        }

        /// <inheritdoc />
        public override void Hide()
        {
            bool wasVisible = Visible;
            _findsWereVisible = FindsVisible;
            _finds.Hide();
            if (wasVisible)
            {
                // Every way off this screen ends a session as far as the log is
                // concerned; the search itself is only stopped by Stop and by Back.
                // Guarded by wasVisible so building the screens at boot -- which
                // hides six of the seven -- writes nothing.
                _host?.PersistFinds();
            }
            base.Hide();
        }

        /// <inheritdoc />
        public override void Tick(float dt)
        {
            if (PumpsSearch)
            {
                _host?.Tick();
                _thumbs?.Tick();
            }
            EvolveHost.Snapshot snapshot = _host != null ? _host.Latest : null;
            if (FindsVisible)
            {
                _finds.Tick(dt);
            }
            RefreshReadouts(snapshot, force: false);
            UpdateButtons();
            UpdatePreview(snapshot != null ? snapshot.BestGenome : null);
            StepPreview(dt);
        }

        /// <inheritdoc />
        public override void ApplyLayout(bool portrait, bool phone)
        {
            _phone = phone;
            _portrait = portrait;
            float pad = phone ? 10f : 16f;
            // A landscape tablet or desktop window has room for the preview beside
            // the numbers; portrait and phone stack it above them, because a
            // 300-unit side column would leave the rule string nowhere to go.
            bool sideBySide = !_portrait && !_phone;
            if (sideBySide)
            {
                _previewArea.anchorMin = new Vector2(1f, 0f);
                _previewArea.anchorMax = Vector2.one;
                _previewArea.offsetMin = new Vector2(-(PreviewColumnWidth + pad), pad);
                _previewArea.offsetMax = new Vector2(-pad, -pad);
                UIBuilder.Stretch(
                    (RectTransform)_scroll.transform,
                    pad, pad, PreviewColumnWidth + pad * 3f, pad);
            }
            else
            {
                float strip = phone ? PreviewStripPhone : PreviewStripTablet;
                _previewArea.anchorMin = new Vector2(0f, 1f);
                _previewArea.anchorMax = Vector2.one;
                _previewArea.offsetMin = new Vector2(pad, -(strip + pad));
                _previewArea.offsetMax = new Vector2(-pad, -pad);
                UIBuilder.Stretch(
                    (RectTransform)_scroll.transform, pad, pad, pad, strip + pad * 2f);
            }

            int font = phone ? RowFontPhone : RowFontWide;
            foreach (ReadoutRow row in _rows)
            {
                row.Apply(phone, font, LabelColumnWide, ColumnGap);
            }
            ApplyThresholdLayout(phone, font);
            _browserNote.fontSize = phone ? 14 : 15;
            _batteryNote.fontSize = _browserNote.fontSize;
            _browserNoteElement.minHeight = NoteHeight;
            _browserNoteElement.preferredHeight = NoteHeight;
            _batteryNoteElement.minHeight = NoteHeight;
            _batteryNoteElement.preferredHeight = NoteHeight;
            // The bar carries only a back button on the left, so the title needs no
            // right-hand reservation beyond the edge margin.
            UIBuilder.PlaceBarTitle(_title, rightControlsWidth: 16f);
            _finds.ApplyLayout(portrait, phone);
        }

        /// <summary>
        /// Start a search with explicit parameters. The Start button calls this with
        /// the PyQt trainer's configuration and a fresh random master seed; the
        /// PlayMode suite calls it with the mini-run vector's parameters so the whole
        /// screen replays a pinned run.
        /// </summary>
        /// <param name="width">Lattice width each candidate is evaluated on.</param>
        /// <param name="height">Lattice height each candidate is evaluated on.</param>
        /// <param name="population">Genomes bred at once, per run.</param>
        /// <param name="cycles">Evaluation cycles per candidate.</param>
        /// <param name="maxSteps">Step ceiling for one evaluation cycle.</param>
        /// <param name="seed">Run 1's seed, and the seed of the per-run seed stream.</param>
        /// <param name="patience">Stalled evaluations before a run restarts.</param>
        /// <param name="lanes">Concurrent runs, or 0 to size to the machine.</param>
        /// <param name="chunkEvals">Evaluations between stop points.</param>
        /// <param name="tournamentRounds">Tournament size for selection and eviction.</param>
        public void StartRun(
            int width,
            int height,
            int population,
            int cycles,
            int maxSteps,
            ulong seed,
            int patience = EvolveHost.DefaultPatience,
            int lanes = 0,
            int chunkEvals = EvolveHost.DefaultChunkEvals,
            int tournamentRounds = EvolveHost.TournamentRounds)
        {
            if (_host == null || _host.Running)
            {
                return;
            }
            _host.Threshold = _threshold;
            _host.Start(
                width,
                height,
                population,
                cycles,
                maxSteps,
                seed,
                patience,
                lanes,
                chunkEvals,
                EvolveHost.CrossoverRate,
                tournamentRounds);
            UpdatePreview(null);
            ResetShownReadouts();
            RefreshReadouts(_host.Latest, force: true);
            UpdateButtons();
        }

        /// <summary>
        /// Ask the search to stop at the next evaluation boundary. The readouts hold
        /// the last run's numbers and the finds log survives, exactly like PyQt's
        /// Stop button.
        /// </summary>
        public void Stop()
        {
            if (_host == null || !_host.Running)
            {
                return;
            }
            _host.RequestStop();
            // PyQt wrote "Stopping..." from the click handler, before the worker
            // had noticed; both buttons go dead in the same breath.
            RefreshReadouts(_host.Latest, force: true);
            UpdateButtons();
        }

        /// <summary>Build the finds page from the log and bring it up. The search keeps running.</summary>
        public void ShowFinds()
        {
            _finds.Show();
            RefreshReadouts(_host != null ? _host.Latest : null, force: true);
        }

        /// <summary>Take the finds page down and return to the readouts.</summary>
        public void HideFinds()
        {
            _finds.Hide();
            RefreshReadouts(_host != null ? _host.Latest : null, force: true);
        }

        /// <summary>
        /// Write <paramref name="snapshot"/> into the nine rows, in PyQt's formats.
        /// Public because the PlayMode suite pins those formats against a
        /// hand-built snapshot rather than against whatever a live search happens to
        /// be doing.
        /// </summary>
        /// <param name="snapshot">
        /// The search state to show, or null before anything has published -- which
        /// blanks the value column the way PyQt's <c>report</c> did with no best
        /// genome.
        /// </param>
        public void RenderReadouts(EvolveHost.Snapshot snapshot)
        {
            EvolveHost.EvolveStatus status = snapshot != null
                ? snapshot.Status
                : (_host != null ? _host.Status : EvolveHost.EvolveStatus.Idle);
            int kept = _host != null ? _host.DiscoveryCount : 0;
            _savedFindsRow.Value.text = FormatCount(kept);
            _statusRow.Value.text = StatusLine(status, snapshot);
            if (snapshot == null)
            {
                _runRow.Value.text = string.Empty;
                _evalRow.Value.text = string.Empty;
                _perMinuteRow.Value.text = string.Empty;
                _ruleRow.Value.text = string.Empty;
                _scoreRow.Value.text = string.Empty;
                _noImproveRow.Value.text = string.Empty;
                _foundRow.Value.text = FormatCount(_host != null ? _host.TotalFound : 0);
                return;
            }
            // PyQt reported runCount, the restart odometer; with concurrent lanes
            // that is TotalRunsStarted, the only number that still only counts up.
            _runRow.Value.text = FormatCount(snapshot.TotalRunsStarted);
            _evalRow.Value.text = FormatCount(snapshot.Evals);
            _perMinuteRow.Value.text = snapshot.EvalsPerMinute.ToString("N2", CultureInfo.InvariantCulture);
            _noImproveRow.Value.text = string.Format(
                CultureInfo.InvariantCulture,
                "{0}/{1}",
                FormatCount(snapshot.NoImprovement),
                FormatCount(snapshot.Patience));
            _foundRow.Value.text = FormatCount(snapshot.TotalFound);
            if (string.IsNullOrEmpty(snapshot.BestGenome))
            {
                // PyQt blanked both cells until the run had a best genome.
                _ruleRow.Value.text = string.Empty;
                _scoreRow.Value.text = string.Empty;
                return;
            }
            _ruleRow.Value.text = snapshot.BestGenome;
            _scoreRow.Value.text = string.Format(
                CultureInfo.InvariantCulture,
                "{0}/{1}",
                Format2(snapshot.BestScore),
                Format2(_threshold));
        }

        // ---- internals ----------------------------------------------------------

        /// <summary>Re-render only when one of the numbers the rows show has moved.</summary>
        private void RefreshReadouts(EvolveHost.Snapshot snapshot, bool force)
        {
            int evals = snapshot != null ? snapshot.Evals : int.MinValue;
            int perMinute = snapshot != null ? snapshot.EvalsPerMinute : int.MinValue;
            int found = snapshot != null
                ? snapshot.TotalFound
                : (_host != null ? _host.TotalFound : 0);
            int kept = _host != null ? _host.DiscoveryCount : 0;
            string best = snapshot != null ? snapshot.BestGenome : null;
            EvolveHost.EvolveStatus status = snapshot != null
                ? snapshot.Status
                : (_host != null ? _host.Status : EvolveHost.EvolveStatus.Idle);
            if (!force
                && evals == _shownEvals
                && perMinute == _shownPerMinute
                && found == _shownFound
                && kept == _shownKept
                && status == _shownStatus
                && string.Equals(best, _shownBest, StringComparison.Ordinal))
            {
                return;
            }
            _shownEvals = evals;
            _shownPerMinute = perMinute;
            _shownFound = found;
            _shownKept = kept;
            _shownStatus = status;
            _shownBest = best;
            RenderReadouts(snapshot);
        }

        private void ResetShownReadouts()
        {
            _shownEvals = int.MinValue;
            _shownPerMinute = int.MinValue;
            _shownFound = int.MinValue;
            _shownKept = int.MinValue;
            _shownStatus = (EvolveHost.EvolveStatus)(-1);
            _shownBest = "\0";
        }

        /// <summary>PyQt's Start / Stop enable rules, which are a three-state machine.</summary>
        private void UpdateButtons()
        {
            bool running = _host != null && _host.Running;
            bool stopping = running && _host.Status == EvolveHost.EvolveStatus.Stopping;
            StartButton.interactable = !running;
            StopButton.interactable = running && !stopping;
        }

        private void OnStart()
        {
            if (_host == null || _host.Running)
            {
                return;
            }
            // A fresh master seed every press, like the PyQt trainer: reproducing a
            // whole session is not something the user asked for (the finds gallery
            // keeps the rules themselves), while run N reproducing from its own seed
            // is the host's guarantee either way.
            var seed = (ulong)(uint)UnityEngine.Random.Range(1, int.MaxValue);
            StartRun(
                EvolveHost.EvolveGridSize,
                EvolveHost.EvolveGridSize,
                EvolveHost.DefaultPopulation,
                EvolveHost.DefaultEvalCycles,
                EvolveHost.DefaultMaxSteps,
                seed,
                EvolveHost.DefaultPatience);
        }

        private void OnStop() => Stop();

        /// <summary>
        /// Back ends the session the way closing PyQt's tab did: the search is asked
        /// to stop and the finds log is banked first. Opening a find in the
        /// simulator is the one exit that leaves the search breeding.
        /// </summary>
        private void OnBack()
        {
            _host?.RequestStop();
            UpdateButtons();
            Nav?.Back();
        }

        private void OnThresholdChanged(float value)
        {
            _threshold = value;
            _thresholdValue.text = Format2(_threshold);
            if (_host != null)
            {
                // Raising the bar narrows what is admitted next; it never retracts a
                // find already in the log (EvolveHost.Threshold documents why).
                _host.Threshold = _threshold;
            }
            RefreshReadouts(_host != null ? _host.Latest : null, force: true);
        }

        /// <summary>Load the rule the run is leading with into the preview lattice.</summary>
        private void UpdatePreview(string rule)
        {
            if (string.Equals(rule, _previewRule, StringComparison.Ordinal))
            {
                return;
            }
            _previewRule = rule;
            if (string.IsNullOrEmpty(rule))
            {
                _previewSim = null;
                _previewHost.Unload();
                _previewImage.enabled = false;
                return;
            }
            _previewSim = new MergeLife(rule, PreviewCells, PreviewCells);
            _previewSim.SeedSoup(PreviewSeed);
            _previewHost.Load(_previewSim);
            _previewHost.StepsPerSecond = PreviewStepsPerSecond;
            _previewBlitter.Blit(_previewSim); // show generation 0 on this frame
            _previewBlitter.Attach(_previewImage);
            _previewHost.Dirty = false;
            _previewImage.enabled = true;
        }

        private void StepPreview(float dt)
        {
            if (_previewSim == null)
            {
                return;
            }
            _previewHost.Tick(dt);
            if (_previewHost.Dirty)
            {
                _previewBlitter.Blit(_previewSim);
                _previewHost.Dirty = false;
            }
        }

        /// <summary>PyQt's status strings, chosen by the host's state machine.</summary>
        private static string StatusLine(EvolveHost.EvolveStatus status, EvolveHost.Snapshot snapshot)
        {
            switch (status)
            {
                case EvolveHost.EvolveStatus.Seeding:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        AppStrings.EvolveStatusSeedingFormat,
                        snapshot != null ? snapshot.SeedingProgress.Done : 0,
                        snapshot != null ? snapshot.SeedingProgress.Total : 0);
                case EvolveHost.EvolveStatus.Running:
                    // The host restarts a converged run itself, so this is the
                    // moment PyQt announced before its Worker looped.
                    if (snapshot != null
                        && snapshot.Patience > 0
                        && snapshot.NoImprovement >= snapshot.Patience)
                    {
                        return string.Format(
                            CultureInfo.InvariantCulture,
                            AppStrings.EvolveStatusNoImprovementFormat,
                            FormatCount(snapshot.Patience));
                    }
                    return AppStrings.EvolveStatusRunning;
                case EvolveHost.EvolveStatus.Stopping:
                    return AppStrings.EvolveStatusStopping;
                case EvolveHost.EvolveStatus.Suspended:
                    return AppStrings.EvolveStatusPausedApp;
                case EvolveHost.EvolveStatus.BrowserPaused:
                    return AppStrings.EvolveStatusPausedBrowser;
                case EvolveHost.EvolveStatus.Stopped:
                    return AppStrings.EvolveStatusStopped;
                default:
                    return string.Empty; // Idle: PyQt started with an empty field
            }
        }

        /// <summary>PyQt's <c>f"{n:,}"</c>.</summary>
        private static string FormatCount(int value) =>
            value.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>PyQt's <c>f"{x:,.2f}"</c>.</summary>
        private static string Format2(double value) =>
            value.ToString("N2", CultureInfo.InvariantCulture);

        private ReadoutRow AddRow(string label, float reserveRight = 4f)
        {
            RectTransform row = UIBuilder.CreatePanel(_content, label);
            var element = row.gameObject.AddComponent<LayoutElement>();
            Text caption = UIBuilder.CreateText(
                row, "Caption", label, RowFontWide, UIBuilder.TextDimColor);
            Text value = UIBuilder.CreateText(
                row, "Value", string.Empty, RowFontWide, UIBuilder.TextColor);
            // A full rule is 39 characters; on a phone column it has to wrap and
            // shrink rather than run out over the Finds button.
            Confine(caption, RowFontWide);
            Confine(value, RowFontWide);
            var readout = new ReadoutRow(label, row, element, caption, value, reserveRight);
            _rows.Add(readout);
            return readout;
        }

        private Text AddNote(string name, string text, out LayoutElement element)
        {
            Text note = UIBuilder.CreateText(
                _content, name, text, 15, UIBuilder.TextDimColor, TextAnchor.UpperLeft);
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            note.verticalOverflow = VerticalWrapMode.Truncate;
            element = note.gameObject.AddComponent<LayoutElement>();
            element.minHeight = NoteHeight;
            element.preferredHeight = NoteHeight;
            return note;
        }

        private void ApplyThresholdLayout(bool phone, int font)
        {
            _thresholdCaption.fontSize = font;
            _thresholdValue.fontSize = font;
            var caption = (RectTransform)_thresholdCaption.transform;
            caption.anchorMin = new Vector2(0f, 0.5f);
            caption.anchorMax = new Vector2(phone ? 0.55f : 0.6f, 1f);
            caption.offsetMin = Vector2.zero;
            caption.offsetMax = new Vector2(-ColumnGap, 0f);
            var value = (RectTransform)_thresholdValue.transform;
            value.anchorMin = new Vector2(phone ? 0.55f : 0.6f, 0.5f);
            value.anchorMax = Vector2.one;
            value.offsetMin = Vector2.zero;
            value.offsetMax = new Vector2(-4f, 0f);
            var slider = (RectTransform)_thresholdSlider.transform;
            slider.anchorMin = Vector2.zero;
            slider.anchorMax = new Vector2(1f, 0.5f);
            slider.offsetMin = new Vector2(0f, 6f);
            slider.offsetMax = new Vector2(-4f, -4f);
        }

        /// <summary>Wrap, truncate, and shrink so a label can never escape its rect.</summary>
        private static void Confine(Text text, int fontSize)
        {
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = fontSize;
            text.resizeTextMinSize = 11;
        }

        /// <summary>
        /// One label/value line of the PyQt readout grid. Two columns on a tablet or
        /// a desktop window; on a phone the value moves under its label, because a
        /// 39-character rule does not fit beside a 24-character label in 420 units.
        /// </summary>
        public sealed class ReadoutRow
        {
            internal ReadoutRow(
                string label,
                RectTransform row,
                LayoutElement element,
                Text caption,
                Text value,
                float reserveRight)
            {
                Label = label;
                Row = row;
                Element = element;
                Caption = caption;
                Value = value;
                ReserveRight = reserveRight;
            }

            /// <summary>The PyQt label, including its colon.</summary>
            public string Label { get; }

            /// <summary>The row's rect, for the layout checks.</summary>
            public RectTransform Row { get; }

            /// <summary>The label on the left (or above, on a phone).</summary>
            public Text Caption { get; }

            /// <summary>The value PyQt showed in a read-only line edit.</summary>
            public Text Value { get; }

            /// <summary>Units kept free on the right for a control living in this row.</summary>
            internal float ReserveRight { get; }

            private LayoutElement Element { get; }

            /// <summary>Height of a two-column row.</summary>
            private const float WideHeight = 34f;

            /// <summary>Height of a stacked (phone) row: two lines instead of one.</summary>
            private const float StackedHeight = 52f;

            internal void Apply(bool stacked, int fontSize, float labelColumn, float gap)
            {
                Element.minHeight = stacked ? StackedHeight : WideHeight;
                Element.preferredHeight = Element.minHeight;
                Caption.fontSize = fontSize;
                Value.fontSize = fontSize;
                Caption.resizeTextMaxSize = fontSize;
                Value.resizeTextMaxSize = fontSize;
                var caption = (RectTransform)Caption.transform;
                var value = (RectTransform)Value.transform;
                if (stacked)
                {
                    caption.anchorMin = new Vector2(0f, 0.5f);
                    caption.anchorMax = Vector2.one;
                    caption.offsetMin = Vector2.zero;
                    caption.offsetMax = new Vector2(-ReserveRight, 0f);
                    Caption.alignment = TextAnchor.LowerLeft;
                    value.anchorMin = Vector2.zero;
                    value.anchorMax = new Vector2(1f, 0.5f);
                    value.offsetMin = Vector2.zero;
                    value.offsetMax = new Vector2(-ReserveRight, 0f);
                    Value.alignment = TextAnchor.UpperLeft;
                }
                else
                {
                    caption.anchorMin = Vector2.zero;
                    caption.anchorMax = new Vector2(0f, 1f);
                    caption.offsetMin = Vector2.zero;
                    caption.offsetMax = new Vector2(labelColumn, 0f);
                    Caption.alignment = TextAnchor.MiddleLeft;
                    value.anchorMin = Vector2.zero;
                    value.anchorMax = Vector2.one;
                    value.offsetMin = new Vector2(labelColumn + gap, 0f);
                    value.offsetMax = new Vector2(-ReserveRight, 0f);
                    Value.alignment = TextAnchor.MiddleLeft;
                }
            }
        }
    }
}
