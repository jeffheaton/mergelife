package org.heatonresearch.mergelife;

public class MergeLifeGrid {
    /**
     * The two grids (double buffering) for MergeLife. Thge first dimension is the
     * grid to use (2 grids supported).  The second and third are the rows and columns.
     * The fourth dimension are the RGB values.
     */
    private final int[][][][] grid;

    /**
     * The merged grid.  This is an average of the 3 RGB values for one of the two
     * RGB grids.
     */
    private final int[][] mergeGrid;

    /**
     * The mode of the grid, this is the most common value in the merged grid.  This is
     * the merged RGB values of the background color.
     */
    private int modeGrid;

    /**
     * Create a MergeLife grid with the specified number of rows and columns.
     * @param rows The number of rows in the grid.
     * @param cols The number of columns in the grid.
     */
    public MergeLifeGrid(int rows, int cols) {
        this.grid = new int[2][rows][cols][3];
        this.mergeGrid = new int[rows][cols];
    }

    /**
     * Get one of the two grids.  There are two grids to support double buffering.
     * @param desiredGrid The desired grid (0 or 1).
     * @return A 3D array holding the grid (with RGB values).
     */
    public int[][][] getGrid(int desiredGrid) {
        if( desiredGrid<0 || desiredGrid>1) {
            throw new MergeLifeException("Only grids 0 or 1 are supported.");
        }
        return this.grid[desiredGrid];
    }

    /**
     * @return The merged grid, this is the average of the 3 RGB values in one of the
     * main grids.  This value is calculated by calling the method calculateModeGrid.
     */
    public int[][] getMergeGrid() {
        return this.mergeGrid;
    }

    /**
     * @return The number of rows in a grid.
     */
    public int getRows() {
        return this.mergeGrid.length;
    }

    /**
     * @return The number of columns in the grid.
     */
    public int getCols() {
        return this.mergeGrid[0].length;
    }

    /**
     * @return Get the most common merged value from the last time that the method
     * calculateModeGrid was called.
     */
    public int getModeGrid() {
        return this.modeGrid;
    }


    public void calculateModeGrid(int desiredGrid) {
        int[] hist = new int[256];

        // First calculate the merged grid, also build up a histogram of the avrage 0-255 colors.
        for(int row = 0; row<getRows(); row++) {
            for(int col = 0; col<getCols(); col++) {
                int[] px = this.grid[desiredGrid][row][col];
                int a = (px[0]+px[1]+px[2])/3;
                this.mergeGrid[row][col] = a;
                hist[a]++;
            }
        }

        // Now determine which merged color (0-255) is used the most.  This is the grid mode.
        int maxIndex = -1;
        for(int i=0;i<hist.length;i++) {
            if( maxIndex==-1 || hist[i]>hist[maxIndex]) {
                maxIndex =i;
            }
        }
        this.modeGrid = maxIndex;
    }

    /**
     * Count the neighbors next to the given cell.
     * @param row The row of the current cell.
     * @param col The column of the current cell.
     * @return The neighbor count.
     */
    public int countNeighbors(int row, int col) {
        final int[] xHat = {0, 0, -1, 1, -1, 1, 1, -1};
        final int[] yHat = {-1, 1, 0, 0, -1, 1, -1, 1};

        int sum = 0;

        for (int i = 0; i < xHat.length; i++)
        {
            final int neighborRow = yHat[i] + row;
            final int neighborCol = xHat[i] + col;
            if (neighborRow >= 0 &&
                    neighborCol >= 0 &&
                    neighborRow < getRows() &&
                    neighborCol < getCols())
            {
                sum += this.mergeGrid[neighborRow][neighborCol];
            } else {
                sum += getModeGrid();
            }
        }
        return sum;
    }

    /**
     * Peform one step/generation on the grid with the specified rule.
     * @param rule The rule to apply.
     */
    public void step(MergeLifeRule rule) {
        calculateModeGrid(0);

        int[][][] m = getGrid(0);
        calculateModeGrid(0);
        //int b = getModeGrid();

        for (int row = 0; row < getRows(); row += 1) {
            int[][] line = getGrid(0)[row];
            int[][] linePrime = getGrid(1)[row];
            for (int col = 0; col < line.length; col += 1) {
                int c = countNeighbors(row,col);

                for(MergeLifeRule.SubRule subRule: rule.getSubRules()) {
                    if( c < subRule.getAlpha() ) {
                        int dPrime = subRule.getAlpha();

                        if( subRule.getBeta()<0) {
                            dPrime=(dPrime%8)+1;
                        }

                        for(int j=0;j<3;j++) {
                            int delta = MergeLifeRule.ColorTable[dPrime][j] - line[col][j];
                            delta = (int)Math.floor(delta*subRule.getBeta());
                            linePrime[col][j] = line[col][j] + delta;
                        }
                        break;
                    }
                }
            }
        }
    }
}
