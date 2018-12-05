import argparse
import json
import multiprocessing as mp
import operator
import time
import sys

import mergelife
import ml_evolve as ev
import numpy as np

bestGenome = None
evalCount = 0
startTime = 0
timeLastUpdate = 0
totalEvalCount = 0
runCount = 1
noImprovement = 0
waitingCount = 0
population = []


def subprocessScore(inputQueue, outputQueue, config):
    while True:
        genome = inputQueue.get()
        rule_str = genome['rule']

        width = config['config']['cols']
        height = config['config']['rows']

        ml_inst = mergelife.new_ml_instance(height, width, rule_str)
        result = mergelife.objective_function(ml_inst, config['config']['evalCycles'], config['objective'])
        outputQueue.put({'rule': rule_str, 'score': result['score'], 'run': genome['run']})


def report(config, inputQueue, genome):
    global bestGenome, evalCount, totalEvalCount, timeLastUpdate, startTime, runCount, noImprovement, population

    requestStop = False
    evalCount += 1
    totalEvalCount += 1

    if bestGenome is None or genome['score'] > bestGenome['score']:
        bestGenome = genome
    else:
        noImprovement += 1

        if noImprovement > config['config']['patience']:
            requestStop = True

    now = time.time()

    if requestStop or (now - timeLastUpdate > 60):
        elapsed = now - startTime
        perSec = totalEvalCount / elapsed
        perMin = int(perSec * 60.0)

        print("Run  #{}, Eval #{}: {}, evals/min={}".format(runCount, evalCount, bestGenome, perMin))
        timeLastUpdate = now

    if requestStop:
        print("No improvement for {}, stopping...".format(config['config']['patience']))

        if bestGenome['score'] > config['config']['scoreThreshold']:
            render(config, bestGenome['rule'])
        noImprovement = 0
        runCount += 1
        evalCount = 0
        population = []
        bestGenome = None
        randomPopulation(config, inputQueue)


def randomPopulation(config, queue):
    global waitingCount
    for i in range(config['config']['populationSize']):
        queue.put({'score': None, 'rule': mergelife.random_update_rule(), 'run': runCount})
    waitingCount += config['config']['populationSize']


def evolve(config):
    global timeLastUpdate, waitingCount, startTime

    cpus = mp.cpu_count()

    print("Forking for {}".format(cpus))
    processes = []
    startTime = time.time()
    timeLastUpdate = startTime

    cycles = config['config']['evalCycles']

    inputQueue = mp.Queue()
    outputQueue = mp.Queue()

    for i in range(cpus):
        # parent_conn, child_conn = mp.Pipe()
        # p = mp.Process(target=subprocessScore, args=(parent_conn,))
        p = mp.Process(target=subprocessScore, args=(inputQueue, outputQueue,config))
        p.start()
        processes.append({'process': p})

    randomPopulation(config, inputQueue)
    population = []

    while True:
        g = outputQueue.get()
        waitingCount -= 1

        if g['run'] == runCount:
            if len(population) < config['config']['populationSize']:
                population.append(g)
                report(config, inputQueue, g)
            else:
                target_idx = ev.select_tournament(population, cycles, operator.lt)
                population[target_idx] = g
                report(config, inputQueue, g)

        if waitingCount < cpus * 2:
            if np.random.uniform() < config['config']['crossover']:
                # Crossover
                parent1_idx = ev.select_tournament(population, cycles, operator.gt)
                parent2_idx = parent1_idx
                while parent1_idx == parent2_idx:
                    parent2_idx = ev.select_tournament(population, cycles, operator.gt)

                parent1 = population[parent1_idx]['rule']
                parent2 = population[parent2_idx]['rule']

                if parent1 != parent2:
                    child1, child2 = ev.crossover(parent1, parent2, cycles)
                    inputQueue.put({'rule': child1, 'score': None, 'run': runCount})
                    inputQueue.put({'rule': child2, 'score': None, 'run': runCount})
                    waitingCount += 2

            else:
                # Mutate
                parent_idx = ev.select_tournament(population, cycles, operator.gt)
                parent = population[parent_idx]['rule']
                child = ev.mutate(parent)
                inputQueue.put({'rule': child, 'score': None, 'run': runCount})
                waitingCount += 1

    for p in processes:
        p['process'].join()


def render(config, ruleText):
    width = config['config']['cols']
    height = config['config']['rows']
    steps = config['config']['renderSteps']

    ml_inst = mergelife.new_ml_instance(height, width, ruleText)

    for i in range(steps):
        mergelife.update_step(ml_inst)

    filename = ruleText + ".png"
    mergelife.save_image(ml_inst, filename)
    print("Saved {}".format(filename))


def score(config, ruleText):
    width = config['config']['cols']
    height = config['config']['rows']

    ml_inst = mergelife.new_ml_instance(height, width, ruleText)
    result = mergelife.objective_function(ml_inst, config['config']['evalCycles'], config['objective'], True)
    print("Final result: {}".format(result['score']))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Mergelife Utility')
    parser.add_argument('--rows', nargs=1, type=int, help="the number of rows in the MergeLife grid")
    parser.add_argument('--cols', nargs=1, type=int, help="the number of cols in the MergeLife grid")
    parser.add_argument('--renderSteps', nargs=1, type=int, help="the number of steps to render")
    parser.add_argument('--zoom', nargs=1, type=int, help="the pixel size for rendering")
    parser.add_argument('--config', nargs=1, type=str, help="the path to a config file")
    parser.add_argument('command', nargs=argparse.REMAINDER, metavar='command', type=str, choices=['evolve', 'score', 'render'],
                        help='an integer for the accumulator')

    args = parser.parse_args()

    if args.config is None:
        config = {'config': {}}
    else:
        with open(args.config[0]) as f:
            config = json.load(f)

    # Override with command line params, if they are there
    if args.rows is not None:
        config['config']['rows'] = args.rows[0]

    if args.cols is not None:
        config['config']['cols'] = args.cols[0]

    if args.renderSteps is not None:
        config['config']['renderSteps'] = args.renderSteps[0]

    if args.config is not None:
        config['config']['config'] = args.config[0]

    if args.zoom is not None:
        config['config']['zoom'] = args.zoom[0]

    # Default values
    if 'cols' not in config['config']:
        config['config']['cols'] = 100

    if 'rows' not in config['config']:
        config['config']['rows'] = 100

    if 'evalCycles' not in config['config']:
        config['config']['renderSteps'] = 250

    if 'zoom' not in config['config']:
        config['config']['cols'] = 5



    if args.command[0] == 'render':
        if len(args.command)<2:
            print("Must specify what rule hex-code you wish to render.")
            sys.exit(0)
        else:
            render(config, args.command[1])
    elif args.command[0] == 'score':
        score(config, args.command[1])
    elif args.command[0] == 'evolve':
        evolve(config)
