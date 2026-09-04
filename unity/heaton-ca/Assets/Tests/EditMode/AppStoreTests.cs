using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HeatonCAApp;
using NUnit.Framework;
using UnityEngine;

namespace HeatonCA.Tests
{
    /// <summary>
    /// The storage seam: a file store and a PlayerPrefs store that must behave
    /// the same way, because the app writes the same documents through both and
    /// only the platform decides which one it gets.
    ///
    /// The file tests work in a throwaway folder under the system temp
    /// directory. PlayerPrefs is machine-global, so the prefs tests use their
    /// own test prefix, save every key they can touch before they run, and put
    /// the developer's values back afterwards — the same protocol
    /// AppSettingsTests follows.
    /// </summary>
    public class AppStoreTests
    {
        /// <summary>Prefix for the prefs store under test; never the app's own.</summary>
        private const string TestPrefix = "heatonca.tests.store.";

        /// <summary>Every key name the prefs tests below write, deleted or not.</summary>
        private static readonly string[] PrefsKeys =
        {
            "alpha",
            "beta",
            "gone",
            "evolve-finds.json",
            "finds/2026/best.json",
            "notes/a",
            "notes/b",
        };

        private readonly List<string> _savedNames = new List<string>();
        private readonly List<string> _savedValues = new List<string>();
        private string _root;

