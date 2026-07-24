"""Evolutionary trainer for MergeLife update rules.

Module-level functions (:func:`mutate`, :func:`crossover`,
:func:`select_tournament`) are the genetic-algorithm primitives, usable on
their own as they were from the historical top-level ``ml_evolve.py``. The
:class:`Evolve` class is the self-contained trainer the PyQt application uses;
its methods of the same names delegate to the module functions.
"""

import logging
import operator
import os
import time

import numpy as np

from . import mergelife

logger = logging.getLogger(__name__)

# Paper Sec. 5.2: the two crossover cut points are always this many characters
# apart on the dashed rule string (one 4-digit sub-rule plus a dash).
CUT_LENGTH = 5


def hms_string(sec_elapsed):
    h = int(sec_elapsed / (60 * 60))
    m = int((sec_elapsed % (60 * 60)) / 60)
    s = sec_elapsed % 60
    return "{}:{:>02}:{:>05.2f}".format(h, m, s)


def mutate(genome):
    # Paper Sec. 5.3: mutation chooses two random hex digits (dashes are not
    # considered) and exchanges them, so the child is a shuffle of the parent.
    if len(set(genome) - {"-"}) < 2:
        return genome  # every digit identical: no swap can produce a new child
    done = False
    while not done:
        i = np.random.randint(0, len(genome))
        j = np.random.randint(0, len(genome))
        if genome[i] != "-" and genome[j] != "-" and genome[i] != genome[j]:
            lo, hi = (i, j) if i < j else (j, i)
            result = (genome[:lo] + genome[hi] + genome[lo + 1:hi]
                      + genome[lo] + genome[hi + 1:])
            done = True
    return result


def crossover(genome1, genome2, cut_length=CUT_LENGTH):
    # The genome must be cut at two positions, determine them
    cutpoint1 = np.random.randint(len(genome1) - cut_length)
    cutpoint2 = cutpoint1 + cut_length

    # Paper Sec. 5.2: the children are complementary -- each keeps its own
    # parent's outer splices and takes the middle splice from the other parent.
    c1 = genome1[0:cutpoint1] + genome2[cutpoint1:cutpoint2] + genome1[cutpoint2:]
    c2 = genome2[0:cutpoint1] + genome1[cutpoint1:cutpoint2] + genome2[cutpoint2:]

    return [c1, c2]


def select_tournament(population, rounds, cmp=operator.gt):
    # Best-of-`rounds` tournament: every challenger is compared, none is an
    # automatic winner (paper Sec. 5.4, "tournament selection with 5 rounds").
    result = None

    for i in range(rounds):
        challenger = np.random.randint(0, len(population))
        if result is None or cmp(
            population[challenger]["score"], population[result]["score"]
        ):
            result = challenger

    return result


