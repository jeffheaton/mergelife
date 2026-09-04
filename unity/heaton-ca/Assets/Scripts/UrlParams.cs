// The WebGL player is the successor to js/web-full-screen/ml-fullscreen2.js, and
// that viewer is embedded in pages by URL: ?rule=<hex>&size=<cells>&controls=on|off.
// AppController reads WebGlBridge.GetQueryString() at boot and hands it here, so
// every one of those old embed links keeps working against the Unity build.
//
// Nothing here is platform specific: the parsing is pure, the desktop and Editor
// players simply get an empty query, and the EditMode suite pins the behavior
// without a browser.

using System;
using System.Globalization;

namespace HeatonCAApp
{
    /// <summary>
    /// The three launch parameters the web viewer honors, parsed out of a URL or a
    /// bare query string. Every field is null when the parameter was absent,
    /// malformed, or out of range, so the caller keeps its own default:
    /// <see cref="Rule"/> opens the Simulator on that rule, <see cref="CellSize"/>
    /// overrides the session cell size, and <see cref="Controls"/> false is the
    /// kiosk look the old embeds used.
    /// </summary>
    public sealed class UrlParams
    {
        /// <summary>Canonical rule from <c>?rule=</c>, or null when absent or invalid.</summary>
        public string Rule;

        /// <summary>Cell size from <c>?size=</c> when it is a whole number of pixels in range, else null.</summary>
        public int? CellSize;

        /// <summary>Toolbar visibility from <c>?controls=</c> (on/1/true, off/0/false), else null.</summary>
        public bool? Controls;

        /// <summary>Query key for the rule, as in ml-fullscreen2.js.</summary>
        private const string RuleKey = "rule";

        /// <summary>Query key for the cell size (the JS viewer's "zoom").</summary>
        private const string SizeKey = "size";

        /// <summary>Query key for toolbar visibility.</summary>
        private const string ControlsKey = "controls";

        /// <summary>
        /// Read the parameters out of <paramref name="url"/>. Accepts a whole URL, a
        /// <c>window.location.search</c> string ("?rule=..."), or a bare query
        /// ("rule=..."): everything before the first '?' is dropped when there is one,
        /// anything from the first '#' on is ignored (a fragment is not a query, even
        /// when it contains a '='), and the rest is split on '&'. Keys and values are
        /// URL-decoded, keys match case-insensitively, unknown keys are ignored, and a
        /// repeated key takes its last value -- including when that last value is the
        /// invalid one. Never throws and never returns null; junk simply parses to a
        /// result with every field null.
        /// </summary>
        public static UrlParams Parse(string url)
        {
            var result = new UrlParams();
            if (string.IsNullOrEmpty(url))
            {
                return result;
            }

            int fragment = url.IndexOf('#');
            string text = fragment < 0 ? url : url.Substring(0, fragment);
            int question = text.IndexOf('?');
            if (question >= 0)
            {
                text = text.Substring(question + 1);
            }
            if (text.Length == 0)
            {
                return result;
            }

            foreach (string pair in text.Split('&'))
            {
                int equals = pair.IndexOf('=');
                if (equals <= 0)
                {
                    continue; // no '=', or an empty key: not a parameter
                }
                string key = Decode(pair.Substring(0, equals)).Trim();
                string value = Decode(pair.Substring(equals + 1)).Trim();

                if (string.Equals(key, RuleKey, StringComparison.OrdinalIgnoreCase))
                {
                    result.Rule = RuleParser.TryParse(value, out string canonical, out _)
                        ? canonical
                        : null;
                }
                else if (string.Equals(key, SizeKey, StringComparison.OrdinalIgnoreCase))
                {
                    result.CellSize = ParseSize(value);
                }
                else if (string.Equals(key, ControlsKey, StringComparison.OrdinalIgnoreCase))
                {
                    result.Controls = ParseFlag(value);
                }
            }
            return result;
        }

        /// <summary>
        /// Percent-decode one query token, leaving it alone when it is not valid
        /// escaping (an embed link with a stray '%' should still open its rule).
        /// '+' is left as a plus: the three values this class reads never contain a
        /// space, so decoding it as one would only corrupt a rule.
        /// </summary>
        private static string Decode(string text)
        {
            if (text.Length == 0 || text.IndexOf('%') < 0)
            {
                return text;
            }
            try
            {
                return Uri.UnescapeDataString(text);
            }
            catch (UriFormatException)
            {
                return text;
            }
        }

        /// <summary>
        /// The cell size a <c>?size=</c> value asks for, or null when it is not a whole
        /// number or falls outside the sizes the app can draw
        /// (<see cref="CellGeometry.MinCellSize"/>..<see cref="CellGeometry.MaxCellSize"/>).
        /// Out of range is refused rather than clamped: a link asking for 400-pixel
        /// cells means something the app cannot honor, and the user's own setting is a
        /// better answer than an arbitrary 25.
        /// </summary>
        private static int? ParseSize(string value)
        {
            if (!int.TryParse(
                    value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int size))
            {
                return null;
            }
            return size >= CellGeometry.MinCellSize && size <= CellGeometry.MaxCellSize
                ? (int?)size
                : null;
        }

        /// <summary>
        /// The boolean a <c>?controls=</c> value asks for: on/1/true and off/0/false in
        /// any casing, null for anything else. ml-fullscreen2.js honored only
        /// "controls=on"; the app honors both directions because its own default is
        /// controls on, so an embed needs a way to say off.
        /// </summary>
        private static bool? ParseFlag(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "on":
                case "1":
                case "true":
                    return true;
                case "off":
                case "0":
                case "false":
                    return false;
                default:
                    return null;
            }
        }
    }
}
