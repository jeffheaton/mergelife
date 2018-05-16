/*
 * MergeLife, Copyrighr 2018 by Jeff Heaton
 * http://www.heatonresearch.com/mergelife/
 * MIT License
 */
package org.heatonresearch.mergelife;

import org.heatonresearch.mergelife.util.LargestRect;

import java.util.HashMap;
import java.util.Map;

public class CalculateObjectiveStats {
    /**
     * The mode age:  How long has the merged matrix mode been at the same value.  Measured in CA generations/updates.
     */
    public static final String STAT_MODE_AGE = "mage";

    /**
     * The current mode (background) color.
     */
    public static final String STAT_MODE = "mode";

    /**
     * The count (numeric, not percent) of mode (background) cells.
     */
    public static final String STAT_MODE_COUNT = "mc";

    /**
     * The percent of cells that have the mode color. (are background color)
     */
    public static final String STAT_BACKGROUND = "background";

    /**
     * The percent of cells that are in a foreground state.  A foreground cell has had the same non-background color
     * on the merged grid for longer than 5 CA generations/updates.  These cells are usually part of "still life".
     */
    public static final String STAT_FOREGROUND = "foreground";

    /**
     * The percent of cells that are in an active state.  An active cell was background between 5 and 25 CA generations
     * ago.  These cells generally have activity passing through them that could indicate "space ship" activity.
     */
    public static final String STAT_ACTIVE = "active";

    /**
     * The percent of cells that are in a chaotic state.  Chaotic cells are the remaining cells that are not
     * background, foreground or active.
     */
    public static final String STAT_CHAOS = "chaos";

    /**
     * The number of CA generations.
     */
    public static final String STAT_STEPS = "steps";

    /**
     * The largest background colored rectangle on the merged grid.  This is given in percent and indicates the
     * amount of space between active regions of the grid.
     */
    public static final String STAT_RECT = "rect";

    /**
     * The grid being evaluated.
     */
    private final MergeLifeGrid grid;

    /**
     * The count of how many CA generations each cell has been the mode.
     */
    private final int[][] modeCount;

    /**
     * The last merged color that each cell was.
     */
    private final int[][] lastColor;

    /**
     * The count of how many CA steps each cell has had its color.
     */
    private final int[][] lastColorCount;

    /**
     * The last CA generation/step that each cell was the merged mode/background.
     */
    private final int[][] lastModeStep;

    /**
     * The last count of merged mode (background) cells.
     */
    private int lastModeCount;

    /**
     * How many CA generations has the merged mode count been the same.
     */
    private int modeCountSame;

    /**
     * The current evaluation stats.
     */
    private final Map<String,Double> currentStats =  new HashMap<>();

    /**
     * Construct to calculate objective stats for the specified grid.
     * @param theGrid The grid to calculate stats for.
     */
    public CalculateObjectiveStats(MergeLifeGrid theGrid) {
        this.grid = theGrid;
        this.modeCount = new int[this.grid.getRows()][this.grid.getCols()];
        this.lastColor = new int[this.grid.getRows()][this.grid.getCols()];
        this.lastColorCount = new int[this.grid.getRows()][this.grid.getCols()];
        this.lastModeStep = new int[this.grid.getRows()][this.grid.getCols()];
    }

    /**
     * Track the objective stats for a current CA generation/step.
     * @return The current stats, after the CA generation..
     */
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

        int rect = LargestRect.maximalRectangle(this.grid.getMergeGrid(),this.grid.getModeGrid());

        currentStats.put(CalculateObjectiveStats.STAT_MODE_COUNT,(double)mc);
        currentStats.put(CalculateObjectiveStats.STAT_BACKGROUND,((double)mc/size));
        currentStats.put(CalculateObjectiveStats.STAT_FOREGROUND,((double)sameColor/size));
        currentStats.put(CalculateObjectiveStats.STAT_ACTIVE,((double)act/size));
        currentStats.put(CalculateObjectiveStats.STAT_CHAOS,((double)cntChaos/size));
        currentStats.put(CalculateObjectiveStats.STAT_STEPS,(double)this.grid.getCurrentStep());
        currentStats.put(CalculateObjectiveStats.STAT_RECT,((double)rect/size));

        return this.currentStats;
    }

    /**
     * @return True, if the grid has stablized to the point that more CA generations are not needed.
     */
    public boolean hasStabilized() {
        if( this.currentStats.size()==0) {
            return false;
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

    /**
     * @return The current stats.
     */
    public Map<String, Double> getCurrentStats() {
        return currentStats;
    }

    /**
     * @return The grid being evaluated.
     */
    public MergeLifeGrid getGrid() {
        return grid;
    }
}
