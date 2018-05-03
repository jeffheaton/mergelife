package org.heatonresearch.util;

import org.heatonresearch.mergelife.EvaluateObjective;
import org.heatonresearch.mergelife.MergeLifeGenome;
import org.junit.Assert;
import org.junit.Test;

import java.util.Random;

public class TestMergeLifeGenome {

    public static class FixedObjTest implements EvaluateObjective {
        private double fixedScore;

        public FixedObjTest(int theFixedScore) {
            this.fixedScore = theFixedScore;
        }

        @Override
        public double calculateObjective(String ruleText, Random rnd) {
            return this.fixedScore;
        }
    }


    @Test
    public void testRandomRule() {
        MergeLifeGenome genome = new MergeLifeGenome(new Random(42));
        Assert.assertEquals("ba41-aee7-4f08-46ee-aa61-e743-5e68-4697",genome.getRuleText());
        Assert.assertEquals("[NaN, ba41-aee7-4f08-46ee-aa61-e743-5e68-4697]",genome.toString());
        Assert.assertTrue(Double.isNaN(genome.getScore()));
    }

    @Test
    public void testEvaluateScore() {
        Random random = new Random(42);
        MergeLifeGenome genome1 = new MergeLifeGenome(random);
        MergeLifeGenome genome2 = new MergeLifeGenome(random);
        EvaluateObjective obj1 = new FixedObjTest(1);
        EvaluateObjective obj2 = new FixedObjTest(2);
        genome1.calculateScore(obj1, random);
        genome2.calculateScore(obj2, random);
        Assert.assertEquals(1, genome1.getScore(),0.1);
        Assert.assertEquals(2, genome2.getScore(),0.1);
        Assert.assertEquals(1, genome1.calculateScore(obj1,random),0.1);
        Assert.assertEquals(2, genome2.calculateScore(obj2,random),0.1);
        Assert.assertEquals(-1, genome1.compareTo(genome2));
        Assert.assertEquals(1, genome2.compareTo(genome1));
        Assert.assertFalse(genome1.equals(genome2));
        Assert.assertTrue(genome1.equals(genome1));
        Assert.assertNotEquals(0, genome1.hashCode());
        Assert.assertFalse(genome1.equals(""));
    }
}
