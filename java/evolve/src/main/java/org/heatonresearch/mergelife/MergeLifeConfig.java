package org.heatonresearch.mergelife;

import com.fasterxml.jackson.databind.ObjectMapper;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.Map;

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

    public MergeLifeConfig(String filename) throws IOException {
        byte[] mapData = Files.readAllBytes(Paths.get(filename));
        ObjectMapper objectMapper = new ObjectMapper();
        Map<String, Object> map = objectMapper.readValue(mapData, HashMap.class);
        Map<String,String> configMap = (Map<String,String>)map.get("config");

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
        ArrayList<Object> list = (ArrayList<Object>)map.get("objective");
        for(Object obj: list) {
            Map map2 = (Map)obj;
            String stat = (String)map2.get("stat");
            double min = Double.parseDouble(map2.get("min").toString());
            double max = Double.parseDouble(map2.get("max").toString());
            double weight = Double.parseDouble(map2.get("weight").toString());
            double minWeight = Double.parseDouble(map2.get("min_weight").toString());
            double maxWeight = Double.parseDouble(map2.get("max_weight").toString());
            objFunction.addStat(new BasicObjectiveFunction.ObjectiveFunctionStat(stat,min,max,weight,minWeight,maxWeight));
        }
        this.objectiveFunction = objFunction;
    }

    private int readInt(Map<String,String> map, String key) {
        if(!map.containsKey(key)) {
            throw new MergeLifeException("Missing value for " + key);
        }

        Object obj = map.get(key);
        if( obj instanceof Integer) {
            return (int)obj;
        }
        String str = map.get(key);
        try {
            return Integer.parseInt(str);
        } catch(NumberFormatException ex) {
            throw new MergeLifeException("Expected numeric value for " + key);
        }
    }

    private double readDouble(Map<String,String> map, String key) {
        if(!map.containsKey(key)) {
            throw new MergeLifeException("Missing value for " + key);
        }

        Object obj = map.get(key);
        if( obj instanceof Double) {
            return (double)obj;
        }

        String str = map.get(key);
        try {
            return Double.parseDouble(str);
        } catch(NumberFormatException ex) {
            throw new MergeLifeException("Expected floating point value for " + key);
        }
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
