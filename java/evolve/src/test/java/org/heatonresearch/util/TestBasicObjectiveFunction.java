package org.heatonresearch.util;

import org.heatonresearch.mergelife.*;
import org.junit.Assert;
import org.junit.Test;

import java.io.File;
import java.io.IOException;
import java.util.Random;

public class TestBasicObjectiveFunction {
    public double obtainStat(String statName, double value) throws IOException {
        // make a grid and obj stats
        MergeLifeGrid grid = new MergeLifeGrid(2,2);
        CalculateObjectiveStats objStats = new CalculateObjectiveStats(grid);

        if(!Double.isNaN(value)) {
            objStats.getCurrentStats().put(statName, value);
        }

        // read from config file
        ClassLoader classLoader = getClass().getClassLoader();
        File file = new File(classLoader.getResource("config1.json").getFile());
        MergeLifeConfig config = new MergeLifeConfig(file.toString());
        BasicObjectiveFunction objectiveFunction = (BasicObjectiveFunction)config.getObjectiveFunction();

        for(BasicObjectiveFunction.ObjectiveFunctionStat stat: objectiveFunction.getStats()) {
            if(stat.getStat().equals(statName)) {
                return stat.calculate(objStats);
            }
        }
        Assert.assertTrue(false);
        return 0;
    }

    @Test
    public void testCalculate() throws IOException {
        double d = obtainStat(CalculateObjectiveStats.STAT_STEPS, 300);
        Assert.assertEquals(0.8571,d,0.001);
    }

    @Test
    public void testCalculateOver() throws IOException {
        double d = obtainStat(CalculateObjectiveStats.STAT_STEPS, 100);
        Assert.assertEquals(-1.0,d,0.001);
    }

    @Test
    public void testCalculateUnder() throws IOException {
        double d = obtainStat(CalculateObjectiveStats.STAT_STEPS, 1100);
        Assert.assertEquals(1.0,d,0.001);
    }

    @Test(expected = MergeLifeException.class)
    public void testCalculateError() throws IOException {
        obtainStat(CalculateObjectiveStats.STAT_STEPS, Double.NaN);
    }

    @Test
    public void testCalculateScore() throws IOException {
        // read from config file
        ClassLoader classLoader = getClass().getClassLoader();
        File file = new File(classLoader.getResource("quickConfig.json").getFile());
        MergeLifeConfig config = new MergeLifeConfig(file.toString());

        double score = config.getObjectiveFunction().calculateObjective("E542-5F79-9341-F31E-6C6B-7F08-8773-7068", new Random(42));
        Assert.assertEquals(-0.04, score, 0.01);
    }

    @Test
    public void testCalculateScoreSolid() throws IOException {
        // read from config file
        ClassLoader classLoader = getClass().getClassLoader();
        File file = new File(classLoader.getResource("quickConfig.json").getFile());
        MergeLifeConfig config = new MergeLifeConfig(file.toString());

        double score = config.getObjectiveFunction().calculateObjective("ff7f-0000-0000-0000-0000-0000-0000-0000", new Random(42));
        Assert.assertEquals(-0.04, score, 0.01);
    }

    @Test
    public void testReport() throws IOException {

        // read from config file
        ClassLoader classLoader = getClass().getClassLoader();
        File file = new File(classLoader.getResource("quickConfig.json").getFile());
        MergeLifeConfig config = new MergeLifeConfig(file.toString());

        MergeLifeReportable rept = message -> { };
        config.getObjectiveFunction().setReport(rept);
        Assert.assertEquals(rept,config.getObjectiveFunction().getReport());
        double score = config.getObjectiveFunction().calculateObjective("E542-5F79-9341-F31E-6C6B-7F08-8773-7068", new Random(42));
        Assert.assertEquals(-0.04, score, 0.01);
    }
}
