package org.heatonresearch.util;

import org.heatonresearch.mergelife.MergeLifeException;
import org.heatonresearch.mergelife.MergeLifeRule;
import org.junit.Assert;
import org.junit.Test;

import java.util.Random;

public class TestMergeLifeRule {

    @Test
    public void testDecode1() {
        MergeLifeRule rule = new MergeLifeRule("E542-5F79-9341-F31E-6C6B-7F08-8773-7068");

        Assert.assertEquals(760, rule.getSubRules().get(0).getAlpha());
        Assert.assertEquals(0.9527, rule.getSubRules().get(0).getBeta(), 0.01);
        Assert.assertEquals(1, rule.getSubRules().get(0).getGamma());

        Assert.assertEquals(864, rule.getSubRules().get(1).getAlpha());
        Assert.assertEquals(0.8425, rule.getSubRules().get(1).getBeta(), 0.01);
        Assert.assertEquals(4, rule.getSubRules().get(1).getGamma());

        Assert.assertEquals(896, rule.getSubRules().get(2).getAlpha());
        Assert.assertEquals(0.8188, rule.getSubRules().get(2).getBeta(), 0.01);
        Assert.assertEquals(7, rule.getSubRules().get(2).getGamma());

        Assert.assertEquals(1016, rule.getSubRules().get(3).getAlpha());
        Assert.assertEquals(0.06299, rule.getSubRules().get(3).getBeta(), 0.01);
        Assert.assertEquals(5, rule.getSubRules().get(3).getGamma());

        Assert.assertEquals(1080, rule.getSubRules().get(4).getAlpha());
        Assert.assertEquals(0.9055, rule.getSubRules().get(4).getBeta(), 0.01);
        Assert.assertEquals(6, rule.getSubRules().get(4).getGamma());

        Assert.assertEquals(1176, rule.getSubRules().get(5).getAlpha());
        Assert.assertEquals(0.5118, rule.getSubRules().get(5).getBeta(), 0.01);
        Assert.assertEquals(2, rule.getSubRules().get(5).getGamma());

        Assert.assertEquals(1832, rule.getSubRules().get(6).getAlpha());
        Assert.assertEquals(0.5196, rule.getSubRules().get(6).getBeta(), 0.01);
        Assert.assertEquals(0, rule.getSubRules().get(6).getGamma());

        Assert.assertEquals(1944, rule.getSubRules().get(7).getAlpha());
        Assert.assertEquals(0.2362, rule.getSubRules().get(7).getBeta(), 0.01);
        Assert.assertEquals(3, rule.getSubRules().get(7).getGamma());

        String str = rule.toString();
        Assert.assertNotEquals(-1,str.indexOf("1016"));

    }

    @Test
    public void testDecode2() {
        MergeLifeRule rule = new MergeLifeRule("a07f-c000-0000-0000-0000-0000-ff80-807f");

        Assert.assertEquals(0, rule.getSubRules().get(0).getAlpha());
        Assert.assertEquals(0, rule.getSubRules().get(0).getBeta(), 0.01);
        Assert.assertEquals(2, rule.getSubRules().get(0).getGamma());

        Assert.assertEquals(0, rule.getSubRules().get(1).getAlpha());
        Assert.assertEquals(0, rule.getSubRules().get(1).getBeta(), 0.01);
        Assert.assertEquals(3, rule.getSubRules().get(1).getGamma());

        Assert.assertEquals(0, rule.getSubRules().get(2).getAlpha());
        Assert.assertEquals(0, rule.getSubRules().get(2).getBeta(), 0.01);
        Assert.assertEquals(4, rule.getSubRules().get(2).getGamma());

        Assert.assertEquals(0, rule.getSubRules().get(3).getAlpha());
        Assert.assertEquals(0, rule.getSubRules().get(3).getBeta(), 0.01);
        Assert.assertEquals(5, rule.getSubRules().get(3).getGamma());

        Assert.assertEquals(1024, rule.getSubRules().get(4).getAlpha());
        Assert.assertEquals(1.0, rule.getSubRules().get(4).getBeta(), 0.01);
        Assert.assertEquals(7, rule.getSubRules().get(4).getGamma());

        Assert.assertEquals(1280, rule.getSubRules().get(5).getAlpha());
        Assert.assertEquals(1.0, rule.getSubRules().get(5).getBeta(), 0.01);
        Assert.assertEquals(0, rule.getSubRules().get(5).getGamma());

        Assert.assertEquals(1536, rule.getSubRules().get(6).getAlpha());
        Assert.assertEquals(0.0, rule.getSubRules().get(6).getBeta(), 0.01);
        Assert.assertEquals(1, rule.getSubRules().get(6).getGamma());

        Assert.assertEquals(2048, rule.getSubRules().get(7).getAlpha());
        Assert.assertEquals(-1.0, rule.getSubRules().get(7).getBeta(), 0.01);
        Assert.assertEquals(6, rule.getSubRules().get(7).getGamma());

        String str = rule.toString();
        Assert.assertNotEquals(-1,str.indexOf("1536"));
    }

