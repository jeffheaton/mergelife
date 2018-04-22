package org.heatonresearch.mergelife;

import java.io.File;
import java.util.Random;

public class MergeLife {
	public static void main(String[] args) {
		try {
			MergeLifeGrid grid = new MergeLifeGrid(100, 100);
			MergeLifeRule rule = new MergeLifeRule("E542-5F79-9341-F31E-6C6B-7F08-8773-7068");
			grid.randomize(0,new Random());

			CalculateObjectiveStats stats = new CalculateObjectiveStats(grid);

			while(!stats.hasStabilized()) {
				grid.step(rule);
				System.out.println(stats.track());
			}

			grid.savePNG(0, 5, new File("D:\\Users\\jheaton\\projects\\test.png"));
			System.out.println("Hello world");
		} catch(Exception ex) {
			ex.printStackTrace();
		}
	}
}