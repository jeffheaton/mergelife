// Seam 3 of the platform seams listed in the plan: every PNG the app hands to the
// user -- the Simulator's Save PNG, a find's <rule>.png on the Evolve Finds page --
// leaves through PngExporter.Save, and this is the only file in the app that names
// NativeGallery or System.Diagnostics.Process. Callers pass bytes, a file name, and
// a status callback; where the bytes land is the platform's business:
//
//   WebGL        the browser's download list (a Blob, through WebGlBridge).
//   iOS/Android  the photo library album "HeatonCA" through NativeGallery (which
//                asks for the Android runtime permission itself and answers on a
//                callback), plus a copy under SnapshotsRoot so the file is also
//                reachable from the Files app (UIFileSharingEnabled, iOSPostBuild).
//   Desktop      a file under SnapshotsRoot, revealed in Finder / Explorer: the Mac
//                App Store build is sandboxed, so snapshots live in a container
//                nobody browses to by hand.
//   Editor       the file only -- no gallery, no reveal -- so batch-mode test runs
//                open no windows and need no permissions.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HeatonCAApp
{
    /// <summary>
    /// Saves PNG bytes where the user can find them again, per platform, and names
    /// snapshot files. The one place the app touches the photo library, the browser
    /// download path, and the desktop file managers; every screen calls
    /// <see cref="Save"/> and reports whatever status text comes back through the
    /// callback.
    /// </summary>
    public static class PngExporter
    {
        /// <summary>
        /// Folder name under the persistent data path (and under an injected storage
        /// root) that holds saved PNGs. iOS publishes it to the Files app.
        /// </summary>
        public const string SnapshotsFolder = "Snapshots";

        /// <summary>Slug used when the caller has no name to offer (or only unusable characters).</summary>
        private const string FallbackSlug = "snapshot";

        /// <summary>
        /// Status text when nothing could be written. TODO: move to AppStrings
        /// (WP2.1 owns the string table; this is the only string this file coins).
        /// </summary>
        private const string SaveFailedMessage = "save failed";

        private static string _snapshotsRoot;

        /// <summary>
        /// Where the file branches write. Defaults, on first use, to
        /// <c>Application.persistentDataPath/Snapshots</c>; the default is resolved
        /// lazily because Unity forbids touching persistentDataPath from a static
        /// constructor. Assign to redirect the app at a test folder or at
        /// AppController's injected storage root; assign null or an empty string to
        /// go back to the default.
        /// </summary>
        public static string SnapshotsRoot
        {
            get
            {
                if (string.IsNullOrEmpty(_snapshotsRoot))
                {
                    _snapshotsRoot = Path.Combine(Application.persistentDataPath, SnapshotsFolder);
                }
                return _snapshotsRoot;
            }
            set => _snapshotsRoot = string.IsNullOrEmpty(value) ? null : value;
        }

        /// <summary>
        /// Hand <paramref name="png"/> to the platform under the name
        /// <paramref name="fileName"/> (directory components are stripped; the caller
        /// does not choose the folder). Reports what happened through
        /// <paramref name="status"/> -- which may be null -- and returns true when the
        /// save was started: a browser download, a photo-library write plus the Files
        /// app copy, or a file on disk. Never throws: a failed write is logged, told
        /// to the user, and returned as false rather than reported as a save that did
        /// not happen.
        /// </summary>
        public static bool Save(byte[] png, string fileName, Action<string> status)
        {
            if (png == null || png.Length == 0 || string.IsNullOrEmpty(fileName))
            {
                Debug.LogWarning("[HeatonCA] save refused: no PNG bytes or no file name");
                status?.Invoke(SaveFailedMessage);
                return false;
            }

            string name = Path.GetFileName(fileName);
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning("[HeatonCA] save refused: '" + fileName + "' names no file");
                status?.Invoke(SaveFailedMessage);
                return false;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            // No filesystem the user can reach: the jslib clicks a Blob anchor, and
            // the browser decides where the file goes.
            if (!WebGlBridge.Download(name, png))
            {
                status?.Invoke(SaveFailedMessage);
                return false;
            }
            status?.Invoke(AppStrings.SimStatusDownloadStarted);
            return true;
#elif (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            // The Files app copy goes first because it is synchronous and needs no
            // permission; it is a convenience, so TryWriteFile logging its own
            // failure is enough -- the photo library is the destination that matters.
            TryWriteFile(png, name, out _);

            // NativeGallery requests the write permission itself (the Android runtime
            // dialog; on iOS PermissionFreeMode saves to the default album with only
            // the system's own prompt) and answers asynchronously, long after this
            // status line was read, so a denial or a MediaStore failure can only be
            // logged here.
            NativeGallery.SaveImageToGallery(png, AppStrings.AppName, name, (success, path) =>
            {
                if (!success)
                {
                    Debug.LogWarning("[HeatonCA] photo library save failed for " + name);
                }
            });
            status?.Invoke(AppStrings.SimStatusSavedToPhotos);
            return true;
#else
            if (!TryWriteFile(png, name, out string saved))
            {
                status?.Invoke(SaveFailedMessage);
                return false;
            }
            Reveal(saved);
            status?.Invoke(string.Format(
                CultureInfo.InvariantCulture, AppStrings.SimStatusSavedFormat, name));
            return true;
#endif
        }

        /// <summary>
        /// The snapshot file name for <paramref name="slug"/> (typically the rule
        /// being displayed) at <paramref name="utc"/>:
        /// <c>heatonca-&lt;slug&gt;-&lt;yyyyMMdd-HHmmss-fff&gt;.png</c>. The slug is
        /// reduced to lowercase ASCII letters, digits, and single dashes -- a
        /// canonical rule already reads that way -- so the name is legal on every
        /// filesystem, in the Android MediaStore, and in a browser's download list.
        /// Pass <c>DateTime.UtcNow</c>; a local time is converted rather than
        /// silently stamped as UTC.
        /// </summary>
        public static string SnapshotFileName(string slug, DateTime utc)
        {
            if (utc.Kind == DateTimeKind.Local)
            {
                utc = utc.ToUniversalTime();
            }
            return "heatonca-" + SanitizeSlug(slug) + "-"
                + utc.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + ".png";
        }

        /// <summary>
        /// Lowercase [a-z0-9-] form of <paramref name="slug"/>: runs of anything else
        /// collapse to one dash, there are no leading or trailing dashes, and an empty
        /// result becomes <see cref="FallbackSlug"/>. Deliberately stricter than
        /// <see cref="UIBuilder.Slug"/>, which keeps any Unicode letter or digit --
        /// this text becomes a file name on four platforms.
        /// </summary>
        private static string SanitizeSlug(string slug)
        {
            if (string.IsNullOrEmpty(slug))
            {
                return FallbackSlug;
            }
            var text = new StringBuilder(slug.Length);
            foreach (char raw in slug)
            {
                char c = raw >= 'A' && raw <= 'Z' ? (char)(raw + ('a' - 'A')) : raw;
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    text.Append(c);
                }
                else if (text.Length > 0 && text[text.Length - 1] != '-')
                {
                    text.Append('-');
                }
            }
            string clean = text.ToString().TrimEnd('-');
            return clean.Length == 0 ? FallbackSlug : clean;
        }

        /// <summary>
        /// Write <paramref name="png"/> as <paramref name="name"/> under
        /// <see cref="SnapshotsRoot"/>, creating the folder when it is missing.
        /// Returns false (with the path set to null) when the platform refuses --
        /// a read-only container, a full disk, a revoked permission.
        /// </summary>
        private static bool TryWriteFile(byte[] png, string name, out string path)
        {
            path = null;
            try
            {
                string root = SnapshotsRoot;
                Directory.CreateDirectory(root);
                string file = Path.Combine(root, name);
                File.WriteAllBytes(file, png);
                path = file;
                Debug.Log("[HeatonCA] saved " + file);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[HeatonCA] PNG write failed: " + exception);
                return false;
            }
        }

        /// <summary>
        /// Show a just-written file to the user on desktop standalone: selected in a
        /// Finder window on macOS, in an Explorer window on Windows. A no-op in the
        /// Editor and on mobile and web, where the Files app, the photo library, and
        /// the download list already show it. Failures (no shell, a locked-down
        /// session) are logged and swallowed: the file is saved either way, and
        /// nothing about the save should depend on the file manager coming up.
        /// </summary>
        private static void Reveal(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            try
            {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
                // NSWorkspace by way of the native bundle: the Mac App Store build is
                // sandboxed and may not spawn /usr/bin/open, so `open -R` there fails
                // and the user sees nothing happen. Finder services this request from
                // its own process instead. The spawn stays as the fallback for a build
                // whose bundle did not load, which cannot happen in a store build.
                if (!NativeMenus.RevealInFinder(path))
                {
                    System.Diagnostics.Process.Start("open", "-R \"" + path + "\"");
                }
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
                System.Diagnostics.Process.Start(
                    "explorer.exe", "/select,\"" + path.Replace('/', '\\') + "\"");
#endif
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[HeatonCA] reveal failed: " + exception.Message);
            }
        }
    }
}
