package org.heatonresearch.util;

import org.heatonresearch.mergelife.MergeLifeGenome;
import org.junit.Assert;
import org.junit.Test;

import java.util.Random;

public class TestMergeLifeGenome {
    @Test
    public void testRandomRule() {
        MergeLifeGenome genome = new MergeLifeGenome(new Random(42));
        Assert.assertEquals("ba41-aee7-4f08-46ee-aa61-e743-5e68-4697",genome.getRuleText());
        Assert.assertEquals("[NaN, ba41-aee7-4f08-46ee-aa61-e743-5e68-4697]",genome.toString());
        Assert.assertTrue(Double.isNaN(genome.getScore()));
    }
}
