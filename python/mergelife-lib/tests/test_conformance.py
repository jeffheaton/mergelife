"""Cross-engine conformance: replay conformance/vectors.txt.

The golden vectors are shared with the C, Java, and JS engines (each replays
the same file from its own test suite), so passing here proves the packaged
engine is behaviorally identical to every other implementation in the repo.

Skipped automatically when the library is used outside the mergelife repo,
where the vectors file is not present.
"""

from pathlib import Path

import numpy as np
import pytest

import mergelife

VECTORS = Path(__file__).resolve().parents[3] / "conformance" / "vectors.txt"


def load_vectors():
    if not VECTORS.is_file():
        return []
    out = []
    for line in VECTORS.read_text().splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        rule, rows, cols, seed, steps, digest = line.split()
        out.append((rule, int(rows), int(cols), int(seed), int(steps), digest))
    return out


def lcg_lattice(seed, rows, cols):
    """Spec PRNG: 32-bit LCG, one byte (state >> 24) per advance, row-major RGB."""
    state = seed & 0xFFFFFFFF
    flat = np.empty(rows * cols * 3, dtype=np.uint8)
    for i in range(flat.size):
        state = (state * 1664525 + 1013904223) & 0xFFFFFFFF
        flat[i] = state >> 24
    return flat.reshape(rows, cols, 3)


def fnv1a64(data):
    """Spec hash: FNV-1a 64-bit, 16 lowercase hex digits."""
    h = 0xCBF29CE484222325
    for b in bytes(data):
        h = ((h ^ b) * 0x100000001B3) & 0xFFFFFFFFFFFFFFFF
    return "%016x" % h


def replay(rule, rows, cols, seed, steps):
    inst = mergelife.new_ml_instance(rows, cols, rule)
    inst["lattice"][0]["data"] = lcg_lattice(seed, rows, cols)
    inst["lattice"][1]["data"] = np.zeros((rows, cols, 3), dtype=np.uint8)
    for _ in range(steps):
        mergelife.update_step(inst)
    return fnv1a64(inst["lattice"][0]["data"].tobytes())


@pytest.mark.skipif(not VECTORS.is_file(), reason="conformance vectors not present")
@pytest.mark.parametrize(
    "rule,rows,cols,seed,steps,digest",
    load_vectors(),
    ids=lambda v: v if isinstance(v, str) and "-" in str(v) else None,
)
def test_packaged_engine_matches_vectors(rule, rows, cols, seed, steps, digest):
    assert replay(rule, rows, cols, seed, steps) == digest
