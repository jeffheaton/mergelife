using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeatonCAApp;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HeatonCA.PlayTests
{
    /// <summary>
    /// The gallery screen, checked against the PyQt tab it replaces: the same
    /// thirty rules in the same order, the same white "no preview" tile, the
    /// same three-per-row desktop grid, and the tap that opens a rule already
    /// running.
    ///
    /// The view is built against a recording navigator and a fixed-size probe
    /// rect rather than through the app's own navigation, so these tests measure
    /// the gallery on a canvas of their choosing — a phone's, which the editor's
    /// screen never is — and never depend on which screen the controller happens
    /// to be showing.
    /// </summary>
    public class GalleryViewTests
    {
        /// <summary>The gallery's fourth tile; the PyQt simulator's third preset.</summary>
        private const string TileThreeRule = "ea44-55df-9025-bead-5f6e-45ca-6168-275a";

        /// <summary>A well-formed rule the build ships no preview for.</summary>
        private const string RuleWithoutArt = "0000-0000-0000-0000-0000-0000-0000-0000";

        /// <summary>Gallery rules the featured catalog names.</summary>
        private const int NamedRuleCount = 13;

        /// <summary>Preview pixel size, which every tile's art area keeps as its aspect.</summary>
        private const int ArtWidth = 300;
        private const int ArtHeight = 192;

        private RecordingNavigator _nav;
        private RectTransform _probe;
        private GalleryView _view;

        /// <summary>Drops the probe (and with it the view) between tests.</summary>
        [UnityTearDown]
        public IEnumerator TearDownProbe()
        {
            if (_probe != null)
            {
                UnityEngine.Object.Destroy(_probe.gameObject);
            }
            _probe = null;
            _view = null;
            _nav = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThirtyTilesInPyQtOrder()
        {
            yield return BuildDesktopGallery();

            IReadOnlyList<string> rules = GalleryCatalog.Rules;
            Assert.AreEqual(rules.Count, _view.Tiles.Count, "one tile per gallery rule");
            Assert.AreEqual(30, _view.Tiles.Count, "PyQt tab_gallery.py ships thirty rules");
            for (int i = 0; i < rules.Count; i++)
            {
                RectTransform tile = _view.Tiles[i];
                Assert.AreEqual(i, tile.GetSiblingIndex(), "tile " + i + " is out of order");
                Assert.AreEqual(
                    rules[i], Caption(tile).text, "caption of tile " + i);

                // Every one of the thirty ships its pre-rendered preview, imported
                // pixel-exact (Point, uncompressed, no rescale) like PyQt's QPixmap.
                RawImage art = Art(tile);
                Assert.IsNotNull(art.texture, "Resources/Gallery art missing for " + rules[i]);
                Assert.AreEqual(ArtWidth, art.texture.width, "art width for " + rules[i]);
                Assert.AreEqual(ArtHeight, art.texture.height, "art height for " + rules[i]);
                Assert.AreEqual(
                    FilterMode.Point, art.texture.filterMode, "art filtering for " + rules[i]);
            }

            // The tile's art area carries the preview's aspect, so nothing is
            // stretched at any column count.
            Rect artRect = ((RectTransform)Art(_view.Tiles[0]).transform).rect;
            Assert.Greater(artRect.width, 0f, "the grid never sized its tiles");
            Assert.AreEqual(
                artRect.width * ArtHeight / ArtWidth, artRect.height, 0.5f, "art aspect");
        }

        [UnityTest]
        public IEnumerator NamedRulesShowSubtitles()
        {
            yield return BuildDesktopGallery();

            int named = 0;
            for (int i = 0; i < _view.Tiles.Count; i++)
            {
                RectTransform tile = _view.Tiles[i];
                string rule = GalleryCatalog.Rules[i];
                string name = GalleryCatalog.Name(rule);
                Text subtitle = Subtitle(tile);
                if (name == null)
                {
                    Assert.IsNull(subtitle, "unnamed rule " + rule + " carries a subtitle");
                    continue;
                }
                Assert.IsNotNull(subtitle, "named rule " + rule + " has no subtitle");
                Assert.AreEqual(name, subtitle.text, "subtitle of " + rule);
                named++;
            }
            Assert.AreEqual(NamedRuleCount, named, "gallery rules the catalog names");
            Assert.AreEqual(NamedRuleCount, GalleryCatalog.NamedRules.Count);
        }

        [UnityTest]
        public IEnumerator MissingArtFallsBackToBlankTile()
        {
            yield return BuildDesktopGallery();

            // PyQt drew a blank white pixmap whenever a preview file was absent.
            Assert.IsNull(GalleryView.LoadArt(RuleWithoutArt), "test rule must ship no art");
            RectTransform tile = GalleryView.CreateTile(_probe, RuleWithoutArt, 300f, null);
            yield return null;

            // A RawImage with no texture draws its flat color, so an unset texture
            // and a white tint are the blank tile.
            RawImage art = Art(tile);
            Assert.IsNull(art.texture, "a missing preview must leave the texture unset");
            Assert.AreEqual(Color.white, art.color, "the fallback tile is solid white");

            // And it is a full-size blank, not a gap: the preview area keeps the
            // 300x192 shape the art would have filled.
            Rect artRect = ((RectTransform)art.transform).rect;
            Assert.Greater(artRect.width, 0f, "the blank tile reserves no preview area");
            Assert.AreEqual(
                artRect.width * ArtHeight / ArtWidth, artRect.height, 0.5f, "blank tile aspect");

            // The rest of the tile is unchanged: the rule is still readable and
            // still tappable.
            Assert.AreEqual(RuleWithoutArt, Caption(tile).text);
            Assert.IsNull(Subtitle(tile), "an unnamed rule has no subtitle");
            Assert.IsNotNull(tile.GetComponent<Button>(), "the blank tile is still a button");
        }

        [UnityTest]
        public IEnumerator TapOpensSimulatorAutoStarted()
        {
            yield return BuildDesktopGallery();

            RectTransform tile = _view.Tiles[3];
            Assert.AreEqual(TileThreeRule, Caption(tile).text, "gallery order");

            // Tapping anywhere on the tile counts: the tap here lands on the
            // caption, a child graphic, and has to reach the card's button.
            var pointer = new PointerEventData(EventSystem.current);
            GameObject handler = ExecuteEvents.ExecuteHierarchy(
                Caption(tile).gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.AreEqual(tile.gameObject, handler, "the tap did not reach the tile");

            Assert.AreEqual(1, _nav.SimulatorCalls.Count, "one simulator navigation");
            Assert.AreEqual(TileThreeRule, _nav.SimulatorCalls[0].Rule);
            Assert.IsTrue(
                _nav.SimulatorCalls[0].AutoStart, "a gallery rule opens already running");
        }

        [Test]
        public void GalleryColumnsFollowPhysicalWidth()
        {
            // Under match-width scaling a phone and a tablet are both exactly
            // ReferenceWidth units wide, so only millimeters separate them.
            // iPhone 15 Pro portrait: 1179 px at 460 dpi is 65 mm, one tile wide.
            Assert.AreEqual(1, GalleryView.Columns(1179f, 460f, true), "phone portrait");
            // The same phone on its side is 141 mm: two.
            Assert.AreEqual(2, GalleryView.Columns(2556f, 460f, true), "phone landscape");
            // iPad 10.9 inch landscape: 2048 px at 264 dpi is 197 mm.
            Assert.AreEqual(3, GalleryView.Columns(2048f, 264f, true), "tablet");
            // A 12.9 inch iPad is 263 mm, which the clamp holds at four.
            Assert.AreEqual(4, GalleryView.Columns(2732f, 264f, true), "large tablet");
            // Desktop and WebGL keep PyQt's three per row whatever the window is.
            Assert.AreEqual(3, GalleryView.Columns(1600f, 96f, false), "desktop");
            Assert.AreEqual(3, GalleryView.Columns(600f, 96f, false), "narrow desktop window");
            // No reported dpi means no physical width to count in: the desktop
            // three, exactly as UIBuilder.PhoneSized treats an unknown dpi.
            Assert.AreEqual(3, GalleryView.Columns(1080f, 0f, true), "dpi unreported");
        }

        [UnityTest]
        public IEnumerator GalleryFitsAPhonePortraitCanvas()
        {
            // iPhone 15 Pro portrait under the constant-physical-density scaler.
            float canvasWidth = UIBuilder.MobileReferenceWidth(1179f, 460f);
            float canvasHeight = canvasWidth * 2556f / 1179f;
            yield return BuildGallery(
                canvasWidth, canvasHeight, portrait: true, phone: true,
                columns: GalleryView.Columns(1179f, 460f, true));

            Assert.AreEqual(1, _view.ColumnCount, "a 65 mm screen holds one tile");
            float cellWidth = _view.Layout.cellSize.x;
            Assert.Greater(cellWidth, 0f, "the grid never sized its tiles");
            Assert.LessOrEqual(
                cellWidth, canvasWidth, "a tile is wider than the phone's canvas");

            foreach (RectTransform tile in _view.Tiles)
            {
                Assert.AreEqual(cellWidth, tile.rect.width, 0.5f, "tile width");
                Assert.LessOrEqual(tile.rect.width, canvasWidth);

                // Text has to stay inside its tile: the 39-character rule is the
                // widest thing on the card.
                Text caption = Caption(tile);
                Assert.LessOrEqual(
                    caption.preferredWidth, tile.rect.width + 0.5f,
                    $"'{caption.text}' at {caption.fontSize}pt is wider than its tile");
                Text subtitle = Subtitle(tile);
                if (subtitle != null)
                {
                    Assert.LessOrEqual(
                        subtitle.preferredWidth, tile.rect.width + 0.5f,
                        $"'{subtitle.text}' at {subtitle.fontSize}pt is wider than its tile");
                }

                // And the card itself has to stay inside the scroll view.
                Assert.LessOrEqual(
                    tile.rect.height, canvasHeight, "a tile is taller than the screen");
            }
        }

        /// <summary>The gallery on a desktop-sized canvas: PyQt's three per row.</summary>
        private IEnumerator BuildDesktopGallery()
        {
            yield return BuildGallery(
                1600f, 900f, portrait: false, phone: false, columns: GalleryView.DesktopColumns);
            Assert.AreEqual(GalleryView.DesktopColumns, _view.ColumnCount);
        }

        /// <summary>
        /// Boots the app, then builds a gallery into a probe rect of the given
        /// size and lays it out for the given form factor and column count.
        /// </summary>
        private IEnumerator BuildGallery(
            float width, float height, bool portrait, bool phone, int columns)
        {
            yield return TestBoot.Boot();
            AppController app = AppController.Instance;
            Assert.IsNotNull(app, "AppController.Instance after boot");

            _nav = new RecordingNavigator();
            _probe = UIBuilder.CreatePanel(app.SafeArea, "GalleryProbe");
            _probe.anchorMin = new Vector2(0.5f, 0.5f);
            _probe.anchorMax = new Vector2(0.5f, 0.5f);
            _probe.pivot = new Vector2(0.5f, 0.5f);
            _probe.sizeDelta = new Vector2(width, height);

            _view = new GalleryView(_probe, _nav, new AppServices());
            _view.Show();
            _view.ApplyLayout(portrait, phone, columns);

            // The grid sizes its cells from the canvas it was given, so the rects
            // have to be laid out before anything is measured.
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_probe);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_view.Layout.transform);
            yield return null;
        }

        /// <summary>A tile's hex caption.</summary>
        private static Text Caption(RectTransform tile)
        {
            Transform caption = tile.Find("Caption");
            Assert.IsNotNull(caption, "tile '" + tile.name + "' has no caption");
            return caption.GetComponent<Text>();
        }

        /// <summary>A tile's name subtitle, or null when the rule is unnamed.</summary>
        private static Text Subtitle(RectTransform tile)
        {
            Transform name = tile.Find("Name");
            return name == null ? null : name.GetComponent<Text>();
        }

        /// <summary>A tile's preview image.</summary>
        private static RawImage Art(RectTransform tile)
        {
            Transform art = tile.Find("Art");
            Assert.IsNotNull(art, "tile '" + tile.name + "' has no art");
            return art.GetComponent<RawImage>();
        }

        /// <summary>
        /// A navigator that records instead of navigating, so a tapped tile can
        /// be checked without an app-wide screen change.
        /// </summary>
        private sealed class RecordingNavigator : INavigator
        {
            /// <summary>Every <see cref="ShowSimulator"/> call, in order.</summary>
            public readonly List<(string Rule, bool AutoStart)> SimulatorCalls =
                new List<(string Rule, bool AutoStart)>();

            /// <summary>How often the bar's Back button was pressed.</summary>
            public int BackCalls { get; private set; }

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
                SimulatorCalls.Add((rule, autoStart));
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
            public void Back()
            {
                BackCalls++;
            }

            /// <inheritdoc/>
            public Task ShowAlertAsync(string title, string message)
            {
                return Task.CompletedTask;
            }

            /// <inheritdoc/>
            public Task<bool> ShowConfirmAsync(
                string title,
                string message,
                string ok = AppStrings.Ok,
                string cancel = AppStrings.Cancel)
            {
                return Task.FromResult(false);
            }

            /// <inheritdoc/>
            public void Status(string text)
            {
            }

            /// <inheritdoc/>
            public void OpenUrl(string url)
            {
            }
        }
    }
}
