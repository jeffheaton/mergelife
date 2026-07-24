"""Trainer tests: GA primitives and the Evolve orchestrator."""

import operator

import numpy as np

import mergelife
from mergelife import ml_evolve

PAPER_RULE = "e542-5f79-9341-f31e-6c6b-7f08-8773-7068"


def tiny_config(**overrides):
    config = {
        "config": {
            "rows": 20,
            "cols": 20,
            "populationSize": 4,
            "crossover": 0.75,
            "evalCycles": 1,
            "patience": 2,
            "scoreThreshold": 1e9,
            "renderSteps": 5,
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
    config["config"].update(overrides)
    return config


def test_hms_string():
    assert ml_evolve.hms_string(0) == "0:00:00.00"
    assert ml_evolve.hms_string(3661.5) == "1:01:01.50"


def test_mutate_swaps_exactly_two_hex_digits():
    # Paper Sec. 5.3: mutation exchanges two random digits of the parent.
    np.random.seed(11)
    for _ in range(25):
        child = ml_evolve.mutate(PAPER_RULE)
        assert len(child) == len(PAPER_RULE)
        assert sorted(child) == sorted(PAPER_RULE)
        diffs = [i for i, (a, b) in enumerate(zip(PAPER_RULE, child)) if a != b]
        assert len(diffs) == 2
        i, j = diffs
        assert PAPER_RULE[i] == child[j] and PAPER_RULE[j] == child[i]
        assert "-" not in (PAPER_RULE[i], PAPER_RULE[j])


def test_mutate_degenerate_genome_unchanged():
    flat = "0000-0000-0000-0000-0000-0000-0000-0000"
    assert ml_evolve.mutate(flat) == flat


def test_crossover_children_are_complementary_valid_rules():
    # Paper Sec. 5.2: each child keeps its own parent's outer splices and takes
    # the middle splice from the other parent, so at every position the two
    # children jointly carry both parents' characters.
    np.random.seed(13)
    p2 = ml_evolve.mutate(ml_evolve.mutate(PAPER_RULE))
    for _ in range(25):
        c1, c2 = ml_evolve.crossover(PAPER_RULE, p2)
        for child in (c1, c2):
            assert len(child) == len(PAPER_RULE)
            assert len(mergelife.parse_update_rule(child)) == 8
        for k in range(len(PAPER_RULE)):
            assert sorted((c1[k], c2[k])) == sorted((PAPER_RULE[k], p2[k]))


def test_select_tournament_single_candidate():
    population = [{"score": 1.0}]
    assert ml_evolve.select_tournament(population, 5) == 0


def test_select_tournament_prefers_direction():
    np.random.seed(17)
    population = [{"score": float(i)} for i in range(10)]
    high = [ml_evolve.select_tournament(population, 20, operator.gt)
            for _ in range(200)]
    low = [ml_evolve.select_tournament(population, 20, operator.lt)
           for _ in range(200)]
    assert np.mean([population[i]["score"] for i in high]) > \
        np.mean([population[i]["score"] for i in low])


def test_evolve_random_population():
    class Reporter:
        def __init__(self):
            self.calls = 0

        def report(self, evolver):
            self.calls += 1

    np.random.seed(23)
    reporter = Reporter()
    ev = ml_evolve.Evolve(report_target=reporter)
    config = tiny_config()
    ev.randomPopulation(config, None)

    assert len(ev.population) == 4
    assert ev.evalCount == 4
    assert reporter.calls == 4
    assert ev.bestGenome is not None
    assert ev.bestGenome["score"] == max(g["score"] for g in ev.population)


def test_evolve_population_stays_capped():
    np.random.seed(29)
    ev = ml_evolve.Evolve()
    config = tiny_config()
    ev.randomPopulation(config, None)
    ev.add_genome(config, mergelife.random_update_rule())
    assert len(ev.population) == config["config"]["populationSize"]


def test_evolve_full_run_stops_on_patience():
    np.random.seed(31)
    ev = ml_evolve.Evolve()
    ev.evolve(tiny_config())
    assert ev.requestStop
    assert ev.noImprovement > tiny_config()["config"]["patience"]
    assert ev.runCount == 2
    assert len(ev.population) == 4
