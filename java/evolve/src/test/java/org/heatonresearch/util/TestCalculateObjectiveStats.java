package org.heatonresearch.util;

import org.heatonresearch.mergelife.CalculateObjectiveStats;
import org.heatonresearch.mergelife.MergeLifeGrid;
import org.heatonresearch.mergelife.MergeLifeRule;
import org.junit.Assert;
import org.junit.Test;
import java.io.File;
import java.io.IOException;
import java.security.NoSuchAlgorithmException;
import java.util.HashMap;
import java.util.Map;
import java.util.Random;

public class TestCalculateObjectiveStats {
    @Test
    public void testStats() throws IOException, NoSuchAlgorithmException {
        ClassLoader classLoader = getClass().getClassLoader();
        File file = new File(classLoader.getResource("randomStart1.png").getFile());
        MergeLifeGrid grid = MergeLifeGrid.loadPNG(1, file);
        Assert.assertEquals("c25194b47e72b26fe7b83440c33f9f0ea89e263e5f9317069288f99b9df27251", grid.toSHA256(0));
        MergeLifeRule rule = new MergeLifeRule("ea44-55dF-9025-bEad-5f6e-45ca-6168-275a");

        CalculateObjectiveStats stats = new CalculateObjectiveStats(grid);
        Map<String,Double> s = null;
        while(!stats.hasStabilized()) {
            grid.step(rule);
            s = stats.track();
        }

        Assert.assertEquals(84, s.get(CalculateObjectiveStats.STAT_MODE), 0.1);
        Assert.assertEquals(997, s.get(CalculateObjectiveStats.STAT_MODE_AGE), 0.1);
        Assert.assertEquals(0.4016, s.get(CalculateObjectiveStats.STAT_BACKGROUND), 0.1);
        Assert.assertEquals(4016, s.get(CalculateObjectiveStats.STAT_MODE_COUNT), 0.1);
        Assert.assertEquals(0.0833, s.get(CalculateObjectiveStats.STAT_ACTIVE),0.1);
        Assert.assertEquals(0.6018, s.get(CalculateObjectiveStats.STAT_CHAOS), 0.6018);
        Assert.assertEquals(0.1023, s.get(CalculateObjectiveStats.STAT_RECT), 0.1);
        Assert.assertEquals(1001, s.get(CalculateObjectiveStats.STAT_STEPS), 0.1);
        Assert.assertEquals(0.0303, s.get(CalculateObjectiveStats.STAT_FOREGROUND), 0.1);

        Assert.assertEquals(84, stats.getCurrentStats().get(CalculateObjectiveStats.STAT_MODE),0.1);
        Assert.assertEquals(grid, stats.getGrid());
    }

}