    @Test
    public void testDecode3() {
        MergeLifeRule rule = new MergeLifeRule("7b58-f7b4-c5b4-fd87-22fa-eb10-6de8-107c");

        Assert.assertEquals(128, rule.getSubRules().get(0).getAlpha());
        Assert.assertEquals(0.9763, rule.getSubRules().get(0).getBeta(), 0.01);
        Assert.assertEquals(7, rule.getSubRules().get(0).getGamma());

        Assert.assertEquals(272, rule.getSubRules().get(1).getAlpha());
        Assert.assertEquals(-0.0468, rule.getSubRules().get(1).getBeta(), 0.01);
        Assert.assertEquals(4, rule.getSubRules().get(1).getGamma());

        Assert.assertEquals(872, rule.getSubRules().get(2).getAlpha());
        Assert.assertEquals(-0.1875, rule.getSubRules().get(2).getBeta(), 0.01);
        Assert.assertEquals(6, rule.getSubRules().get(2).getGamma());

        Assert.assertEquals(984, rule.getSubRules().get(3).getAlpha());
        Assert.assertEquals(0.6929, rule.getSubRules().get(3).getBeta(), 0.01);
        Assert.assertEquals(0, rule.getSubRules().get(3).getGamma());

        Assert.assertEquals(1576, rule.getSubRules().get(4).getAlpha());
        Assert.assertEquals(-0.59375, rule.getSubRules().get(4).getBeta(), 0.01);
        Assert.assertEquals(2, rule.getSubRules().get(4).getGamma());

        Assert.assertEquals(1880, rule.getSubRules().get(5).getAlpha());
        Assert.assertEquals(0.1259, rule.getSubRules().get(5).getBeta(), 0.01);
        Assert.assertEquals(5, rule.getSubRules().get(5).getGamma());

        Assert.assertEquals(1976, rule.getSubRules().get(6).getAlpha());
        Assert.assertEquals(-0.5937, rule.getSubRules().get(6).getBeta(), 0.01);
        Assert.assertEquals(1, rule.getSubRules().get(6).getGamma());

        Assert.assertEquals(2024, rule.getSubRules().get(7).getAlpha());
        Assert.assertEquals(-0.9453, rule.getSubRules().get(7).getBeta(), 0.01);
        Assert.assertEquals(3, rule.getSubRules().get(7).getGamma());

        String str = rule.toString();
        Assert.assertNotEquals(-1,str.indexOf("1976"));
    }

    @Test(expected = MergeLifeException.class)
    public void testIndexOutOfBoundsException() {
        new MergeLifeRule("7b58-f7b4-c5b4-fd87-22fa-eb10-6de8");

    }

    @Test
    public void testRandomRule() {
        MergeLifeRule rule = new MergeLifeRule(new Random(42));
        Assert.assertEquals("",rule.getRuleString());
    }

    @Test
    public void testRuleString() {
        Assert.assertEquals("E542-5F79-9341-F31E-6C6B-7F08-8773-7068", new MergeLifeRule("E542-5F79-9341-F31E-6C6B-7F08-8773-7068").getRuleString());
        Assert.assertEquals("A07F-C000-0000-0000-0000-0000-FF80-807F", new MergeLifeRule("A07F-C000-0000-0000-0000-0000-FF80-807F").getRuleString());
        Assert.assertEquals("7B58-F7B4-C5B4-FD87-22FA-EB10-6DE8-107C", new MergeLifeRule("7b58-f7b4-c5b4-fd87-22fa-eb10-6de8-107c").getRuleString());
        Assert.assertEquals("8503-5EB6-084C-04DF-7657-A5B3-6044-3524", new MergeLifeRule("8503-5eb6-084c-04df-7657-a5b3-6044-3524").getRuleString());
        Assert.assertEquals("6769-5DD6-7D03-564E-A5EC-CAE2-54C4-810C", new MergeLifeRule("6769-5dd6-7d03-564e-a5ec-cae2-54c4-810c").getRuleString());
        Assert.assertEquals("DF1D-BBA1-8E06-AA66-48FF-7414-6A2F-6237", new MergeLifeRule("df1d-bba1-8e06-aa66-48ff-7414-6a2f-6237").getRuleString());
    }
}
