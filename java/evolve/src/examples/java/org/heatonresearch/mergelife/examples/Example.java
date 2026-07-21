/*
 * MergeLife, Copyright 2018 by Jeff Heaton
 * http://www.heatonresearch.com/mergelife/
 * MIT License
 */
package org.heatonresearch.mergelife.examples;

import org.heatonresearch.mergelife.MergeLifeConfig;
import org.heatonresearch.mergelife.MergeLifeGeneticAlgorithm;

import java.util.Random;

/**
 * Reference example showing how to use the MergeLife library to score and render
 * a rule against the paper's objective function.
 *
 * <p>Run with: {@code ./gradlew runExample} (optionally
 * {@code ./gradlew runExample --args="<rule-hex>"}).
 */
public final class Example {

    private Example() {
    }

    public static void main(String[] args) throws Exception {
        // "Red World" -- one of the spaceship-producing rules from the paper.
        String rule = (args.length > 0)
                ? args[0]
                : "E542-5F79-9341-F31E-6C6B-7F08-8773-7068";

        MergeLifeConfig config = MergeLifeConfig.paperObjective();
        MergeLifeGeneticAlgorithm ga = new MergeLifeGeneticAlgorithm(new Random(), config);

        // Score the rule against the paper's objective function.
        double score = ga.score(rule);
        System.out.printf("Rule %s scored %.4f against the paper objective.%n", rule, score);

        // Render it to mergelife-<rule>.png in the working directory.
        ga.render(rule);
        System.out.println("Rendered mergelife-" + rule.toLowerCase() + ".png");

        // To evolve new rules instead, configure a MergeLifeConfig and call
        // ga.process() -- that runs the full genetic algorithm (long-running),
        // so it is intentionally left out of this quick example.
    }
}
