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
continuous cellular automata for aesthetic objectives*, GPEM 20:93–125, 2019)
exactly, including three places where the paper differs from every historical
implementation in this repo:

* mutation exchanges two random hex digits (Sec. 5.3) rather than replacing
  one digit with a random value,
* a stable background cell requires more than 100 CA generations as the
  background color (Sec. 4), not 50,
* convergence uses the paper's three conditions (Sec. 4.1), including the
  "less than 1% of merged cells changed in 100 generations" test, and drops
  the legacy Python-only early exit (stop when stable background stays under
  1% after 100 generations).

Trainer results therefore may differ from the legacy Java/JS/Python trainers
until those are migrated to this library's behavior. The update rule itself is
unchanged and verified against the shared cross-language conformance vectors.

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
