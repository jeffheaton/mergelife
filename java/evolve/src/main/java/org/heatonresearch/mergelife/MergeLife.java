package org.heatonresearch.mergelife;

import java.io.File;
import java.io.IOException;
import java.util.ArrayList;
import java.util.List;
import java.util.Random;

public class MergeLife {
	private final List<MergeLifeGenome> population = new ArrayList<>();
	private final ObjectiveFunction objectiveFunction;

	public MergeLife(Random rnd, int populationSize, String objectiveFilename) throws IOException {
		this.objectiveFunction = new ObjectiveFunction(objectiveFilename);
		for(int i=0;i<populationSize;i++) {
			population.add(new MergeLifeGenome(rnd));
		}
	}

	public MergeLifeGenome tournament(Random rnd, boolean chooseBest, int cycles) {
		MergeLifeGenome best = null;

		for(int i = 0;i<cycles;i++) {
			MergeLifeGenome challenger = this.population.get((int)rnd.nextDouble()*(this.population.size()+1));
			challenger.calculateScore(this.objectiveFunction);

			if( best!=null ) {
				if (chooseBest) {
					if( best.compareTo(challenger)>0) {
						best = challenger;
					}
				} else {
					if( best.compareTo(challenger)<0) {
						best = challenger;
					}
				}
			} else {
				best = challenger;
			}
		}

		return best;
	}

	public void step(Random rnd) {
		if( rnd.nextDouble()<0.75) {
			// crossover
		} else {
			// mutation
		}
	}

	public static void test() throws IOException {
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
	}

	public static void main(String[] args) {
		try {
			MergeLife life = new MergeLife( new Random(),100, "D:\\Users\\jheaton\\projects\\mergelife\\java\\evolve\\paperObjective.json");
			System.out.println(life.tournament(new Random(),true,5));
		} catch(Exception ex) {
			ex.printStackTrace();
		}
	}
}