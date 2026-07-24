MergeLife Trainer (C)
=====================

A rewrite of the MergeLife rule search, aimed at finding rules that produce
spaceships, spawners and other class-4 behavior — and at finding them in
minutes rather than days.

Build and run:

```sh
cd c
make
./mergelife-trainer                 # search until interrupted
./mergelife-trainer --time 600      # search for ten minutes
```

It prints a status line every 15 seconds and a block for every interesting
rule it finds:

```
[   120s] rules/min 18330    archive 225/384 (59%)  best  12.519  screened 79%  found 99

  FOUND  dd2b-60ef-f472-b746-50c1-77c3-a2fc-32a0
         score 10.152   after 90s
         ships 112.0  guns 16.0  travel 7.4   rect 0.286  active 0.0350  foreground 0.1672
         generations 1001  late 2.08  entropy-var 0.408
```

Paste any of those hex strings into the JavaScript viewer
(`js/web-full-screen/ml-fullscreen.html?rule=<hex>&size=5`) to watch it, or
render a filmstrip directly:

```sh
./mergelife-trainer --render dd2b-60ef-f472-b746-50c1-77c3-a2fc-32a0 \
    --start 400 --every 7 --frames 5 -o rule.png
```

Requires a C11 compiler, pthreads and zlib. No other dependencies.


What changed, and why
---------------------

### The objective now measures spaceships directly

The published objective infers interesting behavior from `active` — the
fraction of cells that stopped being background in the last 5–25 generations.
That fires for a spaceship, but it fires just as happily for a blinking
oscillator, a noisy boundary, or a line that is simply growing. Four new
measures look at structure over time instead:

| statistic | what it measures |
|---|---|
| `ships` | small shapes that survive many generations while translating, found by block-matching the foreground mask between frames |
| `guns` | sites that launched two or more spaceships, well separated in time — a spawner |
| `entvar` | spread of the sub-rule usage entropy over the run. Wuensche's input-entropy variance separates class 4 (ordered *and* varied) from class 3 (varied but static) and class 1/2 (neither) |
| `late` | whether things are still moving at the end of the run rather than only during the opening transient |

A tracked shape only counts as a spaceship if it (a) keeps its shape while
moving, (b) moves in a consistent direction rather than jittering, (c) sits in
open space, and (d) **leaves background in its wake**. That last test is what
separates a spaceship from a growing line: both have a tip that translates,
and a block matcher follows either one happily.

The detector was checked against the rules the paper itself names and
describes:

| rule | the paper says | ships | guns |
|---|---|---|---|
| `8503-5eb6-…` | "cellular guns … emit several types of small spaceship" | 30 | **5** |
| `e542-5f79-…` Red World | spaceships frequent, trails are "gun-like" | **38** | 4 |
| `df1d-bba1-…` | sustained blobs, slow movement | 23 | 2 |
| `7b58-f7b4-…` Yellow World | spaceships, converges sooner | 14 | 0 |
| `a07f-c000-…` Game of Life | still life and oscillators from soup | 1 | 0 |
| `6769-5dd6-…` | "does not appear to produce spaceships" | **0** | 0 |
| `4c26-7f82-…` | chaos | 0 | 0 |
| `620c-0efc-…` | dead | 0 | 0 |

The ordering matches the prose, including zero on the rule the paper says has
no spaceships and the highest gun count on the rule it calls a gun.

Objective terms also gained a `shape`, because the published formula centers
its reward on *half the range* rather than the midpoint of `[min, max]`. For
`steps` with min 300 and max 1000 that puts the reward peak at 350 generations
and penalizes everything past it — the opposite of the paper's stated goal of
rules that "run for upwards of 1000 CA generations". Terms can now be
`legacy` (the reference formula, unchanged), `peak` (centered on the true
midpoint), or `rise` (monotonically increasing, for "more is better"
statistics like `ships` and longevity).

### The search is MAP-Elites, not a restarting GA

The original design is a steady-state GA over one population that restarts
whenever it stops improving. It spends most of its time re-converging on the
same basin and discards everything it learned at each restart; the paper
reports 18,796 convergences to collect 1000 rules, a 5.3% yield.

This keeps an archive of behavior cells keyed on three things you can see when
you watch a rule run — how much open space there is (`rect`), how much of the
grid is moving (`active`), and how much static structure has settled out
(`foreground`) — and stores the best rule found in each cell. Two things
follow:

- There is no premature convergence to escape, so there are no restarts. A
  rule that is mediocre overall but occupies an empty cell is kept, and
  becomes the stepping stone to a good rule in a neighboring cell.
- The output is a diverse set by construction. The paper notes it wanted
  "many interesting rules rather than one global maximum"; that is precisely
  what this algorithm optimizes for.

Variation operators are aligned to the genome's real structure. The original
mutates hex digits and crosses over at arbitrary hex offsets, which cuts a
(range, percent) pair in half and turns a small semantic step into an
arbitrary one. This uses Gaussian creep on the range and percent bytes,
whole-sub-rule swaps, sub-rule-aligned uniform crossover, and iso+line
crossover, which steps along directions that are already paying off in the
archive.

Because the objective is noisy — the paper measures σ ≈ 0.5 — elites are
occasionally re-evaluated on fresh starting grids and their stored score
replaced, so a cell filled by one lucky evaluation cannot keep that inflated
score forever. Anything about to be reported is re-run independently and must
clear the threshold again.

### Evaluation is staged, and the engine is much faster

A random rule is almost always worthless: the paper measured 55% chaos and 37%
dead out of 100 random samples. Establishing that with 5 cycles × 1000
generations on a 100×100 grid is the single biggest waste in the original
trainer. Evaluation now happens in three stages:

