// The Unity side of Assets/Plugins/WebGL/HeatonCA.jslib. One of the platform
// seams listed in the plan: the only place the app talks to the browser, and
// the only #if UNITY_WEBGL site outside the scheduler, storage, and exporter
// seams. Every method has a safe body on every other platform (and in the
// Editor, whatever build target is selected) so callers never branch on the
// platform themselves.

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace HeatonCAApp
{
    /// <summary>
    /// Browser integration for the WebGL player: PNG downloads, the unload hook
    /// that persists state because <c>OnApplicationQuit</c> never fires in a
    /// browser, clipboard writes, popup-safe new tabs, the page query string,
    /// and mobile-browser detection. Outside a WebGL player the methods fall
    /// back to the closest Unity API or do nothing and say so.
    /// </summary>
    public static class WebGlBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void HeatonCA_Download(string fileName, byte[] data, int length);

        [DllImport("__Internal")]
        private static extern void HeatonCA_InstallUnloadHook();

        [DllImport("__Internal")]
        private static extern void HeatonCA_CopyText(string text);

        [DllImport("__Internal")]
        private static extern void HeatonCA_OpenInNewTab(string url);

        [DllImport("__Internal")]
        private static extern string HeatonCA_GetQueryString();

        [DllImport("__Internal")]
        private static extern int HeatonCA_IsMobileBrowser();
#endif

        /// <summary>
        /// Hands <paramref name="bytes"/> to the browser as a file download named
        /// <paramref name="fileName"/> (a Blob behind a clicked anchor). Returns
        /// true when the download was started. Everywhere but a WebGL player it
        /// writes nothing and returns false; the caller decides what to do
        /// instead (the exporter saves to disk or the photo library).
        /// </summary>
        public static bool Download(string fileName, byte[] bytes)
        {
            if (string.IsNullOrEmpty(fileName) || bytes == null || bytes.Length == 0)
            {
                return false;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            HeatonCA_Download(fileName, bytes, bytes.Length);
            return true;
#else
            return false;
#endif
        }

        /// <summary>
        /// Asks the page to call <c>SendMessage("App", "OnBeforeUnload")</c> when
        /// the tab is closed, navigated away from, or hidden, so
        /// <see cref="AppController.OnBeforeUnload"/> can persist and save
        /// PlayerPrefs. Idempotent on the JavaScript side; a no-op elsewhere,
        /// where <c>OnApplicationQuit</c> and <c>OnApplicationPause</c> cover it.
        /// </summary>
        public static void InstallUnloadHook()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            HeatonCA_InstallUnloadHook();
#endif
        }

        /// <summary>
        /// Copies <paramref name="text"/> to the clipboard: the async Clipboard API
        /// with an execCommand fallback in the browser,
        /// <see cref="GUIUtility.systemCopyBuffer"/> everywhere else. Null copies
        /// an empty string.
        /// </summary>
        public static void CopyText(string text)
        {
            text = text ?? string.Empty;
#if UNITY_WEBGL && !UNITY_EDITOR
            HeatonCA_CopyText(text);
#else
            GUIUtility.systemCopyBuffer = text;
#endif
        }

        /// <summary>
        /// Opens <paramref name="url"/> in a new browser tab. Call it synchronously
        /// from the click handler: the jslib calls <c>window.open</c> in the same
        /// JavaScript task as the user's gesture, which is what popup blockers
        /// require, whereas <see cref="Application.OpenURL"/> is blocked in
        /// WebGL builds. Elsewhere it is <see cref="Application.OpenURL"/>.
        /// </summary>
        public static void OpenInNewTab(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            HeatonCA_OpenInNewTab(url);
#else
            Application.OpenURL(url);
#endif
        }

        /// <summary>
        /// The page's query string, leading '?' included and fragment excluded,
        /// exactly as <c>window.location.search</c> reports it; empty when there
        /// is none. Outside the browser it is derived from
        /// <see cref="Application.absoluteURL"/> when that is an http(s) URL, so
        /// the Editor and desktop players behave the same on an empty string.
        /// </summary>
        public static string GetQueryString()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return HeatonCA_GetQueryString() ?? string.Empty;
#else
            return QueryOf(Application.absoluteURL);
#endif
        }

        /// <summary>
        /// True when the browser's user agent looks like a phone or tablet (Unity
        /// does not officially support mobile browsers; the template shows a
        /// banner and the app can trim its layout). False on every other platform.
        /// </summary>
        public static bool IsMobileBrowser()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return HeatonCA_IsMobileBrowser() != 0;
#else
            return false;
#endif
        }

        /// <summary>
        /// The <c>window.location.search</c> equivalent of <paramref name="url"/>:
        /// from its first '?' up to (not including) any '#', or empty when the
        /// URL is not http(s) or carries no query. Internal so the EditMode suite
        /// can pin the parsing without a browser.
        /// </summary>
        internal static string QueryOf(string url)
        {
            if (string.IsNullOrEmpty(url)
                || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }
            int query = url.IndexOf('?');
            if (query < 0)
            {
                return string.Empty;
            }
            int fragment = url.IndexOf('#', query);
            return fragment < 0 ? url.Substring(query) : url.Substring(query, fragment - query);
        }
    }
}
