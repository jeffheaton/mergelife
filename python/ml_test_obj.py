import matplotlib
matplotlib.use('TkAgg')

import numpy as np
import mergelife
import csv
import logging
from scipy.spatial.distance import euclidean

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

with open('./data/obj_tests.csv', newline='') as csvfile:
    reader = csv.reader(csvfile)
    next(reader)
    for row in reader:
        ml_inst = mergelife.new_ml_instance(HEIGHT, WIDTH, row[0])
        obj = mergelife.objective_function(ml_inst, CYCLES)
        row += [str(obj)]
        print("\\texttt{{{}}} & {} & {} \\\\".format(row[0],row[1],obj['score']))