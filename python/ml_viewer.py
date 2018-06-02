import matplotlib
matplotlib.use('TkAgg')

import numpy as np
import matplotlib.pyplot as plt
import matplotlib.animation as animation
import mergelife
import sys
import argparse


def updatefig(*args):
    global ml_inst

    data2 = mergelife.update_step(ml_inst)
    im.set_array(data2)

    return im,

def go_animate():
    ani = animation.FuncAnimation(fig, updatefig, interval=10, blit=True)
    plt.show()

parser = argparse.ArgumentParser(description='Mergelife Utility')
parser.add_argument('--rows', nargs=1, type=int, help="the number of rows in the MergeLife grid")
parser.add_argument('--cols', nargs=1, type=int, help="the number of cols in the MergeLife grid")
parser.add_argument('--zoom', nargs=1, type=int, help="the pixel size for rendering")
parser.add_argument('rule', metavar='rule', type=str, help='the MergeLife rule to animate')

args = parser.parse_args()

if args.rows is not None:
    rows = args.rows[0]
else:
    rows = 100

if args.cols is not None:
    cols = args.cols[0]
else:
    cols = 100

time_step = 0

fig = plt.figure()
data = np.random.randint(0,256, size=(rows, cols, 3), dtype=np.uint8)
im = plt.imshow(data, animated=True)

ml_inst = mergelife.new_ml_instance(rows,cols,args.rule)

go_animate()
