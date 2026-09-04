using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HeatonCA.Engine;
using HeatonCAApp;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HeatonCA.PlayTests
{
    /// <summary>
    /// The Simulator screen, driven the way a user drives it: through the real
    /// buttons, the real rule field, and the real preset dropdown, with a
    /// recording navigator standing in for the controller.
    ///
    /// Two things are stubbed and nothing else. The image area is pinned to an
    /// explicit device-pixel size (<c>Open(rule, autoStart, w, h)</c> and
    /// <c>Resize</c>), because a headless run has no window to resize and the
    /// re-cut assertions need an exact lattice; and PngExporter's snapshot root
    /// points at a throwaway folder, because Save PNG really does write a file.
    /// </summary>
    public class SimulatorViewTests
    {
        /// <summary>Image area for most tests, in device pixels.</summary>
        private const float AreaWidth = 480f;

        /// <summary>Image area for most tests, in device pixels.</summary>
        private const float AreaHeight = 320f;

        /// <summary>Beetle Meadow: a gallery rule that is not the default, for the rule field.</summary>
        private const string BeetleMeadow = "ea44-55df-9025-bead-5f6e-45ca-6168-275a";

        /// <summary>Brushfire, undashed and uppercase — a rule as it travels in email.</summary>
        private const string BrushfireUndashed = "6EB6BA3D70B4AC6FBAAE260485298998";

        /// <summary>Brushfire, canonical.</summary>
        private const string Brushfire = "6eb6-ba3d-70b4-ac6f-baae-2604-8529-8998";

        private RecordingNavigator _nav;
        private AppServices _services;
        private SimulatorView _view;
        private string _previousSnapshotsRoot;
        private string _snapshotsRoot;

        [SetUp]
        public void RedirectSnapshotsRoot()
        {
            _previousSnapshotsRoot = PngExporter.SnapshotsRoot;
            _snapshotsRoot = Path.Combine(
                Path.GetTempPath(), "heatonca-simview-" + Guid.NewGuid().ToString("N"));
            PngExporter.SnapshotsRoot = _snapshotsRoot;
        }

        [TearDown]
        public void DestroyView()
        {
            if (_view != null && _view.Root != null)
            {
                // Immediate, not deferred: the view unsubscribes from the static
                // AppSettings.Changed event the first time it notices its root is
                // gone, and the next test's boot raises that event.
                UnityEngine.Object.DestroyImmediate(_view.Root);
            }
            _view = null;
            _services?.Blitter?.Dispose();
            _services = null;
            _nav = null;
            PngExporter.SnapshotsRoot = _previousSnapshotsRoot;
            if (!string.IsNullOrEmpty(_snapshotsRoot) && Directory.Exists(_snapshotsRoot))
            {
                Directory.Delete(_snapshotsRoot, true);
            }
        }

        [UnityTest]
        public IEnumerator StartStopStepResetSemantics()
        {
            yield return BootAndOpen(GalleryCatalog.DefaultRule, autoStart: false);

            // PyQt tab_simulate: the view opens stopped, so Stop is the one
            // button that is dead.
            Assert.IsTrue(_view.StartButton.interactable, "Start on open");
            Assert.IsFalse(_view.StopButton.interactable, "Stop on open");
            Assert.IsTrue(_view.StepButton.interactable, "Step on open");
            Assert.IsFalse(_services.Host.Playing, "the world starts paused");
            Assert.AreEqual(0, _services.Host.Generation);

            _view.StartButton.onClick.Invoke();
            Assert.IsFalse(_view.StartButton.interactable, "Start disables itself");
            Assert.IsTrue(_view.StopButton.interactable, "Start enables Stop");
            Assert.IsTrue(_services.Host.Playing, "Start runs the transport");

            _view.StopButton.onClick.Invoke();
            Assert.IsTrue(_view.StartButton.interactable, "Stop re-enables Start");
            Assert.IsFalse(_view.StopButton.interactable, "Stop disables itself");
            Assert.IsTrue(_view.StepButton.interactable, "Stop re-enables Step");
            Assert.IsFalse(_services.Host.Playing, "Stop pauses the transport");

            _view.StepButton.onClick.Invoke();
            _view.StepButton.onClick.Invoke();
            Assert.AreEqual(2, _services.Host.Generation, "Step advances one generation each");
            Assert.IsFalse(_services.Host.Playing, "Step does not start playback");

            byte[] before = World().Frame();
            _view.ResetButton.onClick.Invoke();
            Assert.AreEqual(0, _services.Host.Generation, "Reset zeroes the step counter");
            CollectionAssert.AreNotEqual(before, World().Frame(), "Reset re-seeds the lattice");
            Assert.IsTrue(_view.StartButton.interactable, "Reset leaves the transport stopped");
            Assert.IsFalse(_view.StopButton.interactable);
        }

        [UnityTest]
        public IEnumerator StepDisabledWhileRunning()
        {
            yield return BootAndOpen(GalleryCatalog.DefaultRule, autoStart: false);

            _view.StartButton.onClick.Invoke();

            Assert.IsFalse(
                _view.StepButton.interactable,
                "PyQt start_game disables Step: single-stepping a running world is meaningless");

            _view.StopButton.onClick.Invoke();
            Assert.IsTrue(_view.StepButton.interactable, "and stop_game hands it back");
        }

        [UnityTest]
        public IEnumerator GridFillsViewportAndRecutsOnResize()
        {
            yield return BootAndOpen(GalleryCatalog.DefaultRule, autoStart: false, AreaWidth, AreaHeight);

            GridSpec expected = GridFor(AreaWidth, AreaHeight);
            Assert.AreEqual(expected.Cols, _view.Cols, "columns fill the area at whole cells");
            Assert.AreEqual(expected.Rows, _view.Rows, "rows fill the area at whole cells");
            Assert.AreEqual(expected.Cols, World().Width);
            Assert.AreEqual(expected.Rows, World().Height);

            // Never stretched: the drawn rect is cols x rows square cells, so its
            // aspect ratio is exactly the lattice's (PyQt used IgnoreAspectRatio).
            var imageRt = (RectTransform)_view.LatticeImage.transform;
            Assert.AreEqual(
                expected.Cols / (float)expected.Rows,
                imageRt.sizeDelta.x / imageRt.sizeDelta.y,
                1e-3f,
                "cells must stay square");

            _view.StepButton.onClick.Invoke();
            _view.StepButton.onClick.Invoke();
            _view.StepButton.onClick.Invoke();
            int generation = _services.Host.Generation;
            int oldCols = World().Width;
            int oldRows = World().Height;
            byte[] oldCells = World().Frame();

            // Wider: the overlap survives and the world keeps counting, where
            // PyQt's resize re-seeded from scratch.
            _view.Resize(AreaWidth * 2f, AreaHeight);

            GridSpec grown = GridFor(AreaWidth * 2f, AreaHeight);
            Assert.AreEqual(grown.Cols, _view.Cols, "a wider area re-cuts wider");
            Assert.Greater(grown.Cols, expected.Cols, "the two canvas sizes must differ");
            Assert.AreEqual(generation, _services.Host.Generation, "the generation survives a re-cut");
            AssertOverlapPreserved(oldCells, oldCols, oldRows, World());

            int grownCols = World().Width;
            int grownRows = World().Height;
            byte[] grownCells = World().Frame();

            // Narrower again: a shrink is a pure crop.
            _view.Resize(AreaWidth, AreaHeight);

            Assert.AreEqual(expected.Cols, _view.Cols, "back to the original cut");
            Assert.AreEqual(generation, _services.Host.Generation);
            AssertOverlapPreserved(grownCells, grownCols, grownRows, World());
        }

        [UnityTest]
        public IEnumerator RuleFieldCanonicalizesAndRejects()
        {
            yield return BootAndOpen(GalleryCatalog.DefaultRule, autoStart: false);

            Assert.AreEqual(GalleryCatalog.DefaultRule, _view.RuleField.text, "the field shows the rule");

            // Uppercase with spaces instead of dashes, the way a rule arrives in
            // an email: accepted and written back canonical.
            const string spaced = "EA44 55DF 9025 BEAD 5F6E 45CA 6168 275A";
            _view.RuleField.text = spaced;
            _view.RuleField.onEndEdit.Invoke(spaced);

            Assert.AreEqual(BeetleMeadow, _view.Rule);
            Assert.AreEqual(BeetleMeadow, _view.RuleField.text, "the field is canonicalized in place");
            Assert.AreEqual(BeetleMeadow, World().Rule, "the running world took the new rule");
            Assert.AreEqual(0, _services.Host.Generation, "a rule change re-seeds (PyQt changeRule)");
            Assert.IsFalse(_view.ErrorLabel.gameObject.activeInHierarchy, "no error for a good rule");

            // Undashed is a rule too.
            _view.RuleField.text = BrushfireUndashed;
            _view.RuleField.onEndEdit.Invoke(BrushfireUndashed);
            Assert.AreEqual(Brushfire, _view.Rule);
            Assert.AreEqual(Brushfire, _view.RuleField.text);

            // 31 digits: the error shows, the running rule reverts to what it
            // was, and the typed text stays so the missing digit can be added.
            const string tooShort = "6eb6-ba3d-70b4-ac6f-baae-2604-8529-899";
            _view.RuleField.text = tooShort;
            _view.RuleField.onEndEdit.Invoke(tooShort);

            Assert.AreEqual(Brushfire, _view.Rule, "a bad rule never reaches the world");
            Assert.AreEqual(Brushfire, World().Rule);
            Assert.AreEqual(tooShort, _view.RuleField.text, "the typed text is kept for correction");
            Assert.IsTrue(_view.ErrorLabel.gameObject.activeInHierarchy, "the inline error shows");
            Assert.AreEqual(AppStrings.SimInvalidRule, _view.ErrorLabel.text);

            // Held about four seconds, then gone.
            _view.Tick(2f);
            Assert.IsTrue(_view.ErrorLabel.gameObject.activeInHierarchy, "still up after two seconds");
            _view.Tick(2.5f);
            Assert.IsFalse(_view.ErrorLabel.gameObject.activeInHierarchy, "gone after four");
        }

        [UnityTest]
        public IEnumerator PresetSelectionAppliesRule()
        {
            yield return BootAndOpen(GalleryCatalog.DefaultRule, autoStart: false);

            Assert.AreEqual(
                _view.PresetRules.Count, _view.PresetDropdown.options.Count,
                "one option per preset rule");
            CollectionAssert.AllItemsAreUnique(_view.PresetRules, "no rule is offered twice");
            for (int i = 0; i < GalleryCatalog.Presets.Count; i++)
            {
                Assert.AreEqual(
                    GalleryCatalog.Presets[i], _view.PresetRules[i],
                    "PyQt's three presets come first, in order");
            }
            Assert.AreEqual(
                "Red World (paper) - " + GalleryCatalog.DefaultRule,
                _view.PresetDropdown.options[0].text,
                "a named rule reads \"Name - hex\"");
            foreach ((string Name, string Rule) named in GalleryCatalog.NamedRules)
            {
                CollectionAssert.Contains(
                    _view.PresetRules, named.Rule, "every named gallery rule is offered");
            }

            _view.StepButton.onClick.Invoke();
            Assert.AreEqual(1, _services.Host.Generation);

            const int index = 2;
            string picked = _view.PresetRules[index];
            Assert.AreNotEqual(_view.Rule, picked, "pick something other than the open rule");

            _view.PresetDropdown.value = index;

            Assert.AreEqual(picked, _view.Rule);
            Assert.AreEqual(picked, World().Rule, "the world runs the picked rule");
            Assert.AreEqual(picked, _view.RuleField.text, "the field follows the dropdown");
            Assert.AreEqual(0, _services.Host.Generation, "picking a preset re-seeds");
        }

        [UnityTest]
        public IEnumerator RandomRuleIsValid()
        {
            yield return BootAndOpen(GalleryCatalog.DefaultRule, autoStart: false);

            _view.StepButton.onClick.Invoke();
            string before = _view.Rule;

            _view.RandomButton.onClick.Invoke();

            Assert.AreNotEqual(before, _view.Rule, "Random rolls a different rule");
            Assert.IsNull(MergeLife.RuleError(_view.Rule), "Random must produce a parsable rule");
            StringAssert.IsMatch("^[0-9a-f]{4}(-[0-9a-f]{4}){7}$", _view.Rule);
            Assert.AreEqual(_view.Rule, _view.RuleField.text, "the field shows the new rule");
            Assert.AreEqual(_view.Rule, World().Rule);
            Assert.AreEqual(0, _services.Host.Generation, "a new rule re-seeds");
        }

        [UnityTest]
        public IEnumerator OverlayFollowsSetting()
        {
            yield return BootAndOpen(GalleryCatalog.DefaultRule, autoStart: false);

            Assert.IsTrue(AppSettings.ShowOverlay, "the default is on (PyQt shipped it on)");
            Assert.IsTrue(_view.OverlayLabel.gameObject.activeInHierarchy, "overlay shows by default");

            AppSettings.ShowOverlay = false;
            Assert.IsFalse(
                _view.OverlayLabel.gameObject.activeInHierarchy,
                "turning the setting off hides the overlay live, without leaving the screen");

            _view.Tick(0.016f);
            Assert.IsFalse(_view.OverlayLabel.gameObject.activeInHierarchy, "and it stays hidden");

            AppSettings.ShowOverlay = true;
            Assert.IsTrue(_view.OverlayLabel.gameObject.activeInHierarchy, "and comes back");
        }

        [UnityTest]
        public IEnumerator OverlayUsesInvariantThousands()
        {
            // PyQt's f"{n:,}" is locale-free, so the port formats with the
            // invariant culture rather than the device's.
            Assert.AreEqual("Steps: 1,234, FPS: 60", SimulatorView.OverlayText(1234, 60));
            Assert.AreEqual("Steps: 0, FPS: 0", SimulatorView.OverlayText(0, 0));

            // The same line, from the live label: a small lattice stepped to 1,234.
            yield return BootAndOpen(GalleryCatalog.DefaultRule, autoStart: false, 160f, 120f);

            for (int i = 0; i < 1234; i++)
            {
                _view.StepButton.onClick.Invoke();
            }
            _view.Tick(0.016f);

            Assert.AreEqual(1234, _services.Host.Generation);
            StringAssert.StartsWith("Steps: 1,234, FPS: ", _view.OverlayLabel.text);
        }

        [UnityTest]
        public IEnumerator AutoStartFromGallery()
        {
            // What INavigator.ShowSimulator(rule, autoStart: true) does, which is
            // the gallery tap and the WebGL ?rule= parameter (PyQt display_rule).
            yield return BootAndOpen(BeetleMeadow, autoStart: true);

            Assert.AreEqual(BeetleMeadow, _view.Rule);
            Assert.AreEqual(BeetleMeadow, World().Rule);
            Assert.IsTrue(_services.Host.Playing, "a gallery rule opens playing");
            Assert.IsFalse(_view.StartButton.interactable, "Start is already down");
            Assert.IsTrue(_view.StopButton.interactable, "Stop is the live button");
            Assert.IsFalse(_view.StepButton.interactable, "Step is dead while running");

            // Leaving the screen pauses the world; coming back resumes it.
            _view.Hide();
            Assert.IsFalse(_services.Host.Playing, "Hide pauses the host");
            _view.Show();
            Assert.IsTrue(_services.Host.Playing, "Show resumes what was running");
        }

        [UnityTest]
        public IEnumerator SaveSnapshotWritesAFile()
        {
            yield return BootAndOpen(GalleryCatalog.DefaultRule, autoStart: false);

            _view.SavePngButton.onClick.Invoke();

            Assert.IsTrue(Directory.Exists(_snapshotsRoot), "the snapshots folder is created");
            string[] files = Directory.GetFiles(_snapshotsRoot, "*.png");
            Assert.AreEqual(1, files.Length, "Save PNG writes exactly one file");
            string name = Path.GetFileName(files[0]);
            StringAssert.StartsWith(
                "heatonca-e542-", name, "the slug is the rule's first hex group");
            Assert.Greater(new FileInfo(files[0]).Length, 0L, "the file holds the encoded frame");
            CollectionAssert.Contains(
                _nav.Statuses, string.Format(AppStrings.SimStatusSavedFormat, name),
                "the save reports itself through the navigator's toast");
        }

        [UnityTest]
        public IEnumerator KioskModeHidesEveryControlButTheLattice()
        {
            // ISimulatorScreen.SetChromeVisible, which is what the WebGL
            // ?controls=off embed reaches for through AppController.
            yield return BootAndOpen(GalleryCatalog.DefaultRule, autoStart: false);

            Assert.IsTrue(_view.ChromeVisible, "chrome is up by default");
            Assert.IsTrue(_view.StartButton.gameObject.activeInHierarchy);

            _view.SetChromeVisible(false);

            Assert.IsFalse(_view.ChromeVisible);
            Assert.IsFalse(_view.StartButton.gameObject.activeInHierarchy, "the toolbar is gone");
            Assert.IsFalse(_view.RuleField.gameObject.activeInHierarchy, "the rule row is gone");
            Assert.IsFalse(_view.BackButton.gameObject.activeInHierarchy, "the top bar is gone");
            Assert.IsTrue(
                _view.LatticeImage.gameObject.activeInHierarchy, "the lattice is what is left");
            Assert.AreEqual(
                GalleryCatalog.DefaultRule, World().Rule, "the world keeps running through it");

            _view.SetChromeVisible(true);
            Assert.IsTrue(_view.StartButton.gameObject.activeInHierarchy, "and it all comes back");
        }

        [UnityTest]
        public IEnumerator RuleButtonOpensTheDecoderForTheCurrentRule()
        {
            yield return BootAndOpen(BeetleMeadow, autoStart: false);

            _view.RuleButton.onClick.Invoke();

            CollectionAssert.AreEqual(new[] { BeetleMeadow }, _nav.Decoded);

            _view.BackButton.onClick.Invoke();
            Assert.AreEqual(1, _nav.BackCalls, "Back pops the navigator's stack");
        }

        // ---- helpers -----------------------------------------------------------------

        /// <summary>
        /// Boot the app, build a Simulator into its safe area, show it, and open
        /// <paramref name="rule"/> for an image area of exactly
        /// <paramref name="areaWidth"/> x <paramref name="areaHeight"/> device
        /// pixels.
        /// </summary>
        private IEnumerator BootAndOpen(
            string rule,
            bool autoStart,
            float areaWidth = AreaWidth,
            float areaHeight = AreaHeight)
        {
            yield return TestBoot.Boot();

            AppController app = AppController.Instance;
            Assert.IsNotNull(app, "AppController.Instance after boot");

            // Boot runs AppController.UseStorageRoot, which deliberately re-points
            // PngExporter at <storage root>/Snapshots. Re-assert this fixture's own
            // directory afterwards so the export assertions read the folder TearDown
            // deletes rather than the controller's temporary one.
            PngExporter.SnapshotsRoot = _snapshotsRoot;
            _nav = new RecordingNavigator();
            _services = new AppServices
            {
                Host = new SimulationHost(),
                Blitter = new FrameBlitter(),
            };
            _view = new SimulatorView(app.SafeArea, _nav, _services);
            _view.Show();
            _view.Open(rule, autoStart, areaWidth, areaHeight);
        }

        /// <summary>The lattice the host is running, as the concrete family type.</summary>
        private MergeLife World()
        {
            Assert.IsInstanceOf<MergeLife>(_services.Host.Sim, "the host runs a MergeLife world");
            return (MergeLife)_services.Host.Sim;
        }

        /// <summary>The lattice the view should have cut for an area of this size.</summary>
        private static GridSpec GridFor(float widthPx, float heightPx)
        {
            return CellGeometry.GridFor(
                widthPx, heightPx, Screen.dpi, AppSettings.CellSize, Application.isMobilePlatform);
        }

        /// <summary>
        /// Every cell in the top-left overlap of the old and new lattices must be
        /// byte-identical: that is the whole point of routing a resize through
        /// LatticeResizer instead of re-seeding.
        /// </summary>
        private static void AssertOverlapPreserved(
            byte[] oldCells, int oldCols, int oldRows, MergeLife world)
        {
            int cols = Math.Min(oldCols, world.Width);
            int rows = Math.Min(oldRows, world.Height);
            Assert.Greater(cols * rows, 0, "the two lattices must actually overlap");
            byte[] newCells = world.Frame();
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int oldIndex = (y * oldCols + x) * 3;
                    int newIndex = (y * world.Width + x) * 3;
                    if (oldCells[oldIndex] != newCells[newIndex]
                        || oldCells[oldIndex + 1] != newCells[newIndex + 1]
                        || oldCells[oldIndex + 2] != newCells[newIndex + 2])
                    {
                        Assert.Fail(
                            "cell (" + x + ", " + y + ") changed across the re-cut: "
                            + oldCells[oldIndex] + "," + oldCells[oldIndex + 1] + ","
                            + oldCells[oldIndex + 2] + " became "
                            + newCells[newIndex] + "," + newCells[newIndex + 1] + ","
                            + newCells[newIndex + 2]);
                    }
                }
            }
        }

        /// <summary>
        /// A navigator that only writes down what it was asked to do. The view
        /// talks to nothing else, so this is the whole outside world for these
        /// tests.
        /// </summary>
        private sealed class RecordingNavigator : INavigator
        {
            /// <summary>Rules passed to <see cref="ShowRuleDecoder"/>, in order.</summary>
            public List<string> Decoded { get; } = new List<string>();

            /// <summary>Toast lines the view raised, in order.</summary>
            public List<string> Statuses { get; } = new List<string>();

            /// <summary>How many times the view popped the back stack.</summary>
            public int BackCalls { get; private set; }

            public void ShowHome()
            {
            }

            public void ShowGallery()
            {
            }

            public void ShowSimulator(string rule = null, bool autoStart = false)
            {
            }

            public void ShowRuleDecoder(string rule)
            {
                Decoded.Add(rule);
            }

            public void ShowEvolve()
            {
            }

            public void ShowSettings()
            {
            }

            public void ShowAbout()
            {
            }

            public void Back()
            {
                BackCalls++;
            }

            public Task ShowAlertAsync(string title, string message)
            {
                return Task.CompletedTask;
            }

            public Task<bool> ShowConfirmAsync(
                string title,
                string message,
                string ok = AppStrings.Ok,
                string cancel = AppStrings.Cancel)
            {
                return Task.FromResult(false);
            }

            public void Status(string text)
            {
                Statuses.Add(text);
            }

            public void OpenUrl(string url)
            {
            }
        }
    }
}