        [SetUp]
        public void CreateRootAndSavePlayerPrefs()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "heatonca-appstore-" + Guid.NewGuid().ToString("N"));
            _savedNames.Clear();
            _savedValues.Clear();
            foreach (string name in FullPrefsNames())
            {
                _savedNames.Add(name);
                _savedValues.Add(PlayerPrefs.HasKey(name) ? PlayerPrefs.GetString(name) : null);
                // Start every test from an empty store, whatever an interrupted
                // earlier run left behind; TearDown puts the saved values back.
                PlayerPrefs.DeleteKey(name);
            }
        }

        [TearDown]
        public void RestorePlayerPrefsAndDeleteRoot()
        {
            for (int i = 0; i < _savedNames.Count; i++)
            {
                if (_savedValues[i] == null)
                {
                    PlayerPrefs.DeleteKey(_savedNames[i]);
                }
                else
                {
                    PlayerPrefs.SetString(_savedNames[i], _savedValues[i]);
                }
            }
            PlayerPrefs.Save();
            if (_root != null && Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        /// <summary>Every PlayerPrefs name the prefs tests touch, index included.</summary>
        private static List<string> FullPrefsNames()
        {
            var names = new List<string> { TestPrefix + AppStore.IndexKeyName };
            foreach (string key in PrefsKeys)
            {
                names.Add(TestPrefix + key);
            }
            return names;
        }

        // ---- FileStore ------------------------------------------------------------------

        [Test]
        public void FileStoreRoundTripsTextThroughExistsReadAndDelete()
        {
            var store = new FileStore(_root);

            Assert.IsFalse(store.Exists("alpha"));
            Assert.IsNull(store.ReadText("alpha"));

            store.WriteText("alpha", "one");

            Assert.IsTrue(store.Exists("alpha"));
            Assert.AreEqual("one", store.ReadText("alpha"));
            Assert.AreEqual("one", File.ReadAllText(Path.Combine(_root, "alpha")));

            store.WriteText("alpha", "two");
            Assert.AreEqual("two", store.ReadText("alpha"));

            store.Delete("alpha");
            Assert.IsFalse(store.Exists("alpha"));
            Assert.IsNull(store.ReadText("alpha"));
            store.Delete("alpha"); // deleting what is not there is not an error
            store.Flush();
        }

        [Test]
        public void FileStoreCreatesFoldersForNestedKeysAndReadsThemBackWithSlashes()
        {
            var store = new FileStore(_root);

            store.WriteText("finds/2026/best.json", "{}");
            store.WriteText("evolve-finds.json", "[]");

            Assert.IsTrue(File.Exists(Path.Combine(_root, "finds", "2026", "best.json")));
            Assert.AreEqual("{}", store.ReadText("finds/2026/best.json"));
            CollectionAssert.AreEqual(
                new[] { "evolve-finds.json", "finds/2026/best.json" },
                new List<string>(store.Keys(null)));
        }

        [Test]
        public void FileStoreKeysFilterByPrefix()
        {
            var store = new FileStore(_root);
            store.WriteText("notes/b", "b");
            store.WriteText("notes/a", "a");
            store.WriteText("evolve-finds.json", "{}");

            CollectionAssert.AreEqual(
                new[] { "notes/a", "notes/b" }, new List<string>(store.Keys("notes/")));
            CollectionAssert.AreEqual(
                new[] { "evolve-finds.json" }, new List<string>(store.Keys("evolve")));
            CollectionAssert.IsEmpty(new List<string>(store.Keys("gallery/")));
            CollectionAssert.AreEqual(
                new[] { "evolve-finds.json", "notes/a", "notes/b" },
                new List<string>(store.Keys(string.Empty)));
        }

        [Test]
        public void FileStoreKeepsTextExactlyIncludingUnicodeAndNewlines()
        {
            var store = new FileStore(_root);
            // Spelled as escapes so this file stays plain ASCII: alpha, beta, gamma.
            string text = "{\"note\":\"\u03b1 \u03b2 \u03b3\"}\nline two\n";

            store.WriteText("notes/a", text);

            Assert.AreEqual(text, store.ReadText("notes/a"));
            Assert.AreEqual(
                text, File.ReadAllText(Path.Combine(_root, "notes", "a"), Encoding.UTF8));
        }

        [Test]
        public void FileStoreIgnoresFilesTheOperatingSystemDrops()
        {
            var store = new FileStore(_root);
            store.WriteText("notes/a", "a");
            File.WriteAllText(Path.Combine(_root, ".DS_Store"), "junk");
            File.WriteAllText(Path.Combine(_root, "notes", ".hidden"), "junk");

            CollectionAssert.AreEqual(new[] { "notes/a" }, new List<string>(store.Keys(null)));
        }

        [Test]
        public void FileStoreStoresNothingOutsideItsRoot()
        {
            var store = new FileStore(_root);
            string outside = Path.Combine(Path.GetDirectoryName(_root), "escaped.json");

            Assert.Throws<ArgumentException>(() => store.WriteText("../escaped.json", "x"));
            Assert.Throws<ArgumentException>(() => store.WriteText("finds/../../escaped.json", "x"));
            Assert.IsFalse(File.Exists(outside));
        }

        [Test]
        public void StoresRejectTheSameBadKeysOnEveryPlatform()
        {
            var file = new FileStore(_root);
            var prefs = new PrefsStore(TestPrefix);
            string[] bad =
            {
                null,
                "",
                "/leading",
                "trailing/",
                "double//slash",
                "..",
                ".hidden",
                "back\\slash",
                "colon:name",
                "star*name",
                "trailing.",
                "trailing ",
                "new\nline",
                AppStore.IndexKeyName,
                new string('k', AppStore.MaxKeyLength + 1),
            };

            foreach (string key in bad)
            {
                Assert.Throws<ArgumentException>(() => AppStore.ValidateKey(key), key ?? "null");
                Assert.Throws<ArgumentException>(() => file.WriteText(key, "x"), key ?? "null");
                Assert.Throws<ArgumentException>(() => prefs.WriteText(key, "x"), key ?? "null");
                Assert.Throws<ArgumentException>(() => file.Exists(key), key ?? "null");
                Assert.Throws<ArgumentException>(() => prefs.Exists(key), key ?? "null");
            }
            CollectionAssert.IsEmpty(new List<string>(file.Keys(null)));
            CollectionAssert.IsEmpty(new List<string>(prefs.Keys(null)));
        }

        [Test]
        public void FileStoreCreatesItsRootAndRejectsAnEmptyOne()
        {
            string nested = Path.Combine(_root, "deeper", "still");

            var store = new FileStore(nested);

            Assert.IsTrue(Directory.Exists(nested));
            CollectionAssert.IsEmpty(new List<string>(store.Keys(null)));
            Assert.Throws<ArgumentException>(() => new FileStore(null));
            Assert.Throws<ArgumentException>(() => new FileStore(string.Empty));
        }

        // ---- PrefsStore -----------------------------------------------------------------

        [Test]
        public void PrefsStoreRoundTripsTextThroughExistsReadAndDelete()
        {
            var store = new PrefsStore(TestPrefix);

            Assert.IsFalse(store.Exists("alpha"));
            Assert.IsNull(store.ReadText("alpha"));

            store.WriteText("alpha", "one");

            Assert.IsTrue(store.Exists("alpha"));
            Assert.AreEqual("one", store.ReadText("alpha"));
            Assert.AreEqual("one", PlayerPrefs.GetString(TestPrefix + "alpha"));

            store.WriteText("alpha", "two");
            Assert.AreEqual("two", store.ReadText("alpha"));

            store.Delete("alpha");
            Assert.IsFalse(store.Exists("alpha"));
            Assert.IsNull(store.ReadText("alpha"));
            Assert.IsFalse(PlayerPrefs.HasKey(TestPrefix + "alpha"));
            store.Delete("alpha"); // deleting what is not there is not an error
            store.Flush();
        }

        [Test]
        public void PrefsStoreKeysComeFromAnIndexANewStoreCanRead()
        {
            var writer = new PrefsStore(TestPrefix);
            writer.WriteText("notes/b", "b");
            writer.WriteText("notes/a", "a");
            writer.WriteText("evolve-finds.json", "{}");
            writer.WriteText("notes/a", "again"); // a rewrite must not duplicate the index entry

            var reader = new PrefsStore(TestPrefix);

            Assert.AreEqual(TestPrefix + AppStore.IndexKeyName, reader.IndexKey);
            Assert.IsTrue(PlayerPrefs.HasKey(reader.IndexKey));
            CollectionAssert.AreEqual(
                new[] { "evolve-finds.json", "notes/a", "notes/b" },
                new List<string>(reader.Keys(null)));
            CollectionAssert.AreEqual(
                new[] { "notes/a", "notes/b" }, new List<string>(reader.Keys("notes/")));
            CollectionAssert.IsEmpty(new List<string>(reader.Keys("gallery/")));
            Assert.AreEqual("again", reader.ReadText("notes/a"));

            reader.Delete("notes/a");
            CollectionAssert.AreEqual(
                new[] { "evolve-finds.json", "notes/b" },
                new List<string>(new PrefsStore(TestPrefix).Keys(null)));
        }

        [Test]
        public void PrefsStoreSkipsIndexEntriesWhoseValuesAreGone()
        {
            var store = new PrefsStore(TestPrefix);
            store.WriteText("alpha", "one");
            store.WriteText("gone", "two");

            // What a browser that cleared its site data leaves behind: the index
            // still names the key, the value no longer exists.
            PlayerPrefs.DeleteKey(TestPrefix + "gone");

            CollectionAssert.AreEqual(new[] { "alpha" }, new List<string>(store.Keys(null)));
            Assert.IsFalse(store.Exists("gone"));
            Assert.IsNull(store.ReadText("gone"));
        }

        [Test]
        public void PrefsStoreRebuildsAnUnreadableIndex()
        {
            var store = new PrefsStore(TestPrefix);
            store.WriteText("alpha", "one");
            PlayerPrefs.SetString(store.IndexKey, "not json at all");

            // The store warns and carries on: an index it cannot read must never
            // cost the user the documents themselves.
            CollectionAssert.IsEmpty(new List<string>(store.Keys(null)));
            Assert.AreEqual("one", store.ReadText("alpha")); // documents are still addressable

            store.WriteText("beta", "two");

            CollectionAssert.AreEqual(new[] { "beta" }, new List<string>(store.Keys(null)));
        }

        // ---- the seam and the size budget -----------------------------------------------

        [Test]
        public void CreateDefaultIsAFileStoreOutsideTheBrowser()
        {
            // EditMode never runs as RuntimePlatform.WebGLPlayer, so this is the
            // desktop and mobile branch of the seam.
            Assert.IsInstanceOf<FileStore>(AppStore.CreateDefault());

            IAppStore injected = AppStore.CreateDefault(_root);

            Assert.IsInstanceOf<FileStore>(injected);
            Assert.AreEqual(Path.GetFullPath(_root), ((FileStore)injected).Root);
            injected.WriteText("alpha", "one");
            Assert.IsTrue(File.Exists(Path.Combine(_root, "alpha")));
        }

        /// <summary>
        /// The one document that could ever grow: the evolve finds log at its
        /// hard cap of 60 discoveries, with a tombstone list on top. Unity's
        /// WebGL PlayerPrefs live in a single IndexedDB record with roughly a
        /// one-megabyte budget shared by every key the app stores, so the log
        /// has to stay a rounding error against it. This pins it under 64 KB --
        /// more than an order of magnitude of headroom — and fails loudly if a
        /// future schema starts storing per-find payloads (thumbnails, seeds,
        /// population dumps) that would put the browser build at risk.
        /// </summary>
        [Test]
        public void StoreSizeStaysUnderWebGlCap()
        {
            const int findsCap = 60;
            const int tombstones = 20;
            const int webGlBudget = 64 * 1024;

            string json = AppJson.Write(BuildFindsLog(findsCap, tombstones));
            int bytes = Encoding.UTF8.GetByteCount(json);

            Assert.Less(
                bytes, webGlBudget,
                $"a {findsCap}-find log serialized to {bytes} bytes, over the {webGlBudget} byte budget");

            // The same payload has to survive both stores unchanged.
            var file = new FileStore(_root);
            file.WriteText("evolve-finds.json", json);
            Assert.AreEqual(json, file.ReadText("evolve-finds.json"));

            var prefs = new PrefsStore(TestPrefix);
            prefs.WriteText("evolve-finds.json", json);
            Assert.AreEqual(json, prefs.ReadText("evolve-finds.json"));
            CollectionAssert.AreEqual(
                new[] { "evolve-finds.json" }, new List<string>(prefs.Keys(null)));

            Dictionary<string, object> parsed = AppJson.Obj(AppJson.Parse(json));
            Assert.AreEqual(1, AppJson.Int(parsed["schema"]));
            Assert.AreEqual(findsCap, AppJson.Int(parsed["totalFound"]));
            Assert.AreEqual(findsCap, AppJson.Arr(parsed["finds"]).Count);
            Assert.AreEqual(tombstones, AppJson.Arr(parsed["deleted"]).Count);
        }

        /// <summary>
        /// A worst-case finds log in the schema WP2.4 persists: every score a
        /// full-precision double (the longest way a number can serialize), every
        /// rule a distinct 32-hex dashed string, and a tombstone list of deleted
        /// rules.
        /// </summary>
        private static Dictionary<string, object> BuildFindsLog(int findCount, int deletedCount)
        {
            var finds = new List<object>(findCount);
            var rules = new HashSet<string>();
            for (int i = 0; i < findCount; i++)
            {
                string rule = RuleAt(i);
                rules.Add(rule);
                finds.Add(new Dictionary<string, object>
                {
                    ["rule"] = rule,
                    ["score"] = 3.5 + i * 0.0123456789012345,
                    ["foundAtEval"] = 12345 + i * 98765,
                    ["run"] = i % 97 + 1,
                });
            }
            Assert.AreEqual(findCount, rules.Count, "the generated rules must all differ");
            var deleted = new List<object>(deletedCount);
            for (int i = 0; i < deletedCount; i++)
            {
                deleted.Add(RuleAt(findCount + i));
            }
            return new Dictionary<string, object>
            {
                ["schema"] = 1,
                ["totalFound"] = findCount,
                ["finds"] = finds,
                ["deleted"] = deleted,
            };
        }

        /// <summary>A distinct, well-formed rule string for index i.</summary>
        private static string RuleAt(int index)
        {
            var rule = new StringBuilder(39);
            for (int group = 0; group < 8; group++)
            {
                if (group > 0)
                {
                    rule.Append('-');
                }
                uint mixed = (uint)(index * 8 + group + 1) * 2654435761u;
                mixed ^= mixed >> 13;
                rule.Append((mixed & 0xffff).ToString("x4", CultureInfo.InvariantCulture));
            }
            return rule.ToString();
        }
    }
}
