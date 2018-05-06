# MergeLife

Evolving Continuous Cellular Automata for Aesthetic Objectives
Jeff Heaton

**Abstract;** We present MergeLife, a genetic algorithm (GA) capable of evolving continuous
cellular automata (CA) that generate full color dynamic animations according to
aesthetic user specifications. A simple 16-byte update rule is introduced that is evolved
through an objective function that requires only initial human aesthetic guidelines. The
16-byte update rule provides a search space that is close to being convex, as small
changes to a single byte produce correspondingly small changes in the resulting
animation. Also introduced are several novel aesthetic fitness measures that
encourage the evolution of complex animations that often include spaceships,
oscillators, still life, and other complex emergent behavior.

The results of this research are several complex and long running update rules and the
objective function parameters that produced them. Several update rules produced from
this paper exhibit complex emergent behavior through patterns, such as spaceships,
guns, oscillators, and Universal Turing Machines. Because the true animated behavior
of these CA cannot be observed from static images, we also present an on-line
JavaScript viewer that is capable of animating any MergeLife 16-byte update rule

[JavaScript Demo](http://www.heatonresearch.com/mergelife)
