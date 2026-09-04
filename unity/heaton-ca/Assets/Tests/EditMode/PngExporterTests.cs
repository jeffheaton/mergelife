using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using HeatonCAApp;
using NUnit.Framework;
using UnityEngine;

namespace HeatonCA.Tests
{
    /// <summary>
    /// PngExporter's file branch and its snapshot names. The Editor takes the
    /// #else branch of the export seam (no photo library, no browser download, no
    /// Finder reveal), which is exactly the branch the desktop players take, so
    /// these tests pin what macOS and Windows do. SnapshotsRoot is redirected at a
    /// temp folder for the run and restored afterwards -- the real root is the
    /// developer's own persistent data path.
    /// </summary>
    public class PngExporterTests
    {
        /// <summary>An 8-byte payload: the PNG signature, enough to prove the bytes round-trip.</summary>
        private static readonly byte[] Payload = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private string _previousRoot;
        private string _root;

        [SetUp]
        public void RedirectSnapshotsRoot()
        {
            _previousRoot = PngExporter.SnapshotsRoot;
            _root = Path.Combine(
                Path.GetTempPath(), "heatonca-tests-" + Guid.NewGuid().ToString("N"));
            PngExporter.SnapshotsRoot = _root;
        }

        [TearDown]
        public void RestoreSnapshotsRoot()
        {
            PngExporter.SnapshotsRoot = _previousRoot;
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void SaveWritesThePngAndReportsTheName()
        {
            string status = null;
            const string name = "heatonca-e542-5f79-20260902-140301-123.png";

            bool saved = PngExporter.Save(Payload, name, text => status = text);

            Assert.IsTrue(saved);
            string file = Path.Combine(_root, name);
            Assert.IsTrue(File.Exists(file), file);
            CollectionAssert.AreEqual(Payload, File.ReadAllBytes(file));
            Assert.AreEqual(string.Format(AppStrings.SimStatusSavedFormat, name), status);
        }

        [Test]
        public void SaveCreatesTheRootFolderWhenItIsMissing()
        {
            Assert.IsFalse(Directory.Exists(_root), "the temp root starts out absent");

            Assert.IsTrue(PngExporter.Save(Payload, "made-up.png", null));

            Assert.IsTrue(Directory.Exists(_root));
            Assert.IsTrue(File.Exists(Path.Combine(_root, "made-up.png")));
        }

        [Test]
        public void SaveOverwritesAnExistingFile()
        {
            const string name = "twice.png";
            Assert.IsTrue(PngExporter.Save(new byte[] { 1, 2, 3, 4 }, name, null));

            Assert.IsTrue(PngExporter.Save(Payload, name, null));

            CollectionAssert.AreEqual(Payload, File.ReadAllBytes(Path.Combine(_root, name)));
        }

        [Test]
        public void SaveRefusesEmptyBytesOrNames()
        {
            string status = null;
            Action<string> report = text => status = text;

            Assert.IsFalse(PngExporter.Save(null, "a.png", report));
            Assert.IsNotNull(status, "the user is told nothing was written");
            Assert.IsFalse(PngExporter.Save(new byte[0], "a.png", report));
            Assert.IsFalse(PngExporter.Save(Payload, null, report));
            Assert.IsFalse(PngExporter.Save(Payload, "", report));
            Assert.IsFalse(PngExporter.Save(Payload, "some/folder/", report));

            Assert.IsFalse(Directory.Exists(_root), "nothing was written");
        }

        [Test]
        public void SaveKeepsTheNameInsideTheSnapshotsRoot()
        {
            Assert.IsTrue(PngExporter.Save(Payload, "../escape.png", null));

            Assert.IsTrue(File.Exists(Path.Combine(_root, "escape.png")));
            Assert.IsFalse(
                File.Exists(Path.Combine(Path.GetDirectoryName(_root) ?? ".", "escape.png")),
                "a directory component in the file name must not move the file");
        }

        [Test]
        public void SaveToleratesANullStatusCallback()
        {
            Assert.IsTrue(PngExporter.Save(Payload, "quiet.png", null));
            Assert.IsFalse(PngExporter.Save(null, "quiet.png", null));
        }

        [Test]
        public void SnapshotsRootDefaultsUnderPersistentDataPath()
        {
            PngExporter.SnapshotsRoot = null;

            Assert.AreEqual(
                Path.Combine(Application.persistentDataPath, "Snapshots"),
                PngExporter.SnapshotsRoot);
        }

        [Test]
        public void SnapshotFileNameStampsTheSlugAndTime()
        {
            var utc = new DateTime(2026, 9, 2, 14, 3, 1, 123, DateTimeKind.Utc);

            string name = PngExporter.SnapshotFileName(
                "e542-5f79-9341-f31e-6c6b-7f08-8773-7068", utc);

            Assert.AreEqual(
                "heatonca-e542-5f79-9341-f31e-6c6b-7f08-8773-7068-20260902-140301-123.png", name);
        }

        [Test]
        public void SnapshotFileNameMatchesThePattern()
        {
            var pattern = new Regex(@"^heatonca-[a-z0-9]+(-[a-z0-9]+)*-\d{8}-\d{6}-\d{3}\.png$");

            foreach (string slug in new[]
            {
                "e542-5f79-9341-f31e-6c6b-7f08-8773-7068",
                "Diamonds Are Forever!",
                "  ",
                "",
                null,
                "ÜBER_rule 7",
            })
            {
                string name = PngExporter.SnapshotFileName(slug, DateTime.UtcNow);
                Assert.IsTrue(pattern.IsMatch(name), name);
            }
        }

        [Test]
        public void SnapshotFileNameSanitizesTheSlug()
        {
            var utc = new DateTime(2026, 1, 2, 3, 4, 5, 6, DateTimeKind.Utc);
            const string stamp = "20260102-030405-006";

            Assert.AreEqual(
                "heatonca-diamonds-are-forever-" + stamp + ".png",
                PngExporter.SnapshotFileName("Diamonds Are Forever!", utc));
            Assert.AreEqual(
                "heatonca-rule-7-" + stamp + ".png",
                PngExporter.SnapshotFileName("__rule__7__", utc));
            Assert.AreEqual(
                "heatonca-snapshot-" + stamp + ".png",
                PngExporter.SnapshotFileName(null, utc));
            Assert.AreEqual(
                "heatonca-snapshot-" + stamp + ".png",
                PngExporter.SnapshotFileName("///", utc));
        }

        [Test]
        public void SnapshotFileNameIsInvariantAndUtc()
        {
            var utc = new DateTime(2026, 12, 31, 23, 59, 58, 7, DateTimeKind.Utc);
            string fromUtc = PngExporter.SnapshotFileName("rule", utc);
            string fromLocal = PngExporter.SnapshotFileName("rule", utc.ToLocalTime());

            Assert.AreEqual("heatonca-rule-20261231-235958-007.png", fromUtc);
            Assert.AreEqual(fromUtc, fromLocal, "a local time is converted, not restamped");
            Assert.AreEqual(
                fromUtc, "heatonca-rule-" + utc.ToString(
                    "yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + ".png");
        }

        [Test]
        public void SavedSnapshotNameSurvivesARoundTrip()
        {
            string name = PngExporter.SnapshotFileName("e542-5f79", DateTime.UtcNow);
            string status = null;

            Assert.IsTrue(PngExporter.Save(Payload, name, text => status = text));

            Assert.IsTrue(File.Exists(Path.Combine(_root, name)));
            Assert.AreEqual(string.Format(AppStrings.SimStatusSavedFormat, name), status);
        }
    }
}
