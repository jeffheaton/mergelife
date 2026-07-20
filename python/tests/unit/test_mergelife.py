"""Unit tests for the core ``mergelife`` module.

These exercise the pure, deterministic helpers in ``mergelife.py`` so the suite
covers real application code rather than only the test harness itself.
"""

import numpy as np

import mergelife


def test_fromhex_parses_eight_channels(sample_rule_code):
    """fromHex expands a 32-char hex rule into 8 (range, signed-pct) pairs."""
    code = mergelife.fromHex(sample_rule_code)

    assert len(code) == len(mergelife.COLOR_TABLE) == 8
    # First channel: "51" -> 0x51 = 81, "a2" -> 0xa2 = 162 -> signed byte -94.
    assert code[0] == (81, -94)
    # Second channel: "4a" -> 74, "5a" -> 90 (already positive).
    assert code[1] == (74, 90)
    for rng, pct in code:
        assert 0 <= rng <= 255
        assert -128 <= pct <= 127


def test_tohex_fromhex_roundtrip(sample_rule_code):
    """Encoding a decoded rule and decoding it again is the identity."""
    code = mergelife.fromHex(sample_rule_code)
    assert mergelife.fromHex(mergelife.toHex(code)) == code


def test_parse_update_rule_is_sorted_permutation(sample_rule_code):
    """parse_update_rule yields one sorted entry per colour, keeping indices."""
    rule = mergelife.parse_update_rule(sample_rule_code)

    assert len(rule) == 8
    assert rule == sorted(rule)  # returned sorted by (limit, pct, index)
    assert sorted(entry[2] for entry in rule) == list(range(8))


def test_random_update_rule_has_valid_shape():
    """A generated rule round-trips through fromHex into 8 channels."""
    code = mergelife.random_update_rule()

    assert isinstance(code, str)
    assert len(code.replace("-", "")) == 32
    assert len(mergelife.fromHex(code)) == 8


def test_new_ml_instance_builds_lattice(sample_rule_code):
    """new_ml_instance wires up dimensions, rule and a duplicated RGB lattice."""
    inst = mergelife.new_ml_instance(8, 12, sample_rule_code)

    assert inst["height"] == 8
    assert inst["width"] == 12
    assert inst["time_step"] == 0
    assert len(inst["sorted_rule"]) == 8

    front = inst["lattice"][0]["data"]
    back = inst["lattice"][1]["data"]
    assert front.shape == (8, 12, 3)
    assert front.dtype == np.uint8
    # Both lattice buffers start identical.
    assert np.array_equal(front, back)
