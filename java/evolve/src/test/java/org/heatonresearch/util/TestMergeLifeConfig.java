package org.heatonresearch.util;

import org.heatonresearch.mergelife.MergeLifeConfig;
import org.heatonresearch.mergelife.MergeLifeException;
import org.heatonresearch.mergelife.ObjectiveFunction;
import org.junit.Assert;
import org.junit.Test;

import java.io.File;
import java.io.IOException;

public class TestMergeLifeConfig {
    @Test
    public void testBasicReadConfig() throws IOException {
        ClassLoader classLoader = getClass().getClassLoader();
        File file = new File(classLoader.getResource("config1.json").getFile());
        MergeLifeConfig config = new MergeLifeConfig(file.toString());
        Assert.assertEquals(100,config.getRows());
        Assert.assertEquals(200,config.getCols());
        Assert.assertEquals(50,config.getPopulationSize());
        Assert.assertEquals(5, config.getTournamentCycles());
        Assert.assertEquals(75, (int)(config.getCrossoverPct()*100));

        Assert.assertEquals(5, config.getObjectiveFunction().getStats().size());
        for(ObjectiveFunction.ObjectiveFunctionStat stat: config.getObjectiveFunction().getStats()) {
            if(stat.equals("steps")) {
                Assert.assertEquals(1000,stat.getMax(), 0.01);
                Assert.assertEquals(300,stat.getMin(), 0.01);
                Assert.assertEquals(1,stat.getWeight(), 0.01);
                Assert.assertEquals(-1,stat.getMinWeight(), 0.01);
                Assert.assertEquals(1,stat.getMaxWeight(), 0.01);
            }
        }

    }

    @Test
    public void testStringDoubleConfig() throws IOException {
        ClassLoader classLoader = getClass().getClassLoader();
        File file = new File(classLoader.getResource("config2.json").getFile());
        MergeLifeConfig config = new MergeLifeConfig(file.toString());
        Assert.assertEquals(75, (int)(config.getCrossoverPct()*100));
    }

    @Test(expected = MergeLifeException.class)
    public void testBadIntConfig() throws IOException {
        ClassLoader classLoader = getClass().getClassLoader();
        File file = new File(classLoader.getResource("config3.json").getFile());
        MergeLifeConfig config = new MergeLifeConfig(file.toString());
    }

    @Test(expected = MergeLifeException.class)
    public void testBadDoubleConfig() throws IOException {
        ClassLoader classLoader = getClass().getClassLoader();
        File file = new File(classLoader.getResource("config4.json").getFile());
        MergeLifeConfig config = new MergeLifeConfig(file.toString());
    }

    @Test(expected = MergeLifeException.class)
    public void testMissingIntConfig() throws IOException {
        ClassLoader classLoader = getClass().getClassLoader();
        File file = new File(classLoader.getResource("config5.json").getFile());
        MergeLifeConfig config = new MergeLifeConfig(file.toString());
    }

    @Test(expected = MergeLifeException.class)
    public void testMissingDoubleConfig() throws IOException {
        ClassLoader classLoader = getClass().getClassLoader();
        File file = new File(classLoader.getResource("config6.json").getFile());
        MergeLifeConfig config = new MergeLifeConfig(file.toString());
    }

}
