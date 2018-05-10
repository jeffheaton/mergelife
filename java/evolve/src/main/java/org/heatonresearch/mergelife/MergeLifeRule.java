/*
 * MergeLife, Copyrighr 2018 by Jeff Heaton
 * http://www.heatonresearch.com/mergelife/
 * MIT License
 */
package org.heatonresearch.mergelife;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Random;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class MergeLifeRule {

    /**
     * One subrule within a MergeLife rule.
     */
    public static class SubRule implements Comparable<SubRule> {

        /**
         * Alpha: The high-end of the neighbor count range that this subrule applies to.
         */
        private final int alpha;

        /**
         * Beta: The percentage that a color tha this subrule applies to should move towards
         * the key color.
         */
        private final double beta;

        /**
         * Gamma: Used to obtain the index of the key color.  If beta is positive, gamma is the
         * index of the key color.  If beta is negative, the key color is the row after
         * the index specified by gamma.
         */
        private final int gamma;

        /**
         * Construct a SubRule.
         * @param alpha Alpha: The high end of the ragne.
         * @param beta Beta: The percent to move towards key color.
         * @param gamma Gamma: The index.
         */
        private SubRule(int alpha, double beta, int gamma) {
            this.alpha = alpha;
            this.beta = beta;
            this.gamma = gamma;
        }

        /**
         * @return Alpha: The high-end of the neighbor count range that this subrule aplies to.
         */
        public int getAlpha() {
            return alpha;
        }

        /**
         * @return Beta: The percentage that a color tha this subrule aplies to should move towards
         * the key color.
         */
        public double getBeta() {
            return beta;
        }

        /**
         * @return Gamma: Used to obtain the index of the key color.  If beta is positive,
         * gamma is the index of the key color.  If beta is negative, the key color is the
         * row after the index specified by gama.
         */
        public int getGamma() {
            return gamma;
        }

        /**
         * {@inheritDoc}
         */
        public int compareTo(SubRule otherRule) {
            return Integer.compare(getAlpha(),otherRule.getAlpha());
        }

        /**
         * {@inheritDoc}
         */
        public String toString() {
            StringBuilder result = new StringBuilder();
            result.append("[SubRule: alpha=");
            result.append(getAlpha());
            result.append(", beta=");
            result.append(getBeta());
            result.append(", gamma=");
            result.append(getGamma());
            result.append("]");
            return result.toString();
        }
    }

    /**
     * The key colors.
     */
    public static int[][] ColorTable = {
        {0, 0,0},  // Black 0
        {255,0,0},   // Red 1
        {0,255,0},  // Green 2
        {255,255,0},  // Yellow 3
        {0,0,255},  // Blue 4
        {255,0,255},  // Purple 5
        {0,255,255},  // Cyan 6
        {255,255,255}  // White 7
    };

    /**
     * REGEX for parsing the hex string for MergeLife.
     */
    Pattern rulePattern = Pattern.compile("([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})");

    /**
     * The subrules.
     */
    List<SubRule> subRules = new ArrayList<>();

    /**
     * Construct a MergeLife rule.
     * @param ruleText The hex string that defines this rule.
     * @throws MergeLifeException Invalid rule string.
     */
    public MergeLifeRule(String ruleText) throws MergeLifeException {
        Matcher matcher = rulePattern.matcher(ruleText);
        if(matcher.find()) {
            for(int i=0;i<ColorTable.length;i++) {
                String hex = matcher.group(i+1);

                int o1 = Integer.parseInt(hex.substring(0,2),16);
                int o2 = Integer.valueOf(hex.substring(2,4),16).byteValue();

                int alpha = (int)(o1 * 8);
                double beta = o2>0 ? o2/127.0 : o2/128.0;

                if (alpha == 2040) {
                    alpha = 2048;
                }

                this.subRules.add(new SubRule(alpha,beta,i));

            }

            Collections.sort(this.subRules);
        } else {
            throw new MergeLifeException("Can't parse: " + ruleText);
        }
    }

    /**
     * Construct a random MergeLife rule.
     */
    public MergeLifeRule(Random rnd) {
        this(MergeLifeRule.generateRandomRuleString(rnd));
    }

    /**
     * Generate a random MergeLife rule string.
     * @return
     */
    public static String generateRandomRuleString(Random rnd) {
        StringBuilder result = new StringBuilder();
        for(int i=0;i<8;i++) {
            int j = (int)(0x10000 * rnd.nextDouble());
            String str = Integer.toString(j,16);
            while(str.length()<4) {
                str = "0" + str;
            }
            result.append(str);
            if( i<7 ) {
                result.append("-");
            }
        }
        return result.toString();
    }

    /**
     * @return The subrules.
     */
    public List<SubRule> getSubRules() {
        return subRules;
    }

    /**
     * {@inheritDoc}
     */
    public String toString() {
        StringBuilder result = new StringBuilder();
        for(SubRule subRule: this.subRules) {
            result.append(subRule.toString());
            result.append("\n");
        }
        return result.toString();
    }

    private String hexTwo(int d) {
        String result = String.format("%02X", d);
        return result.substring(result.length()-2);
    }

    public String getRuleString() {
        StringBuilder result = new StringBuilder();
        for(int i=0;i<this.getSubRules().size();i++) {
            for(SubRule subRule: this.subRules) {
                if( subRule.getGamma() == i) {
                    int o1 = subRule.getAlpha()/8;
                    int o2 = (int)(subRule.getBeta()>0 ? subRule.getBeta()*127.0 : subRule.getBeta()*128.0);
                    o1 = Math.min(255,o1);
                    o2 = Math.min(255,o2);
                    result.append(hexTwo(o1));
                    result.append(hexTwo(o2));
                }
            }
            if( i<7 ) {
                result.append("-");
            }
        }
        return result.toString().toUpperCase();
    }
}
