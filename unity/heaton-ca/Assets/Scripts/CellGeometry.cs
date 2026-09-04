using System;

namespace HeatonCAApp
{
    /// <summary>
    /// The lattice a canvas of a given pixel size gets: how many columns and
    /// rows, and how many device pixels one cell occupies.
    ///
    /// <see cref="EffectiveCellSize"/> is the cell size actually used, which is
    /// the requested one unless the canvas was so dense that the lattice had to
    /// be coarsened to stay under <see cref="CellGeometry.MaxCells"/>; the
    /// Simulator surfaces that as a note.
    /// </summary>
    public readonly struct GridSpec
    {
        /// <summary>Lattice columns; never below <see cref="CellGeometry.MinGrid"/>.</summary>
        public readonly int Cols;

        /// <summary>Lattice rows; never below <see cref="CellGeometry.MinGrid"/>.</summary>
        public readonly int Rows;

        /// <summary>The cell size the lattice was actually cut at (see the type summary).</summary>
        public readonly int EffectiveCellSize;

        /// <summary>Device pixels per cell at <see cref="EffectiveCellSize"/>.</summary>
        public readonly float CellPx;

        /// <summary>Builds a spec; callers get theirs from <see cref="CellGeometry.GridFor"/>.</summary>
        public GridSpec(int cols, int rows, int effectiveCellSize, float cellPx)
        {
            Cols = cols;
            Rows = rows;
            EffectiveCellSize = effectiveCellSize;
            CellPx = cellPx;
        }

        /// <summary>True when the canvas forced a coarser lattice than the settings asked for.</summary>
        public bool WasCapped(int requestedCellSize) => EffectiveCellSize > requestedCellSize;

        /// <summary>Width in device pixels of the lattice drawn at whole cells.</summary>
        public float WidthPx => Cols * CellPx;

        /// <summary>Height in device pixels of the lattice drawn at whole cells.</summary>
        public float HeightPx => Rows * CellPx;
    }

    /// <summary>
    /// Turns the Settings "Cell Size (1-25)" number into a lattice for the
    /// screen in front of the user.
    ///
    /// PyQt used the setting as a literal pixel count, which was fine on one
    /// desktop and useless everywhere else: 5 px cells vanish on a 460 dpi
    /// phone and look like postage stamps on a 96 dpi monitor. So the setting is
    /// read as a *density-independent* size and converted to device pixels here:
    /// mobile scales by dpi/160 (Android's density-independent pixel, which iOS
    /// dpi values land close enough to), desktop by whole-ish dpi/96 with a
    /// floor of 1 so a plain 1080p monitor keeps PyQt's exact pixel counts.
    ///
    /// The lattice then fills the canvas at whole cells, coarsening if the count
    /// would blow past <see cref="MaxCells"/> (a 264 dpi tablet asking for cell
    /// size 1 wants two million cells, which no device steps at 30 gen/s).
    ///
    /// Pure math — no UnityEngine types, so the whole model is unit-testable.
    /// </summary>
    public static class CellGeometry
    {
        /// <summary>Smallest cell size the Settings screen offers.</summary>
        public const int MinCellSize = 1;

        /// <summary>Largest cell size the Settings screen offers.</summary>
        public const int MaxCellSize = 25;

        /// <summary>
        /// Ceiling on Cols*Rows, 512*512. Above this a MergeLife step costs more
        /// than a frame even on a desktop, so the lattice is coarsened instead.
        /// </summary>
        public const int MaxCells = 262144;

        /// <summary>
        /// Floor on Cols and Rows. A sliver of a canvas (a mid-rotation layout
        /// pass, a collapsed panel) must still produce a runnable world.
        /// </summary>
        public const int MinGrid = 8;

        /// <summary>Reference density for mobile: Android's density-independent pixel.</summary>
        public const float MobileReferenceDpi = 160f;

        /// <summary>Reference density for desktop: the classic 96 dpi monitor.</summary>
        public const float DesktopReferenceDpi = 96f;

        /// <summary>
        /// Device pixels one cell of <paramref name="cellSize"/> occupies.
        ///
        /// An unknown density — <c>Screen.dpi</c> returns 0 on many desktops and
        /// 1 on some emulators — means the setting is used as a literal pixel
        /// count, which is exactly the PyQt behavior.
        /// </summary>
        public static float CellPixels(int cellSize, float dpi, bool isMobile)
        {
            if (cellSize < MinCellSize)
            {
                cellSize = MinCellSize;
            }
            if (!(dpi > 1f))
            {
                return cellSize;
            }
            return isMobile
                ? cellSize * dpi / MobileReferenceDpi
                : cellSize * Math.Max(1f, dpi / DesktopReferenceDpi);
        }

        /// <summary>
        /// The lattice for a canvas of <paramref name="widthPx"/> x
        /// <paramref name="heightPx"/> device pixels: as many whole cells as fit,
        /// coarsened one cell size at a time until Cols*Rows fits
        /// <see cref="MaxCells"/>, then floored at <see cref="MinGrid"/> in each
        /// direction.
        ///
        /// The coarsening loop can push <see cref="GridSpec.EffectiveCellSize"/>
        /// past <see cref="MaxCellSize"/> on an extreme canvas (an 8K display
        /// asking for cell size 1). That is deliberate: the cell budget is a
        /// performance contract and the settings range is only a UI range.
        /// </summary>
        public static GridSpec GridFor(float widthPx, float heightPx, float dpi, int cellSize, bool isMobile)
        {
            int requested = Math.Clamp(cellSize, MinCellSize, MaxCellSize);
            int effective = requested;
            float cellPx = CellPixels(effective, dpi, isMobile);
            int cols = CellCount(widthPx, cellPx);
            int rows = CellCount(heightPx, cellPx);
            // Coarsen until the lattice fits the cell budget. Each step raises
            // cellPx, so cols*rows strictly shrinks and the loop terminates; the
            // 8x8 floor is applied afterwards, never inside the loop, or a
            // clamped-up dimension could re-trip the budget.
            while ((long)cols * rows > MaxCells)
            {
                effective++;
                cellPx = CellPixels(effective, dpi, isMobile);
                cols = CellCount(widthPx, cellPx);
                rows = CellCount(heightPx, cellPx);
            }
            return new GridSpec(Math.Max(MinGrid, cols), Math.Max(MinGrid, rows), effective, cellPx);
        }

        /// <summary>Whole cells of <paramref name="cellPx"/> pixels that fit in <paramref name="extentPx"/>.</summary>
        private static int CellCount(float extentPx, float cellPx)
        {
            if (!(extentPx > 0f) || !(cellPx > 0f))
            {
                return 0;
            }
            return (int)Math.Floor(extentPx / cellPx);
        }
    }
}
