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
    private int populationSize;
    private int tournamentCycles;
    private double crossoverPct;
    private ObjectiveFunction objectiveFunction;


    public MergeLifeConfig(String filename) throws IOException {
        byte[] mapData = Files.readAllBytes(Paths.get(filename));
        ObjectMapper objectMapper = new ObjectMapper();
        Map<String, Object> map = objectMapper.readValue(mapData, HashMap.class);
        Map<String,String> configMap = (Map<String,String>)map.get("config");

        this.rows = readInt(configMap, "rows");
        this.cols = readInt(configMap, "cols");
        this.populationSize = readInt(configMap, "populationSize");
        this.tournamentCycles = readInt(configMap, "tournamentCycles");
        this.crossoverPct = readDouble(configMap, "crossover");

        this.objectiveFunction = new ObjectiveFunction(this);
        ArrayList<Object> list = (ArrayList<Object>)map.get("objective");
        for(Object obj: list) {
            Map map2 = (Map)obj;
            String stat = (String)map2.get("stat");
            double min = Double.parseDouble(map2.get("min").toString());
            double max = Double.parseDouble(map2.get("max").toString());
            double weight = Double.parseDouble(map2.get("weight").toString());
            double minWeight = Double.parseDouble(map2.get("min_weight").toString());
            double maxWeight = Double.parseDouble(map2.get("max_weight").toString());
            objectiveFunction.addStat(new ObjectiveFunction.ObjectiveFunctionStat(stat,min,max,weight,minWeight,maxWeight));
        }
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

    public ObjectiveFunction getObjectiveFunction() {
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
}
