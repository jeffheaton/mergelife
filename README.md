MergeLife
=========

![MergeLife](https://raw.githubusercontent.com/jeffheaton/mergelife/master/img/mergelife-1.png)


Evolving Continuous Cellular Automata for Aesthetic Objectives
--------------------------------------------------------------
Jeff Heaton

**Abstract:** We present MergeLife, a Genetic Algorithm (GA) capable of evolving continuous
Cellular Automata (CA) that generate full color dynamic animations according to
aesthetic user specifications. A simple 16-byte update rule is introduced that is evolved
through an objective function that requires only initial human aesthetic guidelines. This
update rule provides a fixed-length genome that can be successfully optimized by a
GA. Also introduced are several novel fitness measures that when given human
selected aesthetic guidelines encourage the evolution of complex animations that often
include spaceships, oscillators, still life, and other complex emergent behavior.

The results of this research are several complex and long running update rules and the
objective function parameters that produced them. Several update rules produced
from this paper exhibit complex emergent behavior through patterns, such as
spaceships, guns, oscillators, and Universal Turing Machines. Because the true
animated behavior of these CA cannot be observed from static images, we also
present an on-line JavaScript viewer that is capable of animating any MergeLife 16-byte update rule.

Accepted for publication in: [Journal of Genetic Programming and Evolvable Machines](https://www.springer.com/computer/ai/journal/10710)
Article title: Evolving Continuous Cellular Automata for Aesthetic Objectives
DOI: 10.1007/s10710-018-9336-1

Useful Links
------------

* [JavaScript Demo](http://www.heatonresearch.com/mergelife)
* [Binary Downloads](https://github.com/jeffheaton/mergelife/blob/master/binaries.md)

MergeLife Implementations
-------------------------

* [MergeLife in JavaScript](https://github.com/jeffheaton/mergelife/tree/master/js/)
* [MergeLife in Java](https://github.com/jeffheaton/mergelife/tree/master/java/)
* [MergeLife in Python](https://github.com/jeffheaton/mergelife/tree/master/python/)
