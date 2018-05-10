/*
 * MergeLife, Copyrighr 2018 by Jeff Heaton
 * http://www.heatonresearch.com/mergelife/
 * MIT License
 */
package org.heatonresearch.mergelife;

import java.util.Random;

public interface EvaluateObjective {
    double calculateObjective(String ruleText, Random random);

    MergeLifeReportable getReport();

    void setReport(MergeLifeReportable report);
}