1. a screen on a 48×48 grid with a short generation cap, scored with the
   subset of the objective a short run can measure;
2. full-fidelity cycles that abandon a run as soon as the grid is provably
   dead or provably boiling;
3. a race — if two cycles land far below the threshold, the rest cannot
   rescue the rule.

Typically ~80% of candidates never reach a full evaluation.

The CA engine itself was restructured, without approximating anything:

- Every key color component is 0 or 255, so `g' = g + floor((key - g) × pct)`
  has only two possible transfer curves per sub-rule. Both are precomputed
  into byte→byte tables, removing all floating point from the inner loop.
- Choosing a sub-rule is a search for the first `alpha` above the neighbor
  count; that becomes a single 2041-entry table lookup.
- The neighbor count is a 3×3 box sum minus the center, computed with a
  separable pass, and a one-cell halo holding the background color removes the
  edge tests.
- The largest-background-rectangle scan runs once at the end rather than every
  generation, as it does in the JavaScript version.

Measured at **544 million cell updates per second on one core** (Apple M5
Max), and about 18,000 candidate rules per minute on 18 threads.


Fidelity to the reference implementations
-----------------------------------------

`make test` fuzzes the optimized engine against `test/verify.c`, a deliberately
naive transcription of `MergeLifeGrid.step()` from the reference Java, and
requires the full RGB buffers to match byte for byte at every generation:

```
PASS  3000 rules x 40 generations on a 37x53 grid, byte identical
      (2347 rules left some cells untouched, 707 had tied ranges)
```

Those two counts matter — they confirm the fuzz actually exercised the two
subtle paths, described below.

`--preset paper` reproduces the published objective. Scores land within the
paper's own σ ≈ 0.5 for the rules it classifies as good:

| rule | published | here |
|---|---|---|
| `a07f-c000-…` (Game of Life) | 2.698 | 2.739 |
| `e542-5f79-…` (Red World) | 3.793 | 3.860 |
| `df1d-bba1-…` | 4.545 | 4.434 |
| `0de6-3496-…` | 3.931 | 4.038 |
| `6007-7d42-…` | 3.995 | 3.855 |
| `620c-0efc-…` (dead) | −0.100 | −0.100 |

Rules the paper classes as chaotic vary by ±2 between runs in both directions,
because their `steps` statistic flips between converging early and reaching the
cap depending on the starting grid; single measurements of them are not
reproducible in any implementation.

### Details that had to be matched exactly

**Runs end at generation 1001, not 1000.** The reference calls
`hasStabilized()` *before* each step and tests `step > 1000`, so an
unconverged rule stops at 1001 — which is *above* the objective's max of 1000
and therefore earns `max_weight` (+1) instead of the steeply negative in-range
value (−0.857). Stopping at exactly 1000 makes every long-running rule score
two points lower than the published table.

**Cells matched by no sub-rule keep their current value.** The paper says "no
change is made to that cell", but the original reference implementations
double-buffered and simply skipped the write, so the cell reverted to the
other buffer's contents — a real source of the flashing the paper mentions.
Every engine in this repo (including this one; see the `ML_NOOP` identity
table in `ca.c`) has since been corrected to the paper's stated behavior, and
the shared conformance vectors in `conformance/` encode it.

**Sub-rules with equal ranges keep their original hex order.** Java's
`Collections.sort` and JavaScript's `Array.sort` are both stable, so ties break
by position in the hex string.


Two inconsistencies found in the historical implementations
-----------------------------------------------------------

Found while establishing the above; neither was introduced by this trainer.
The Python issue is resolved: the legacy module is gone.

**The legacy top-level `python/mergelife.py` simulated a different CA.** It
merged RGB with a floating point dot product and truncated:

```python
data_avg = np.dot(prev_data, [THIRD, THIRD, THIRD]).astype(int)
```

Java and JavaScript use integer division, `(r + g + b) / 3`. These disagree on
**42 of the 256 gray levels** — for `r = g = b = 85` the float path gives
`84.99999999999999 → 84` where the integer path gives 85. Since the merged
value feeds the neighbor count and the background mode, Python's trajectories
diverged from the viewer's. That module also sorted sub-rules by
`(alpha, percent, index)`, breaking tied ranges differently than Java and
JavaScript. Both defects were fixed before the module was replaced by the
shared `mergelife` library (python/mergelife-lib), which merges with integer
arithmetic, sorts stably by alpha alone, and is the reference engine for the
conformance vectors.

**Java averages evaluation cycles where the paper says maximum.**
`BasicObjectiveFunction.calculateObjective` returns `sum / evalCycles`, while
the paper states "the max score from these 5 runs becomes the score" and the
mergelife library's `objective_function` takes `np.max`. Since Java produced
the published rules, the published scores are means of five cycles, not
maxima. `--aggregate mean|max` selects either; max is the default here,
matching the paper text.


Command line
------------

```
--preset NAME     spaceships (default), guns, or paper
--config FILE     load an objective in the published JSON format
--threads N       worker threads (default: one per core)
--time SECONDS    stop after this long
--threshold X     report rules scoring at least X
--cycles N        evaluation cycles per rule (default 5)
--rows N --cols N grid size (default 100x100)
--no-screen       evaluate every rule at full fidelity
--aggregate WHICH combine cycles by max (default) or mean (Java)
-o, --out FILE    append found rules to a tab separated file

--eval RULE       print every statistic for one rule
--render RULE     render a filmstrip to --out, with --start/--every/--frames
--bench [SECS]    measure CA throughput
```

`--config` reads the existing `paperObjective.json` format, and understands an
optional `"shape"` on each objective term.
