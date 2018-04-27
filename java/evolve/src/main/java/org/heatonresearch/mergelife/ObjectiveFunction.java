package org.heatonresearch.mergelife;

import com.fasterxml.jackson.databind.ObjectMapper;

import java.io.File;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.util.*;

public class ObjectiveFunction {

    private MergeLifeConfig config;

    public ObjectiveFunction(MergeLifeConfig theConfig) {
        this.config = theConfig;
    }

    public void addStat(ObjectiveFunctionStat stat) {
        this.stats.add(stat);
    }

    public static class ObjectiveFunctionStat {
        private final String stat;
        private final double min;
        private final double max;
        private final double weight;
        private final double minWeight;
        private final double maxWeight;

        public ObjectiveFunctionStat(String stat, double min, double max, double weight, double minWeight, double maxWeight) {
            this.stat = stat;
            this.min = min;
            this.max = max;
            this.weight = weight;
            this.minWeight = minWeight;
            this.maxWeight = maxWeight;
        }

        public String getStat() {
            return stat;
        }

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

    private final List<ObjectiveFunctionStat> stats = new ArrayList<>();

    public List<ObjectiveFunctionStat> getStats() {
        return stats;
    }

    private double calculateObjectiveCycle(String ruleText) {
        MergeLifeGrid grid = new MergeLifeGrid(this.config.getRows(), this.config.getCols());
        MergeLifeRule rule = new MergeLifeRule(ruleText);
        grid.randomize(0,new Random());

        CalculateObjectiveStats calcStats = new CalculateObjectiveStats(grid);

        while(!calcStats.hasStabilized()) {
            grid.step(rule);
            calcStats.track();
            //System.out.println(calcStats.track());
        }

        double score = 0;
        for(ObjectiveFunctionStat stat: this.stats) {
            score += stat.calculate(calcStats);
        }

        return score;
    }

    public double calculateObjective(String ruleText) {
        double sum = 0;
        for(int i=0;i<5;i++) {
            //System.out.println("Cycle #"+ i);
            sum+=calculateObjectiveCycle(ruleText);
        }
        return sum/5.0;
    }

}