class Evolve:
    def __init__(
        self,
        report_target=None,
        path=None,
    ):
        self.bestGenome = None
        self.evalCount = 0
        self.startTime = time.time()
        self.timeLastUpdate = 0
        self.totalEvalCount = 0
        self.runCount = 1
        self.noImprovement = 0
        self.population = []
        self.requestStop = False
        self.perMin = 0
        self.status = ""
        self._output_path = path
        self._input_queue = []
        self._output_queue = []
        self._report_target = report_target
        self.rules_found = 0
        self.score_threshold = 0
        self.patience = 250
        self._perf_count = 0

    def mutate(self, genome):
        return mutate(genome)

    def crossover(self, genome1, genome2, cut_length=CUT_LENGTH):
        return crossover(genome1, genome2, cut_length)

    def select_tournament(self, rounds, cmp=operator.gt):
        return select_tournament(self.population, rounds, cmp)

    def score(self, config, rule):
        genome = {"rule": rule}
        width = config["config"]["cols"]
        height = config["config"]["rows"]
        ml_inst = mergelife.new_ml_instance(height, width, rule)
        result = mergelife.objective_function(
            ml_inst, config["config"]["evalCycles"], config["objective"]
        )
        genome["score"] = result["score"]
        genome["run"] = self.runCount
        self.evalCount += 1
        self.totalEvalCount += 1
        self._perf_count += 1
        if self._report_target is not None:
            self._report_target.report(self)
        # Performance
        now = time.time()
        if now - self.timeLastUpdate > 60:
            elapsed = now - self.timeLastUpdate
            perSec = self._perf_count / elapsed
            self._perf_count = 0
            self.perMin = int(perSec * 60.0)
            logger.info(
                "Run  #{}, Eval #{}: {}, evals/min={}".format(
                    self.runCount, self.evalCount, self.bestGenome, self.perMin
                )
            )
            self.timeLastUpdate = now

        return genome

    def add_genome(self, config, rule):
        rounds = config["config"].get("tournamentCycles", 5)

        # first make space for it
        while (len(self.population) + 1) > config["config"]["populationSize"]:
            target_idx = self.select_tournament(rounds, operator.lt)
            del self.population[target_idx]

        # now score the new rule
        genome = self.score(config, rule)
        self.population.append(genome)

        # new best genome?
        if self.bestGenome is None or genome["score"] > self.bestGenome["score"]:
            self.bestGenome = genome
            self.noImprovement = 0

            if self.bestGenome["score"] > self.score_threshold:
                self.rules_found += 1
                logger.info(f"New top genome: {genome}, above threshold, saving")
                if not self.render(config, self.bestGenome["rule"]):
                    self._display_status("Failed to write CA")
            else:
                logger.info(f"New top genome: {genome}, not above threshold")
        else:
            self.noImprovement += 1

            if self.noImprovement > self.patience:
                self.requestStop = True

    def randomPopulation(self, config, queue):
        self.population = []
        self.bestGenome = None
        sz = config["config"]["populationSize"]
        i = 0
        while (i < sz) and (not self.requestStop):
            self._display_status(f"Generating new population: {i+1}/{sz}")
            rule = mergelife.random_update_rule()
            self.add_genome(config, rule)
            i += 1

    def evolve(self, config):
        rounds = config["config"].get("tournamentCycles", 5)
        self.patience = config["config"]["patience"]
        self.requestStop = False
        self.timeLastUpdate = time.time()
        self.score_threshold = config["config"]["scoreThreshold"]
        self.randomPopulation(config, self._input_queue)
        self._display_status("Running...")

        while not self.requestStop:
            if np.random.uniform() < config["config"]["crossover"]:
                # Crossover
                parent1_idx = self.select_tournament(rounds, operator.gt)
                parent2_idx = parent1_idx
                while parent1_idx == parent2_idx:
                    parent2_idx = self.select_tournament(rounds, operator.gt)

                parent1 = self.population[parent1_idx]["rule"]
                parent2 = self.population[parent2_idx]["rule"]

                if parent1 != parent2:
                    child1, child2 = self.crossover(parent1, parent2)
                    self.add_genome(config, child1)
                    self.add_genome(config, child2)
            else:
                # Mutate
                parent_idx = self.select_tournament(rounds, operator.gt)
                parent = self.population[parent_idx]["rule"]
                child = self.mutate(parent)
                self.add_genome(config, child)
        self.runCount += 1

        self._display_status(
            f"No improvement for {config['config']['patience']}, stopping run."
        )

    def _display_status(self, str):
        self.status = str
        logger.info(str)

    def render(self, config, ruleText):
        if self._output_path is None:
            return True

        try:
            steps = config["config"]["renderSteps"]

            ml_inst = mergelife.new_ml_instance(100, 100, ruleText)

            for i in range(steps):
                mergelife.update_step(ml_inst)

            filename = os.path.join(self._output_path, ruleText + ".png")
            mergelife.save_image(ml_inst, filename)
            self._display_status(f"Saved {filename}")
        except Exception:
            logger.exception("Error rendering CA")
            return False

        return True
