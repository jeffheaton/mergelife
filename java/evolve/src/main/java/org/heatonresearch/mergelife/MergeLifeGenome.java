package org.heatonresearch.mergelife;

import java.util.Objects;
import java.util.Random;

public class MergeLifeGenome implements Comparable<MergeLifeGenome> {
    private final String ruleText;
    private double score = Double.NaN;

    public MergeLifeGenome(String theRuleText) {
        this.ruleText = theRuleText.toLowerCase();
    }

    public MergeLifeGenome(Random rnd) {
        this(MergeLifeRule.generateRandomRuleString(rnd));
    }

    public double calculateScore(ObjectiveFunction objectiveFunction) {
        if( !Double.isNaN(this.score) ) {
            return score;
        }

        this.score = objectiveFunction.calculateObjective(this.ruleText);
        return this.score;
    }

    public String getRuleText() {
        return ruleText;
    }

    public double getScore() {
        return score;
    }

    @Override
    public int compareTo(MergeLifeGenome other) {
        return Double.compare(getScore(),other.getScore());
    }

    @Override
    public boolean equals(Object o) {
        if (this == o) return true;
        if (o == null || getClass() != o.getClass()) return false;
        MergeLifeGenome that = (MergeLifeGenome) o;
        return Objects.equals(ruleText, that.ruleText);
    }

    @Override
    public int hashCode() {

        return Objects.hash(ruleText);
    }

    public String toString() {
        StringBuilder result = new StringBuilder();
        result.append("[");
        result.append(getScore());
        result.append(", ");
        result.append(getRuleText());
        result.append("]");
        return result.toString();
    }
}
