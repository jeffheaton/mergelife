package org.heatonresearch.util;

import org.heatonresearch.mergelife.MergeLifeException;
import org.heatonresearch.mergelife.MergeLifeGrid;
import org.heatonresearch.mergelife.MergeLifeRule;
import org.junit.Assert;
import org.junit.Test;

import java.io.File;
import java.io.IOException;
import java.security.NoSuchAlgorithmException;
import java.util.Random;

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

    @Test
    public void testRandomize() {
        MergeLifeGrid grid = new MergeLifeGrid(2,2);
        grid.randomize(0, new Random(42));
        Assert.assertEquals(186, grid.getGrid(0)[0][0][0]);
        Assert.assertEquals(174, grid.getGrid(0)[0][0][1]);
        Assert.assertEquals(79, grid.getGrid(0)[0][0][2]);

        Assert.assertEquals(94, grid.getGrid(0)[1][0][0]);
        Assert.assertEquals(70, grid.getGrid(0)[1][0][1]);
        Assert.assertEquals(118, grid.getGrid(0)[1][0][2]);
    }

    private void setPixel(MergeLifeGrid grid, int row, int col, int value) {
        grid.getGrid(0)[row][col][0] = value;
        grid.getGrid(0)[row][col][1] = value;
        grid.getGrid(0)[row][col][2] = value;
    }

    private MergeLifeGrid createGrid() {
        int value = 0;
        MergeLifeGrid grid = new MergeLifeGrid(3,3);
        for(int row=0;row<3;row++) {
            for(int col=0;col<3;col++) {
                setPixel(grid,row,col,value);
                value+=16;
            }
        }
        return grid;
    }

    @Test
    public void testModeGrid() {
        MergeLifeGrid grid = createGrid();
        setPixel(grid,1,1,32);
        grid.calculateModeGrid(0);
        Assert.assertEquals(32,grid.getModeGrid());

    }

    @Test
    public void testCountNeighbors() {
        MergeLifeGrid grid = createGrid();
        grid.calculateModeGrid(0);
        int c1 = grid.countNeighbors(1,1);
        int c2 = grid.countNeighbors(0,0);
        Assert.assertEquals(512, c1);
        Assert.assertEquals(128, c2);
    }

    private void checkUpdate(MergeLifeGrid grid, int row, int col, int idx, int from, int to) {
        Assert.assertEquals(from, grid.getGrid(0)[row][col][idx]);
        Assert.assertEquals(to, grid.getGrid(1)[row][col][idx]);
    }

    @Test
    public void testStep() {
        MergeLifeGrid grid = createGrid();
        MergeLifeRule rule = new MergeLifeRule("2080-0000-6040-0000-0000-0000-0000-0000");
        grid.step(rule);

        Assert.assertEquals(0, grid.getGrid(0)[0][0][0]);
        Assert.assertEquals(255, grid.getGrid(1)[0][0][0]);

        checkUpdate(grid,0,0, 0, 0,255);
        checkUpdate(grid,0,0,1,0,0);
        checkUpdate(grid,0,0,2,0,0);
        checkUpdate(grid,0,1,0,16,255);
        checkUpdate(grid,0,1,1,16,0);
        checkUpdate(grid,0,1,2,16,0);
        checkUpdate(grid,0,2,0,32,255);
        checkUpdate(grid,0,2,1,32,0);
        checkUpdate(grid,0,2,2,32,0);
        checkUpdate(grid,1,0,0,48,23);
        checkUpdate(grid,1,0,1,48,152);
        checkUpdate(grid,1,0,2,48,23);
        checkUpdate(grid,1,1,0,64,31);
        checkUpdate(grid,1,1,1,64,160);
        checkUpdate(grid,1,1,2,64,31);
        checkUpdate(grid,1,2,0,80,39);
        checkUpdate(grid,1,2,1,80,168);
        checkUpdate(grid,1,2,2,80,39);
        checkUpdate(grid,2,0,0,96,255);
        checkUpdate(grid,2,0,1,96,0);
        checkUpdate(grid,2,0,2,96,0);
        checkUpdate(grid,2,1,0,112,55);
        checkUpdate(grid,2,1,1,112,184);
        checkUpdate(grid,2,1,2,112,55);
        checkUpdate(grid,2,2,0,128,63);
        checkUpdate(grid,2,2,1,128,192);
        checkUpdate(grid,2,2,2,128,63);
    }

    @Test
    public void testSavePNG() throws IOException, NoSuchAlgorithmException {
        File tempFile = File.createTempFile("mergelife-test-", ".png");
        tempFile.deleteOnExit();
        MergeLifeGrid grid = createGrid();
        String before = grid.toSHA256(0);
        grid.savePNG(0,1,tempFile);
        MergeLifeGrid grid2 = grid.loadPNG(1, tempFile);
        String after = grid2.toSHA256(0);
        Assert.assertEquals("eae2f610ad238a0cc6d840af033606b74fa104a437c9d6665c84059d2bc27e53",before);
        Assert.assertEquals("eae2f610ad238a0cc6d840af033606b74fa104a437c9d6665c84059d2bc27e53",after);
    }

    public void testCurrentGrid() {
        MergeLifeGrid grid = createGrid();
        MergeLifeRule rule = new MergeLifeRule("2080-0000-6040-0000-0000-0000-0000-0000");
        Assert.assertEquals(0,grid.getCurrentGrid());
        Assert.assertEquals(0,grid.getCurrentStep());
        grid.step(rule);
        Assert.assertEquals(1,grid.getCurrentGrid());
        Assert.assertEquals(1,grid.getCurrentStep());
        grid.step(rule);
        Assert.assertEquals(0,grid.getCurrentGrid());
        Assert.assertEquals(2,grid.getCurrentStep());
    }

    public void testSetCurrentGrid() {
        MergeLifeGrid grid = createGrid();
        grid.setCurrentGrid(0);
        MergeLifeRule rule = new MergeLifeRule("2080-0000-6040-0000-0000-0000-0000-0000");
        Assert.assertEquals(0,grid.getCurrentGrid());
        Assert.assertEquals(0,grid.getCurrentStep());
        grid.step(rule);
        grid.setCurrentStep(10);
        Assert.assertEquals(1,grid.getCurrentGrid());
        Assert.assertEquals(10,grid.getCurrentStep());
        grid.step(rule);
        Assert.assertEquals(0,grid.getCurrentGrid());
        Assert.assertEquals(11,grid.getCurrentStep());
    }

    @Test
    public void testLastNegative() {
        MergeLifeRule rule = new MergeLifeRule("136C-67D1-D2AF-49FF-9E1C-5FA6-36AA-83B5");
        MergeLifeGrid grid = new MergeLifeGrid(100,100);
        grid.randomize(0,new Random());
        grid.step(rule);
        Assert.assertTrue(rule.getSubRules().get(7).getBeta()<0);
    }

    @Test
    public void testGridAttributes() {
        MergeLifeGrid grid = createGrid();
        grid.setCurrentGrid(42);
        Assert.assertEquals(42, grid.getCurrentGrid());

        grid.setCurrentStep(43);
        Assert.assertEquals(43, grid.getCurrentStep());
    }
}
