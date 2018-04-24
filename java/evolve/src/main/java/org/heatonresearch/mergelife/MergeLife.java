package org.heatonresearch.mergelife;

import java.io.File;
import java.io.IOException;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.Random;

public class MergeLife {
	private final List<MergeLifeGenome> population = new ArrayList<>();
	private final ObjectiveFunction objectiveFunction;
	private final int CUT_LENGTH = 5;
	private int evalCount;
	private MergeLifeGenome topGenome;

	public MergeLife(Random rnd, int populationSize, String objectiveFilename) throws IOException {
		this.objectiveFunction = new ObjectiveFunction(objectiveFilename);
		for(int i=0;i<populationSize;i++) {
			population.add(new MergeLifeGenome(rnd));
		}
	}

	public MergeLifeGenome tournament(Random rnd, int cycles) {
		MergeLifeGenome best = null;
		this.evalCount++;
		System.out.println("Eval " + this.evalCount + ":" + this.topGenome);

		for(int i = 0;i<cycles;i++) {
			int idx = (int)(rnd.nextDouble()*(this.population.size()));
			MergeLifeGenome challenger = this.population.get(idx);
			challenger.calculateScore(this.objectiveFunction);

			if( best!=null ) {
				if( best.compareTo(challenger)<0) {
					best = challenger;
					if(this.topGenome == null || best.getScore()>this.topGenome.getScore()) {
						this.topGenome = best;
						System.out.println("New best: " + this.topGenome);
					}
				}
			} else {
				best = challenger;
			}
		}

		return best;
	}

	public String mutate(Random rnd, String parent) {
		final String h = "0123456789ABCDEF";
		String result = "";

		boolean done = false;
		while( !done ) {
			int i = (int) (rnd.nextDouble() * parent.length());

			if (parent.charAt(i) != '-') {
				int i2 = (int) (rnd.nextDouble() * h.length());
				result = parent.substring(0, i) + h.charAt(i2) + parent.substring(i + 1);
				if (!result.equalsIgnoreCase(parent)) {
					done = true;
				}
			}
		}
		return result;
	}

	public void addChild(Random rnd, String ruleText) {
		MergeLifeGenome genome = new MergeLifeGenome(ruleText);
		genome.calculateScore(this.objectiveFunction);
		int worstIdx =-1;

		synchronized (this.population) {
			MergeLifeGenome worst = null;

			for(int i = 0;i<5;i++) {
				int idx = (int)(rnd.nextDouble()*(this.population.size()));
				MergeLifeGenome challenger = this.population.get(idx);
				challenger.calculateScore(this.objectiveFunction);

				if( worst!=null ) {
					if( worst.compareTo(challenger)>0) {
						worst = challenger;
						worstIdx = idx;
					}
				} else {
					worst = challenger;
					worstIdx = idx;
				}
			}

			this.population.remove(worstIdx);
			this.population.add(worstIdx,genome);
		}
	}

	public String[] crossover(Random rnd, String parent1, String parent2) {
		// The genome must be cut at two positions, determine them
		int cutpoint1 = (int)(rnd.nextDouble() * (parent1.length() - CUT_LENGTH));
		int cutpoint2 = cutpoint1 + CUT_LENGTH;

		// Produce two offspring
		String[] r = new String[2];
		r[0] = parent1.substring(0,cutpoint1) + parent2.substring(cutpoint1,cutpoint2) + parent1.substring(cutpoint2);
		r[1] = parent2.substring(0,cutpoint1) + parent1.substring(cutpoint1,cutpoint2) + parent2.substring(cutpoint2);
		return r;
	}

	public void step(Random rnd) {
		if( rnd.nextDouble()<0.75) {
			// Crossover
			MergeLifeGenome p1 = tournament(rnd,5);
			MergeLifeGenome p2 = tournament(rnd,5);
			String[] c = crossover(rnd,p1.getRuleText(),p2.getRuleText());
			addChild(rnd,c[0]);
			addChild(rnd,c[1]);
		} else {
			// mutation
			MergeLifeGenome p1 = tournament(rnd,5);
			String c = mutate(rnd, p1.getRuleText());
			addChild(rnd,c);
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
			Random rnd = new Random();
			MergeLife life = new MergeLife( rnd,100, "D:\\Users\\jheaton\\projects\\mergelife\\java\\evolve\\paperObjective.json");
			for(;;) {
				life.step(rnd);
			}
		} catch(Exception ex) {
			ex.printStackTrace();
		}
	}
}