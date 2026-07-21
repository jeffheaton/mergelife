/*
 * MergeLife, Copyright 2018 by Jeff Heaton
 * http://www.heatonresearch.com/mergelife/
 * MIT License
 */
package org.heatonresearch.mergelife;

import com.fasterxml.jackson.databind.ObjectMapper;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.Map;

/**
 * Configuration for a MergeLife run: grid size, render/GA parameters, and the
 * objective function.
 *
 * <p>Build one programmatically with the setters, or use {@link #paperObjective()}
 * for the exact objective published in the MergeLife paper (equivalent to the
 * shared {@code paperObjective.json}).
 */
public class MergeLifeConfig {
    private int rows;
    private int cols;
    private int zoom;
    private int renderSteps;
    private int populationSize;
    private int tournamentCycles;
    private double crossoverPct;
    private int evalCycles;
    private int patience;
    private double scoreThreshold;
    private int maxRuns;

    private EvaluateObjective objectiveFunction;

    public MergeLifeConfig() {
    }

    /**
     * Load a configuration from a JSON file (the shared {@code paperObjective.json}
     * format): a {@code "config"} object of scalar parameters plus an
     * {@code "objective"} array of stat weightings.
     *
     * @param filename path to the JSON configuration.
     * @throws IOException if the file cannot be read.
     */
    @SuppressWarnings("unchecked")
    public MergeLifeConfig(String filename) throws IOException {
        byte[] mapData = Files.readAllBytes(Paths.get(filename));
        ObjectMapper objectMapper = new ObjectMapper();
        Map<String, Object> map = objectMapper.readValue(mapData, HashMap.class);
        Map<String, String> configMap = (Map<String, String>) map.get("config");

        this.rows = readInt(configMap, "rows");
        this.cols = readInt(configMap, "cols");
        this.zoom = readInt(configMap, "zoom");
        this.renderSteps = readInt(configMap, "renderSteps");
        this.populationSize = readInt(configMap, "populationSize");
        this.tournamentCycles = readInt(configMap, "tournamentCycles");
        this.crossoverPct = readDouble(configMap, "crossover");
        this.evalCycles = readInt(configMap, "evalCycles");
        this.patience = readInt(configMap, "patience");
        this.scoreThreshold = readDouble(configMap, "scoreThreshold");
        this.maxRuns = readInt(configMap, "maxRuns");

        BasicObjectiveFunction objFunction = new BasicObjectiveFunction(this);
        ArrayList<Object> list = (ArrayList<Object>) map.get("objective");
        for (Object obj : list) {
            Map map2 = (Map) obj;
            String stat = (String) map2.get("stat");
            double min = Double.parseDouble(map2.get("min").toString());
            double max = Double.parseDouble(map2.get("max").toString());
            double weight = Double.parseDouble(map2.get("weight").toString());
            double minWeight = Double.parseDouble(map2.get("min_weight").toString());
            double maxWeight = Double.parseDouble(map2.get("max_weight").toString());
            objFunction.addStat(new BasicObjectiveFunction.ObjectiveFunctionStat(stat, min, max, weight, minWeight, maxWeight));
        }
        this.objectiveFunction = objFunction;
    }

    private int readInt(Map<String, String> map, String key) {
        if (!map.containsKey(key)) {
            throw new MergeLifeException("Missing value for " + key);
        }
        Object obj = map.get(key);
        if (obj instanceof Integer) {
            return (int) obj;
        }
        String str = map.get(key);
        try {
            return Integer.parseInt(str);
        } catch (NumberFormatException ex) {
            throw new MergeLifeException("Expected numeric value for " + key);
        }
    }

    private double readDouble(Map<String, String> map, String key) {
        if (!map.containsKey(key)) {
            throw new MergeLifeException("Missing value for " + key);
        }
        Object obj = map.get(key);
        if (obj instanceof Double) {
            return (double) obj;
        }
        String str = map.get(key);
        try {
            return Double.parseDouble(str);
        } catch (NumberFormatException ex) {
            throw new MergeLifeException("Expected floating point value for " + key);
        }
    }

    /**
     * The parameters and objective function used in the MergeLife paper
     * (Heaton, 2018), equivalent to the shared {@code paperObjective.json}.
     *
     * @return a fully-populated configuration.
     */
    public static MergeLifeConfig paperObjective() {
        MergeLifeConfig config = new MergeLifeConfig();
        config.rows = 100;
        config.cols = 100;
        config.zoom = 5;
        config.renderSteps = 250;
        config.populationSize = 100;
        config.tournamentCycles = 5;
        config.crossoverPct = 0.75;
        config.evalCycles = 5;
        config.patience = 1000;
        config.scoreThreshold = 3.5;
        config.maxRuns = 1000000;

        BasicObjectiveFunction obj = new BasicObjectiveFunction(config);
        //                                                           stat          min    max    weight  minW   maxW
        obj.addStat(new BasicObjectiveFunction.ObjectiveFunctionStat("steps",      300,   1000,  1,      -1,    1));
        obj.addStat(new BasicObjectiveFunction.ObjectiveFunctionStat("foreground", 0.001, 0.1,   1,      -0.1,  -1));
        obj.addStat(new BasicObjectiveFunction.ObjectiveFunctionStat("active",     0.001, 0.1,   1,      -1,    -1));
        obj.addStat(new BasicObjectiveFunction.ObjectiveFunctionStat("rect",       0.02,  0.25,  2,      -2,    2));
        obj.addStat(new BasicObjectiveFunction.ObjectiveFunctionStat("mage",       5,     10,    0,      -5,    0));
        config.objectiveFunction = obj;
        return config;
    }

    public EvaluateObjective getObjectiveFunction() {
        return objectiveFunction;
    }

    public int getRows() {
        return rows;
    }

    public int getCols() {
        return cols;
    }

    public int getPopulationSize() {
        return populationSize;
    }

    public double getCrossoverPct() {
        return crossoverPct;
    }

    public int getTournamentCycles() {
        return tournamentCycles;
    }

    public int getEvalCycles() {
        return evalCycles;
    }

    public void setRows(int rows) {
        this.rows = rows;
    }

    public void setCols(int cols) {
        this.cols = cols;
    }

    public int getZoom() {
        return zoom;
    }

    public void setZoom(int zoom) {
        this.zoom = zoom;
    }

    public int getRenderSteps() {
        return renderSteps;
    }

    public void setRenderSteps(int renderSteps) {
        this.renderSteps = renderSteps;
    }

    public int getPatience() {
        return patience;
    }

    public double getScoreThreshold() {
        return scoreThreshold;
    }

    public int getMaxRuns() {
        return maxRuns;
    }
}
