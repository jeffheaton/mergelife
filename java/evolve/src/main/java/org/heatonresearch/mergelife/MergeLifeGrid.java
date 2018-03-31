package org.heatonresearch.mergelife;

public class MergeLifeGrid {
    private final int[][][][] grid;
    private final int[][] mergeGrid;
    private int modeGrid;

    public MergeLifeGrid(int rows, int cols) {
        this.grid = new int[2][rows][cols][3];
        this.mergeGrid = new int[rows][cols];
    }

    public int[][][] getGrid(int desiredGrid) {
        if( desiredGrid<0 || desiredGrid>1) {
            throw new MergeLifeException("Only grids 0 or 1 are supported.");
        }
        return this.grid[desiredGrid];
    }

    public int[][] getMergeGrid() {
        return this.mergeGrid;
    }

    public int getRows() {
        return this.mergeGrid.length;
    }

    public int getCols() {
        return this.mergeGrid[0].length;
    }

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


}
