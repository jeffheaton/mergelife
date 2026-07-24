"""Pre-render the gallery preview images.

The Gallery tab used to simulate all of its rules live, which blocked the UI
for over a second every time the tab was opened. Instead the previews are
generated once, here, and committed under ``data/gallery/`` where PyInstaller
picks them up (the spec bundles ``data/`` recursively).

Run from the pyqt app directory:

    venv/bin/python tools/generate_gallery.py            # only missing images
    venv/bin/python tools/generate_gallery.py --force    # regenerate everything

Rendering is seeded per rule, so regenerating produces byte-identical files and
does not create noise in git.
"""

import argparse
import os
import sys

import numpy as np
from PIL import Image
from mergelife.mergelife import new_ml_instance, randomize_lattice, update_step

# Allow running as `python tools/generate_gallery.py` from the app directory.
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from tab_gallery import GalleryTab  # noqa: E402

# Must match the values GalleryTab renders at, or the previews will not line up
# with the rest of the grid.
HEIGHT = GalleryTab.PREVIEW_HEIGHT
WIDTH = GalleryTab.PREVIEW_WIDTH
CELL_SIZE = GalleryTab.PREVIEW_CELL_SIZE
STEPS = 50


def render_rule(rule, seed):
    """Simulate a rule and return an upscaled RGB array ready to save."""
    np.random.seed(seed)
    ml = new_ml_instance(HEIGHT, WIDTH, rule)
    randomize_lattice(ml)
    for _ in range(STEPS):
        update_step(ml)

    grid = np.asarray(ml["lattice"][0]["data"], dtype=np.uint8)
    # Nearest-neighbour upscale -- the CA look depends on hard pixel edges, so
    # this must not be a smooth scale.
    return np.repeat(np.repeat(grid, CELL_SIZE, axis=0), CELL_SIZE, axis=1)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--force", action="store_true", help="regenerate images that already exist")
    parser.add_argument("--seed", type=int, default=42, help="base RNG seed (default: 42)")
    args = parser.parse_args()

    app_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    out_dir = os.path.join(app_dir, GalleryTab.GALLERY_DIR)
    os.makedirs(out_dir, exist_ok=True)

    written = skipped = 0
    for idx, rule in enumerate(GalleryTab.GALLERY_RULES):
        path = os.path.join(out_dir, f"{rule}.png")
        if os.path.exists(path) and not args.force:
            skipped += 1
            continue
        Image.fromarray(render_rule(rule, args.seed + idx)).save(path, optimize=True)
        written += 1
        print(f"  wrote {os.path.basename(path)}")

    print(f"\n{written} written, {skipped} already present -> {out_dir}")
    if skipped and not args.force:
        print("Pass --force to regenerate the existing images.")


if __name__ == "__main__":
    main()
