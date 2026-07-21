MergeLife (Java reference library)
==================================

A small, dependency-free Java library implementing the MergeLife continuous
cellular automaton and its genetic-algorithm objective function, as described in
[the paper](https://doi.org/10.1007/s10710-018-9336-1). Its update rule is
paper-correct and verified byte-identical to the C engine in this repository.

This is the **reference implementation** — favouring readability over speed. For
performance use the C engine (`c/`); to animate rules use the
[JavaScript viewer](https://www.heatonresearch.com/mergelife/). This library is a
clean, portable source to read from or port elsewhere (e.g. C#/Unity).

Requirements
------------

- A JDK 17 or newer to build. The produced library targets Java 17 bytecode. Its
  only runtime dependency is `jackson-databind`, used by `MergeLifeConfig` to read
  the shared JSON objective format.

Build
-----

```
./gradlew build          # compile, run tests, produce build/libs/mergelife-2.0.0.jar
```

There is no fat jar and no CLI wrapper — this is a plain library.

Run the example
---------------

`src/examples/java/.../Example.java` demonstrates the library end to end (score a
rule against the paper objective, then render it to a PNG):

```
./gradlew runExample
./gradlew runExample --args="E542-5F79-9341-F31E-6C6B-7F08-8773-7068"
```

Use the library
---------------

```java
MergeLifeConfig config = MergeLifeConfig.paperObjective();
MergeLifeGeneticAlgorithm ga = new MergeLifeGeneticAlgorithm(new Random(), config);

double score = ga.score("E542-5F79-9341-F31E-6C6B-7F08-8773-7068"); // objective score
ga.render("E542-5F79-9341-F31E-6C6B-7F08-8773-7068");                // -> PNG
ga.process();                                                        // evolve new rules
```

The paper's objective is available programmatically via
`MergeLifeConfig.paperObjective()`, or loaded from
[`paperObjective.json`](paperObjective.json) with
`new MergeLifeConfig("paperObjective.json")` — the same shared file the Python, C,
and JavaScript tools use.

Note: `render()` and the genetic algorithm write `mergelife-<rule>.png` into the
working directory.
