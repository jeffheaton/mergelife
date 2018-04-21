package org.heatonresearch.mergelife;

import java.io.File;
import java.util.Random;

public class MergeLife {
	public static void main(String[] args) {
		try {
			MergeLifeGrid grid = new MergeLifeGrid(100, 100);
			MergeLifeRule rule = new MergeLifeRule("8503-5eb6-084c-04df-7657-a5b3-6044-3524");
			grid.randomize(0,new Random());
			for(int i=0;i<500;i++) {
				grid.step(rule);
			}

			grid.savePNG(0, 5, new File("D:\\Users\\jheaton\\projects\\test.png"));
			System.out.println("Hello world");
		} catch(Exception ex) {
			ex.printStackTrace();
		}
	}
}