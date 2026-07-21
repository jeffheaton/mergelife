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


def test_parse_update_rule_sorted_by_alpha_stable(sample_rule_code):
    """Sub-rules are ordered by alpha only; equal ranges keep hex order (paper)."""
    rule = mergelife.parse_update_rule(sample_rule_code)

    assert len(rule) == 8
    alphas = [entry[0] for entry in rule]
    assert alphas == sorted(alphas)                        # ordered by alpha
    assert sorted(entry[2] for entry in rule) == list(range(8))
    # Within each tied-alpha group the original hex index order is preserved.
    for a in set(alphas):
        idxs = [entry[2] for entry in rule if entry[0] == a]
        assert idxs == sorted(idxs)


def test_parse_update_rule_ties_break_by_index_not_percent():
    """Equal alphas with descending percents keep index order, not percent order."""
    # All octet-1 bytes equal -> every alpha == 128; octet-2 descending -> pct desc.
    rule = mergelife.parse_update_rule("1008-1007-1006-1005-1004-1003-1002-1001")
    assert [entry[0] for entry in rule] == [128] * 8
    assert [entry[2] for entry in rule] == list(range(8))  # not reordered by percent


def test_update_step_unmatched_cell_keeps_value():
    """Paper: a cell matched by no sub-rule keeps its current value (issue #4)."""
    # Every alpha is 0, so no neighbor count ever matches -> every cell is noop.
    inst = mergelife.new_ml_instance(6, 6, "0000-0000-0000-0000-0000-0000-0000-0000")
    current = np.copy(inst["lattice"][0]["data"])
    # Make the back buffer different so a 2-generation revert would be visible.
    inst["lattice"][1]["data"][:] = 0
    mergelife.update_step(inst)
    # Unmatched cells keep their current value rather than reverting to the buffer.
    assert np.array_equal(inst["lattice"][0]["data"], current)


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
