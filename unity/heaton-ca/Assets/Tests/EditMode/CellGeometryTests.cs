using HeatonCAApp;
using NUnit.Framework;

namespace HeatonCA.Tests
{
    /// <summary>
    /// The lattice model, pinned against the screens the app actually ships on.
    /// PyQt read "cell size" as raw pixels; here it is a density-independent
    /// size, so the same setting has to produce a usable world on a 460 dpi
    /// phone, a 264 dpi tablet, a 96 dpi monitor, a Retina Mac, and a browser
    /// canvas at devicePixelRatio 2. The two invariants that make the Simulator
    /// safe — never more than <see cref="CellGeometry.MaxCells"/> cells, never
    /// smaller than 8x8 — are tested on their own as well.
    /// </summary>
    public class CellGeometryTests
    {
        [Test]
        public void CellPixelsScalesByDensityIndependentPixelsOnMobile()
        {
            // iPhone 15 Pro: 460 dpi is 2.875 dp scale, so a 5-unit cell is ~14 px.
            Assert.AreEqual(14.375f, CellGeometry.CellPixels(5, 460f, true), 1e-4f);
            // A 264 dpi tablet: 1.65 px per unit.
            Assert.AreEqual(1.65f, CellGeometry.CellPixels(1, 264f, true), 1e-4f);
        }

        [Test]
        public void CellPixelsNeverShrinksBelowTheSettingOnDesktop()
        {
            // 96 dpi is the reference: the setting is the pixel count, as in PyQt.
            Assert.AreEqual(5f, CellGeometry.CellPixels(5, 96f, false), 1e-4f);
            // A monitor reporting less than 96 dpi still gets whole pixels, not
            // fractions of one: the floor of 1 keeps PyQt's exact behavior.
            Assert.AreEqual(5f, CellGeometry.CellPixels(5, 72f, false), 1e-4f);
            // Retina Mac at 227 dpi: 2.365 px per unit.
            Assert.AreEqual(11.8229f, CellGeometry.CellPixels(5, 227f, false), 1e-3f);
        }

        [Test]
        public void CellPixelsFallsBackToRawPixelsWhenDensityIsUnknown()
        {
            // Screen.dpi is 0 on most desktops and 1 on some emulators.
            Assert.AreEqual(5f, CellGeometry.CellPixels(5, 0f, false), 1e-4f);
            Assert.AreEqual(5f, CellGeometry.CellPixels(5, 1f, false), 1e-4f);
            Assert.AreEqual(5f, CellGeometry.CellPixels(5, 0f, true), 1e-4f);
            Assert.AreEqual(5f, CellGeometry.CellPixels(5, 1f, true), 1e-4f);
        }

        [Test]
        public void PhoneCanvasGetsAWorkableLattice()
        {
            // iPhone 15 Pro portrait, full screen, the default cell size 5.
            GridSpec grid = CellGeometry.GridFor(1179f, 2556f, 460f, 5, true);
            Assert.AreEqual(82, grid.Cols);
            Assert.AreEqual(177, grid.Rows);
            Assert.AreEqual(5, grid.EffectiveCellSize);
            Assert.IsFalse(grid.WasCapped(5));
            Assert.AreEqual(14.375f, grid.CellPx, 1e-4f);
            // The lattice is drawn at whole cells, so it never exceeds the canvas.
            Assert.LessOrEqual(grid.WidthPx, 1179f);
            Assert.LessOrEqual(grid.HeightPx, 2556f);
        }

        [Test]
        public void DenseTabletAtCellSizeOneIsCoarsenedToFitTheBudget()
        {
            // iPad Pro 12.9" at cell size 1 wants 1241 x 1655 = 2.05M cells.
            GridSpec grid = CellGeometry.GridFor(2048f, 2732f, 264f, 1, true);
            Assert.GreaterOrEqual(grid.EffectiveCellSize, 3, "the lattice must be coarsened");
            Assert.IsTrue(grid.WasCapped(1));
            Assert.LessOrEqual(grid.Cols * grid.Rows, CellGeometry.MaxCells);
            Assert.AreEqual(413, grid.Cols);
            Assert.AreEqual(551, grid.Rows);
            Assert.AreEqual(3, grid.EffectiveCellSize);
        }

        [Test]
        public void PlainDesktopMatchesThePyQtPixelCounts()
        {
            // 1600x900 with an unreported dpi: exactly what PyQt produced.
            GridSpec grid = CellGeometry.GridFor(1600f, 900f, 0f, 5, false);
            Assert.AreEqual(320, grid.Cols);
            Assert.AreEqual(180, grid.Rows);
            Assert.AreEqual(5, grid.EffectiveCellSize);
            Assert.AreEqual(5f, grid.CellPx, 1e-4f);
        }

        [Test]
        public void RetinaMacGetsFewerLargerCellsThanItsPixelCount()
        {
            // 2560x1440 backing pixels at 227 dpi: cells are 11.8 px, so the
            // lattice is 216x121 rather than the 512x288 raw pixels would give.
            GridSpec grid = CellGeometry.GridFor(2560f, 1440f, 227f, 5, false);
            Assert.AreEqual(216, grid.Cols);
            Assert.AreEqual(121, grid.Rows);
            Assert.AreEqual(5, grid.EffectiveCellSize);
            Assert.IsFalse(grid.WasCapped(5));
        }

