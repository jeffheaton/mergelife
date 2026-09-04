using System;
using System.Collections;
using System.Collections.Generic;
using HeatonCAApp;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HeatonCA.PlayTests
{
    /// <summary>
    /// Boots the whole app headlessly and checks what the shell promises: the
    /// determinism self-check ran and passed, every screen opens and closes through
    /// the navigator, the back stack behaves, the browser's launch parameters open
    /// the simulator, and Home fits a phone held upright. Any error log fails a test
    /// (the runner treats an unexpected <c>Debug.LogError</c> as a failure), so a
    /// self-check FAIL -- which is logged as an error -- fails twice over.
    /// </summary>
    public class AppSmokeTests
    {
        private static readonly string[] CheckNames =
        {
            "pcg32",
            "mergelife-upstream-1",
            "mergelife-upstream-60",
            "mergelife-soup50",
            "objective-redworld48",
        };

        /// <summary>Every screen except Home, with the navigator call that opens it.</summary>
        private static readonly ScreenId[] PushedScreens =
        {
            ScreenId.Gallery,
            ScreenId.Simulator,
            ScreenId.RuleDecoder,
            ScreenId.Evolve,
            ScreenId.Settings,
            ScreenId.About,
        };

        [UnityTest]
        public IEnumerator BootsAndRunsSelfCheck()
        {
            yield return TestBoot.Boot();

            AppController app = AppController.Instance;
            Assert.IsNotNull(app, "AppController.Instance after boot");
            Assert.IsTrue(app.SelfCheckPassed, app.SelfCheckReport);
            StringAssert.Contains("HeatonCA determinism self-check:", app.SelfCheckReport);
            foreach (string name in CheckNames)
            {
                StringAssert.Contains("PASS " + name + "\n", app.SelfCheckReport);
            }
            StringAssert.DoesNotContain("FAIL ", app.SelfCheckReport);
        }

        /// <summary>
        /// The self-check runs in the Editor too, not only in players: the About
        /// page's verdict and the services bag's report both read it, so an Editor
        /// run that skipped it would report a pass nobody computed.
        /// </summary>
        [UnityTest]
        public IEnumerator SelfCheckPassesInEditor()
        {
            yield return TestBoot.Boot();

            AppController app = AppController.Instance;
            Assert.IsTrue(Application.isEditor, "this suite runs in the Editor");
            Assert.IsTrue(app.SelfCheckPassed, app.SelfCheckReport);
            Assert.IsNotNull(app.Services, "services");
            Assert.IsTrue(app.Services.SelfCheckPassed(), "AppServices.SelfCheckPassed");
            Assert.AreEqual(app.SelfCheckReport, app.Services.SelfCheckReport());
            Assert.IsNotEmpty(app.Services.SelfCheckReport());
        }

        /// <summary>
        /// Booting the app builds seven screens, three services, and two overlays.
        /// None of that may log an error: the gate fails on the first one, and this
        /// test says so in one place instead of leaving it to whichever test drew
        /// the failure.
        /// </summary>
        [UnityTest]
        public IEnumerator NoErrorLogsDuringBoot()
        {
            // The plan's rule is that any Debug.LogError fails the gate. LogAssert's
            // NoUnexpectedReceived() is stricter than that: it flags every log the
            // test did not Expect, including the determinism self-check's own
            // multi-line Debug.Log on a healthy boot. Watch the log stream instead
            // and fail only on Error, Assert and Exception.
            var bad = new List<string>();
            Application.LogCallback watch = (message, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
                {
                    bad.Add(type + ": " + message);
                }
            };
            Application.logMessageReceived += watch;
            try
            {
                yield return TestBoot.Boot();
                yield return null;
            }
            finally
            {
                Application.logMessageReceived -= watch;
            }

            Assert.IsEmpty(bad, "boot logged no errors:\n" + string.Join("\n", bad));
            Assert.IsNotNull(AppController.Instance, "AppController.Instance after boot");
        }

        [UnityTest]
        public IEnumerator VersionBadgeShowsApplicationVersion()
        {
            yield return TestBoot.Boot();

            AppController app = AppController.Instance;
            Assert.IsNotNull(app, "AppController.Instance after boot");
            Assert.IsFalse(string.IsNullOrEmpty(Application.version), "Application.version");
            Assert.IsNotNull(app.VersionBadge, "version badge");
            Assert.IsTrue(app.VersionBadge.gameObject.activeInHierarchy, "badge visible");
            Assert.AreEqual(
                AppStrings.VersionBadgePrefix + Application.version, app.VersionBadge.text);
        }

        /// <summary>
        /// Home is the root, every other screen opens on top of it exactly one deep,
        /// and only the screen being visited is on display. This is the walk a user
        /// takes through the whole app in one pass.
        /// </summary>
        [UnityTest]
        public IEnumerator BootsAndVisitsEveryScreen()
        {
            yield return TestBoot.Boot();

            AppController app = AppController.Instance;
            INavigator nav = app;
            Assert.AreEqual(ScreenId.Home, app.CurrentScreen);
            AssertOnlyVisible(app, ScreenId.Home);
            CollectionAssert.AreEqual(new[] { ScreenId.Home }, app.BackStack);

            foreach (ScreenId screen in PushedScreens)
            {
                Open(nav, screen);
                yield return null;

                Assert.AreEqual(screen, app.CurrentScreen, "current screen after opening " + screen);
                AssertOnlyVisible(app, screen);
                CollectionAssert.AreEqual(
                    new[] { ScreenId.Home, screen }, app.BackStack, "back stack at " + screen);

                nav.Back();
                yield return null;

                Assert.AreEqual(ScreenId.Home, app.CurrentScreen, "Back from " + screen);
                AssertOnlyVisible(app, ScreenId.Home);
                CollectionAssert.AreEqual(new[] { ScreenId.Home }, app.BackStack);
            }
        }

        /// <summary>
        /// Back leads home from every screen the Home page opens, and stops there:
        /// the app never backs out of its own first screen, on desktop or on Android.
        /// </summary>
        [UnityTest]
        public IEnumerator BackFromEveryRootViewReturnsHome()
        {
            yield return TestBoot.Boot();

            AppController app = AppController.Instance;
            INavigator nav = app;
            foreach (ScreenId screen in PushedScreens)
            {
                nav.ShowHome();
                Open(nav, screen);
                yield return null;
                Assert.AreEqual(screen, app.CurrentScreen);

                nav.Back();
                yield return null;

                Assert.AreEqual(ScreenId.Home, app.CurrentScreen, "Back from " + screen);
                Assert.IsTrue(app.ViewFor(ScreenId.Home).Visible, "Home visible after Back");
                Assert.AreEqual(1, app.BackStack.Count, "stack depth after Back from " + screen);
            }

            // At the root, Back is a no-op: it neither quits nor empties the stack.
            nav.Back();
            yield return null;
            Assert.AreEqual(ScreenId.Home, app.CurrentScreen);
            CollectionAssert.AreEqual(new[] { ScreenId.Home }, app.BackStack);
            Assert.IsTrue(app.ViewFor(ScreenId.Home).Visible, "Home still visible");
        }

        /// <summary>
        /// The browser's launch parameters, driven through the same hook the WebGL
        /// boot uses: <c>?rule=</c> opens the simulator (auto-started), <c>?size=</c>
        /// sets the session cell size, and <c>?controls=off</c> is the kiosk look the
        /// old ml-fullscreen2.js embeds asked for.
        /// </summary>
        [UnityTest]
        public IEnumerator UrlParamsOpenSimulator()
        {
            yield return TestBoot.Boot();

            AppController app = AppController.Instance;
            Assert.AreEqual(ScreenId.Home, app.CurrentScreen, "Home before the URL is applied");
            Assert.IsTrue(app.ControlsVisible, "controls are on by default");

            string rule = GalleryCatalog.DefaultRule;
            UrlParams applied = app.ApplyUrl("?rule=" + rule + "&size=7&controls=off");
            yield return null;

            Assert.AreEqual(rule, applied.Rule, "parsed rule");
            Assert.AreEqual(ScreenId.Simulator, app.CurrentScreen);
            Assert.IsTrue(app.ViewFor(ScreenId.Simulator).Visible, "simulator visible");
            AssertOnlyVisible(app, ScreenId.Simulator);
            CollectionAssert.AreEqual(new[] { ScreenId.Home, ScreenId.Simulator }, app.BackStack);
            Assert.AreEqual(7, AppSettings.CellSize, "?size= overrides the cell size");
            Assert.IsFalse(app.ControlsVisible, "?controls=off hides the chrome");

            // Junk changes nothing: an embed with a broken query still shows the app.
            UrlParams junk = app.ApplyUrl("?rule=not-a-rule&size=999");
            Assert.IsNull(junk.Rule, "an invalid rule is dropped");
            Assert.IsNull(junk.CellSize, "an out-of-range size is dropped");
            Assert.AreEqual(7, AppSettings.CellSize, "the good size survives the junk one");
        }

        /// <summary>
        /// Home laid out for a phone held upright: every button stays inside the
        /// canvas and none is smaller than the 44-unit touch target. The phone
        /// layout is asserted directly rather than by resizing the game view, which
        /// a batch-mode run cannot do.
        /// </summary>
        [UnityTest]
        public IEnumerator HomeFitsAPhonePortraitCanvas()
        {
            yield return TestBoot.Boot();
            // Let the controller's debounced resize pass fire first: it re-lays the
            // visible screen for the real (desktop) canvas, and would otherwise undo
            // the phone layout this test applies by hand.
            yield return new WaitForSecondsRealtime(AppController.ResizeSettleSeconds + 0.1f);

            AppController app = AppController.Instance;
            HomeView home = app.Home;
            Assert.IsNotNull(home, "Home view");
            Assert.AreEqual(5, home.Buttons.Count, "Gallery, Simulator, Evolve, Settings, About");

            home.ApplyLayout(portrait: true, phone: true);
            Canvas.ForceUpdateCanvases();

            var canvas = app.SafeArea.GetComponentInParent<Canvas>();
            Assert.IsNotNull(canvas, "canvas");
            var canvasRect = (RectTransform)canvas.transform;
            Rect page = canvasRect.rect;
            Assert.Greater(page.height, 0f, "canvas height");

            var corners = new Vector3[4];
            var seen = new List<string>();
            foreach (Button button in home.Buttons)
            {
                var rt = (RectTransform)button.transform;
                seen.Add(button.gameObject.name);
                Assert.IsTrue(
                    button.gameObject.activeSelf,
                    button.gameObject.name + " must be part of the page");
                Assert.GreaterOrEqual(
                    rt.rect.height, HomeView.MinButtonHeight,
                    button.gameObject.name + " is shorter than a touch target");
                Assert.Greater(rt.rect.width, 0f, button.gameObject.name + " has no width");

                rt.GetWorldCorners(corners);
                for (int i = 0; i < corners.Length; i++)
                {
                    Vector3 local = canvasRect.InverseTransformPoint(corners[i]);
                    Assert.IsTrue(
                        local.x >= page.xMin - 0.5f && local.x <= page.xMax + 0.5f
                        && local.y >= page.yMin - 0.5f && local.y <= page.yMax + 0.5f,
                        button.gameObject.name + " corner " + i + " at " + local
                        + " is outside the canvas " + page);
                }
            }
            CollectionAssert.AreEqual(
                new[]
                {
                    AppStrings.HomeGallery,
                    AppStrings.HomeSimulator,
                    AppStrings.HomeEvolve,
                    AppStrings.HomeSettings,
                    AppStrings.HomeAbout,
                },
                seen,
                "the Home buttons, in page order");
        }

        /// <summary>Open <paramref name="screen"/> through the navigator.</summary>
        private static void Open(INavigator nav, ScreenId screen)
        {
            switch (screen)
            {
                case ScreenId.Gallery:
                    nav.ShowGallery();
                    break;
                case ScreenId.Simulator:
                    nav.ShowSimulator(GalleryCatalog.DefaultRule);
                    break;
                case ScreenId.RuleDecoder:
                    nav.ShowRuleDecoder(GalleryCatalog.DefaultRule);
                    break;
                case ScreenId.Evolve:
                    nav.ShowEvolve();
                    break;
                case ScreenId.Settings:
                    nav.ShowSettings();
                    break;
                case ScreenId.About:
                    nav.ShowAbout();
                    break;
                default:
                    nav.ShowHome();
                    break;
            }
        }

        /// <summary>Exactly one screen is on display, and it is the expected one.</summary>
        private static void AssertOnlyVisible(AppController app, ScreenId expected)
        {
            foreach (ScreenId id in (ScreenId[])Enum.GetValues(typeof(ScreenId)))
            {
                IAppView view = app.ViewFor(id);
                Assert.IsNotNull(view, "no view was built for " + id);
                Assert.AreEqual(id == expected, view.Visible, id + " visibility");
            }
        }
    }
}
