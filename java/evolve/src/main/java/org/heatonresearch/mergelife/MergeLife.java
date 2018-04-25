package org.heatonresearch.mergelife;

import org.apache.commons.cli.*;

import java.io.File;
import java.io.IOException;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.Random;

public class MergeLife implements Runnable {
	private final List<MergeLifeGenome> population = new ArrayList<>();
	private final ObjectiveFunction objectiveFunction;
	private final int CUT_LENGTH = 5;
	private int evalCount;
	private MergeLifeGenome topGenome;
	private boolean requestStop;
	private int noImprovement;
	private double lastBestScore;
	private int populationSize;
	private Random rnd;

	public MergeLife(Random rnd, int populationSize, String objectiveFilename) throws IOException {
		this.objectiveFunction = new ObjectiveFunction(objectiveFilename);
		this.populationSize = populationSize;
		this.rnd = rnd;
	}

	public void init() {
		this.population.clear();
		for(int i=0;i<populationSize;i++) {
			population.add(new MergeLifeGenome(this.rnd));
		}
	}

	private synchronized void report() throws IOException {
		this.evalCount++;
		System.out.println("Eval #" + this.evalCount + ":" + this.topGenome);
		if( this.topGenome!=null) {
			if(this.topGenome.getScore()>this.lastBestScore) {
				this.lastBestScore = this.topGenome.getScore();
				this.noImprovement = 0;
			} else {
				this.noImprovement++;
				if(this.noImprovement>1000 && !this.requestStop) {
					System.out.println("No improvement for 1000, stopping...");
					this.requestStop = true;
					if( this.topGenome.getScore()>3.5 ) {
						render(this.topGenome.getRuleText());
					}
				}
			}
		}
	}

	public MergeLifeGenome tournament(Random rnd, int cycles) throws IOException {
		MergeLifeGenome best = null;


		for(int i = 0;i<cycles;i++) {
			int idx = (int)(rnd.nextDouble()*(this.population.size()));
			MergeLifeGenome challenger = this.population.get(idx);
			challenger.calculateScore(this.objectiveFunction);
			report();

			if( best!=null ) {
				if( best.compareTo(challenger)<0) {
					best = challenger;
					if(this.topGenome == null || best.getScore()>this.topGenome.getScore()) {
						this.topGenome = best;
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

	public void scorePopulation() {
		System.out.println("Scoring initial population");
		this.population.parallelStream().forEach(genome -> genome.calculateScore(this.objectiveFunction));
		System.out.println("Initial population scored");
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

	@Override
	public void run() {
		try {
			Random rnd = new Random();
			while(!this.requestStop) {
				if (rnd.nextDouble() < 0.75) {
					// Crossover
					MergeLifeGenome p1 = tournament(rnd, 5);
					MergeLifeGenome p2 = tournament(rnd, 5);
					String[] c = crossover(rnd, p1.getRuleText(), p2.getRuleText());
					addChild(rnd, c[0]);
					addChild(rnd, c[1]);
				} else {
					// mutation
					MergeLifeGenome p1 = tournament(rnd, 5);
					String c = mutate(rnd, p1.getRuleText());
					addChild(rnd, c);
				}
			}
		}
		catch(Exception ex) {
			ex.printStackTrace();
		}
	}

	public void startup() throws InterruptedException {
		List<Thread> threads = new ArrayList<>();
		this.requestStop = false;
		this.noImprovement = 0;
		this.lastBestScore = -100;
		this.evalCount = 0;
		this.topGenome = null;

		int cores = Runtime.getRuntime().availableProcessors();
		for(int i=0;i<cores;i++) {
			Thread thread = new Thread(this);
			threads.add(thread);
			thread.start();
		}

		for (Thread thread : threads) {
			thread.join();
		}
		System.out.println("Done.");
	}

	public static void render(String ruleText) throws IOException {
		MergeLifeGrid grid = new MergeLifeGrid(100, 100);
		MergeLifeRule rule = new MergeLifeRule(ruleText);
		grid.randomize(0,new Random());

		for(int i=0;i<250;i++) {
			grid.step(rule);
		}

		File file = new File("mergelife-"+ruleText+".png");
		grid.savePNG(0, 5, file);
		System.out.println("Saved: " + file);
	}

	public static void main(String[] args) {

		try {
			Options options = new Options();
			options.addOption("t", false, "display current time");

			Option buildfile = OptionBuilder.withArgName( "file" )
					.hasArg()
					.withDescription(  "the configuration file" )
					.create( "config");
			Option numRows = OptionBuilder.withArgName( "num" )
					.hasArg()
					.withDescription(  "the number of rows" )
					.create( "rows");
			Option numCols = OptionBuilder.withArgName( "num" )
					.hasArg()
					.withDescription(  "the number of columns" )
					.create( "numCols");
			options.addOption(buildfile);
			options.addOption(numRows);
			options.addOption(numCols);

			CommandLineParser parser = new GnuParser();
			CommandLine cmd = parser.parse( options, args);

			String configFile;

			if( cmd.hasOption("config")) {
			    configFile = cmd.getOptionValue("config");
            } else {
			    configFile = "./mergelife.json";
            }

			if( cmd.getArgs().length==0) {
				// automatically generate the help statement
				HelpFormatter formatter = new HelpFormatter();
				formatter.printHelp( "mergelife", options );
			} else {
                Random rnd = new Random();
                MergeLife life = new MergeLife(rnd, 100, configFile);
                for(;;) {
                    life.init();
                    life.scorePopulation();
                    life.startup();
                }
            }


		} catch (Exception ex) {
			ex.printStackTrace();
		}
	}
}