package org.heatonresearch.mergelife;

import org.apache.commons.cli.*;
import java.util.Random;

public class MergeLife {

	public static void displayHelp(Options options) {
        // automatically generate the help statement
        HelpFormatter formatter = new HelpFormatter();
        formatter.printHelp( "mergelife", options );
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
					.create( "cols");
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
                displayHelp(options);
			} else if(cmd.getArgs()[0].equalsIgnoreCase("evolve")) {
                Random rnd = new Random();
                MergeLifeGeneticAlgorithm ga = new MergeLifeGeneticAlgorithm(rnd, configFile);
                ga.process();
            } else {
                displayHelp(options);
            }


		} catch (Exception ex) {
			ex.printStackTrace();
		}
	}
}