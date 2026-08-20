# mergelife (Python library)

The canonical Python implementation of the [MergeLife](https://github.com/jeffheaton/mergelife)
cellular automaton — the update rule, the objective function, and the
evolutionary trainer — packaged as an installable `mergelife` wheel.

This library was consolidated from two previously diverged in-repo copies
(the historical top-level `python/*.py` modules and the PyQt app's vendored
package, both since removed). It is the single Python source of truth: the
PyQt app installs it as a dependency, the runnable examples build on it, and
`conformance/gen_vectors.py` uses it as the reference engine that generates
the cross-language golden vectors.

## Paper conformance

The engine and trainer follow the published paper (Heaton, *Evolving
continuous cellular automata for aesthetic objectives*, GPEM 20:93–125, 2019),
including one place where the paper differs from every historical
implementation in this repo:

* mutation exchanges two random hex digits (Sec. 5.3) rather than replacing
  one digit with a random value.

### Why the objective follows the 2018 trainer, not the paper's Sec. 4.1 text

The **evaluation layer** — the bookkeeping behind the objective statistics and
the convergence test that decides when to measure them — is deliberately the
2018 reference trainer's, the code that produced the published rules and the
gallery. A run ends when the world dies (stable background under 1% after
generation 100), when the stable background count has not moved for more than
100 generations, or at the generation cap; a stable background cell is one that
has held the background color for more than 50 generations.

This library briefly used the paper's Sec. 4.1 wording instead — the "less than
1% of merged cells changed in the last 100 generations" test, with the
100-generation stable-background threshold the same section implies. That
combination reads nearly every world as converged around generation 101: no
cell can qualify as stable background before then, and MergeLife's signature
look (a settled background carrying gliders and sparks) moves well under 1% of
a 10,000-cell lattice. Scored over the 30 curated gallery rules at the paper's
configuration (100×100, five cycles, best of three lattice seeds):

| | 2018 trainer | paper Sec. 4.1 |
|---|---|---|
| median score | 3.76 | 1.54 |
| at or above the 3.5 save threshold | 20 / 30 | 5 / 30 |
| negative | 6 / 30 | 11 / 30 |

A fitness function that ranks its own hall of fame that far down is optimizing
for something else — under Sec. 4.1 a GA out-scores every curated rule with
static worlds that converge at exactly 101 generations. The paper's text and
the paper's code disagree, and every score of record came from the code.

Both settings share the objective table (Sec. 4, Table 3) verbatim — the
weights and reward bands never changed. The update rule itself is untouched by
any of this and stays verified against the shared cross-language conformance
vectors, which pin lattice evolution only.

The C, Java, and JS trainers in this repo already use the 2018 evaluation layer
(minus the Python-only dead-world exit), so the four engines now agree on
convergence again; trainer results still differ from them through the mutation
operator above.

## Layout

```
src/mergelife/
    __init__.py     public API re-exports
    mergelife.py    engine: update rule, objective stats, objective function
    ml_evolve.py    trainer: mutate/crossover/tournament primitives + Evolve class
    dp.py           largest-rectangle dynamic program (vendored, ISC licensed)
tests/              pytest suite, including cross-language conformance replay
```

## Usage

```python
import mergelife

ml = mergelife.new_ml_instance(100, 100, "e542-5f79-9341-f31e-6c6b-7f08-8773-7068")
for _ in range(250):
    mergelife.update_step(ml)
mergelife.save_image(ml, "out.png")
```

Both import styles work — the flat style above (as the historical top-level
`mergelife.py` module was used) and the submodule style used by the PyQt app
(`import mergelife.mergelife`, `import mergelife.ml_evolve`, `import mergelife.dp`).

## Building the wheel

```bash
pip wheel --no-deps . -w dist
```

(or `python -m build --wheel`). Not published to PyPI; consumers in this repo
install it as a local path dependency.

## Testing

```bash
pip install -e '.[test]'
pytest
```

`tests/test_conformance.py` replays the shared golden vectors from
`../../conformance/vectors.txt` — the same file the C, Java, and JS engines
verify against — and is skipped automatically if the library is built outside
the repo.
