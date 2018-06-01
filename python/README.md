MergeLife Python Evolve Utility
===============================

This project contains a Python implementation of the MergeLife evolutionary algorithm and viewer.
This project is used both to evolve new MergeLife rule strings and to view animated MergeLife CA. The animation
uses matplotlib.  For evolution, multiple cores are used via the Python multiprocessing package.  This sidesteps
Python's GIL limitation.

Viewing a CA
------------

It is easiest just to use the [Javascript viewer](http://www.heatonresearch.com/mergelife/).  However, I do provide a Python viewer too.

The Python viewer will launch a Matplotlib window and animate the CA.  The value
CODE in the file ml_viewer.py specifies the ML rule that is to be viewed.
There are many CODE values to choose from in the comments of the source code.


Render an Image
---------------

The **render** command allows a single image to be generated for a MergeLife CA.
This image is the grid after the specified number of render steps.  A MergeLife
hex string must be specified for the CA to be rendered.  The output will be
a **.png** file for that hex string. The dimensions and zoom factor can also
be specified.

```
python ml_utility.py --rows 100 --cols 100 --renderSteps 250 --zoom 5 render E542-5F79-9341-F31E-6C6B-7F08-8773-7068
```


Evolve Cellular Automata
------------------------

The **evolve** command will search begin a search for MergeLife update rules.
Any interesting rule found will be written as a **.png** file where the filename
matches the update rule hex string.  A configuration file must be specified to
define the objective function.  A sample configuration file, that was used
with the MergeLife paper, is [provided here](https://github.com/jeffheaton/mergelife/blob/master/java/evolve/paperObjective.json).

```
python ml_utility.py --config paperObjective.json evolve
```


Score a Cellular Automation
---------------------------

The **score** command will run the objective function against the specified hex string.
A configuration file must be provided that specifies the objective function. A sample configuration file, that was used
with the MergeLife paper, is [provided here](https://github.com/jeffheaton/mergelife/blob/master/java/evolve/paperObjective.json).

```
python ml_utility.py --config ../java/evolve/paperObjective.json score E542-5F79-9341-F31E-6C6B-7F08-8773-7068
```
