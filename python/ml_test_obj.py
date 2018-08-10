import matplotlib
matplotlib.use('TkAgg')

import numpy as np
import mergelife
import csv
import logging
from scipy.spatial.distance import euclidean
import json
import statistics

HEIGHT = 100
WIDTH = 100
TRACK_SIZE = 100
time_step = 0
CYCLES = 5

logger = logging.getLogger("mergelife")
logger.setLevel(logging.DEBUG)

# create a file handler
handler = logging.FileHandler('mergelife.log')
handler.setLevel(logging.DEBUG)
formatter = logging.Formatter('%(asctime)s - %(name)s - %(levelname)s - %(message)s')
handler.setFormatter(formatter)
logger.addHandler(handler)

def produceLatexTable():
    with open('./data/obj_tests.csv', newline='') as csvfile, \
            open('./data/paperObjective.json') as configFile:
                
        config = json.load(configFile)
        reader = csv.reader(csvfile)
        next(reader)
        for row in reader:
            ml_inst = mergelife.new_ml_instance(HEIGHT, WIDTH, row[0])
            obj = mergelife.objective_function(ml_inst, config['config']['evalCycles'], config['objective'])
            row += [str(obj)]
            print("\\texttt{{{}}} & {} & {} \\\\".format(row[0],row[1],obj['score']))

def calcStdDev():
    devs = []
    with open('./data/obj_tests.csv', newline='') as csvfile, \
            open('./data/paperObjective.json') as configFile:
        config = json.load(configFile)
        reader = csv.reader(csvfile)
        next(reader)
        for row in reader:
            sample = []
            for i in range(10):
                ml_inst = mergelife.new_ml_instance(HEIGHT, WIDTH, row[0])
                obj = mergelife.objective_function(ml_inst, config['config']['evalCycles'], config['objective'])
                sample.append(float(obj['score']))
            print(sample)
            d = statistics.stdev(sample)
            devs.append(d)
            print("{} - {}".format(row[0],d))
        print("Average deviation: {}".format(statistics.mean(devs)))

def expand_rule(rule):
    digits = '0123456789abcdef'

    results = []
    for i in range(len(rule)):
        hexIdx = digits.find(rule[i])

        if hexIdx != -1:
            if hexIdx >= 1:
                results.append( rule[0:i] + digits[hexIdx-1] + rule[i+1:])
            if hexIdx <= 14:
                results.append( rule[0:i] + digits[hexIdx+1] + rule[i+1:])

    return results

def calc_smooth():
    sample = []
    with open('./data/obj_tests.csv', newline='') as csvfile, \
            open('./data/paperObjective.json') as configFile:
        config = json.load(configFile)
        reader = csv.reader(csvfile)
        next(reader)
        for row in reader:
            ml_inst = mergelife.new_ml_instance(HEIGHT, WIDTH, row[0])
            obj = mergelife.objective_function(ml_inst, config['config']['evalCycles'], config['objective'])
            base_score = float(obj['score'])
            print("** {} {}".format(row[0],base_score))

            moves = expand_rule(row[0])
            sum = 0
            for move in moves:
                ml_inst = mergelife.new_ml_instance(HEIGHT, WIDTH, move)
                obj = mergelife.objective_function(ml_inst, config['config']['evalCycles'], config['objective'])
                move_score = float(obj['score'])
                diff = abs(base_score - move_score)
                # print("{}:{}".format(diff,move_score))
                if diff>0.9999:
                    sum += 1
            a = float(sum)/float(len(moves))
            sample.append(a)
            print("Above: {}".format(a))
        print("Avg: {}".format(statistics.mean(sample)))



# produceLatexTable()
# calcStdDev()
calc_smooth()