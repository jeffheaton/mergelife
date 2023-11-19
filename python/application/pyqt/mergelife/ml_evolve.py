import logging
import operator
import os
import time

import numpy as np

import mergelife.mergelife as mergelife
import mergelife.ml_evolve as ev

logger = logging.getLogger(__name__)


class Evolve:
    def __init__(
        self,
        report_target,
        path,
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
        self._output_path = path
        self._input_queue = []
        self._output_queue = []
        self._report_target = report_target
        self.rules_found = 0
        self.score_threshold = 0
        self.patience = 250
        self._perf_count = 0

    def mutate(self, genome):
        h = "0123456789abcdef"
        done = False
        while not done:
            i = np.random.randint(0, len(genome))
            if genome[i] != "-":
                i2 = np.random.randint(0, len(h))
                result = genome[:i] + h[i2] + genome[(i + 1) :]
                if result != genome:
                    done = True
        return result

    def crossover(self, genome1, genome2, cut_length):
        # The genome must be cut at two positions, determine them
        cutpoint1 = np.random.randint(len(genome1) - cut_length)
        cutpoint2 = cutpoint1 + cut_length

        # Produce two offspring
        c1 = genome1[0:cutpoint1] + genome2[cutpoint1:cutpoint2] + genome1[cutpoint2:]
        c2 = genome1[0:cutpoint1] + genome2[cutpoint1:cutpoint2] + genome1[cutpoint2:]

        return [c1, c2]

    def select_tournament(self, rounds, cmp=operator.gt):
        result = np.random.randint(0, len(self.population))

        for i in range(rounds):
            challenger = np.random.randint(0, len(self.population))
            if cmp(
                self.population[challenger]["score"], self.population[result]["score"]
            ):
                result = challenger
                break

        return result

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
        cycles = config["config"]["evalCycles"]

        # first make space for it
        while (len(self.population) + 1) > config["config"]["populationSize"]:
            target_idx = self.select_tournament(cycles, operator.lt)
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
                logging.info(f"New top genome: {genome}, above threshold, saving")
                if not self.render(config, self.bestGenome["rule"]):
                    self._display_status("Failed to write CA")
            else:
                logging.info(f"New top genome: {genome}, not above threshold")
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
        cycles = config["config"]["evalCycles"]
        self.patience = config["config"]["patience"]
        self.requestStop = False
        self.timeLastUpdate = time.time()
        self.score_threshold = config["config"]["scoreThreshold"]
        self.randomPopulation(config, self._input_queue)
        self._display_status("Running...")

        while not self.requestStop:
            if np.random.uniform() < config["config"]["crossover"]:
                # Crossover
                parent1_idx = self.select_tournament(cycles, operator.gt)
                parent2_idx = parent1_idx
                while parent1_idx == parent2_idx:
                    parent2_idx = self.select_tournament(cycles, operator.gt)

                parent1 = self.population[parent1_idx]["rule"]
                parent2 = self.population[parent2_idx]["rule"]

                if parent1 != parent2:
                    child1, child2 = self.crossover(parent1, parent2, cycles)
                    self.add_genome(config, child1)
                    self.add_genome(config, child2)
            else:
                # Mutate
                parent_idx = self.select_tournament(cycles, operator.gt)
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
        try:
            steps = config["config"]["renderSteps"]

            ml_inst = mergelife.new_ml_instance(100, 100, ruleText)

            for i in range(steps):
                mergelife.update_step(ml_inst)

            filename = os.path.join(self._output_path, ruleText + ".png")
            mergelife.save_image(ml_inst, filename)
            self._display_status(f"Saved {filename}")
        except Exception as e:
            logger.error("Error rendering CA", e)
            return False

        return True
