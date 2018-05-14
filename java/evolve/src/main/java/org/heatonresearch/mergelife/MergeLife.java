/*
 * MergeLife, Copyrighr 2018 by Jeff Heaton
 * http://www.heatonresearch.com/mergelife/
 * MIT License
 */
package org.heatonresearch.mergelife;

import org.apache.commons.cli.*;
import java.util.Random;


// D:\Users\jheaton\projects\mergelife\java\evolve\build\libs>java -jar evolve-all.jar --rows 1000 --cols 1000 render E542-5F79-9341-F31E-6C6B-7F08-8773-7068
public class MergeLife {

	public static void displayHelp(Options options) {
		System.out.println("MergeLife [evolve/render/score]");
        // automatically generate the help statement
        HelpFormatter formatter = new HelpFormatter();
        formatter.printHelp( "mergelife", options );
    }

	public static void main(String[] args) {

		try {
			Options options = new Options();

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
					.create( "cols");

            Option renderSteps = OptionBuilder.withArgName( "num" )
                    .hasArg()
                    .withDescription(  "the number of render steps" )
                    .create( "renderSteps");

            Option zoom = OptionBuilder.withArgName( "zoom" )
                    .hasArg()
                    .withDescription(  "how large of a pixel to use when rendering" )
                    .create( "zoom");

			options.addOption(buildfile);
			options.addOption(numRows);
			options.addOption(numCols);
			options.addOption(renderSteps);
			options.addOption(zoom);


			CommandLineParser parser = new GnuParser();
			CommandLine cmd = parser.parse( options, args);

			MergeLifeConfig config;

			if( cmd.hasOption("config")) {
			    config = new MergeLifeConfig(cmd.getOptionValue("config"));
            } else {
			    config = new MergeLifeConfig();
			    config.setRows(100);
			    config.setCols(100);
			    config.setZoom(5);
			    config.setRenderSteps(250);
            }

            if( cmd.hasOption("rows")) {
				config.setRows(Integer.parseInt(cmd.getOptionValue("rows")));
			}

			if( cmd.hasOption("cols")) {
				config.setCols(Integer.parseInt(cmd.getOptionValue("cols")));
			}

			if( cmd.hasOption("zoom")) {
				config.setZoom(Integer.parseInt(cmd.getOptionValue("zoom")));
			}

			if( cmd.hasOption("zoom")) {
				config.setRenderSteps(Integer.parseInt(cmd.getOptionValue("renderSteps")));
			}


            Random rnd = new Random();

			if( cmd.getArgs().length==0) {
                displayHelp(options);
			} else if(cmd.getArgs()[0].equalsIgnoreCase("evolve")) {
			    if(cmd.getArgs().length!=1) {
			        System.out.println("Usage: evolve");
                } else {
                    MergeLifeGeneticAlgorithm ga = new MergeLifeGeneticAlgorithm(rnd, config);
                    ga.process();
                }
            } else if(cmd.getArgs()[0].equalsIgnoreCase("render")) {
                if(cmd.getArgs().length!=2) {
                    System.out.println("Usage: render [rule-text]");
                } else {
                    MergeLifeGeneticAlgorithm ga = new MergeLifeGeneticAlgorithm(rnd, config);
                    ga.render(cmd.getArgs()[1]);
                }
			} else if(cmd.getArgs()[0].equalsIgnoreCase("score")) {
                if(cmd.getArgs().length!=2) {
                    System.out.println("Usage: score [rule-text]");
                } else {
                    MergeLifeGeneticAlgorithm ga = new MergeLifeGeneticAlgorithm(rnd, config);
                    double score = ga.score(cmd.getArgs()[1]);
					System.out.println("Score: " + score);
                }
			} else {
                displayHelp(options);
            }


		} catch (Exception ex) {
			ex.printStackTrace();
		}
	}
}