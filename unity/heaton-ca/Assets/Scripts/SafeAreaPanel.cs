using UnityEngine;

namespace HeatonCAApp
{
    /// <summary>
    /// Anchors its RectTransform to Screen.safeArea (the dynaface SafeAreaPanel
    /// pattern): every view parents into this rect so content clears the notch /
    /// Dynamic Island and the home indicator, while the full-bleed backdrop stays
    /// on the canvas root behind it. Self-updating — rotation moves the insets.
    /// On screens without insets (desktop, editor) it is a full-rect no-op.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaPanel : MonoBehaviour
    {
        private RectTransform _rt;
        private Rect _lastSafeArea;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != _lastSafeArea)
                Apply();
        }

        private void Apply()
        {
            _lastSafeArea = Screen.safeArea;
            Rect area = Screen.safeArea;
            _rt.anchorMin = new Vector2(area.x / Screen.width, area.y / Screen.height);
            _rt.anchorMax = new Vector2(
                (area.x + area.width) / Screen.width,
                (area.y + area.height) / Screen.height);
            _rt.offsetMin = _rt.offsetMax = Vector2.zero;
        }
    }
}
