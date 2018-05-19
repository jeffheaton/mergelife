MergeLife Java Evolve Utility
=============================

This project contains a Java implementation of the MergeLife evolutionary algorithm.
This project is used mainly to evolve new MergeLife rule strings. It does not have
ability to animate a CA.  If you wish to view an animated MergeLife rule, the
[JavaScript MergeLife viewer](https://www.heatonresearch.com/mergelife/) is your best option. The Java version makes use of
multithreading to enhance performance on multiprocessor systems.

This project builds a JAR file named **mergelife-evolve-all.jar** ([binary download]()) that can be
used from the command line to render, evolve, and score MergeLife update rules.

Render an Image
---------------

The **render** command allows a single image to be generated for a MergeLife CA.
This image is the grid after the specified number of render steps.  A MergeLife
hex string must be specified for the CA to be rendered.  The output will be
a **.png** file for that hex string. The dimensions and zoom factor can also
be specified.

```
java -jar mergelife-evolve-all.jar -cols 100 -rows 100 -renderSteps 250 -zoom 5 render E542-5F79-9341-F31E-6C6B-7F08-8773-7068
```


Evolve Cellular Automata
------------------------

The **evolve** command will search begin a search for MergeLife update rules.
Any interesting rule found will be written as a **.png** file where the filename
matches the update rule hex string.  A configuration file must be specified to
define the objective function.  A sample configuration file, that was used
with the MergeLife paper, is [provided here](https://github.com/jeffheaton/mergelife/blob/master/java/evolve/paperObjective.json).

```
java -jar mergelife-evolve-all.jar -config paperObjective.json evolve
```


Score a Cellular Automation
---------------------------

The **score** command will run the objective function against the specified hex string.
A configuration file must be provided that specifies the objective function. A sample configuration file, that was used
with the MergeLife paper, is [provided here](https://github.com/jeffheaton/mergelife/blob/master/java/evolve/paperObjective.json).

```
java -jar mergelife-evolve-all.jar -config paperObjective.json  score E542-5F79-9341-F31E-6C6B-7F08-8773-7068
```
