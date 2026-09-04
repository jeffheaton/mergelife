using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using HeatonCAApp;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HeatonCA.PlayTests
{
    /// <summary>
    /// The About screen's contract: it names this build, carries the paper and
    /// its DOI, opens the four documented links through the navigator (never
    /// <c>Application.OpenURL</c>, which a browser blocks), reports the boot
    /// determinism verdict, ships the third-party notices, and fits a phone.
    /// </summary>
    public class AboutViewTests
    {
        private RectTransform _page;

        [TearDown]
        public void TearDown()
        {
            if (_page != null)
            {
                UnityEngine.Object.Destroy(_page.gameObject);
                _page = null;
            }
        }

        [UnityTest]
        public IEnumerator AboutNamesVersionCopyrightAndDoi()
        {
            yield return TestBoot.Boot();

            var nav = new RecordingNavigator();
            AboutView view = BuildView(nav, passed: true, 900f, 620f);

            Assert.AreEqual(Application.productName, view.ProductNameText.text);
            Assert.AreEqual(
                string.Format(
                    CultureInfo.InvariantCulture, AppStrings.AboutVersionFormat,
                    Application.version, BuildInfo.BuildNumber),
                view.VersionText.text);
            StringAssert.Contains(Application.version, view.VersionText.text);
            Assert.AreEqual(
                string.Format(
                    CultureInfo.InvariantCulture, AppStrings.AboutBuiltFormat, BuildInfo.BuildTimeUtc),
                view.BuiltText.text);
            Assert.AreEqual(AppLinks.CopyrightLine, view.CopyrightText.text);
            Assert.AreEqual(AppLinks.Citation, view.CitationText.text);

            // Every link goes through the navigator, so WebGL can open it from
            // inside the click handler where the popup blocker allows it.
            view.DoiButton.onClick.Invoke();
            view.TutorialButton.onClick.Invoke();
            view.ManualButton.onClick.Invoke();
            view.PrivacyButton.onClick.Invoke();
            view.SourceCodeButton.onClick.Invoke();
            CollectionAssert.AreEqual(
                new[]
                {
                    AppLinks.PaperDoiUrl,
                    AppLinks.TutorialUrl,
                    AppLinks.ManualUrl,
                    AppLinks.PrivacyUrl,
                    AppLinks.RepoUrl,
                },
                nav.OpenedUrls);

            // The PyQt About page printed a log path; this one deliberately does not.
            foreach (Text row in view.ListContent.GetComponentsInChildren<Text>(true))
            {
                StringAssert.DoesNotContain("Player.log", row.text);
            }
        }

        [UnityTest]
        public IEnumerator AboutSelfCheckLineSaysPass()
        {
            yield return TestBoot.Boot();

            AppController app = AppController.Instance;
            Assert.IsNotNull(app, "AppController.Instance after boot");
            Assert.IsTrue(app.SelfCheckPassed, app.SelfCheckReport);

            AboutView passing = BuildView(new RecordingNavigator(), passed: true, 900f, 620f);
            Assert.AreEqual(
                string.Format(
                    CultureInfo.InvariantCulture, AppStrings.AboutSelfCheckFormat,
                    AppStrings.AboutSelfCheckPassWord),
                passing.SelfCheckText.text);
            Assert.IsFalse(
                passing.SelfCheckDetailText.gameObject.activeSelf,
                "a passing check does not need its report on screen");

            // The same row is where a failure is triaged, so it carries the report.
            var services = new AppServices
            {
                SelfCheckPassed = () => false,
                SelfCheckReport = () => "FAIL mergelife-soup50",
            };
            var failing = new AboutView(_page, new RecordingNavigator(), services);
            Assert.AreEqual(
                string.Format(
                    CultureInfo.InvariantCulture, AppStrings.AboutSelfCheckFormat,
                    AppStrings.AboutSelfCheckFailWord),
                failing.SelfCheckText.text);
            Assert.IsTrue(failing.SelfCheckDetailText.gameObject.activeSelf);
            StringAssert.Contains("mergelife-soup50", failing.SelfCheckDetailText.text);
        }

        [UnityTest]
        public IEnumerator AboutThirdPartyNoticesLoad()
        {
            yield return TestBoot.Boot();

            AboutView view = BuildView(new RecordingNavigator(), passed: true, 900f, 620f);

            Assert.IsNotEmpty(
                view.ThirdPartyNoticesText,
                "Assets/Resources/ThirdPartyNotices.txt must import as a TextAsset");
            StringAssert.Contains("Apache", view.ThirdPartyNoticesText);
            StringAssert.Contains("HeatonLife.Core", view.ThirdPartyNoticesText);
            Assert.GreaterOrEqual(view.ThirdPartyBlockCount, 1, "the notices are split into blocks");

            // uGUI meshes four vertices per character and stops at 65,000, so every
            // block has to stay well under that or the license text is truncated.
            var rebuilt = new System.Text.StringBuilder();
            foreach (Text block in view.ListContent.GetComponentsInChildren<Text>(true))
            {
                if (block.gameObject.name != "Notices")
                {
                    continue;
                }
                Assert.LessOrEqual(block.text.Length, AboutView.NoticesBlockChars);
                rebuilt.Append(block.text);
            }
            Assert.AreEqual(
                view.ThirdPartyNoticesText, rebuilt.ToString(),
                "the blocks reassemble into the whole document");

            Assert.IsFalse(view.ThirdPartyExpanded, "the notices start collapsed");
            view.ThirdPartyButton.onClick.Invoke();
            Assert.IsTrue(view.ThirdPartyExpanded, "the header expands the notices");
            view.ThirdPartyButton.onClick.Invoke();
            Assert.IsFalse(view.ThirdPartyExpanded, "the header collapses them again");
        }

        [UnityTest]
        public IEnumerator AboutRowsFitAPhonePage()
        {
            yield return TestBoot.Boot();

            Assert.AreEqual(64, AboutView.TitleSize(phone: false));
            Assert.AreEqual(40, AboutView.TitleSize(phone: true));

            // The narrowest phone the app supports (iPhone SE class: 750 px at
            // 326 dpi) under the constant-physical-density scaler.
            float pageWidth = UIBuilder.MobileReferenceWidth(750f, 326f);
            AboutView view = BuildView(new RecordingNavigator(), passed: true, pageWidth, 800f);
            view.ApplyLayout(portrait: true, phone: true);
            yield return null;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_page);
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.AreEqual(40, view.ProductNameText.fontSize, "the phone title size");
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_page);
            Assert.GreaterOrEqual(
                bounds.min.x, -pageWidth / 2f - 0.5f,
                $"the About page spills {(-pageWidth / 2f) - bounds.min.x:0.0} units off the left edge");
            Assert.LessOrEqual(
                bounds.max.x, pageWidth / 2f + 0.5f,
                $"the About page spills {bounds.max.x - (pageWidth / 2f):0.0} units off the right edge");

            float titleWidth = view.ProductNameText.rectTransform.rect.width;
            Assert.LessOrEqual(
                view.ProductNameText.preferredWidth, titleWidth + 0.5f,
                $"'{view.ProductNameText.text}' at {view.ProductNameText.fontSize}pt "
                + $"is wider than the {titleWidth:0} units a phone page gives it");
            Assert.LessOrEqual(
                view.VersionText.preferredWidth,
                view.VersionText.rectTransform.rect.width + 0.5f,
                $"'{view.VersionText.text}' at {view.VersionText.fontSize}pt is wider than a phone page");

            foreach (Text row in view.ListContent.GetComponentsInChildren<Text>(true))
            {
                if (row.horizontalOverflow == HorizontalWrapMode.Overflow)
                {
                    Assert.LessOrEqual(
                        row.preferredWidth, row.rectTransform.rect.width + 0.5f,
                        $"'{row.text}' at {row.fontSize}pt is wider than its own rect");
                }
            }

            // Four link buttons across a phone page leave "Privacy policy" about 90
            // units, which it cannot use; the phone layout stacks them two by two.
            AssertLabelFits(view.TutorialButton);
            AssertLabelFits(view.ManualButton);
            AssertLabelFits(view.PrivacyButton);
            AssertLabelFits(view.SourceCodeButton);
            AssertLabelFits(view.DoiButton);
            AssertLabelFits(view.ThirdPartyButton);
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
        private AboutView BuildView(INavigator nav, bool passed, float width, float height)
        {
            AppController app = AppController.Instance;
            Assert.IsNotNull(app, "AppController.Instance after boot");
            _page = UIBuilder.CreatePanel(app.SafeArea, "AboutProbe");
            _page.anchorMin = new Vector2(0.5f, 0.5f);
            _page.anchorMax = new Vector2(0.5f, 0.5f);
            _page.pivot = new Vector2(0.5f, 0.5f);
            _page.sizeDelta = new Vector2(width, height);
            var services = new AppServices
            {
                SelfCheckPassed = () => passed,
                SelfCheckReport = () => app.SelfCheckReport,
            };
            return new AboutView(_page, nav, services);
        }

        /// <summary>
        /// A navigator that records instead of navigating: the About screen is
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
