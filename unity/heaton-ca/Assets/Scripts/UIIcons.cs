using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HeatonCAApp
{
    /// <summary>
    /// Icon-sprite cache (ported from dynaface-unity): each icon is a white Google
    /// Material Symbols glyph PNG under Assets/Resources/Icons/, loaded once and
    /// converted to a Sprite. Icons tint via Image.color.
    /// </summary>
    public static class UIIcons
    {
        private static readonly Dictionary<string, Sprite> Cache =
            new Dictionary<string, Sprite>();

        public static Image MakeIcon(Transform parent, string iconName)
        {
            var go = new GameObject("Icon", typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            if (!Cache.TryGetValue(iconName, out Sprite sprite))
            {
                var texture = Resources.Load<Texture2D>("Icons/" + iconName);
                sprite = texture != null
                    ? Sprite.Create(
                        texture, new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f))
                    : null;
                Cache[iconName] = sprite;
            }
            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = Color.white;
            }
            else
            {
                image.color = Color.clear;
            }
            image.preserveAspect = true;
            image.raycastTarget = false;
            var rt = image.rectTransform;
            rt.anchorMin = new Vector2(0.1f, 0.1f);
            rt.anchorMax = new Vector2(0.9f, 0.9f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            return image;
        }
    }
}
