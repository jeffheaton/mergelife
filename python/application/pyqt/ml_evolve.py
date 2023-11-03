import numpy as np
import operator
import argparse
import json
import multiprocessing as mp
import operator
import time
import os
import sys

import mergelife
import ml_evolve as ev
import numpy as np
import logging

logger = logging.getLogger(__name__)

STOP_STAGE_RUNNING = 0
STOP_STAGE_STOPPING = 1
STOP_STAGE_STOPPED = 2



_manager = None 

class Evolve:
    def __init__(self):
        self.bestGenome = None
        self.evalCount = 0
        self.startTime = 0
        self.timeLastUpdate = 0
        self.totalEvalCount = 0
        self.runCount = 1
        self.noImprovement = 0
        self.waitingCount = 0
        self.population = []

    def hms_string(self, sec_elapsed):
        h = int(sec_elapsed / (60 * 60))
        m = int((sec_elapsed % (60 * 60)) / 60)
        s = sec_elapsed % 60
        return "{}:{:>02}:{:>05.2f}".format(h, m, s)

    def mutate(self, genome):
        h = "0123456789abcdef"
        done = False
        while not done:
            i = np.random.randint(0,len(genome))
            if genome[i] != '-':
                i2 = np.random.randint(0,len(h))
                result = genome[:i] + h[i2] + genome[(i+1):]
                if result != genome:
                    done = True
        return result

    def crossover(self, genome1,genome2,cut_length):
        # The genome must be cut at two positions, determine them
        cutpoint1 = np.random.randint(len(genome1) - cut_length)
        cutpoint2 = cutpoint1 + cut_length

        # Produce two offspring
        c1 = genome1[0:cutpoint1] + genome2[cutpoint1:cutpoint2] + genome1[cutpoint2:]
        c2 = genome1[0:cutpoint1] + genome2[cutpoint1:cutpoint2] + genome1[cutpoint2:]

        return [c1,c2]

    def select_tournament(self, rounds,cmp=operator.gt):
        result = np.random.randint(0,len(self.population))

        for i in range(rounds):
            challenger = np.random.randint(0,len(self.population))
            if cmp(self.population[challenger]['score'], self.population[result]['score']):
                result = challenger
                break

        return result


    def subprocessScore(self, inputQueue, outputQueue, config, stop_state):
        while stop_state.value==STOP_STAGE_RUNNING:
            genome = inputQueue.get()
            rule_str = genome['rule']

            width = config['config']['cols']
            height = config['config']['rows']

            ml_inst = mergelife.new_ml_instance(height, width, rule_str)
            result = mergelife.objective_function(ml_inst, config['config']['evalCycles'], config['objective'])
            outputQueue.put({'rule': rule_str, 'score': result['score'], 'run': genome['run']})
        outputQueue.put("STOP")
        logger.info("Evolve worker shut down")


    def report(self, config, inputQueue, genome):
        requestStop = False
        self.evalCount += 1
        self.totalEvalCount += 1

        if self.bestGenome is None or genome['score'] > self.bestGenome['score']:
            self.bestGenome = genome
        else:
            self.noImprovement += 1

            if self.noImprovement > config['config']['patience']:
                requestStop = True

        now = time.time()

        if requestStop or (now - self.timeLastUpdate > 60):
            elapsed = now - self.startTime
            perSec = self.totalEvalCount / elapsed
            perMin = int(perSec * 60.0)
            logger.info("Run  #{}, Eval #{}: {}, evals/min={}".format(self.runCount, self.evalCount, self.bestGenome, perMin))
            self.timeLastUpdate = now

        if requestStop:
            logger.info("No improvement for {}, stopping...".format(config['config']['patience']))

            if self.bestGenome['score'] > config['config']['scoreThreshold']:
                self.render(config, self.bestGenome['rule'])
            self.noImprovement = 0
            self.runCount += 1
            self.evalCount = 0
            self.population = []
            self.bestGenome = None
            self.randomPopulation(config, inputQueue)


    def randomPopulation(self, config, queue):
        for i in range(config['config']['populationSize']):
            queue.put({'score': None, 'rule': mergelife.random_update_rule(), 'run': self.runCount})
        self.waitingCount += config['config']['populationSize']

    def evolve(self, config, stop_mode):
        cpus = mp.cpu_count()

        logger.info("Forking for {}".format(cpus))
        processes = []
        self.startTime = time.time()
        self.timeLastUpdate = self.startTime

        cycles = config['config']['evalCycles']

        inputQueue = mp.Queue()
        outputQueue = mp.Queue()

        for i in range(cpus):
            p = mp.Process(target=self.subprocessScore, args=(inputQueue, outputQueue,config,stop_mode))
            p.start()
            processes.append({'process': p})

        self.randomPopulation(config, inputQueue)
        self.population = []

        while stop_mode.value==STOP_STAGE_RUNNING:
            g = outputQueue.get()
            if g=="STOP": break
            self.waitingCount -= 1

            if g['run'] == self.runCount:
                if len(self.population) < config['config']['populationSize']:
                    self.population.append(g)
                    self.report(config, inputQueue, g)
                else:
                    target_idx = self.select_tournament(cycles, operator.lt)
                    self.population[target_idx] = g
                    self.report(config, inputQueue, g)

            if self.waitingCount < cpus * 2:
                if np.random.uniform() < config['config']['crossover']:
                    # Crossover
                    parent1_idx = self.select_tournament(cycles, operator.gt)
                    parent2_idx = parent1_idx
                    while parent1_idx == parent2_idx:
                        parent2_idx = self.select_tournament(cycles, operator.gt)

                    parent1 = self.population[parent1_idx]['rule']
                    parent2 = self.population[parent2_idx]['rule']

                    if parent1 != parent2:
                        child1, child2 = self.crossover(parent1, parent2, cycles)
                        inputQueue.put({'rule': child1, 'score': None, 'run': self.runCount})
                        inputQueue.put({'rule': child2, 'score': None, 'run': self.runCount})
                        self.waitingCount += 2

                else:
                    # Mutate
                    parent_idx = self.select_tournament(cycles, operator.gt)
                    parent = self.population[parent_idx]['rule']
                    child = self.mutate(parent)
                    inputQueue.put({'rule': child, 'score': None, 'run': self.runCount})
                    self.waitingCount += 1
        logger.info("Stopped evolve main loop, waiting for subprocesses to stop")
        for p in processes:
            p['process'].join()
        stop_mode.value=STOP_STAGE_STOPPED
        logger.info("Evolve shutting down")

    def render(self, config, ruleText):
        width = config['config']['cols']
        height = config['config']['rows']
        steps = config['config']['renderSteps']

        ml_inst = mergelife.new_ml_instance(height, width, ruleText)

        for i in range(steps):
            mergelife.update_step(ml_inst)

        filename = os.path.join("/Users/jeff/Documents/HeatonCA/", ruleText + ".png")
        mergelife.save_image(ml_inst, filename)
        print("Saved {}".format(filename))

    def start(self, config):
        global _manager
        _manager = mp.Manager()
        self.stop_mode = _manager.Value('i', 0)
        self.main_process = mp.Process(target=self.evolve, args=(config,self.stop_mode))
        self.main_process.start()

    def stop(self):
        logging.info("Requesting evolve process to stop")
        self.stop_mode.value = STOP_STAGE_STOPPING
