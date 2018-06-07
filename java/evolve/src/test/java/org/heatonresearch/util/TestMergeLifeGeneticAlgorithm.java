/*
 * MergeLife, Copyrighr 2018 by Jeff Heaton
 * http://www.heatonresearch.com/mergelife/
 * MIT License
 */
package org.heatonresearch.util;

import javafx.scene.media.MediaException;
import org.heatonresearch.mergelife.MergeLifeConfig;
import org.heatonresearch.mergelife.MergeLifeException;
import org.heatonresearch.mergelife.MergeLifeGeneticAlgorithm;
import org.heatonresearch.mergelife.MergeLifeGenome;
import org.junit.Assert;
import org.junit.Test;

import java.io.File;
import java.io.IOException;
import java.util.Random;

public class TestMergeLifeGeneticAlgorithm {

    @Test
    public void testInit() throws IOException {
        Random rnd = new Random(42);
        ClassLoader classLoader = getClass().getClassLoader();
        File file = new File(classLoader.getResource("quickConfig.json").getFile());
        MergeLifeConfig config = new MergeLifeConfig(file.toString());
        MergeLifeGeneticAlgorithm ga = new MergeLifeGeneticAlgorithm(rnd,config);
        ga.init();
        Assert.assertEquals(10,ga.getPopulation().size());
    }

    @Test
    public void testAddChild() throws IOException {
        Random rnd = new Random(42);
        ClassLoader classLoader = getClass().getClassLoader();
        File file = new File(classLoader.getResource("quickConfig.json").getFile());
        MergeLifeConfig config = new MergeLifeConfig(file.toString());
        MergeLifeGeneticAlgorithm ga = new MergeLifeGeneticAlgorithm(rnd,config);
        ga.init();
        MergeLifeGenome child1 = new MergeLifeGenome(rnd);
        ga.addChild(rnd, child1.getRuleText());
        Assert.assertTrue(ga.getPopulation().contains(child1));
    }

    @Test
    public void testMutate() throws IOException {
        Random rnd = new Random(42);
        ClassLoader classLoader = getClass().getClassLoader();
        File file = new File(classLoader.getResource("quickConfig.json").getFile());
        MergeLifeConfig config = new MergeLifeConfig(file.toString());
        MergeLifeGeneticAlgorithm ga = new MergeLifeGeneticAlgorithm(rnd,config);
        ga.init();
        MergeLifeGenome parent1 = new MergeLifeGenome(rnd);
        ga.addChild(rnd, parent1.getRuleText());
        String child1 = ga.mutate(rnd,parent1.getRuleText());
        Assert.assertEquals("e4df-994d-f9dd-13bb-c66e-4740-e632-5fb3", child1);
    }

    @Test
    public void testCrossover() throws IOException {
        Random rnd = new Random(42);
        ClassLoader classLoader = getClass().getClassLoader();
        File file = new File(classLoader.getResource("quickConfig.json").getFile());
        MergeLifeConfig config = new MergeLifeConfig(file.toString());
        MergeLifeGeneticAlgorithm ga = new MergeLifeGeneticAlgorithm(rnd,config);
        ga.init();
        MergeLifeGenome parent1 = new MergeLifeGenome(rnd);
        MergeLifeGenome parent2 = new MergeLifeGenome(rnd);
        ga.addChild(rnd, parent1.getRuleText());
        String[] children = ga.crossover(rnd,parent1.getRuleText(),parent2.getRuleText());
        Assert.assertEquals("6e6a-994d-f9dd-13bb-c66e-4760-e632-5fb3", children[0]);
        Assert.assertEquals("e4df-5513-50ad-7157-18b1-bec2-2bbd-ce2e", children[1]);
    }

    @Test
    public void testProcess() throws IOException, InterruptedException {
        Random rnd = new Random(42);
        ClassLoader classLoader = getClass().getClassLoader();
        File file = new File(classLoader.getResource("quickConfig.json").getFile());
        MergeLifeConfig config = new MergeLifeConfig(file.toString());
        MergeLifeGeneticAlgorithm ga = new MergeLifeGeneticAlgorithm(rnd,config);
        ga.process();
    }

    @Test(expected = MergeLifeException.class)
    public void testScoreError() throws IOException {
        Random rnd = new Random(42);
        MergeLifeConfig config = new MergeLifeConfig();
        MergeLifeGeneticAlgorithm ga = new MergeLifeGeneticAlgorithm(rnd,config);
        ga.score("E542-5F79-9341-F31E-6C6B-7F08-8773-7068");
    }

    @Test
    public void testScore() throws IOException {
        Random rnd = new Random(42);
        ClassLoader classLoader = getClass().getClassLoader();
        File file = new File(classLoader.getResource("quickConfig.json").getFile());
        MergeLifeConfig config = new MergeLifeConfig(file.toString());
        MergeLifeGeneticAlgorithm ga = new MergeLifeGeneticAlgorithm(rnd,config);
        double score = ga.score("E542-5F79-9341-F31E-6C6B-7F08-8773-7068");
        Assert.assertTrue(score>-0.5);
    }
}
