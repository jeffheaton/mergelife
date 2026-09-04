using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HeatonCAApp
{
    /// <summary>
    /// The app's persistent store: small UTF-8 text documents (the evolve finds
    /// log today, whatever a later screen needs tomorrow) addressed by a
    /// path-like key such as "evolve-finds.json" or "finds/2026/best.json".
    ///
    /// It is an interface because storage is one of the port's platform seams.
    /// Desktop, iOS, and Android get a real folder under
    /// <c>Application.persistentDataPath</c>; WebGL has no durable file system,
    /// so the browser build keeps the same documents in PlayerPrefs, which Unity
    /// backs with IndexedDB. Every screen and service above this line writes
    /// text through <see cref="IAppStore"/> and never touches
    /// <c>System.IO</c> or <c>PlayerPrefs</c> itself, so nothing above has to
    /// know which platform it is running on.
    /// </summary>
    public interface IAppStore
    {
        /// <summary>True when the key holds a document.</summary>
        bool Exists(string key);

        /// <summary>The document's text, or null when the key holds nothing.</summary>
        string ReadText(string key);

        /// <summary>
        /// Store text under the key, replacing whatever was there. Null is
        /// stored as the empty string, so a written key always exists.
        /// </summary>
        void WriteText(string key, string text);

        /// <summary>Remove the key. Removing an absent key does nothing.</summary>
        void Delete(string key);

        /// <summary>
        /// Push pending writes to the underlying medium. A file store has
        /// nothing to do; the PlayerPrefs store needs this before the browser
        /// tab can close, which is why every caller must invoke it on quit,
        /// pause, and focus loss instead of assuming writes are durable.
        /// </summary>
        void Flush();

        /// <summary>
        /// Every key that begins with <paramref name="prefix"/> (null or empty
        /// means every key), ordinal-sorted so callers and tests see a stable
        /// order. Keys always read back with '/' separators, whatever the
        /// platform's own path separator is.
        /// </summary>
        IEnumerable<string> Keys(string prefix);
    }

    /// <summary>
    /// The store used everywhere a real file system exists: one file per key
    /// under <c>root</c>, with a key's '/' separators becoming folders, so the
    /// finds log lands at a path a developer (or the iOS Files app, which sees
    /// this folder) can open and read.
    ///
    /// Writes are not atomic — a crash between opening and closing the file can
    /// truncate a document. That is deliberate: the alternative (temp file plus
    /// rename) is not available in the same form on every target, and the one
    /// document that matters, the finds log, is re-written every couple of
    /// seconds and its loader already treats an unparsable file as empty.
    /// </summary>
    public sealed class FileStore : IAppStore
    {
        private readonly string _root;

        /// <summary>
        /// Create (or adopt) a store folder. The folder is made immediately so
        /// that a later <see cref="Keys"/> on an untouched store is empty rather
        /// than an error.
        /// </summary>
        public FileStore(string root)
        {
            if (string.IsNullOrEmpty(root))
            {
                throw new ArgumentException("a file store needs a root folder", nameof(root));
            }
            _root = Path.GetFullPath(root);
            Directory.CreateDirectory(_root);
        }

        /// <summary>The absolute folder this store writes into.</summary>
        public string Root => _root;

        /// <inheritdoc/>
        public bool Exists(string key) => File.Exists(PathFor(key));

        /// <inheritdoc/>
        public string ReadText(string key)
        {
            string path = PathFor(key);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        /// <inheritdoc/>
        public void WriteText(string key, string text)
        {
            string path = PathFor(key);
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }
            File.WriteAllText(path, text ?? string.Empty);
        }

        /// <inheritdoc/>
        public void Delete(string key)
        {
            string path = PathFor(key);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Nothing to do: <see cref="WriteText"/> already handed the bytes to
        /// the operating system.
        /// </summary>
        public void Flush()
        {
        }

        /// <inheritdoc/>
        public IEnumerable<string> Keys(string prefix)
        {
            var keys = new List<string>();
            if (!Directory.Exists(_root))
            {
                return keys; // the folder was removed underneath us
            }
            foreach (string path in Directory.GetFiles(_root, "*", SearchOption.AllDirectories))
            {
                string key = KeyFor(path);
                if (key != null && AppStore.HasPrefix(key, prefix))
                {
                    keys.Add(key);
                }
            }
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }

        /// <summary>
        /// The key a file under the root stands for, or null when the file is
        /// not one of ours — the operating system drops files such as
        /// .DS_Store into any folder it displays, and a key may not start with
        /// a dot, so those can never be mistaken for stored documents.
        /// </summary>
        private string KeyFor(string path)
        {
            if (path.Length <= _root.Length)
            {
                return null;
            }
            string key = path.Substring(_root.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
            foreach (string segment in key.Split('/'))
            {
                if (segment.Length == 0 || segment[0] == '.')
                {
                    return null;
                }
            }
            return key;
        }

        private string PathFor(string key)
        {
            AppStore.ValidateKey(key);
            return Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar));
        }
    }

    /// <summary>
    /// The WebGL store: each document is one PlayerPrefs string under
    /// <c>prefix + key</c>, which Unity persists to the browser's IndexedDB.
    ///
    /// PlayerPrefs cannot enumerate itself, so the store keeps its own index —
    /// a JSON array of the keys it holds — at the reserved key
    /// <see cref="AppStore.IndexKeyName"/>. The index is read back from
    /// PlayerPrefs on every call rather than cached, so two stores over the
    /// same prefix (the app's and a test's) can never disagree, and any entry
    /// whose PlayerPrefs value has gone (a browser that cleared site data, a
    /// <c>PlayerPrefs.DeleteAll</c>) is skipped instead of returned as a key
    /// that reads back null.
    ///
    /// The whole budget here is roughly a megabyte for every key the app
    /// stores, PlayerPrefs settings included, so callers keep documents small;
    /// AppStoreTests pins the finds log — the largest thing the app writes —
    /// far below that.
    /// </summary>
    public sealed class PrefsStore : IAppStore
    {
        private readonly string _prefix;
        private readonly string _indexKey;

        /// <summary>
        /// Create a store namespaced by <paramref name="prefix"/>, which is
        /// prepended to every key so the app's documents cannot collide with
        /// the PlayerPrefs entries <see cref="AppSettings"/> owns.
        /// </summary>
        public PrefsStore(string prefix)
        {
            _prefix = prefix ?? string.Empty;
            _indexKey = _prefix + AppStore.IndexKeyName;
        }

        /// <summary>The PlayerPrefs prefix every key of this store carries.</summary>
        public string Prefix => _prefix;

        /// <summary>The PlayerPrefs key holding this store's key index.</summary>
        public string IndexKey => _indexKey;

        /// <inheritdoc/>
        public bool Exists(string key)
        {
            AppStore.ValidateKey(key);
            return PlayerPrefs.HasKey(_prefix + key);
        }

        /// <inheritdoc/>
        public string ReadText(string key)
        {
            AppStore.ValidateKey(key);
            string full = _prefix + key;
            return PlayerPrefs.HasKey(full) ? PlayerPrefs.GetString(full) : null;
        }

        /// <inheritdoc/>
        public void WriteText(string key, string text)
        {
            AppStore.ValidateKey(key);
            PlayerPrefs.SetString(_prefix + key, text ?? string.Empty);
            List<string> index = ReadIndex();
            if (!index.Contains(key))
            {
                index.Add(key);
                WriteIndex(index);
            }
        }

        /// <inheritdoc/>
        public void Delete(string key)
        {
            AppStore.ValidateKey(key);
            PlayerPrefs.DeleteKey(_prefix + key);
            List<string> index = ReadIndex();
            if (index.Remove(key))
            {
                WriteIndex(index);
            }
        }

        /// <summary>
        /// Write PlayerPrefs through to IndexedDB. On WebGL nothing is durable
        /// until this runs, so the app calls it from the unload hook the
        /// browser gives us instead of OnApplicationQuit, which never fires
        /// there.
        /// </summary>
        public void Flush()
        {
            PlayerPrefs.Save();
        }

        /// <inheritdoc/>
        public IEnumerable<string> Keys(string prefix)
        {
            var keys = new List<string>();
            foreach (string key in ReadIndex())
            {
                if (AppStore.HasPrefix(key, prefix) && PlayerPrefs.HasKey(_prefix + key))
                {
                    keys.Add(key);
                }
            }
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }

        /// <summary>
        /// The stored key index. An index that will not parse (hand-edited, or
        /// written by a future schema) is reported once and treated as empty:
        /// the documents themselves are still readable by key, and the next
        /// write rebuilds the index.
        /// </summary>
        private List<string> ReadIndex()
        {
            var index = new List<string>();
            string json = PlayerPrefs.GetString(_indexKey, string.Empty);
            if (json.Length == 0)
            {
                return index;
            }
            try
            {
                foreach (object item in AppJson.Arr(AppJson.Parse(json)))
                {
                    if (item is string key && key.Length > 0 && !index.Contains(key))
                    {
                        index.Add(key);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[HeatonCA] the storage index was unreadable and is being rebuilt: "
                    + exception.Message);
                index.Clear();
            }
            return index;
        }

        private void WriteIndex(List<string> index)
        {
            var items = new List<object>(index.Count);
            foreach (string key in index)
            {
                items.Add(key);
            }
            PlayerPrefs.SetString(_indexKey, AppJson.Write(items));
        }
    }

    /// <summary>
    /// Storage seam and shared key rules: <see cref="CreateDefault"/> picks the
    /// store this platform can actually persist to, and
    /// <see cref="ValidateKey"/> holds every key to the same contract on every
    /// platform, so a key that works on a Mac cannot fail in a browser or on
    /// Windows.
    ///
    /// The platform choice is a runtime test rather than a compile-time
    /// platform define because <c>Application.platform</c> already answers it,
    /// and the Editor then behaves like the desktop build it stands in for.
    /// </summary>
    public static class AppStore
    {
        /// <summary>PlayerPrefs prefix the browser build stores documents under.</summary>
        public const string WebGlPrefix = "heatonca.store.";

        /// <summary>
        /// Reserved key: <see cref="PrefsStore"/> keeps its key index here, so
        /// no store may hold a document by this name. Rejected on every
        /// platform, not just WebGL, so the rule cannot surprise a browser user
        /// with a key that worked everywhere else.
        /// </summary>
        public const string IndexKeyName = "__keys";

        /// <summary>
        /// Longest key the store accepts. Well inside every platform's path and
        /// PlayerPrefs-name limits, and far longer than the keys the app uses.
        /// </summary>
        public const int MaxKeyLength = 200;

        // Characters no key may contain: illegal in a Windows file name, and
        // ':' would also start a macOS-hostile or NTFS alternate-data-stream
        // path. '/' is legal and separates a key into folders.
        private const string ForbiddenCharacters = "\\:*?\"<>|";

        /// <summary>
        /// The store this build persists to: PlayerPrefs (IndexedDB) in a
        /// browser, a folder under <c>Application.persistentDataPath</c>
        /// everywhere else. Pass <paramref name="root"/> to force a file store
        /// somewhere else — the PlayMode suite points it at a throwaway folder
        /// through <c>AppController.UseStorageRoot</c> so a test run never
        /// touches the user's real finds.
        /// </summary>
        public static IAppStore CreateDefault(string root = null)
        {
            if (!string.IsNullOrEmpty(root))
            {
                return new FileStore(root);
            }
            return Application.platform == RuntimePlatform.WebGLPlayer
                ? (IAppStore)new PrefsStore(WebGlPrefix)
                : new FileStore(Application.persistentDataPath);
        }

        /// <summary>
        /// Throw unless the key is one every store can hold: a non-empty,
        /// bounded, '/'-separated name whose segments are legal file names on
        /// every desktop platform and cannot walk out of the store folder.
        /// These messages are for developers, not users, so they do not go
        /// through AppStrings.
        /// </summary>
        public static void ValidateKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("a store key may not be empty", nameof(key));
            }
            if (key.Length > MaxKeyLength)
            {
                throw new ArgumentException(
                    $"store key '{key}' is longer than {MaxKeyLength} characters", nameof(key));
            }
            if (key == IndexKeyName)
            {
                throw new ArgumentException(
                    $"store key '{IndexKeyName}' is reserved for the key index", nameof(key));
            }
            foreach (char character in key)
            {
                if (character < ' ' || character == (char)0x7f
                    || ForbiddenCharacters.IndexOf(character) >= 0)
                {
                    throw new ArgumentException(
                        $"store key '{key}' contains a character no file name may hold", nameof(key));
                }
            }
            foreach (string segment in key.Split('/'))
            {
                if (segment.Length == 0)
                {
                    throw new ArgumentException(
                        $"store key '{key}' has an empty path segment", nameof(key));
                }
                if (segment[0] == '.')
                {
                    // Blocks "." and ".." (escaping the root) and hidden files
                    // in one rule, which is also what lets a file store tell
                    // its own documents from a stray .DS_Store.
                    throw new ArgumentException(
                        $"store key '{key}' has a path segment starting with '.'", nameof(key));
                }
                char last = segment[segment.Length - 1];
                if (last == '.' || last == ' ')
                {
                    throw new ArgumentException(
                        $"store key '{key}' has a path segment ending in '.' or a space", nameof(key));
                }
            }
        }

        /// <summary>
        /// Whether a key belongs to a <c>Keys(prefix)</c> listing: an empty or
        /// null prefix lists everything, and matching is ordinal because keys
        /// are identifiers, not text in the user's language.
        /// </summary>
        internal static bool HasPrefix(string key, string prefix)
        {
            return string.IsNullOrEmpty(prefix) || key.StartsWith(prefix, StringComparison.Ordinal);
        }
    }
}