        [Test]
        public void BrowserCanvasAtDevicePixelRatioTwoDoublesTheCellPixels()
        {
            // WebGL reports Screen.dpi as 96 * devicePixelRatio; DPR 2 is 192.
            GridSpec grid = CellGeometry.GridFor(1280f, 720f, 192f, 5, false);
            Assert.AreEqual(10f, grid.CellPx, 1e-4f);
            Assert.AreEqual(128, grid.Cols);
            Assert.AreEqual(72, grid.Rows);
            // Same CSS canvas at DPR 1 has half the backing pixels and half the
            // cell size in pixels, so the lattice is identical.
            GridSpec dpr1 = CellGeometry.GridFor(640f, 360f, 96f, 5, false);
            Assert.AreEqual(grid.Cols, dpr1.Cols);
            Assert.AreEqual(grid.Rows, dpr1.Rows);
        }

        [Test]
        public void LatticeIsNeverSmallerThanEightByEight()
        {
            // A sliver of a canvas mid-rotation, or a collapsed panel.
            GridSpec sliver = CellGeometry.GridFor(20f, 10f, 0f, 5, false);
            Assert.AreEqual(CellGeometry.MinGrid, sliver.Cols);
            Assert.AreEqual(CellGeometry.MinGrid, sliver.Rows);
            // Even a degenerate canvas produces a runnable world.
            GridSpec empty = CellGeometry.GridFor(0f, 0f, 0f, 25, false);
            Assert.AreEqual(CellGeometry.MinGrid, empty.Cols);
            Assert.AreEqual(CellGeometry.MinGrid, empty.Rows);
        }

        [Test]
        public void CellBudgetHoldsAcrossEveryCellSizeAndScreen()
        {
            float[] widths = { 320f, 1179f, 1600f, 2048f, 2560f, 3840f, 7680f };
            float[] heights = { 240f, 720f, 900f, 1440f, 2556f, 2732f, 4320f };
            float[] dpis = { 0f, 96f, 160f, 192f, 227f, 264f, 460f };
            foreach (float width in widths)
            {
                foreach (float height in heights)
                {
                    foreach (float dpi in dpis)
                    {
                        for (int cell = CellGeometry.MinCellSize; cell <= CellGeometry.MaxCellSize; cell++)
                        {
                            foreach (bool mobile in new[] { false, true })
                            {
                                GridSpec grid = CellGeometry.GridFor(width, height, dpi, cell, mobile);
                                string where = $"{width}x{height} @{dpi} cell {cell} mobile {mobile}";
                                Assert.LessOrEqual(grid.Cols * grid.Rows, CellGeometry.MaxCells, where);
                                Assert.GreaterOrEqual(grid.Cols, CellGeometry.MinGrid, where);
                                Assert.GreaterOrEqual(grid.Rows, CellGeometry.MinGrid, where);
                                Assert.GreaterOrEqual(grid.EffectiveCellSize, cell, where);
                                Assert.Greater(grid.CellPx, 0f, where);
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public void AnEightKDisplayAtCellSizeOneCoarsensToTwelve()
        {
            // 7680x4320 at cell size 1 wants 33M cells; 12 px cells fit the
            // budget exactly (640x360 = 230,400).
            GridSpec grid = CellGeometry.GridFor(7680f, 4320f, 0f, 1, false);
            Assert.AreEqual(12, grid.EffectiveCellSize);
            Assert.AreEqual(640, grid.Cols);
            Assert.AreEqual(360, grid.Rows);
        }

        [Test]
        public void TheBudgetWinsOverTheSettingsRangeOnAnAbsurdCanvas()
        {
            // No shipping display needs this, but the rule matters: the 1..25
            // range is a UI affordance while the cell budget is a promise that a
            // step fits in a frame, so coarsening is allowed to pass 25.
            GridSpec grid = CellGeometry.GridFor(20000f, 20000f, 0f, 1, false);
            Assert.Greater(grid.EffectiveCellSize, CellGeometry.MaxCellSize);
            Assert.LessOrEqual(grid.Cols * grid.Rows, CellGeometry.MaxCells);
        }

        [Test]
        public void RequestedCellSizeIsClampedToTheSettingsRange()
        {
            // Out-of-range values (a corrupt PlayerPrefs, a stale URL parameter)
            // land on the ends of the range rather than producing a bad lattice.
            // A 400x300 canvas at cell size 1 is 120,000 cells, well inside the
            // budget, so nothing but the clamp can change the effective size.
            GridSpec low = CellGeometry.GridFor(400f, 300f, 0f, 0, false);
            Assert.AreEqual(CellGeometry.MinCellSize, low.EffectiveCellSize);
            Assert.AreEqual(400, low.Cols);
            GridSpec high = CellGeometry.GridFor(1600f, 900f, 0f, 99, false);
            Assert.AreEqual(CellGeometry.MaxCellSize, high.EffectiveCellSize);
            Assert.AreEqual(64, high.Cols);
            Assert.AreEqual(36, high.Rows);
        }
    }
}
