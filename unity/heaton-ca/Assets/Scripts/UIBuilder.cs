using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HeatonCAApp
{
    /// <summary>
    /// Code-built uGUI helpers (dynaface pattern: no prefabs). Dark theme; all
    /// controls are plain solid-color Images + legacy-font Text.
    /// </summary>
    public static class UIBuilder
    {
        public static readonly Color BackgroundColor = new Color32(0x10, 0x14, 0x18, 0xFF);
        public static readonly Color PanelColor = new Color32(0x1B, 0x22, 0x2A, 0xFF);
        public static readonly Color ControlColor = new Color32(0x26, 0x2F, 0x3A, 0xFF);
        public static readonly Color AccentColor = new Color32(0x4F, 0xC3, 0xF7, 0xFF);
        public static readonly Color TextColor = new Color32(0xEC, 0xEC, 0xEC, 0xFF);
        public static readonly Color TextDimColor = new Color32(0x9A, 0xA5, 0xB1, 0xFF);

        private static Font _font;

        public static Font DefaultFont =>
            _font != null ? _font : _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>
        /// Per-platform UI density: desktop windows get a wide fixed reference (the
        /// same unit sizes come out physically smaller — touch controls look
        /// gigantic under a mouse). Mobile targets a constant PHYSICAL density
        /// instead: 6.75 units/mm, the look of the 1080-unit canvas on a 160 mm
        /// iPad, so a phone's canvas holds fewer units and every control renders
        /// physically larger. Capped at 1080 — tablets keep the designed canvas.
        /// Rotation changes the physical width, so AppController re-applies the
        /// scaler (ApplyReference) whenever the screen size changes.
        /// </summary>
        public static float ReferenceWidth =>
            Application.isMobilePlatform
                ? MobileReferenceWidth(Screen.width, Screen.dpi)
                : 1600f;

        /// <summary>Units per millimeter every mobile screen renders at (1080/160).</summary>
        public const float MobileUnitsPerMm = 6.75f;

        /// <summary>The mobile reference width for a screen (pure math; tested).</summary>
        public static float MobileReferenceWidth(float widthPixels, float dpi)
        {
            if (dpi <= 1f)
                return 1080f; // dpi unreported: keep the historical tablet canvas
            float widthMm = widthPixels * 25.4f / dpi;
            return Mathf.Min(1080f, MobileUnitsPerMm * widthMm);
        }

        /// <summary>(Re)apply the density to the scaler — at boot and every resize.</summary>
        public static void ApplyReference(CanvasScaler scaler) =>
            scaler.referenceResolution = new Vector2(ReferenceWidth, 675f);

        /// <summary>
        /// Phone-sized screen? True when the smallest physical dimension is under
        /// 90 mm (pure math; tested) — an iPhone in either orientation, never an
        /// iPad (its short side is ~134 mm+). Screens that don't report dpi count
        /// as tablets: the historical layouts are the safe default.
        /// </summary>
        public static bool PhoneSized(float widthPixels, float heightPixels, float dpi)
        {
            if (dpi <= 1f)
                return false;
            float minMm = Mathf.Min(widthPixels, heightPixels) * 25.4f / dpi;
            return minMm < 90f;
        }

        /// <summary>
        /// The third layout mode (M42): phone — too cramped for the dock/sheet
        /// layouts, so views present viewing-first (SimulationView: full-bleed
        /// canvas, params on the World page, docked edit strip). Physical gate,
        /// the CategoryColumns technique; mobile-only, so a narrow desktop window
        /// never trips it and tablet/desktop layouts stay untouched.
        /// </summary>
        public static bool IsPhoneLayout =>
            Application.isMobilePlatform && PhoneSized(Screen.width, Screen.height, Screen.dpi);

        /// <summary>
        /// A screen's top-bar title. Desktop and tablet bars carry "Group: Name"
        /// ("Cellular Automata: Life-like"); a phone bar has room for the name only —
        /// with the prefix, PlaceBarTitle's wrap+shrink put "Cellular Automata:
        /// Life-like" on THREE lines of 16-point text between the back button and
        /// the generation label (2026-08-22 manual screenshots). Pure, unit-pinned.
        /// </summary>
        public static string BarTitle(string group, string name, bool phone) =>
            phone || string.IsNullOrEmpty(group) ? name : $"{group}: {name}";

        /// <summary>
        /// Top-bar title placement: 170 units always clear the back button on the
        /// left. Desktop mirrors the inset on the right (perfect centering);
        /// narrow mobile canvases keep only what the bar's right-side controls
        /// need, so the title rides right of center (the iOS look) instead of
        /// overlapping the back button. Wrap+truncate so long titles can never
        /// spill over neighbors.
        /// </summary>
        public static void PlaceBarTitle(Text title, float rightControlsWidth = 0f)
        {
            // Honor the caller's right-controls width on every platform: a bar with
            // wide right-side furniture (the evolve status label, the finds page's
            // Refresh+Clear) otherwise centers the title in a rect that runs
            // underneath it. Desktop keeps the 170 floor so short-furniture bars
            // stay symmetric with the back button.
            float right = Application.isMobilePlatform
                ? Mathf.Max(16f, rightControlsWidth)
                : Mathf.Max(170f, rightControlsWidth);
            Stretch((RectTransform)title.transform, 170f, 0f, right, 0f);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            // Shrink-to-fit: long titles drop a few points and stay on one line
            // on narrow phone bars rather than wrapping against the screen edge.
            title.resizeTextForBestFit = true;
            title.resizeTextMaxSize = title.fontSize;
            title.resizeTextMinSize = 16;
        }

        /// <summary>Screen-space overlay canvas, reference width per platform (dynaface convention).</summary>
        public static RectTransform CreateCanvas(out Canvas canvas)
        {
            var go = new GameObject(
                "Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            ApplyReference(scaler);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f; // match width
            return (RectTransform)go.transform;
        }

        public static RectTransform CreatePanel(Transform parent, string name, Color? color = null)
        {
            GameObject go = color.HasValue
                ? new GameObject(name, typeof(RectTransform), typeof(Image))
                : new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            if (color.HasValue)
                go.GetComponent<Image>().color = color.Value;
            return rt;
        }

        public static void Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static Text CreateText(
            Transform parent,
            string name,
            string content,
            int size,
            Color color,
            TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(
            Transform parent, string name, string label, int fontSize, UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = ControlColor;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null)
                button.onClick.AddListener(onClick);
            Text text = CreateText(go.transform, "Label", label, fontSize, TextColor, TextAnchor.MiddleCenter);
            // Button labels must never escape their button (CreateText defaults to
            // overflow, which is right for standalone labels and wrong here).
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            Stretch((RectTransform)text.transform);
            return button;
        }

        public static Slider CreateSlider(
            Transform parent,
            string name,
            float min,
            float max,
            float value,
            bool wholeNumbers,
            UnityAction<float> onChanged)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);

            var backgroundGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var backgroundRt = (RectTransform)backgroundGo.transform;
            backgroundRt.SetParent(go.transform, false);
            backgroundRt.anchorMin = new Vector2(0f, 0.5f);
            backgroundRt.anchorMax = new Vector2(1f, 0.5f);
            backgroundRt.sizeDelta = new Vector2(0f, 10f);
            backgroundGo.GetComponent<Image>().color = new Color32(0x0C, 0x10, 0x14, 0xFF);

            var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            var fillAreaRt = (RectTransform)fillAreaGo.transform;
            fillAreaRt.SetParent(go.transform, false);
            fillAreaRt.anchorMin = new Vector2(0f, 0.5f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.5f);
            fillAreaRt.offsetMin = new Vector2(10f, -5f);
            fillAreaRt.offsetMax = new Vector2(-10f, 5f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRt = (RectTransform)fillGo.transform;
            fillRt.SetParent(fillAreaGo.transform, false);
            fillRt.sizeDelta = new Vector2(10f, 0f);
            fillGo.GetComponent<Image>().color = AccentColor;

            var handleAreaGo = new GameObject("Handle Slide Area", typeof(RectTransform));
            var handleAreaRt = (RectTransform)handleAreaGo.transform;
            handleAreaRt.SetParent(go.transform, false);
            Stretch(handleAreaRt, 10f, 0f, 10f, 0f);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            var handleRt = (RectTransform)handleGo.transform;
            handleRt.SetParent(handleAreaGo.transform, false);
            // Height 0, not 20: the Slider stretches its handle across the track's
            // perpendicular axis and ADDS sizeDelta, so any positive height makes
            // the handle taller than the slider's own rect — it rode up over the
            // value labels in every param row (the "numbers get overwritten" bug).
            handleRt.sizeDelta = new Vector2(20f, 0f);
            var handleImage = handleGo.GetComponent<Image>();
            handleImage.color = new Color32(0xD5, 0xDD, 0xE5, 0xFF);

            var slider = go.GetComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handleImage;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.SetValueWithoutNotify(value);
            if (onChanged != null)
                slider.onValueChanged.AddListener(onChanged);
            return slider;
        }

        public static RawImage CreateRawImage(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var raw = go.GetComponent<RawImage>();
            raw.color = Color.white;
            return raw;
        }

        public static InputField CreateInputField(
            Transform parent, string name, string initial, int fontSize, UnityAction<string> onEndEdit)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = new Color32(0x0C, 0x10, 0x14, 0xFF);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var textRt = (RectTransform)textGo.transform;
            textRt.SetParent(go.transform, false);
            Stretch(textRt, 10f, 4f, 10f, 4f);
            var text = textGo.GetComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = fontSize;
            text.color = TextColor;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            var input = go.GetComponent<InputField>();
            input.targetGraphic = image;
            input.textComponent = text;
            input.lineType = InputField.LineType.SingleLine;
            input.text = initial;
            if (onEndEdit != null)
                input.onEndEdit.AddListener(onEndEdit);
            return input;
        }

        /// <summary>
        /// Vertical scroll view (drag + wheel, clamped, no scrollbar). The returned
        /// content transform should get a layout group + ContentSizeFitter from the caller.
        /// </summary>
        public static ScrollRect CreateScrollView(Transform parent, string name, out RectTransform content)
        {
            var go = new GameObject(
                name, typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D),
                typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            // Invisible raycast surface: wheel/drag events are routed by graphic
            // raycast, so without one the gaps between cards, the side margins,
            // and the space below the last row are scroll-dead.
            go.GetComponent<Image>().color = Color.clear;
            var contentGo = new GameObject("Content", typeof(RectTransform));
            content = (RectTransform)contentGo.transform;
            content.SetParent(go.transform, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var scroll = go.GetComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = rt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return scroll;
        }

        /// <summary>
        /// A picker card: live preview square left, name + blurb right; returns the
        /// preview image for the caller to drive (usually with a PreviewTile). The
        /// shape the New-world picker, the MergeLife rule gallery, and the evolver's
        /// finds catalog all share — they ask the user the same question ("which of
        /// these worlds do I want?"), so they look the same answering it.
        /// </summary>
        public static RawImage CreatePickerCard(
            RectTransform grid, string title, string blurb, int titleSize,
            System.Action onClick, float previewSize)
        {
            Button card = CreateButton(grid, title, "", 16, () => onClick());
            var preview = CreateRawImage(card.transform, "Preview");
            preview.raycastTarget = false;
            var previewRt = (RectTransform)preview.transform;
            previewRt.anchorMin = new Vector2(0f, 0.5f);
            previewRt.anchorMax = new Vector2(0f, 0.5f);
            previewRt.pivot = new Vector2(0f, 0.5f);
            previewRt.anchoredPosition = new Vector2(14f, 0f);
            previewRt.sizeDelta = new Vector2(previewSize, previewSize);

            Text name = CreateText(card.transform, "Name", title, titleSize, TextColor);
            name.fontStyle = FontStyle.Bold;
            name.raycastTarget = false;
            // Titles must never run off the card: wrap, and shrink-to-fit on
            // narrow phone cells where the text column is thin.
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            name.resizeTextForBestFit = true;
            name.resizeTextMaxSize = titleSize;
            name.resizeTextMinSize = 15;
            var nameRt = (RectTransform)name.transform;
            nameRt.anchorMin = new Vector2(0f, 0.55f);
            nameRt.anchorMax = Vector2.one;
            nameRt.offsetMin = new Vector2(previewSize + 28f, 0f);
            nameRt.offsetMax = new Vector2(-12f, -10f);

            Text detail = CreateText(
                card.transform, "Blurb", blurb, 15, TextDimColor, TextAnchor.UpperLeft);
            detail.horizontalOverflow = HorizontalWrapMode.Wrap;
            detail.verticalOverflow = VerticalWrapMode.Truncate;
            detail.raycastTarget = false;
            var detailRt = (RectTransform)detail.transform;
            detailRt.anchorMin = Vector2.zero;
            detailRt.anchorMax = new Vector2(1f, 0.55f);
            detailRt.offsetMin = new Vector2(previewSize + 28f, 10f);
            detailRt.offsetMax = new Vector2(-12f, 4f);
            return preview;
        }

        /// <summary>Filesystem-friendly lowercase slug for names ("Burning Ship" → "burning-ship").</summary>
        public static string Slug(string name)
        {
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (char c in name.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(c);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '-')
                    sb.Append('-');
            }
            return sb.ToString().TrimEnd('-');
        }
    }
}
