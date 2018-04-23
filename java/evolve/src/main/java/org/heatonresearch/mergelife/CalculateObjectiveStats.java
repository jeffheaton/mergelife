package org.heatonresearch.mergelife;

import java.util.HashMap;
import java.util.Map;

public class CalculateObjectiveStats {
    public static final String STAT_MODE_AGE = "mage";
    public static final String STAT_MODE = "mode";
    public static final String STAT_MODE_COUNT = "mc";
    public static final String STAT_BACKGROUND = "background";
    public static final String STAT_FOREGROUND = "foreground";
    public static final String STAT_ACTIVE = "active";
    public static final String STAT_CHAOS = "chaos";
    public static final String STAT_STEPS = "steps";
    public static final String STAT_RECT = "rect";

    final MergeLifeGrid grid;

    final int[][] modeCount;
    final int[][] lastColor;
    final int[][] lastColorCount;
    final int[][] lastModeStep;
    int lastModeCount;
    int modeCountSame;

    private Map<String,Double> currentStats =  new HashMap<>();

    public CalculateObjectiveStats(MergeLifeGrid theGrid) {
        this.grid = theGrid;
        this.modeCount = new int[this.grid.getRows()][this.grid.getCols()];
        this.lastColor = new int[this.grid.getRows()][this.grid.getCols()];
        this.lastColorCount = new int[this.grid.getRows()][this.grid.getCols()];
        this.lastModeStep = new int[this.grid.getRows()][this.grid.getCols()];
    }

    public Map<String,Double>  track() {
        int height = this.grid.getRows();
        int width = this.grid.getCols();
        int size = height * width;
        int timeStep = this.grid.getCurrentStep();
        int cg = this.grid.getCurrentGrid();

        if( currentStats.size()==0) {
            currentStats.put(CalculateObjectiveStats.STAT_MODE_AGE,0.0);
            currentStats.put(CalculateObjectiveStats.STAT_MODE,0.0);
            currentStats.put(CalculateObjectiveStats.STAT_MODE_COUNT,0.0);
            currentStats.put(CalculateObjectiveStats.STAT_BACKGROUND,0.0);
            currentStats.put(CalculateObjectiveStats.STAT_FOREGROUND,0.0);
            currentStats.put(CalculateObjectiveStats.STAT_ACTIVE,0.0);
            currentStats.put(CalculateObjectiveStats.STAT_CHAOS,0.0);
            return this.currentStats;
        }

        if( currentStats.get(CalculateObjectiveStats.STAT_MODE).intValue()==this.grid.getModeGrid()) {
            int age = currentStats.get(CalculateObjectiveStats.STAT_MODE_AGE).intValue()+1;
            currentStats.put(CalculateObjectiveStats.STAT_MODE_AGE,(double)age);
        } else {
            currentStats.put(CalculateObjectiveStats.STAT_MODE, (double) this.grid.getModeGrid());
            currentStats.put(CalculateObjectiveStats.STAT_MODE_AGE,0.0);
        }

        // What percent of the grid is the mode, what percent is the background
        int modeCount = 0;
        int mc = 0;
        int sameColor = 0;
        int act = 0;

        for(int row=0;row<this.grid.getRows();row++) {
            for(int col=0;col<this.grid.getCols();col++) {
                if(this.grid.getMergeGrid()[row][col]==this.grid.getModeGrid()) {
                    modeCount++;
                    this.modeCount[row][col]++;
                    this.lastModeStep[row][col] = this.grid.getCurrentStep();
                    if( this.modeCount[row][col]>50) {
                        mc++;
                    }
                } else {
                    this.modeCount[row][col] = 0;
                }

                int sinceMode = this.grid.getCurrentStep() - this.lastModeStep[row][col];
                if( this.grid.getCurrentStep()>25 && (sinceMode>5 && sinceMode<25) ) {
                    act++;
                }

                if(this.lastColor[row][col]!=this.grid.getMergeGrid()[row][col] ||
                    this.grid.getMergeGrid()[row][col]==this.grid.getModeGrid() ) {
                    this.lastColor[row][col] = this.grid.getMergeGrid()[row][col];
                    this.lastColorCount[row][col] = 0;
                } else {
                    this.lastColorCount[row][col]++;
                    if(this.lastColorCount[row][col]>5) {
                        sameColor++;
                    }
                }
            }
        }

        int cntChaos = size - (mc + sameColor + act);

        currentStats.put(CalculateObjectiveStats.STAT_MODE_COUNT,(double)mc);
        currentStats.put(CalculateObjectiveStats.STAT_BACKGROUND,((double)mc/size));
        currentStats.put(CalculateObjectiveStats.STAT_FOREGROUND,((double)sameColor/size));
        currentStats.put(CalculateObjectiveStats.STAT_ACTIVE,((double)act/size));
        currentStats.put(CalculateObjectiveStats.STAT_CHAOS,((double)cntChaos/size));
        currentStats.put(CalculateObjectiveStats.STAT_STEPS,(double)this.grid.getCurrentStep());
        currentStats.put(CalculateObjectiveStats.STAT_RECT,(double)this.grid.getCurrentStep());

        return this.currentStats;
    }

    public boolean hasStabilized() {
        if( this.currentStats.size()==0) {
            return false;
        }
        if(this.grid.getCurrentStep()>100) {
            if (this.currentStats.get(STAT_BACKGROUND) < 0.01) {
                return true;
            }
        }

        // Time to stop?
        int mc = this.currentStats.get(CalculateObjectiveStats.STAT_MODE_COUNT).intValue();
        if( mc==lastModeCount) {
            this.modeCountSame++;
            if( this.modeCountSame>100) {
                return true;
            }
        } else {
            this.modeCountSame = 0;
            this.lastModeCount = mc;
        }

        return this.grid.getCurrentStep()>1000;
    }

    public Map<String, Double> getCurrentStats() {
        return currentStats;
    }

    public MergeLifeGrid getGrid() {
        return grid;
    }
}
