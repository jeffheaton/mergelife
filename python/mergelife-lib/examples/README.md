# mergelife examples

Runnable examples for the `mergelife` library. Each example imports the
installed `mergelife` package, and falls back to `../src` so they also run
straight from a source checkout without installing.

## 1. Interactive viewer (pygame)

[`pygame_viewer.py`](pygame_viewer.py) — a desktop version of the
[web viewer](https://www.heatonresearch.com/mergelife/ml-viewer.html): type or
paste a rule hex code, load one of the paper's six named rules, and watch the
CA run. Applying a rule also prints its decoded sub-rule table to the
terminal, like the web viewer's "Update Rule (decoded)" panel.

```bash
pip install pygame
python pygame_viewer.py --rule e542-5f79-9341-f31e-6c6b-7f08-8773-7068
```

| Key | Action |
| --- | --- |
| click the rule box | edit the rule — text starts selected so typing or pasting replaces it; Cmd/Ctrl+A selects all, arrows/Home/End move the cursor, held keys repeat, Cmd/Ctrl+V pastes, Enter applies, Esc cancels |
| P | paste a rule from the clipboard and apply it immediately |
| Cmd/Ctrl+C | copy the current rule to the clipboard |
| Space | pause / resume |
| S | single step while paused |
| R | reset the lattice (same rule, new random grid) |
| N | random rule |
| 1–6 | presets: Red world, Game of Life, Yellow world, Shrinking cells, Still life, Sustained |
| + / − | double / halve generations per second |
| Q | quit |

Clipboard access uses SDL's native clipboard (pygame ≥ 2.1.3), with a
`pbpaste` fallback on macOS.

Options: `--rows`/`--cols` (grid size, default 100×100), `--zoom` (pixels per
cell, default 5), `--gps` (generations per second, default 20).

## 2. Training (the library's paper-compliant GA)

[`train.py`](train.py) — evolves new rules using `mergelife.ml_evolve.Evolve`,
the library's built-in steady-state GA (paper Sec. 5). The script is a thin
CLI: the paper's published config and Table 3 objective are the defaults, a
`ConsoleReporter` demonstrates the `report_target` progress hook, and rules
that beat `scoreThreshold` are rendered to PNGs by the trainer itself via the
`path` argument. Each GA run seeds a random population and breeds until
`patience` evaluations pass with no new best — looping runs reproduces the
paper's convergence-and-restart protocol.

```bash
# Paper settings (100x100 grid, population 100, threshold 3.5) — hours-scale:
python train.py

# Quick demo — a few minutes, lower bar:
python train.py --rows 50 --cols 50 --population 20 --patience 100 \
                --threshold 2.0 --runs 3
```

Useful flags: `--config file.json` (your own config/objective), `--out DIR`
(where found-rule PNGs go), `--runs N` (0 = until Ctrl-C), `--seed N`
(reproducibility), `--verbose` (show the library's own log messages).

[`paperObjective.json`](paperObjective.json) is the paper's published
config/objective as a ready-made `--config` sample — the same settings the
script uses by default, in the file format you would copy to define your own
aesthetic objective.

## 3. Command-line renderer

[`render.py`](render.py) — give it a rule, a step count, and a resolution; it
runs the CA from a random start grid and writes the final generation as a PNG.
The whole pipeline is three library calls: `new_ml_instance`, `update_step`,
`save_image`.

```bash
python render.py e542-5f79-9341-f31e-6c6b-7f08-8773-7068
python render.py <rule> --steps 1000 --rows 200 --cols 320 --zoom 4 --seed 42
```

Defaults: 250 steps on a 100×100 grid, written to `<rule>.png`. `--zoom N`
scales the image up with crisp pixels, `--out` names the file, and `--seed`
makes the start grid (and therefore the output) reproducible.
