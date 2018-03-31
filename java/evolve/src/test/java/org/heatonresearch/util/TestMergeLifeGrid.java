package org.heatonresearch.util;

import org.heatonresearch.mergelife.MergeLifeException;
import org.heatonresearch.mergelife.MergeLifeGrid;
import org.heatonresearch.mergelife.MergeLifeRule;
import org.junit.Assert;
import org.junit.Test;

public class TestMergeLifeGrid {

    @Test
    public void testRowsColumns() {
        MergeLifeGrid grid = new MergeLifeGrid(50,100);
        Assert.assertEquals(50, grid.getRows());
        Assert.assertEquals(100, grid.getCols());
    }

    @Test
    public void testGrids() {
        MergeLifeGrid grid = new MergeLifeGrid(50,100);
        int[][][] g1 = grid.getGrid(0);
        int[][][] g2 = grid.getGrid(1);

        Assert.assertFalse(g1==g2);
        g1[0][0][0] = 1;
        g2[0][0][0] = 2;

        Assert.assertNotEquals(g1[0][0][0],g2[0][0][0]);
    }

    @Test
    public void testMerge() {
        MergeLifeGrid grid = new MergeLifeGrid(2,3);

        grid.getGrid(0)[0][0][0] = 0;
        grid.getGrid(0)[0][0][1] = 0;
        grid.getGrid(0)[0][0][2] = 0;

        grid.getGrid(0)[0][1][0] = 0;
        grid.getGrid(0)[0][1][1] = 255;
        grid.getGrid(0)[0][1][2] = 0;

        grid.getGrid(0)[0][2][0] = 0;
        grid.getGrid(0)[0][2][1] = 255;
        grid.getGrid(0)[0][2][2] = 0;

        grid.getGrid(0)[1][0][0] = 64;
        grid.getGrid(0)[1][0][1] = 0;
        grid.getGrid(0)[1][0][2] = 0;

        grid.getGrid(0)[1][1][0] = 0;
        grid.getGrid(0)[1][1][1] = 32;
        grid.getGrid(0)[1][1][2] = 0;

        grid.getGrid(0)[1][2][0] = 0;
        grid.getGrid(0)[1][2][1] = 0;
        grid.getGrid(0)[1][2][2] = 16;

        grid.calculateModeGrid(0);

        Assert.assertEquals(0,grid.getMergeGrid()[0][0]);
        Assert.assertEquals(85,grid.getMergeGrid()[0][1]);
        Assert.assertEquals(85,grid.getMergeGrid()[0][2]);

        Assert.assertEquals(21,grid.getMergeGrid()[1][0]);
        Assert.assertEquals(10,grid.getMergeGrid()[1][1]);
        Assert.assertEquals(5,grid.getMergeGrid()[1][2]);

        Assert.assertEquals(85,grid.getModeGrid());
    }

    @Test(expected = MergeLifeException.class)
    public void testIndexOutOfBoundsException() {
        MergeLifeGrid grid = new MergeLifeGrid(2,3);
        grid.getGrid(2);
    }
}
