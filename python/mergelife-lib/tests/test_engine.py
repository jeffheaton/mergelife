"""Engine tests: hex codec, rule parsing, instances, stepping, images."""

import re

import numpy as np
from PIL import Image

import mergelife

PAPER_RULE = "e542-5f79-9341-f31e-6c6b-7f08-8773-7068"
RULE_RE = re.compile(r"^[0-9a-f]{4}(-[0-9a-f]{4}){7}$")

PAPER_OBJECTIVE = [
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
]


def test_hex_roundtrip_string():
    assert mergelife.toHex(mergelife.fromHex(PAPER_RULE)) == PAPER_RULE


def test_hex_roundtrip_code():
    code = [(0x12, -5), (0xFF, 127), (0x00, -128), (0x80, 0),
            (0x40, 64), (0xC0, -64), (0x01, 1), (0xFE, -1)]
    assert mergelife.fromHex(mergelife.toHex(code)) == code


def test_parse_update_rule_sorted_and_complete():
    parsed = mergelife.parse_update_rule(PAPER_RULE)
    assert len(parsed) == 8
    alphas = [entry[0] for entry in parsed]
    assert alphas == sorted(alphas)
    assert sorted(entry[2] for entry in parsed) == list(range(8))
    for _, pct, _ in parsed:
        assert -1.0 <= pct <= 1.0


def test_parse_update_rule_promotes_2040_to_2048():
    rule = "ff7f-0000-0000-0000-0000-0000-0000-0000"
    parsed = mergelife.parse_update_rule(rule)
    assert max(entry[0] for entry in parsed) == 2048


def test_new_ml_instance_shape():
    ml = mergelife.new_ml_instance(30, 40, PAPER_RULE)
    assert ml["height"] == 30
    assert ml["width"] == 40
    assert ml["time_step"] == 0
    assert ml["rule_str"] == PAPER_RULE
    for lat in ml["lattice"]:
        assert lat["data"].shape == (30, 40, 3)
        assert lat["data"].dtype == np.uint8


def test_update_step_is_deterministic():
    def run():
        np.random.seed(42)
        ml = mergelife.new_ml_instance(50, 50, PAPER_RULE)
        for _ in range(25):
            mergelife.update_step(ml)
        return ml

    a, b = run(), run()
    assert a["time_step"] == 25
    assert np.array_equal(a["lattice"][0]["data"], b["lattice"][0]["data"])


def test_random_update_rule_format():
    np.random.seed(7)
    for _ in range(20):
        rule = mergelife.random_update_rule()
        assert RULE_RE.match(rule), rule
        assert len(mergelife.parse_update_rule(rule)) == 8


def test_count_discrete_uniform_lattice():
    ml = mergelife.new_ml_instance(10, 10, PAPER_RULE)
    ml["lattice"][0]["data"] = np.zeros((10, 10, 3), dtype=np.uint8)
    assert mergelife.count_discrete(ml) == 1


def test_objective_function_smoke():
    np.random.seed(1)
    ml = mergelife.new_ml_instance(20, 20, PAPER_RULE)
    result = mergelife.objective_function(ml, 1, PAPER_OBJECTIVE)
    assert set(result) == {"time_step", "score"}
    assert result["time_step"] > 0
    assert np.isfinite(result["score"])


def test_static_rule_converges_when_change_window_closes():
    # Paper Sec. 4.1, first convergence condition: stop once less than 1% of
    # the merged cells have changed value during the last 100 CA generations.
    # An all-zero rule never matches any cell, so the grid is static from the
    # first generation and must stop the moment the 100-generation window can
    # be judged.
    np.random.seed(5)
    ml = mergelife.new_ml_instance(20, 20, "0000-0000-0000-0000-0000-0000-0000-0000")
    result = mergelife.calc_objective_function(ml, PAPER_OBJECTIVE)
    assert result["time_step"] == 101


def test_save_image_roundtrip(tmp_path):
    np.random.seed(3)
    ml = mergelife.new_ml_instance(12, 17, PAPER_RULE)
    out = tmp_path / "out.png"
    mergelife.save_image(ml, str(out))
    img = np.asarray(Image.open(out))
    assert img.shape == (12, 17, 3)
    assert np.array_equal(img, ml["lattice"][0]["data"])
