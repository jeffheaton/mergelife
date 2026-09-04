using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace HeatonCAApp
{
    /// <summary>
    /// The finds gallery: what PyQt's "Output Directory" of <c>&lt;rule&gt;.png</c>
    /// files became. A full-screen page over the Evolve screen, one card per
    /// <see cref="EvolveHost.Discovery"/> in best-score order, each with the rule's
    /// derived thumbnail, the rule itself, its score and run, and the three things
    /// a user wants to do with a treasure: watch it (Open in Simulator, which
    /// deliberately leaves the search breeding), keep it (Save PNG, written as
    /// <c>&lt;rule&gt;.png</c> at 4x -- PyQt's file name), or throw it away
    /// (Delete, confirmed and tombstoned so the live population cannot re-log it).
    ///
    /// The page is a snapshot of the log, built on show and torn down on hide:
    /// sixty cards each holding a rendered thumbnail is not something to rebuild
    /// while a search lands finds underneath it. Thumbnails arrive at most one per
    /// frame from the shared <see cref="FindsThumbnailer"/>, so the page fills in
    /// over a second instead of stalling a frame building all of them.
    ///
    /// Not an <see cref="IAppView"/>: it is a page inside the Evolve screen, not a
    /// destination on the back stack, which is why <c>ScreenId</c> has no entry for
    /// it and its Back button returns to the readouts rather than popping the app's
    /// navigation.
    /// </summary>
    public sealed class FindsView
    {
        /// <summary>Magnification of a saved find, over the 100x100 thumbnail lattice.</summary>
        public const int SavePngScale = 4;

        private const float TopBarHeight = 64f;
        private const float ClearButtonWidth = 140f;

        private readonly INavigator _nav;
        private readonly EvolveHost _host;
        private readonly FindsThumbnailer _thumbs;
        private readonly GameObject _root;
        private readonly RectTransform _grid;
        private readonly GridLayoutGroup _gridLayout;
        private readonly GameObject _empty;
        private readonly Text _title;
        private readonly List<FindCard> _cards = new List<FindCard>();

        private bool _phone;
        private bool _portrait;

        /// <summary>
        /// Build the page into <paramref name="parent"/> (the Evolve screen's root,
        /// so it covers the readouts and hides with them).
        /// </summary>
        /// <param name="parent">The Evolve screen's root rect.</param>
        /// <param name="nav">The navigator, for Open in Simulator and the confirms.</param>
        /// <param name="host">The search whose finds log this page shows.</param>
        /// <param name="thumbs">The shared thumbnail cache.</param>
        /// <param name="onBack">What the Back button does -- return to the readouts.</param>
        public FindsView(
            RectTransform parent,
            INavigator nav,
            EvolveHost host,
            FindsThumbnailer thumbs,
            Action onBack)
        {
            _nav = nav;
            _host = host;
            _thumbs = thumbs;

            RectTransform page = UIBuilder.CreatePanel(parent, "FindsPage", UIBuilder.BackgroundColor);
            UIBuilder.Stretch(page);
            _root = page.gameObject;

            RectTransform bar = UIBuilder.CreatePanel(page, "TopBar", UIBuilder.PanelColor);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = Vector2.one;
            bar.offsetMin = new Vector2(0f, -TopBarHeight);
            bar.offsetMax = Vector2.zero;

            BackButton = UIBuilder.CreateButton(
                bar, "Back", AppStrings.Back, 20, () => onBack?.Invoke());
            var backRt = (RectTransform)BackButton.transform;
            backRt.anchorMin = new Vector2(0f, 0.5f);
            backRt.anchorMax = new Vector2(0f, 0.5f);
            backRt.pivot = new Vector2(0f, 0.5f);
            backRt.sizeDelta = new Vector2(140f, 42f);
            backRt.anchoredPosition = new Vector2(16f, 0f);

            ClearAllButton = UIBuilder.CreateButton(
                bar, "ClearAll", AppStrings.FindsClearAll, 18, OnClearAll);
            var clearRt = (RectTransform)ClearAllButton.transform;
            clearRt.anchorMin = new Vector2(1f, 0.5f);
            clearRt.anchorMax = new Vector2(1f, 0.5f);
            clearRt.pivot = new Vector2(1f, 0.5f);
            clearRt.sizeDelta = new Vector2(ClearButtonWidth, 40f);
            clearRt.anchoredPosition = new Vector2(-16f, 0f);

            _title = UIBuilder.CreateText(
                bar, "Title", AppStrings.FindsTitle, 26, UIBuilder.TextColor, TextAnchor.MiddleCenter);
            _title.fontStyle = FontStyle.Bold;
            UIBuilder.PlaceBarTitle(_title, rightControlsWidth: ClearButtonWidth + 32f);

            ScrollRect scroll = UIBuilder.CreateScrollView(page, "Scroll", out RectTransform content);
            UIBuilder.Stretch((RectTransform)scroll.transform, 20f, 10f, 20f, TopBarHeight + 8f);
            Scroll = scroll;
            _gridLayout = content.gameObject.AddComponent<GridLayoutGroup>();
            _gridLayout.spacing = new Vector2(14f, 14f);
            _gridLayout.padding = new RectOffset(0, 0, 8, 8);
            _gridLayout.childAlignment = TextAnchor.UpperCenter;
            _gridLayout.constraint = GridLayoutGroup.Constraint.Flexible;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            _grid = content;

            Text empty = UIBuilder.CreateText(
                page, "Empty", AppStrings.FindsEmpty, 20, UIBuilder.TextDimColor,
                TextAnchor.MiddleCenter);
            empty.horizontalOverflow = HorizontalWrapMode.Wrap;
            empty.verticalOverflow = VerticalWrapMode.Truncate;
            UIBuilder.Stretch((RectTransform)empty.transform, 40f, 40f, 40f, TopBarHeight + 40f);
            _empty = empty.gameObject;
            EmptyText = empty;

            ApplyLayout(portrait: false, phone: UIBuilder.IsPhoneLayout);
            _root.SetActive(false);
        }

        /// <summary>Back button: returns to the Evolve readouts, not to the back stack.</summary>
        public Button BackButton { get; }

        /// <summary>Clear all button: empties the whole log, behind a confirmation.</summary>
        public Button ClearAllButton { get; }

        /// <summary>The scrolling card grid.</summary>
        public ScrollRect Scroll { get; }

        /// <summary>The empty-state line, shown instead of the grid when there is nothing.</summary>
        public Text EmptyText { get; }

        /// <summary>The page's own root object.</summary>
        public GameObject Root => _root;

        /// <summary>True while the page covers the Evolve screen.</summary>
        public bool Visible => _root != null && _root.activeSelf;

        /// <summary>The cards as built, best score first.</summary>
        public IReadOnlyList<FindCard> Cards => _cards;

        /// <summary>How many cards the page is showing.</summary>
        public int CardCount => _cards.Count;

        /// <summary>True while the empty-state line is up instead of cards.</summary>
        public bool EmptyVisible => _empty != null && _empty.activeSelf;

        /// <summary>Build the page from the log and show it.</summary>
        public void Show()
        {
            Rebuild();
            _root.SetActive(true);
        }

        /// <summary>Hide the page and drop its cards (and their thumbnails' references).</summary>
        public void Hide()
        {
            if (_root == null || !_root.activeSelf)
            {
                return;
            }
            ClearCards();
            _root.SetActive(false);
        }

        /// <summary>
        /// Rebuild the cards from the current log. Called on every show, and after
        /// a delete or a clear, so the page always agrees with the log it came from.
        /// </summary>
        public void Rebuild()
        {
            ClearCards();
            List<EvolveHost.Discovery> finds = _host != null
                ? _host.Discoveries
                : new List<EvolveHost.Discovery>();
            float cardWidth = _phone ? 360f : (_portrait ? 420f : 470f);
            float cardHeight = _phone ? 158f : (_portrait ? 172f : 180f);
            float previewSize = _phone ? 106f : (_portrait ? 126f : 140f);
            int actionFont = _phone ? 12 : 14;
            _gridLayout.cellSize = new Vector2(cardWidth, cardHeight);
            foreach (EvolveHost.Discovery find in finds)
            {
                _cards.Add(BuildCard(find, previewSize, actionFont));
            }
            _empty.SetActive(_cards.Count == 0);
        }

        /// <summary>
        /// Collect whatever thumbnails were rendered since the last frame. The
        /// render itself is rationed elsewhere (one rule per frame), so a page of
        /// sixty cards costs sixty quiet frames rather than one frozen one.
        /// </summary>
        /// <param name="dt">Unscaled seconds since the previous frame (unused today).</param>
        public void Tick(float dt)
        {
            if (_thumbs == null)
            {
                return;
            }
            foreach (FindCard card in _cards)
            {
                if (card.Preview == null || card.Preview.texture != null)
                {
                    continue;
                }
                Texture2D thumbnail = _thumbs.Get(card.Rule);
                if (thumbnail == null)
                {
                    continue;
                }
                card.Preview.texture = thumbnail;
                card.Preview.color = Color.white;
            }
        }

        /// <summary>
        /// Re-lay for a new orientation or form factor. Card size is baked in at
        /// build time (the preview square's size is a constructor argument), so a
        /// visible page is rebuilt rather than nudged.
        /// </summary>
        /// <param name="portrait">True when the screen is taller than it is wide.</param>
        /// <param name="phone">True for phone-sized screens.</param>
        public void ApplyLayout(bool portrait, bool phone)
        {
            bool changed = phone != _phone || portrait != _portrait;
            _phone = phone;
            _portrait = portrait;
            _title.fontSize = phone ? 22 : 26;
            UIBuilder.PlaceBarTitle(
                _title, rightControlsWidth: (phone ? 120f : ClearButtonWidth) + 32f);
            var clearRt = (RectTransform)ClearAllButton.transform;
            clearRt.sizeDelta = new Vector2(phone ? 120f : ClearButtonWidth, 40f);
            if (changed && Visible)
            {
                Rebuild();
            }
        }

        // ---- internals ----------------------------------------------------------

        private FindCard BuildCard(EvolveHost.Discovery find, float previewSize, int actionFont)
        {
            var card = new FindCard
            {
                Rule = find.Rule,
                Score = find.Score,
                Run = find.Run,
                FoundAtEval = find.FoundAtEval,
            };
            // The rule is the card's headline because on a find card the rule IS the
            // payload; the score and the run that bred it are the subtitle.
            string caption = string.Format(
                CultureInfo.InvariantCulture,
                AppStrings.FindsScoreRunFormat,
                find.Score.ToString("N2", CultureInfo.InvariantCulture),
                find.Run.ToString("N0", CultureInfo.InvariantCulture));
            RawImage preview = UIBuilder.CreatePickerCard(
                _grid, find.Rule, caption, 17, () => OpenFind(card), previewSize);
            // A RawImage with no texture paints solid white; hold the control color
            // until the thumbnailer has this rule ready.
            preview.color = new Color32(0x0C, 0x10, 0x14, 0xFF);
            card.Preview = preview;
            Transform cardTransform = preview.transform.parent;
            card.Card = cardTransform.GetComponent<Button>();
            card.Title = Find(cardTransform, "Name");
            card.Caption = Find(cardTransform, "Blurb");
            if (card.Caption != null)
            {
                // Lift the subtitle clear of the action row added below it.
                var blurbRt = (RectTransform)card.Caption.transform;
                blurbRt.offsetMin = new Vector2(previewSize + 28f, 46f);
            }

            RectTransform actions = UIBuilder.CreatePanel(cardTransform, "Actions");
            actions.anchorMin = new Vector2(0f, 0f);
            actions.anchorMax = new Vector2(1f, 0f);
            actions.offsetMin = new Vector2(previewSize + 28f, 6f);
            actions.offsetMax = new Vector2(-12f, 40f);

            card.OpenButton = ActionButton(
                actions, "Open", AppStrings.FindsOpenInSimulator, actionFont,
                0f, 0.5f, () => OpenFind(card));
            card.SaveButton = ActionButton(
                actions, "SavePng", AppStrings.FindsSavePng, actionFont,
                0.5f, 0.78f, () => SaveFind(card));
            card.DeleteButton = ActionButton(
                actions, "Delete", AppStrings.FindsDelete, actionFont,
                0.78f, 1f, () => DeleteFind(card));
            return card;
        }

        private static Button ActionButton(
            RectTransform row, string name, string label, int fontSize,
            float min, float max, UnityEngine.Events.UnityAction onClick)
        {
            Button button = UIBuilder.CreateButton(row, name, label, fontSize, onClick);
            var rt = (RectTransform)button.transform;
            rt.anchorMin = new Vector2(min, 0f);
            rt.anchorMax = new Vector2(max, 1f);
            rt.offsetMin = new Vector2(min > 0f ? 4f : 0f, 0f);
            rt.offsetMax = new Vector2(max < 1f ? -4f : 0f, 0f);
            Text text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                // "Open in Simulator" is three words in a half-card button: shrink
                // rather than truncate it.
                text.resizeTextForBestFit = true;
                text.resizeTextMaxSize = fontSize;
                text.resizeTextMinSize = 9;
            }
            return button;
        }

        private static Text Find(Transform parent, string child)
        {
            Transform found = parent.Find(child);
            return found != null ? found.GetComponent<Text>() : null;
        }

        /// <summary>
        /// Open a find in the simulator, auto-started. The search is deliberately
        /// left running: browsing a find must not cost the run that bred it (Home
        /// carries the "Evolving..." chip while it keeps going).
        /// </summary>
        private void OpenFind(FindCard card) => _nav?.ShowSimulator(card.Rule, true);

        /// <summary>Export one find as PyQt named it: <c>&lt;rule&gt;.png</c>, at 4x.</summary>
        private void SaveFind(FindCard card)
        {
            if (_thumbs == null)
            {
                return;
            }
            byte[] png = _thumbs.EncodePng(card.Rule, SavePngScale);
            string reported = null;
            bool saved = PngExporter.Save(png, card.Rule + ".png", text => reported = text);
            _nav?.Status(saved
                ? string.Format(
                    CultureInfo.InvariantCulture, AppStrings.EvolveSavedPngFormat, card.Rule)
                : string.Format(
                    CultureInfo.InvariantCulture,
                    AppStrings.EvolveSaveFailedFormat,
                    reported ?? string.Empty));
        }

        private async void DeleteFind(FindCard card)
        {
            if (_nav != null)
            {
                bool confirmed = await _nav.ShowConfirmAsync(
                    AppStrings.FindsDeleteConfirmTitle,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        AppStrings.FindsDeleteConfirmBodyFormat,
                        card.Rule));
                if (!confirmed)
                {
                    return;
                }
            }
            if (_host == null || _root == null)
            {
                return; // the screen went away while the confirmation was up
            }
            _host.RemoveDiscovery(card.Rule);
            // Deletes are rare and final: bank the tombstone now rather than waiting
            // for the autosave, which only runs while a search is in flight.
            _host.PersistFinds();
            Rebuild();
        }

        private async void OnClearAll()
        {
            if (_nav != null)
            {
                bool confirmed = await _nav.ShowConfirmAsync(
                    AppStrings.FindsClearAllConfirmTitle,
                    AppStrings.FindsClearAllConfirmBody);
                if (!confirmed)
                {
                    return;
                }
            }
            if (_host == null || _root == null)
            {
                return;
            }
            _host.ClearDiscoveries();
            _host.PersistFinds();
            Rebuild();
        }

        private void ClearCards()
        {
            _cards.Clear();
            if (_grid == null)
            {
                return;
            }
            for (int i = _grid.childCount - 1; i >= 0; i--)
            {
                // Detach before destroying: Destroy is deferred to the end of the
                // frame, so a card rebuilt in the same frame (a delete) would
                // otherwise lay out beside the cards it replaced.
                Transform child = _grid.GetChild(i);
                child.SetParent(null, false);
                DestroyObject(child.gameObject);
            }
        }

        /// <summary>
        /// Destroy from either mode: the EditMode contract tests may build a view,
        /// and <c>Object.Destroy</c> outside play mode is an error rather than a
        /// no-op.
        /// </summary>
        private static void DestroyObject(GameObject go)
        {
            if (go == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(go);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>One find, as the page shows it.</summary>
        public sealed class FindCard
        {
            /// <summary>The rule this card stands for, canonical dashed hex.</summary>
            public string Rule { get; internal set; }

            /// <summary>Its objective score when it was logged.</summary>
            public double Score { get; internal set; }

            /// <summary>The run that bred it.</summary>
            public int Run { get; internal set; }

            /// <summary>Evaluation number within that run.</summary>
            public int FoundAtEval { get; internal set; }

            /// <summary>The card itself; tapping it opens the find, like the Open button.</summary>
            public Button Card { get; internal set; }

            /// <summary>The derived thumbnail, or a dark square until it is rendered.</summary>
            public RawImage Preview { get; internal set; }

            /// <summary>The rule as the card's headline.</summary>
            public Text Title { get; internal set; }

            /// <summary>The "score {0} - run {1}" subtitle.</summary>
            public Text Caption { get; internal set; }

            /// <summary>Open in Simulator; does not stop the search.</summary>
            public Button OpenButton { get; internal set; }

            /// <summary>Save PNG, as &lt;rule&gt;.png at 4x.</summary>
            public Button SaveButton { get; internal set; }

            /// <summary>Delete, behind a confirmation.</summary>
            public Button DeleteButton { get; internal set; }
        }
    }
}
