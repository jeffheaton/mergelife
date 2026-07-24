"""Evolve new MergeLife rules with the library's paper-compliant trainer.

Everything that matters here is the library's own API — this script only adds
a command line around it:

* ``mergelife.ml_evolve.Evolve`` runs the paper's steady-state GA: tournament
  selection, complementary crossover, two-digit swap mutation, and
  patience-based convergence (Sec. 5 of the paper).
* The config dict below is the paper's published settings (Sec. 5.4 JSON),
  including the Table 3 objective. Pass ``--config`` to use your own.
* ``Evolve(report_target=...)`` calls ``report(evolver)`` after every
  evaluation — ConsoleReporter below turns that into a progress line.
* ``Evolve(path=...)`` makes the trainer render any rule that beats
  ``scoreThreshold`` to ``<path>/<rule>.png`` as it is discovered.

Each ``evolve()`` call is one GA run: it seeds a random population, breeds
until ``patience`` evaluations pass without a new best genome, and stops.
Looping over runs reproduces the paper's convergence-and-restart protocol.

Run (paper settings — slow, hours-scale in Python):
    python train.py

Quick demo (small grid, small population, low bar; a couple of minutes):
    python train.py --rows 50 --cols 50 --population 20 --patience 100 \
                    --threshold 2.0 --runs 3
"""

import argparse
import copy
import json
import logging
import os
import sys
import time

try:
    import mergelife
except ImportError:  # allow running straight from a source checkout
    sys.path.insert(
        0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src")
    )
    import mergelife

from mergelife import ml_evolve

# The paper's GA configuration and objective (Sec. 5.4 and Table 3).
PAPER_CONFIG = {
    "config": {
        "rows": 100,
        "cols": 100,
        "populationSize": 100,
        "crossover": 0.75,
        "tournamentCycles": 5,
        "zoom": 5,
        "renderSteps": 250,
        "evalCycles": 5,
        "patience": 1000,
        "scoreThreshold": 3.5,
        "maxRuns": 1000000,
    },
    "objective": [
        {"stat": "steps", "min": 300, "max": 1000, "weight": 1,
         "min_weight": -1, "max_weight": 1},
        {"stat": "foreground", "min": 0.001, "max": 0.1, "weight": 1,
         "min_weight": -0.1, "max_weight": -1},
        {"stat": "active", "min": 0.001, "max": 0.1, "weight": 1,
         "min_weight": -1, "max_weight": -1},
        {"stat": "rect", "min": 0.02, "max": 0.25, "weight": 2,
         "min_weight": -2, "max_weight": 2},
        {"stat": "mage", "min": 5, "max": 10, "weight": 0,
         "min_weight": -5, "max_weight": 0},
    ],
}


class ConsoleReporter:
    """Progress hook for Evolve: called after every objective evaluation."""

    def __init__(self, every_seconds=5.0):
        self.every = every_seconds
        self.last = 0.0
        self.started = time.time()

    def report(self, evolver):
        now = time.time()
        if now - self.last < self.every:
            return
        self.last = now
        best = (f"{evolver.bestGenome['score']:7.3f}"
                if evolver.bestGenome else "     --")
        rate = evolver.totalEvalCount / max(now - self.started, 1e-9) * 60.0
        print(
            f"  run {evolver.runCount}"
            f" | eval {evolver.evalCount}"
            f" | best {best}"
            f" | stall {evolver.noImprovement}/{evolver.patience}"
            f" | {rate:5.0f} evals/min"
            f" | found {evolver.rules_found}",
            flush=True,
        )


def main(argv=None):
    parser = argparse.ArgumentParser(
        description="Evolve MergeLife rules (paper-compliant GA from the "
                    "mergelife library)")
    parser.add_argument("--config", help="JSON config/objective file "
                        "(default: the paper's published settings)")
    parser.add_argument("--out", default="found_rules",
                        help="directory for PNGs of found rules")
    parser.add_argument("--runs", type=int, default=0,
                        help="GA runs to perform (0 = until Ctrl-C)")
    parser.add_argument("--rows", type=int, help="override grid rows")
    parser.add_argument("--cols", type=int, help="override grid cols")
    parser.add_argument("--population", type=int,
                        help="override populationSize")
    parser.add_argument("--patience", type=int, help="override patience")
    parser.add_argument("--threshold", type=float,
                        help="override scoreThreshold")
    parser.add_argument("--seed", type=int, help="numpy random seed")
    parser.add_argument("--verbose", action="store_true",
                        help="show the library's own log messages")
    args = parser.parse_args(argv)

    if args.config:
        with open(args.config) as f:
            config = json.load(f)
    else:
        config = copy.deepcopy(PAPER_CONFIG)
    for key, value in [("rows", args.rows), ("cols", args.cols),
                       ("populationSize", args.population),
                       ("patience", args.patience),
                       ("scoreThreshold", args.threshold)]:
        if value is not None:
            config["config"][key] = value

    logging.basicConfig(
        level=logging.INFO if args.verbose else logging.WARNING,
        format="%(message)s")
    if args.seed is not None:
        import numpy as np
        np.random.seed(args.seed)

    out_dir = os.path.abspath(args.out)
    os.makedirs(out_dir, exist_ok=True)

    c = config["config"]
    print(f"Grid {c['rows']}x{c['cols']} | population {c['populationSize']} | "
          f"patience {c['patience']} | threshold {c['scoreThreshold']}")
    print(f"Rules scoring above the threshold are rendered to {out_dir}")
    print("Ctrl-C stops after the current evaluation.\n")

    evolver = ml_evolve.Evolve(report_target=ConsoleReporter(), path=out_dir)
    started = time.time()
    completed_runs = 0
    try:
        while args.runs <= 0 or completed_runs < args.runs:
            evolver.evolve(config)
            completed_runs += 1
            best = evolver.bestGenome
            print(f"run {completed_runs} converged: best {best['rule']} "
                  f"scored {best['score']:.3f}")
    except KeyboardInterrupt:
        print("\nStopped by user.")

    elapsed = time.time() - started
    print(f"\n{completed_runs} run(s), {evolver.totalEvalCount} evaluations "
          f"in {ml_evolve.hms_string(elapsed)}")
    print(f"{evolver.rules_found} rule(s) beat the threshold; PNGs in {out_dir}")


if __name__ == "__main__":
    main()
