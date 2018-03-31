package org.heatonresearch.util;

import org.heatonresearch.mergelife.util.LargestRect;
import org.junit.Assert;
import org.junit.Test;

public class TestLargestRect {
    @Test
    public void testCase1() {
        int[][] matrix = {
                {0, 0, 0, 0, 1, 0},
                {0, 0, 1, 0, 0, 1},
                {0, 0, 0, 0, 0, 0},
                {1, 0, 0, 0, 0, 0},
                {0, 0, 0, 0, 0, 1},
                {0, 0, 1, 0, 0, 0}
        };
        Assert.assertEquals(12, LargestRect.maximalRectangle(matrix,0));
    }

    @Test
    public void testCase1a() {
        int[][] matrix1 = {{1,1},{0,0}};
        int[][] matrix2 = {{0,0},{1,1}};
        int[][] matrix3 = {{1,0},{1,0}};
        int[][] matrix4 = {{0,1},{0,1}};

        Assert.assertEquals(2, LargestRect.maximalRectangle(matrix1,0));
        Assert.assertEquals(2, LargestRect.maximalRectangle(matrix2,0));
        Assert.assertEquals(2, LargestRect.maximalRectangle(matrix3,0));
        Assert.assertEquals(2, LargestRect.maximalRectangle(matrix4,0));
    }

    @Test
    public void testCase2() {
        int[][] matrix = {
                {0, 0, 0, 0, 1, 0},
                {0, 0, 1, 0, 0, 1},
                {0, 0, 0, 0, 0, 0},
                {1, 0, 0, 0, 0, 0},
                {0, 0, 0, 0, 0, 1},
                {0, 0, 1, 0, 0, 0},
                {0, 0, 0, 0, 0, 0},
                {0, 0, 0, 0, 0, 0}
        };
        Assert.assertEquals(14, LargestRect.maximalRectangle(matrix,0));
    }

    @Test
    public void testCase2a() {
        int[][] matrix1 = {{}};
        int[][] matrix2 = {};

        Assert.assertEquals(0, LargestRect.maximalRectangle(matrix1,0));
        Assert.assertEquals(0, LargestRect.maximalRectangle(matrix2,0));
    }

    @Test
    public void testCase3() {
        int[][] matrix1 = {
                {0, 0, 0, 0, 1, 0},
                {0, 0, 1, 0, 0, 1},
                {0, 0, 0, 0, 0, 0},
                {1, 0, 0, 0, 0, 0},
                {0, 0, 0, 0, 0, 0},
                {0, 0, 1, 0, 0, 1},
                {0, 0, 0, 0, 0, 0},
                {0, 0, 0, 0, 0, 0}
        };

        Assert.assertEquals(15, LargestRect.maximalRectangle(matrix1,0));
    }

    @Test
    public void testCase4() {
        int[][] matrix1 = {
                {0, 0, 0, 0, 1, 0},
                {0, 0, 0, 0, 0, 0},
                {0, 0, 1, 0, 0, 1},
                {0, 0, 0, 0, 0, 0},
                {1, 0, 0, 0, 0, 0},
                {0, 0, 0, 0, 0, 0},
                {0, 0, 1, 0, 0, 1},
                {0, 0, 0, 0, 0, 0},
                {0, 0, 0, 0, 0, 1}
        };

        Assert.assertEquals(16, LargestRect.maximalRectangle(matrix1,0));
    }

    @Test
    public void testCase5() {
        int[][] matrix1 = {
                {0, 0, 0, 0, 1, 1, 1},
                {0, 0, 0, 0, 0, 0, 0},
                {0, 0, 0, 1, 1, 1, 1},
                {0, 0, 1, 1, 1, 1, 1},
                {1, 0, 1, 1, 1, 1, 1},
                {1, 0, 1, 1, 1, 1, 1},
                {1, 0, 1, 1, 1, 1, 1}
        };

        Assert.assertEquals(9, LargestRect.maximalRectangle(matrix1,0));
    }

}