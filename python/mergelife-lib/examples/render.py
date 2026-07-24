"""Render a MergeLife rule to a PNG from the command line.

The whole pipeline is three library calls: ``new_ml_instance`` builds a
randomly-seeded grid for the rule, ``update_step`` advances it one CA
generation, and ``save_image`` writes the final grid as a PNG.

Run:
    python render.py e542-5f79-9341-f31e-6c6b-7f08-8773-7068
    python render.py <rule> --steps 1000 --rows 200 --cols 320 --zoom 4 --seed 42
"""

import argparse
import os
import sys

try:
    import mergelife
except ImportError:  # allow running straight from a source checkout
    sys.path.insert(
        0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src")
    )
    import mergelife


def normalize_rule(text):
    """Return the cleaned rule string, or None if it is not a valid rule."""
    hexpart = text.strip().lower().replace("-", "")
    if len(hexpart) != 32 or any(c not in "0123456789abcdef" for c in hexpart):
        return None
    return "-".join(hexpart[i:i + 4] for i in range(0, 32, 4))


def main(argv=None):
    parser = argparse.ArgumentParser(
        description="Run a MergeLife rule and save the final grid as a PNG")
    parser.add_argument("rule", help="rule hex code, e.g. "
                        "e542-5f79-9341-f31e-6c6b-7f08-8773-7068")
    parser.add_argument("--steps", type=int, default=250,
                        help="CA generations to run (default 250)")
    parser.add_argument("--rows", type=int, default=100,
                        help="grid rows (default 100)")
    parser.add_argument("--cols", type=int, default=100,
                        help="grid cols (default 100)")
    parser.add_argument("--zoom", type=int, default=1,
                        help="scale the PNG by this factor (default 1)")
    parser.add_argument("--out", help="output file (default <rule>.png)")
    parser.add_argument("--seed", type=int,
                        help="numpy random seed for a reproducible start grid")
    args = parser.parse_args(argv)

    rule = normalize_rule(args.rule)
    if rule is None:
        parser.error(f"invalid rule: {args.rule!r} (need 32 hex digits)")
    if args.steps < 0 or args.rows < 3 or args.cols < 3 or args.zoom < 1:
        parser.error("steps must be >= 0, rows/cols >= 3, zoom >= 1")

    if args.seed is not None:
        import numpy as np
        np.random.seed(args.seed)

    out = args.out or (rule + ".png")

    ml = mergelife.new_ml_instance(args.rows, args.cols, rule)
    for _ in range(args.steps):
        mergelife.update_step(ml)
    mergelife.save_image(ml, out)

    if args.zoom > 1:
        from PIL import Image
        img = Image.open(out)
        img = img.resize((args.cols * args.zoom, args.rows * args.zoom),
                         Image.NEAREST)
        img.save(out)

    print(f"{rule}: {args.steps} generations on {args.rows}x{args.cols} "
          f"-> {out} ({args.cols * args.zoom}x{args.rows * args.zoom} px)")


if __name__ == "__main__":
    main()
