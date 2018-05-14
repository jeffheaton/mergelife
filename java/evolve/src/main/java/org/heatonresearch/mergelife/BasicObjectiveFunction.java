/*
 * MergeLife, Copyrighr 2018 by Jeff Heaton
 * http://www.heatonresearch.com/mergelife/
 * MIT License
 */
package org.heatonresearch.mergelife;

import java.util.*;

public class BasicObjectiveFunction implements EvaluateObjective {

    /**
     * The configuration object to use.
     */
    private MergeLifeConfig config;

    /**
     * The object to report status to.
     */
    private MergeLifeReportable report;

    /**
     * Construct a basic objective function for MergeLife.
     * @param theConfig The configuration to use.
     */
    public BasicObjectiveFunction(MergeLifeConfig theConfig)
    {
        this.config = theConfig;
    }

    /**
     * Add an evaluation stat.
     * @param stat The evaluation stat to use.
     */
    public void addStat(ObjectiveFunctionStat stat) {
        this.stats.add(stat);
    }

    /**
     * An individual evaluation stat for the multi-objective MergeLife objective function.
     */
    public static class ObjectiveFunctionStat {
        /**
         * Tbe name of the evaluation stat.
         */
        private final String stat;

        /**
         * The minimum value that this value can have to be in the range for getting full weight.
         */
        private final double min;

        /**
         * The maximum value that this value can have to be in the range for getting full weight.
         */
        private final double max;

        /**
         * The weight value that is awarded for being in the center of min-max.
         */
        private final double weight;

        /**
         * The weight if the stat is below the min.
         */
        private final double minWeight;

        /**
         * The weight if the stat is above the max.
         */
        private final double maxWeight;

        /**
         * Construct the objective stat.
         * @param stat Tbe name of the evaluation stat.
         * @param min The minimum value that this value can have to be in the range for getting full weight.
         * @param max The maximum value that this value can have to be in the range for getting full weight.
         * @param weight The weight value that is awarded for being in the center of min-max.
         * @param minWeight The weight if the stat is below the min.
         * @param maxWeight The weight if the stat is above the max.
         */
        public ObjectiveFunctionStat(String stat, double min, double max, double weight, double minWeight, double maxWeight) {
            this.stat = stat;
            this.min = min;
            this.max = max;
            this.weight = weight;
            this.minWeight = minWeight;
            this.maxWeight = maxWeight;
        }

        /**
         * @return The minimum value that this value can have to be in the range for getting full weight.
         */
        public double getMin() {
            return min;
        }

        /**
         * @return The maximum value that this value can have to be in the range for getting full weight.
         */
        public double getMax() {
            return max;
        }

        /**
         * @return The weight value that is awarded for being in the center of min-max.
         */
        public double getWeight() {
            return weight;
        }

        /**
         * @return The weight if the stat is below the min.
         */
        public double getMinWeight() {
            return minWeight;
        }

        /**
         * @return The weight if the stat is above the max.
         */
        public double getMaxWeight() {
            return maxWeight;
        }

        /**
         * @return Tbe name of the evaluation stat.
         */
        public String getStat() {
            return stat;
        }

        /**
         * Calculat the weight for this objective stat.
         * @param stats The stats.
         * @return The weight contribution for this stat.
         */
        public double calculate(CalculateObjectiveStats stats) {
            double range = this.max - this.min;
            double ideal = range / 2;
            if( !stats.getCurrentStats().containsKey(this.stat)) {
                throw new MergeLifeException("Unknown objective stat: " + this.stat);
            }
            double actual = stats.getCurrentStats().get(this.stat);

            if(actual < this.min) {
                // too small
                return this.minWeight;
            }
            else if(actual > this.max) {
                // too big
                return this.maxWeight;
            } else {
                double adjust = ((range / 2) - Math.abs(actual - ideal)) / (range / 2);
                adjust *= this.weight;
                return adjust;
            }
        }
    }

    /**
     * The objective stats.
     */
    private final List<ObjectiveFunctionStat> stats = new ArrayList<>();

    /**
     * @return The objective stats.
     */
    public List<ObjectiveFunctionStat> getStats() {
        return stats;
    }

    /**
     * Run a MergeLife rule until it stablizes one time.
     * @param ruleText The MergeLife rule to evaluate.
     * @param random Random number generator.
     * @return The objective function score for this cycle.
     */
    private double calculateObjectiveCycle(String ruleText, Random random) {
        MergeLifeGrid grid = new MergeLifeGrid(this.config.getRows(), this.config.getCols());
        MergeLifeRule rule = new MergeLifeRule(ruleText);
        grid.randomize(0, random);

        CalculateObjectiveStats calcStats = new CalculateObjectiveStats(grid);

        while(!calcStats.hasStabilized()) {
            grid.step(rule);
            Map<String, Double> t = calcStats.track();
            if(report!=null) {
                report.report(t.toString());
            }
        }

        double score = 0;
        for(ObjectiveFunctionStat stat: this.stats) {
            score += stat.calculate(calcStats);
        }

        return score;
    }

    /**
     * Calculate the objective function as the average of a number of cycles.
     * @param ruleText The MergeLife rule to evaluate.
     * @param random Random number generator.
     * @return The objective function score over all cycles.
     */
    @Override
    public double calculateObjective(String ruleText, Random random) {
        double sum = 0;
        for(int i=0;i<config.getEvalCycles();i++) {
            if(this.report!=null) {
                report.report("Cycle #"+ i);
            }
            sum+=calculateObjectiveCycle(ruleText, random);
        }
        return sum/config.getEvalCycles();
    }

    /**
     * @return The report object.
     */
    public MergeLifeReportable getReport() {
        return report;
    }

    /**
     * Set the report object.
     * @param report The new report object.
     */
    public void setReport(MergeLifeReportable report) {
        this.report = report;
    }
}
