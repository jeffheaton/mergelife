using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace HeatonCAApp
{
    /// <summary>
    /// The Settings screen: PyQt's three preferences (cell size, animation
    /// speed, and the FPS/steps overlay) with PyQt's Save and Cancel buttons and
    /// one addition, Restore Defaults.
    ///
    /// The PyQt tab's editing model is preserved exactly: moving a control
    /// changes nothing outside this screen. Every edit lands in the pending
    /// fields (<see cref="PendingCellSize"/>, <see cref="PendingStepsPerSecond"/>,
    /// <see cref="PendingShowOverlay"/>); only <see cref="Save"/> writes
    /// <see cref="AppSettings"/>, and only Restore Defaults + Save persists the
    /// shipped defaults. Cancel (and the bar's Back button, which is the same
    /// action) reloads the stored values and leaves.
    ///
    /// Saving applies live, which is what <see cref="AppStrings.SettingsNote"/>
    /// promises: each <see cref="AppSettings"/> setter raises
    /// <c>AppSettings.Changed</c>, which is how the open simulator re-cuts its
    /// lattice for a new cell size and shows or hides its overlay, and the new
    /// speed is pushed straight into the shared
    /// <see cref="SimulationHost.StepsPerSecond"/> so playback retimes in the
    /// same frame rather than at the next visit.
    /// </summary>
    public sealed class SettingsView : AppViewBase
    {
        /// <summary>
        /// Label of the "one less" cell-size stepper. A typographic symbol rather
        /// than a word, so it lives here rather than in
        /// <see cref="AppStrings"/> — nothing about it is translatable and it is
        /// pinned by <c>SettingsSlidersClampToTheirRanges</c>.
        /// </summary>
        public const string DecrementGlyph = "-";

        /// <summary>Label of the "one more" cell-size stepper; see <see cref="DecrementGlyph"/>.</summary>
        public const string IncrementGlyph = "+";

        /// <summary>Height of the top bar, in canvas units.</summary>
        private const float BarHeight = 64f;

        /// <summary>Left and right margin of the settings list.</summary>
        private const float PadX = 16f;

        /// <summary>Vertical inset of a row's controls inside its own rect.</summary>
        private const float RowPadY = 6f;

        /// <summary>Width of one cell-size stepper button.</summary>
        private const float StepperWidthDesktop = 56f;

        /// <summary>Width of one cell-size stepper button on a phone, where fingers are the pointer.</summary>
        private const float StepperWidthPhone = 52f;

        /// <summary>Width of the right-hand numeric readout column.</summary>
        private const float ValueWidthDesktop = 88f;

        /// <summary>Width of that column on a phone, where it shares the label's line.</summary>
        private const float ValueWidthPhone = 62f;

        private readonly RectTransform _root;
        private readonly RectTransform _listContent;
        private readonly SettingRow _cellRow;
        private readonly SettingRow _speedRow;
        private readonly SettingRow _overlayRow;
        private readonly RectTransform _buttonsRow;
        private readonly LayoutElement _buttonsLayout;
        private readonly Text _saveLabel;
        private readonly Text _cancelLabel;
        private readonly Text _restoreLabel;
        private readonly Text _backLabel;
        private readonly RectTransform _overlayBox;

        private int _pendingCellSize;
        private int _pendingStepsPerSecond;
        private bool _pendingShowOverlay;

        /// <summary>
        /// Builds the screen into <paramref name="parent"/>. The controls start on
        /// the values <see cref="AppSettings"/> holds right now; <see cref="Show"/>
        /// reloads them on every later visit.
        /// </summary>
        /// <param name="parent">The safe-area rect (or a test's stand-in page).</param>
        /// <param name="nav">The navigator Save, Cancel, and Back call.</param>
        /// <param name="services">
        /// The shared services; <see cref="AppServices.Host"/> is the one this
        /// screen touches, and only to retime playback on Save.
        /// </param>
        public SettingsView(RectTransform parent, INavigator nav, AppServices services)
            : base(parent, nav, services)
        {
            _root = UIBuilder.CreatePanel(parent, "SettingsView");
            UIBuilder.Stretch(_root);
            Root = _root.gameObject;

            RectTransform bar = UIBuilder.CreatePanel(_root, "TopBar", UIBuilder.PanelColor);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(0f, -BarHeight);
            bar.offsetMax = Vector2.zero;

            BackButton = UIBuilder.CreateButton(bar, "Back", AppStrings.Back, 20, Cancel);
            var backRect = (RectTransform)BackButton.transform;
            backRect.anchorMin = new Vector2(0f, 0.5f);
            backRect.anchorMax = new Vector2(0f, 0.5f);
            backRect.pivot = new Vector2(0f, 0.5f);
            backRect.sizeDelta = new Vector2(140f, 44f);
            backRect.anchoredPosition = new Vector2(12f, 0f);
            _backLabel = BackButton.GetComponentInChildren<Text>();

            TitleText = UIBuilder.CreateText(
                bar, "Title", AppStrings.SettingsTitle, 26, UIBuilder.TextColor,
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
            layout.padding = new RectOffset((int)PadX, (int)PadX, 14, 18);
            _listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            _pendingCellSize = AppSettings.CellSize;
            _pendingStepsPerSecond = AppSettings.StepsPerSecond;
            _pendingShowOverlay = AppSettings.ShowOverlay;

            _cellRow = new SettingRow(_listContent, "CellSizeRow", AppStrings.SettingsCellSizeLabel, true);
            DecrementCellSizeButton = UIBuilder.CreateButton(
                _cellRow.Controls, "Decrement", DecrementGlyph, 26, () => NudgeCellSize(-1));
            CellSizeSlider = UIBuilder.CreateSlider(
                _cellRow.Controls, "CellSizeSlider", AppSettings.MinCellSize, AppSettings.MaxCellSize,
                _pendingCellSize, true, OnCellSizeSliderChanged);
            IncrementCellSizeButton = UIBuilder.CreateButton(
                _cellRow.Controls, "Increment", IncrementGlyph, 26, () => NudgeCellSize(1));

            _speedRow = new SettingRow(_listContent, "SpeedRow", AppStrings.SettingsSpeedLabel, true);
            SpeedSlider = UIBuilder.CreateSlider(
                _speedRow.Controls, "SpeedSlider", AppSettings.MinStepsPerSecond,
                AppSettings.MaxStepsPerSecond, _pendingStepsPerSecond, true, OnSpeedSliderChanged);

            _overlayRow = new SettingRow(_listContent, "OverlayRow", AppStrings.SettingsOverlayLabel, false);
            OverlayToggle = CreateCheckbox(_overlayRow.Row, "OverlayToggle", out _overlayBox);
            OverlayToggle.SetIsOnWithoutNotify(_pendingShowOverlay);
            OverlayToggle.onValueChanged.AddListener(OnOverlayToggled);

            _buttonsRow = UIBuilder.CreatePanel(_listContent, "Buttons");
            _buttonsLayout = _buttonsRow.gameObject.AddComponent<LayoutElement>();
            SaveButton = UIBuilder.CreateButton(
                _buttonsRow, "Save", AppStrings.SettingsSave, 20, Save);
            CancelButton = UIBuilder.CreateButton(
                _buttonsRow, "Cancel", AppStrings.SettingsCancel, 20, Cancel);
            RestoreDefaultsButton = UIBuilder.CreateButton(
                _buttonsRow, "RestoreDefaults", AppStrings.SettingsRestoreDefaults, 20, RestoreDefaults);
            _saveLabel = SaveButton.GetComponentInChildren<Text>();
            _cancelLabel = CancelButton.GetComponentInChildren<Text>();
            _restoreLabel = RestoreDefaultsButton.GetComponentInChildren<Text>();

            NoteText = UIBuilder.CreateText(
                _listContent, "Note", AppStrings.SettingsNote, 16, UIBuilder.TextDimColor,
                TextAnchor.UpperLeft);
            NoteText.horizontalOverflow = HorizontalWrapMode.Wrap;
            NoteText.verticalOverflow = VerticalWrapMode.Overflow;

            RefreshControls();
            ApplyLayout(Screen.height > Screen.width, UIBuilder.IsPhoneLayout);
        }

        /// <summary>The bar's Back button; it discards edits, exactly like Cancel.</summary>
        public Button BackButton { get; }

        /// <summary>
        /// The scrolling list every row lives in. Exposed so the layout suite can
        /// measure the page's content without also measuring the top bar, whose
        /// title rect follows <c>UIBuilder.PlaceBarTitle</c>'s desktop insets in
        /// the Editor whatever canvas a test builds.
        /// </summary>
        public RectTransform ListContent => _listContent;

        /// <summary>The bar title ("Settings").</summary>
        public Text TitleText { get; }

        /// <summary>Cell size, 1..25, whole numbers.</summary>
        public Slider CellSizeSlider { get; }

        /// <summary>Cell size, one unit down (clamped at <see cref="AppSettings.MinCellSize"/>).</summary>
        public Button DecrementCellSizeButton { get; }

        /// <summary>Cell size, one unit up (clamped at <see cref="AppSettings.MaxCellSize"/>).</summary>
        public Button IncrementCellSizeButton { get; }

        /// <summary>Animation speed in generations per second, 1..60, whole numbers.</summary>
        public Slider SpeedSlider { get; }

        /// <summary>The "Display FPS/Steps" checkbox.</summary>
        public Toggle OverlayToggle { get; }

        /// <summary>Writes the pending values to <see cref="AppSettings"/>, applies them, and leaves.</summary>
        public Button SaveButton { get; }

        /// <summary>Reloads the stored values and leaves; nothing is written.</summary>
        public Button CancelButton { get; }

        /// <summary>Puts the shipped defaults into the controls; still unsaved until Save.</summary>
        public Button RestoreDefaultsButton { get; }

        /// <summary>The readout right of the cell-size slider.</summary>
        public Text CellSizeValueText => _cellRow.Value;

        /// <summary>The readout right of the speed slider.</summary>
        public Text SpeedValueText => _speedRow.Value;

        /// <summary>The "changes apply without restarting" note under the buttons.</summary>
        public Text NoteText { get; }

        /// <summary>Cell size the controls currently show; written to settings only by <see cref="Save"/>.</summary>
        public int PendingCellSize => _pendingCellSize;

        /// <summary>Speed the controls currently show; written to settings only by <see cref="Save"/>.</summary>
        public int PendingStepsPerSecond => _pendingStepsPerSecond;

        /// <summary>Overlay state the controls currently show; written to settings only by <see cref="Save"/>.</summary>
        public bool PendingShowOverlay => _pendingShowOverlay;

        /// <summary>
        /// Brings the screen up with the stored values, so an abandoned edit
        /// (Back, or the Android back button) never survives into the next visit.
        /// </summary>
        public override void Show()
        {
            base.Show();
            LoadFromSettings();
        }

        /// <summary>
        /// Re-lays the three rows, the button row, and the type sizes for a new
        /// form factor. Phones stack each row's label over its control and give
        /// Restore Defaults a full-width line of its own; every other layout puts
        /// the label in a left column with the control beside it.
        /// </summary>
        /// <param name="portrait">True when the screen is taller than it is wide.</param>
        /// <param name="phone">True for phone-sized screens (<c>UIBuilder.IsPhoneLayout</c>).</param>
        public override void ApplyLayout(bool portrait, bool phone)
        {
            float valueWidth = phone ? ValueWidthPhone : ValueWidthDesktop;
            float stepper = phone ? StepperWidthPhone : StepperWidthDesktop;
            int labelSize = phone ? 18 : 22;
            int valueSize = phone ? 18 : 22;
            int buttonSize = phone ? 19 : 20;

            TitleText.fontSize = phone ? 22 : 26;
            UIBuilder.PlaceBarTitle(TitleText);
            _backLabel.fontSize = phone ? 18 : 20;

            _cellRow.ApplyLayout(phone, labelSize, valueSize, valueWidth);
            _speedRow.ApplyLayout(phone, labelSize, valueSize, valueWidth);
            _overlayRow.ApplyLayout(phone, labelSize, valueSize, valueWidth);

            // Cell size: [-] [ slider ] [+] inside the row's control strip.
            var decrementRect = (RectTransform)DecrementCellSizeButton.transform;
            decrementRect.anchorMin = new Vector2(0f, 0f);
            decrementRect.anchorMax = new Vector2(0f, 1f);
            decrementRect.pivot = new Vector2(0f, 0.5f);
            decrementRect.sizeDelta = new Vector2(stepper, 0f);
            decrementRect.anchoredPosition = Vector2.zero;

            var incrementRect = (RectTransform)IncrementCellSizeButton.transform;
            incrementRect.anchorMin = new Vector2(1f, 0f);
            incrementRect.anchorMax = new Vector2(1f, 1f);
            incrementRect.pivot = new Vector2(1f, 0.5f);
            incrementRect.sizeDelta = new Vector2(stepper, 0f);
            incrementRect.anchoredPosition = Vector2.zero;

            UIBuilder.Stretch((RectTransform)CellSizeSlider.transform, stepper + 12f, 0f, stepper + 12f, 0f);
            UIBuilder.Stretch((RectTransform)SpeedSlider.transform);

            // Overlay: a checkbox in the value column, vertically centered.
            float boxSize = phone ? 44f : 40f;
            _overlayBox.anchorMin = new Vector2(1f, 0.5f);
            _overlayBox.anchorMax = new Vector2(1f, 0.5f);
            _overlayBox.pivot = new Vector2(1f, 0.5f);
            _overlayBox.sizeDelta = new Vector2(boxSize, boxSize);
            _overlayBox.anchoredPosition = Vector2.zero;

            _buttonsLayout.preferredHeight = phone ? 128f : 60f;
            _saveLabel.fontSize = buttonSize;
            _cancelLabel.fontSize = buttonSize;
            _restoreLabel.fontSize = buttonSize;
            if (phone)
            {
                // "Restore Defaults" does not fit a third of a phone canvas, so it
                // takes a full-width line under Save and Cancel.
                PlaceButton(SaveButton, 0f, 0.5f, 0.5f, 1f);
                PlaceButton(CancelButton, 0.5f, 1f, 0.5f, 1f);
                PlaceButton(RestoreDefaultsButton, 0f, 1f, 0f, 0.5f);
            }
            else
            {
                PlaceButton(SaveButton, 0f, 1f / 3f, 0f, 1f);
                PlaceButton(CancelButton, 1f / 3f, 2f / 3f, 0f, 1f);
                PlaceButton(RestoreDefaultsButton, 2f / 3f, 1f, 0f, 1f);
            }

            NoteText.fontSize = phone ? 15 : 16;
        }

        /// <summary>
        /// Writes the pending values to <see cref="AppSettings"/> (each setter
        /// raising <c>AppSettings.Changed</c>, which re-cuts the open simulator's
        /// lattice and re-reads the overlay flag), retimes the shared
        /// <see cref="SimulationHost"/>, and returns to the previous screen.
        /// Values that did not change are not written, so a plain "Save" never
        /// disturbs a running simulation.
        /// </summary>
        public void Save()
        {
            if (AppSettings.CellSize != _pendingCellSize)
            {
                AppSettings.CellSize = _pendingCellSize;
            }
            if (AppSettings.StepsPerSecond != _pendingStepsPerSecond)
            {
                AppSettings.StepsPerSecond = _pendingStepsPerSecond;
            }
            if (AppSettings.ShowOverlay != _pendingShowOverlay)
            {
                AppSettings.ShowOverlay = _pendingShowOverlay;
            }
            if (Services != null && Services.Host != null)
            {
                Services.Host.StepsPerSecond = _pendingStepsPerSecond;
            }
            Nav?.Back();
        }

        /// <summary>
        /// Drops every edit made since the screen opened and returns. Nothing was
        /// written on the way in, so this is a reload plus a Back.
        /// </summary>
        public void Cancel()
        {
            LoadFromSettings();
            Nav?.Back();
        }

        /// <summary>
        /// Puts the shipped defaults (5 / 30 / on) into the controls. Deliberately
        /// does not persist: the user still has to press Save, and Cancel still
        /// backs the defaults out.
        /// </summary>
        public void RestoreDefaults()
        {
            _pendingCellSize = AppSettings.DefaultCellSize;
            _pendingStepsPerSecond = AppSettings.DefaultStepsPerSecond;
            _pendingShowOverlay = AppSettings.DefaultShowOverlay;
            RefreshControls();
        }

        /// <summary>Re-reads <see cref="AppSettings"/> into the pending values and the controls.</summary>
        private void LoadFromSettings()
        {
            _pendingCellSize = AppSettings.CellSize;
            _pendingStepsPerSecond = AppSettings.StepsPerSecond;
            _pendingShowOverlay = AppSettings.ShowOverlay;
            RefreshControls();
        }

        /// <summary>Pushes the pending values into the controls without firing their callbacks.</summary>
        private void RefreshControls()
        {
            CellSizeSlider.SetValueWithoutNotify(_pendingCellSize);
            SpeedSlider.SetValueWithoutNotify(_pendingStepsPerSecond);
            OverlayToggle.SetIsOnWithoutNotify(_pendingShowOverlay);
            RefreshReadouts();
        }

        /// <summary>Rewrites the two numeric readouts and the stepper enabled states.</summary>
        private void RefreshReadouts()
        {
            _cellRow.Value.text = _pendingCellSize.ToString(CultureInfo.InvariantCulture);
            _speedRow.Value.text = _pendingStepsPerSecond.ToString(CultureInfo.InvariantCulture);
            DecrementCellSizeButton.interactable = _pendingCellSize > AppSettings.MinCellSize;
            IncrementCellSizeButton.interactable = _pendingCellSize < AppSettings.MaxCellSize;
        }

        private void OnCellSizeSliderChanged(float value)
        {
            _pendingCellSize = Mathf.Clamp(
                Mathf.RoundToInt(value), AppSettings.MinCellSize, AppSettings.MaxCellSize);
            RefreshReadouts();
        }

        private void OnSpeedSliderChanged(float value)
        {
            _pendingStepsPerSecond = Mathf.Clamp(
                Mathf.RoundToInt(value), AppSettings.MinStepsPerSecond, AppSettings.MaxStepsPerSecond);
            RefreshReadouts();
        }

        private void OnOverlayToggled(bool isOn)
        {
            _pendingShowOverlay = isOn;
        }

        /// <summary>One step of the cell-size steppers, clamped to the slider's own range.</summary>
        private void NudgeCellSize(int delta)
        {
            int next = Mathf.Clamp(
                _pendingCellSize + delta, AppSettings.MinCellSize, AppSettings.MaxCellSize);
            if (next == _pendingCellSize)
            {
                return;
            }
            _pendingCellSize = next;
            CellSizeSlider.SetValueWithoutNotify(_pendingCellSize);
            RefreshReadouts();
        }

        /// <summary>Anchors one action button to a fraction of the button row.</summary>
        private static void PlaceButton(Button button, float xMin, float xMax, float yMin, float yMax)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(4f, 4f);
            rect.offsetMax = new Vector2(-4f, -4f);
        }

        /// <summary>
        /// A square checkbox: a control-colored well with an accent square that
        /// the Toggle enables and disables. Built here rather than in
        /// <c>UIBuilder</c> because this is the app's only checkbox.
        /// </summary>
        private static Toggle CreateCheckbox(RectTransform parent, string name, out RectTransform box)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Toggle));
            box = (RectTransform)go.transform;
            box.SetParent(parent, false);
            var background = go.GetComponent<Image>();
            background.color = UIBuilder.ControlColor;

            var markGo = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            var markRect = (RectTransform)markGo.transform;
            markRect.SetParent(go.transform, false);
            UIBuilder.Stretch(markRect, 8f, 8f, 8f, 8f);
            var mark = markGo.GetComponent<Image>();
            mark.color = UIBuilder.AccentColor;
            mark.raycastTarget = false;

            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = mark;
            return toggle;
        }

        /// <summary>
        /// One labeled settings row: a label, an optional numeric readout, and a
        /// control strip. The strip is where the sliders and steppers live, so a
        /// layout change only has to move three rects rather than every control.
        /// </summary>
        private sealed class SettingRow
        {
            private readonly LayoutElement _layout;
            private readonly bool _hasControls;

            /// <summary>Builds the row under <paramref name="content"/>.</summary>
            /// <param name="content">The settings list's layout content.</param>
            /// <param name="name">GameObject name, for the hierarchy and tests.</param>
            /// <param name="label">The row's label, from <see cref="AppStrings"/>.</param>
            /// <param name="withControls">
            /// True for the slider rows, which get a numeric readout and a control
            /// strip; false for the overlay row, whose checkbox sits in the value
            /// column itself.
            /// </param>
            public SettingRow(RectTransform content, string name, string label, bool withControls)
            {
                _hasControls = withControls;
                Row = UIBuilder.CreatePanel(content, name);
                _layout = Row.gameObject.AddComponent<LayoutElement>();

                Label = UIBuilder.CreateText(Row, "Label", label, 22, UIBuilder.TextColor);
                Label.horizontalOverflow = HorizontalWrapMode.Wrap;
                Label.verticalOverflow = VerticalWrapMode.Truncate;

                Value = UIBuilder.CreateText(
                    Row, "Value", string.Empty, 22, UIBuilder.AccentColor, TextAnchor.MiddleRight);
                Value.gameObject.SetActive(withControls);

                Controls = UIBuilder.CreatePanel(Row, "Controls");
                Controls.gameObject.SetActive(withControls);
            }

            /// <summary>The row's own rect.</summary>
            public RectTransform Row { get; }

            /// <summary>The row's label.</summary>
            public Text Label { get; }

            /// <summary>The right-hand numeric readout (inactive on the overlay row).</summary>
            public Text Value { get; }

            /// <summary>The strip holding the row's sliders and steppers.</summary>
            public RectTransform Controls { get; }

            /// <summary>
            /// Places the label, the readout, and the control strip for one form
            /// factor: phones stack label over control, everything else puts the
            /// label in a left column.
            /// </summary>
            /// <param name="phone">True for the phone layout.</param>
            /// <param name="labelSize">Point size of the label.</param>
            /// <param name="valueSize">Point size of the readout.</param>
            /// <param name="valueWidth">Width of the readout column.</param>
            public void ApplyLayout(bool phone, int labelSize, int valueSize, float valueWidth)
            {
                Label.fontSize = labelSize;
                Value.fontSize = valueSize;
                _layout.preferredHeight = _hasControls
                    ? (phone ? 108f : 72f)
                    : (phone ? 76f : 64f);

                Value.rectTransform.anchorMin = new Vector2(1f, phone && _hasControls ? 0.5f : 0f);
                Value.rectTransform.anchorMax = Vector2.one;
                Value.rectTransform.pivot = new Vector2(1f, 0.5f);
                Value.rectTransform.offsetMin = new Vector2(-valueWidth, 0f);
                Value.rectTransform.offsetMax = Vector2.zero;

                Label.rectTransform.anchorMin = new Vector2(
                    0f, phone && _hasControls ? 0.5f : 0f);
                Label.rectTransform.anchorMax = phone || !_hasControls
                    ? Vector2.one
                    : new Vector2(0.44f, 1f);
                Label.rectTransform.pivot = new Vector2(0f, 0.5f);
                Label.rectTransform.offsetMin = Vector2.zero;
                Label.rectTransform.offsetMax = new Vector2(
                    phone || !_hasControls ? -(valueWidth + 12f) : -12f, 0f);

                if (!_hasControls)
                {
                    return;
                }
                Controls.anchorMin = phone ? Vector2.zero : new Vector2(0.44f, 0f);
                Controls.anchorMax = phone ? new Vector2(1f, 0.5f) : Vector2.one;
                Controls.pivot = new Vector2(0.5f, 0.5f);
                Controls.offsetMin = new Vector2(0f, RowPadY);
                Controls.offsetMax = new Vector2(phone ? 0f : -(valueWidth + 12f), -RowPadY);
            }
        }
    }
}
