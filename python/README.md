MergeLife Python
================

The Python side of MergeLife lives in two places:

* **[mergelife-lib/](mergelife-lib/)** — the `mergelife` library: the update
  rule engine, the paper-compliant evolutionary trainer, and the objective
  function, packaged as an installable wheel. This is the single Python source
  of truth; it is verified against the shared cross-language conformance
  vectors (`conformance/vectors.txt`) and is also the reference engine that
  generates them.
* **[application/pyqt/](application/pyqt/)** — the HeatonCA desktop app
  (PyQt6), which imports the library.

Install the library
-------------------

```
pip install ./mergelife-lib            # from this directory
pip install './mergelife-lib[test]'    # with pytest, to run its test suite
```

Use it
------

```python
import mergelife

ml = mergelife.new_ml_instance(100, 100, "e542-5f79-9341-f31e-6c6b-7f08-8773-7068")
for _ in range(250):
    mergelife.update_step(ml)
mergelife.save_image(ml, "out.png")
```

The trainer is `mergelife.ml_evolve.Evolve`; see the library
[README](mergelife-lib/README.md) for the API and paper-conformance notes.

Runnable examples
-----------------

[mergelife-lib/examples/](mergelife-lib/examples/) covers the tasks the old
top-level scripts used to do:

| Task | Example |
|---|---|
| View a CA interactively | `pygame_viewer.py` (pygame; rule entry, presets, pause/step) |
| Evolve new rules | `train.py` (the paper's GA and objective by default) |
| Render a rule to a PNG | `render.py` |

The paper's config/objective JSON ships alongside them as
`examples/paperObjective.json`.

The desktop app
---------------

```
cd application/pyqt
python -m venv venv && venv/bin/pip install -r requirements.txt
venv/bin/python heaton-ca.py
```
