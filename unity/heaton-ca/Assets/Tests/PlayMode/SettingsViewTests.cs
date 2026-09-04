using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeatonCA.Engine;
using HeatonCAApp;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HeatonCA.PlayTests
{
    /// <summary>
    /// The Settings screen's contract: PyQt's editing model (nothing is written
    /// until Save), Save applying live to the running simulator, Restore
    /// Defaults staying unsaved, both sliders clamped to their published ranges,
    /// and the whole page fitting the narrowest phone the app ships on.
    /// </summary>
    public class SettingsViewTests
    {
        /// <summary>Canvas the stand-in simulator cuts its lattice from, in device pixels.</summary>
        private const float CanvasWidthPx = 600f;

        /// <summary>Height of that canvas, in device pixels.</summary>
        private const float CanvasHeightPx = 400f;

        private RectTransform _page;

        [TearDown]
        public void TearDown()
        {
            if (_page != null)
            {
                UnityEngine.Object.Destroy(_page.gameObject);
                _page = null;
            }
            AppSettings.Reset();
        }

        [UnityTest]
        public IEnumerator SettingsSaveAppliesLive()
        {
            yield return TestBoot.Boot();

            // A stand-in for the open simulator: it does exactly what SimulatorView
            // does with the settings -- re-cut for the new cell size, retime, and
            // re-read the overlay flag -- so this test pins the plumbing Settings
            // owns (the AppSettings write and the host retime) without reaching
            // into another screen's internals.
            var host = new SimulationHost();
            bool overlayInSimulator = AppSettings.ShowOverlay;
            int recuts = 0;
            Action recut = () =>
            {
                GridSpec grid = CellGeometry.GridFor(
                    CanvasWidthPx, CanvasHeightPx, 0f, AppSettings.CellSize, false);
                var world = new MergeLife(GalleryCatalog.DefaultRule, grid.Cols, grid.Rows);
                world.SeedSoup(0);
                host.Load(world, false);
                overlayInSimulator = AppSettings.ShowOverlay;
                recuts++;
            };
            recut();
            AppSettings.Changed += recut;
            try
            {
                GridSpec before = CellGeometry.GridFor(CanvasWidthPx, CanvasHeightPx, 0f, 5, false);
                Assert.AreEqual(before.Cols, host.Sim.Width, "grid before the change");
                Assert.AreEqual(before.Rows, host.Sim.Height, "grid before the change");

                var nav = new RecordingNavigator();
                SettingsView view = BuildView(nav, host, 900f, 620f);

                view.CellSizeSlider.value = 10f;
                view.SpeedSlider.value = 12f;
                view.OverlayToggle.isOn = false;

                // PyQt semantics: editing writes nothing.
                Assert.AreEqual(5, AppSettings.CellSize, "cell size before Save");
                Assert.AreEqual(30, AppSettings.StepsPerSecond, "speed before Save");
                Assert.IsTrue(AppSettings.ShowOverlay, "overlay before Save");
                Assert.AreEqual(before.Cols, host.Sim.Width, "no re-cut before Save");
                Assert.AreEqual(1, recuts, "no re-cut before Save");

                view.Save();

                Assert.AreEqual(10, AppSettings.CellSize, "cell size after Save");
                Assert.AreEqual(12, AppSettings.StepsPerSecond, "speed after Save");
                Assert.IsFalse(AppSettings.ShowOverlay, "overlay after Save");

                GridSpec after = CellGeometry.GridFor(CanvasWidthPx, CanvasHeightPx, 0f, 10, false);
                Assert.AreNotEqual(before.Cols, after.Cols, "the two cell sizes must cut different grids");
                Assert.AreEqual(after.Cols, host.Sim.Width, "the simulator re-cut its grid");
                Assert.AreEqual(after.Rows, host.Sim.Height, "the simulator re-cut its grid");
                Assert.AreEqual(12d, host.StepsPerSecond, 1e-9d, "the speed reached the host");
                Assert.IsFalse(overlayInSimulator, "the overlay toggled off");
                Assert.AreEqual(1, nav.BackCount, "Save returns to the previous screen");
            }
            finally
            {
                AppSettings.Changed -= recut;
            }
        }

        [UnityTest]
        public IEnumerator SettingsCancelDiscards()
        {
            yield return TestBoot.Boot();

            var nav = new RecordingNavigator();
            SettingsView view = BuildView(nav, null, 900f, 620f);

            view.CellSizeSlider.value = 20f;
            view.SpeedSlider.value = 55f;
            view.OverlayToggle.isOn = false;
            Assert.AreEqual(20, view.PendingCellSize, "the edit reached the pending value");

            view.Cancel();

            Assert.AreEqual(5, AppSettings.CellSize, "Cancel writes nothing");
            Assert.AreEqual(30, AppSettings.StepsPerSecond, "Cancel writes nothing");
            Assert.IsTrue(AppSettings.ShowOverlay, "Cancel writes nothing");
            Assert.AreEqual(5, view.PendingCellSize, "Cancel reloads the stored value");
            Assert.AreEqual(30, view.PendingStepsPerSecond, "Cancel reloads the stored value");
            Assert.IsTrue(view.PendingShowOverlay, "Cancel reloads the stored value");
            Assert.AreEqual(5f, view.CellSizeSlider.value, 1e-4f, "the control follows the reload");
            Assert.AreEqual(30f, view.SpeedSlider.value, 1e-4f, "the control follows the reload");
            Assert.IsTrue(view.OverlayToggle.isOn, "the control follows the reload");
            Assert.AreEqual(1, nav.BackCount, "Cancel returns to the previous screen");

            // The bar's Back button is the same action, not a silent save.
            view.CellSizeSlider.value = 22f;
            view.BackButton.onClick.Invoke();
            Assert.AreEqual(5, AppSettings.CellSize, "Back writes nothing either");
            Assert.AreEqual(2, nav.BackCount, "Back returns to the previous screen");
        }

        [UnityTest]
        public IEnumerator SettingsRestoreDefaultsNeedsSave()
        {
            yield return TestBoot.Boot();

            AppSettings.CellSize = 12;
            AppSettings.StepsPerSecond = 7;
            AppSettings.ShowOverlay = false;

            var nav = new RecordingNavigator();
            SettingsView view = BuildView(nav, null, 900f, 620f);
            Assert.AreEqual(12, view.PendingCellSize, "the screen opens on the stored values");
            Assert.AreEqual(7, view.PendingStepsPerSecond, "the screen opens on the stored values");
            Assert.IsFalse(view.PendingShowOverlay, "the screen opens on the stored values");

            view.RestoreDefaults();

            Assert.AreEqual(AppSettings.DefaultCellSize, view.PendingCellSize);
            Assert.AreEqual(AppSettings.DefaultStepsPerSecond, view.PendingStepsPerSecond);
            Assert.AreEqual(AppSettings.DefaultShowOverlay, view.PendingShowOverlay);
            Assert.AreEqual(5f, view.CellSizeSlider.value, 1e-4f, "the slider shows the default");
            Assert.AreEqual(30f, view.SpeedSlider.value, 1e-4f, "the slider shows the default");
            Assert.IsTrue(view.OverlayToggle.isOn, "the toggle shows the default");
            Assert.AreEqual("5", view.CellSizeValueText.text, "the readout shows the default");
            Assert.AreEqual("30", view.SpeedValueText.text, "the readout shows the default");

            // Still unsaved: Restore Defaults fills the controls, Save commits them.
            Assert.AreEqual(12, AppSettings.CellSize, "Restore Defaults does not persist");
            Assert.AreEqual(7, AppSettings.StepsPerSecond, "Restore Defaults does not persist");
            Assert.IsFalse(AppSettings.ShowOverlay, "Restore Defaults does not persist");

            view.Save();

            Assert.AreEqual(5, AppSettings.CellSize, "Save commits the defaults");
            Assert.AreEqual(30, AppSettings.StepsPerSecond, "Save commits the defaults");
            Assert.IsTrue(AppSettings.ShowOverlay, "Save commits the defaults");
            Assert.AreEqual(1, nav.BackCount);
        }

        [UnityTest]
        public IEnumerator SettingsSlidersClampToTheirRanges()
        {
            yield return TestBoot.Boot();

            SettingsView view = BuildView(new RecordingNavigator(), null, 900f, 620f);

            Assert.AreEqual(AppSettings.MinCellSize, view.CellSizeSlider.minValue, 1e-4f);
            Assert.AreEqual(AppSettings.MaxCellSize, view.CellSizeSlider.maxValue, 1e-4f);
            Assert.IsTrue(view.CellSizeSlider.wholeNumbers, "cell size is a whole number");
            Assert.AreEqual(AppSettings.MinStepsPerSecond, view.SpeedSlider.minValue, 1e-4f);
            Assert.AreEqual(AppSettings.MaxStepsPerSecond, view.SpeedSlider.maxValue, 1e-4f);
            Assert.IsTrue(view.SpeedSlider.wholeNumbers, "speed is a whole number");

            view.CellSizeSlider.value = 999f;
            Assert.AreEqual(25, view.PendingCellSize, "cell size clamps at its maximum");
            Assert.AreEqual("25", view.CellSizeValueText.text);
            view.IncrementCellSizeButton.onClick.Invoke();
            Assert.AreEqual(25, view.PendingCellSize, "the stepper cannot pass the maximum");

            view.CellSizeSlider.value = -50f;
            Assert.AreEqual(1, view.PendingCellSize, "cell size clamps at its minimum");
            view.DecrementCellSizeButton.onClick.Invoke();
            Assert.AreEqual(1, view.PendingCellSize, "the stepper cannot pass the minimum");

            view.CellSizeSlider.value = 10f;
            view.IncrementCellSizeButton.onClick.Invoke();
            Assert.AreEqual(11, view.PendingCellSize, "the stepper moves one unit");
            Assert.AreEqual(11f, view.CellSizeSlider.value, 1e-4f, "the slider follows the stepper");
            Assert.AreEqual("11", view.CellSizeValueText.text, "the readout follows the stepper");
            view.DecrementCellSizeButton.onClick.Invoke();
            Assert.AreEqual(10, view.PendingCellSize, "the stepper moves one unit");

            view.SpeedSlider.value = 999f;
            Assert.AreEqual(60, view.PendingStepsPerSecond, "speed clamps at its maximum");
            Assert.AreEqual("60", view.SpeedValueText.text);
            view.SpeedSlider.value = -50f;
            Assert.AreEqual(1, view.PendingStepsPerSecond, "speed clamps at its minimum");
        }

        [UnityTest]
        public IEnumerator SettingsFitsAPhonePortraitCanvas()
        {
            yield return TestBoot.Boot();

            // The narrowest phone the app supports (iPhone SE class: 750 px at
            // 326 dpi) under the constant-physical-density scaler.
            float pageWidth = UIBuilder.MobileReferenceWidth(750f, 326f);
            SettingsView view = BuildView(new RecordingNavigator(), null, pageWidth, 800f);
            view.ApplyLayout(portrait: true, phone: true);
            yield return null;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_page);
            Canvas.ForceUpdateCanvases();
            yield return null;

            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_page);
            Assert.GreaterOrEqual(
                bounds.min.x, -pageWidth / 2f - 0.5f,
                $"the settings page spills {(-pageWidth / 2f) - bounds.min.x:0.0} units off the left edge");
            Assert.LessOrEqual(
                bounds.max.x, pageWidth / 2f + 0.5f,
                $"the settings page spills {bounds.max.x - (pageWidth / 2f):0.0} units off the right edge");

            foreach (Text row in view.ListContent.GetComponentsInChildren<Text>(true))
            {
                if (row.horizontalOverflow == HorizontalWrapMode.Overflow)
                {
                    Assert.LessOrEqual(
                        row.preferredWidth, row.rectTransform.rect.width + 0.5f,
                        $"'{row.text}' at {row.fontSize}pt is wider than its own rect");
                }
                else
                {
                    Assert.LessOrEqual(
                        row.preferredHeight, row.rectTransform.rect.height + 0.5f,
                        $"'{row.text}' at {row.fontSize}pt is taller than its own rect");
                }
            }

            // The three action buttons are the row that breaks first: "Restore
            // Defaults" does not fit a third of a phone canvas, so the phone layout
            // gives it a line of its own.
            AssertLabelFits(view.SaveButton);
            AssertLabelFits(view.CancelButton);
            AssertLabelFits(view.RestoreDefaultsButton);
        }

        /// <summary>Fails when a button's label needs more width than the button has.</summary>
        private static void AssertLabelFits(Button button)
        {
            Text label = button.GetComponentInChildren<Text>();
            float width = ((RectTransform)button.transform).rect.width;
            Assert.LessOrEqual(
                label.preferredWidth, width + 0.5f,
                $"'{label.text}' at {label.fontSize}pt needs more than the {width:0} units its button has");
        }

        /// <summary>
        /// Builds the screen into a probe page of a known size, so every
        /// measurement is against the canvas the test names rather than whatever
        /// the machine running the suite happens to have.
        /// </summary>
        private SettingsView BuildView(INavigator nav, SimulationHost host, float width, float height)
        {
            AppController app = AppController.Instance;
            Assert.IsNotNull(app, "AppController.Instance after boot");
            _page = UIBuilder.CreatePanel(app.SafeArea, "SettingsProbe");
            _page.anchorMin = new Vector2(0.5f, 0.5f);
            _page.anchorMax = new Vector2(0.5f, 0.5f);
            _page.pivot = new Vector2(0.5f, 0.5f);
            _page.sizeDelta = new Vector2(width, height);
            return new SettingsView(_page, nav, new AppServices { Host = host });
        }

        /// <summary>
        /// A navigator that records instead of navigating: the Settings screen is
        /// built on its own here, so nothing else has to exist for it to work.
        /// </summary>
        private sealed class RecordingNavigator : INavigator
        {
            /// <summary>How many times the screen asked to go back.</summary>
            public int BackCount { get; private set; }

            /// <summary>Every URL the screen asked to open, in order.</summary>
            public List<string> OpenedUrls { get; } = new List<string>();

            /// <inheritdoc/>
            public void ShowHome()
            {
            }

            /// <inheritdoc/>
            public void ShowGallery()
            {
            }

            /// <inheritdoc/>
            public void ShowSimulator(string rule = null, bool autoStart = false)
            {
            }

            /// <inheritdoc/>
            public void ShowRuleDecoder(string rule)
            {
            }

            /// <inheritdoc/>
            public void ShowEvolve()
            {
            }

            /// <inheritdoc/>
            public void ShowSettings()
            {
            }

            /// <inheritdoc/>
            public void ShowAbout()
            {
            }

            /// <inheritdoc/>
            public void Back() => BackCount++;

            /// <inheritdoc/>
            public Task ShowAlertAsync(string title, string message) => Task.CompletedTask;

            /// <inheritdoc/>
            public Task<bool> ShowConfirmAsync(
                string title,
                string message,
                string ok = AppStrings.Ok,
                string cancel = AppStrings.Cancel) => Task.FromResult(false);

            /// <inheritdoc/>
            public void Status(string text)
            {
            }

            /// <inheritdoc/>
            public void OpenUrl(string url) => OpenedUrls.Add(url);
        }
    }
}
