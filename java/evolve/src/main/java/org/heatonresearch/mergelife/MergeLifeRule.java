package org.heatonresearch.mergelife;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class MergeLifeRule {

    public static class SubRule implements Comparable<SubRule> {
        private final int alpha;
        private final double beta;
        private final int gamma;

        private SubRule(int alpha, double beta, int gamma) {
            this.alpha = alpha;
            this.beta = beta;
            this.gamma = gamma;
        }

        public int getAlpha() {
            return alpha;
        }

        public double getBeta() {
            return beta;
        }

        public int getGamma() {
            return gamma;
        }

        public int compareTo(SubRule otherRule) {
            return Integer.compare(getAlpha(),otherRule.getAlpha());
        }

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

    Pattern rulePattern = Pattern.compile("([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})");
    List<SubRule> subRules = new ArrayList<>();


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

    public List<SubRule> getSubRules() {
        return subRules;
    }

    public String toString() {
        StringBuilder result = new StringBuilder();
        for(SubRule subRule: this.subRules) {
            result.append(subRule.toString());
            result.append("\n");
        }
        return result.toString();
    }
}
