import os
os.environ['QT_MAC_WANTS_LAYER'] = '1'
import sys
import random
from PyQt6.QtWidgets import QApplication, QMainWindow, \
    QGraphicsView, QGraphicsScene, QGraphicsRectItem, \
    QToolBar, QPushButton, QComboBox, QMessageBox, \
    QMenu, QMenuBar

from PyQt6.QtGui import QAction
from PyQt6.QtCore import QTimer, QRectF
from PyQt6.QtGui import QBrush, QColor
from scipy.ndimage import convolve
import ctypes
import numpy as np
import scipy
import platform

CELL_SIZE = 5
INITIAL_GRID_WIDTH = 100
INITIAL_GRID_HEIGHT = 100

RULES = [
    "e542-5f79-9341-f31e-6c6b-7f08-8773-7068",
    "a07f-c000-0000-0000-0000-0000-ff80-807f",
    "ea44-55df-9025-bead-5f6e-45ca-6168-275a"
]

RULE_STRING = "ea44-55df-9025-bead-5f6e-45ca-6168-275a"  # One of the known MergeLife rule strings. You can replace this.

# The color table.
COLOR_TABLE = [
    [0, 0, 0],  # Black 0
    [255, 0, 0],  # Red 1
    [0, 255, 0],  # Green 2
    [255, 255, 0],  # Yellow 3
    [0, 0, 255],  # Blue 4
    [255, 0, 255],  # Purple 5
    [0, 255, 255],  # Cyan 6
    [255, 255, 255]  # White 7
]

def toHex(code):
    result = ""

    for rng, pct in code:
        if len(result) > 0:
            result += "-"
        result += "%02x" % int(rng)
        result += "%02x" % ctypes.c_ubyte(int(pct)).value

    return result


def fromHex(str):
    result = []
    str = str.replace('-', '')
    for i in range(len(COLOR_TABLE)):
        idx = i * 4
        rng = str[idx:idx + 2]
        pct = str[idx + 2:idx + 4]
        rng = int(rng, 16)
        pct = int(pct, 16)

        pct = ctypes.c_byte(pct).value  # Twos complement
        result.append((rng, pct))

    return result

def parse_update_rule(code):
    code = fromHex(code)

    sorted_code = []
    for i, x in enumerate(code):
        rng = int(x[0] * 8)
        if x[1] > 0:
            pct = x[1] / 127.0
        else:
            pct = x[1] / 128.0
        sorted_code.append((2048 if rng == 2040 else rng, pct, i))

    sorted_code = sorted(sorted_code)
    return sorted_code

def randomize_lattice(ml_instance):
    height = ml_instance['height']
    width = ml_instance['width']
    ml_instance['track'] = {}
    ml_instance['time_step'] = 0
    ml_instance['lattice'][0]['data'] = np.random.randint(0, 256, size=(height, width, 3), dtype=np.uint8)
    ml_instance['lattice'][1]['data'] = np.copy(ml_instance['lattice'][0]['data'])


def new_ml_instance(height, width, rule_str):
    result = {
        'height': height,
        'width': width,
        'rule_str': rule_str,
        'sorted_rule': parse_update_rule(rule_str),
        'time_step': 0,
        'track': {},
        'lattice': [
            {'data': None, 'eval': None},
            {'data': None, 'eval': None}
        ]
    }

    randomize_lattice(result)
    result['lattice'][1]['data'] = np.copy(result['lattice'][0]['data'])
    return result

def update_step(ml_instance):
    kernel = [[1, 1, 1], [1, 0, 1], [1, 1, 1]]
    THIRD = 1.0 / 3.0

    # Get important values
    sorted_rule = ml_instance['sorted_rule']
    height = ml_instance['height']
    width = ml_instance['width']
    changed = np.zeros((height, width), dtype=bool)

    # Swap lattice
    t = ml_instance['lattice'][1]
    ml_instance['lattice'][1] = ml_instance['lattice'][0]
    ml_instance['lattice'][0] = t

    # Get current and previous lattice
    prev_data = ml_instance['lattice'][1]['data']
    current_data = ml_instance['lattice'][0]['data']

    # Merge RGB
    data_avg = np.dot(prev_data, [THIRD, THIRD, THIRD])
    data_avg = data_avg.astype(int)
    print("**", data_avg)
    pad_val = scipy.stats.mode(data_avg, axis=None)[0]
    pad_val = int(pad_val)
    data_cnt = convolve(data_avg, kernel, cval=pad_val, mode='constant')

    # Perform update
    for limit, pct, cidx in sorted_rule:
        mask = data_cnt < limit
        mask = np.logical_and(mask, np.logical_not(changed))
        changed = np.logical_or(changed, mask)

        if pct < 0:
            pct = abs(pct)
            cidx = cidx + 1
            if cidx >= len(COLOR_TABLE):
                cidx = 0

        d = COLOR_TABLE[cidx] - prev_data[mask]
        current_data[mask] = prev_data[mask] + np.floor(d * pct)
        ml_instance['lattice'][0]['eval'] = {
            'mode': pad_val,
            'merge': data_avg,
            'neighbor': data_cnt
        }

    ml_instance['time_step'] += 1
    return current_data

class MergeLife(QMainWindow):
    def __init__(self):
        super().__init__()
        self.running = False
        self.setWindowTitle("Heaton's Game of Life")
        self.setGeometry(100, 100, 
            INITIAL_GRID_WIDTH * CELL_SIZE, 
            INITIAL_GRID_HEIGHT * CELL_SIZE)

        self.setup_mac_menu()
        self.initUI()


    def setup_mac_menu(self):
        # Create a main menu bar
        #if platform.uname().system.startswith('Darw') :
        #    self.menubar = QMenuBar() # parentless menu bar for Mac OS
        #else:
        #    self.menubar = self.menuBar() # refer to the default one

        self.menubar = QMenuBar() #self.menuBar()

        # Create the app menu and add it to the menu bar
        app_menu = QMenu("My App", self)
        self.menubar.addMenu(app_menu)

        # Add items to the app menu
        about_action = QAction("About My App", self)
        app_menu.addAction(about_action)

        preferences_action = QAction("Preferences...", self)
        app_menu.addAction(preferences_action)

        exit_action = QAction("Quit", self)
        exit_action.triggered.connect(self.close)
        app_menu.addAction(exit_action)

    def initUI(self):
        # Graphics View
        self.view = QGraphicsView(self)
        self.setCentralWidget(self.view)

        self.scene = QGraphicsScene(self)
        self.view.setScene(self.scene)

        # Toolbar
        toolbar = QToolBar(self)
        self.addToolBar(toolbar)

        # Start Button
        self.btn_start = QPushButton("Start", self)
        self.btn_start.clicked.connect(self.startGame)
        toolbar.addWidget(self.btn_start)

        # Stop Button
        self.btn_stop = QPushButton("Stop", self)
        self.btn_stop.clicked.connect(self.stopGame)
        toolbar.addWidget(self.btn_stop)
        self.btn_stop.setEnabled(False)

        # Combo Box
        self.combo = QComboBox(self)
        self.combo.addItems(RULES)
        self.combo.currentTextChanged.connect(self.changeRule)
        toolbar.addWidget(self.combo)

        # Configure the resize timer
        self.resize_timer = QTimer(self)
        self.resize_timer.timeout.connect(self.finished_resizing)
        self.resize_timer.setInterval(300)  # 300 milliseconds

        # Animation timer
        self.timer = QTimer(self)
        self.timer.timeout.connect(self.nextGeneration)
        self.timer.start(10)


    def nextGeneration(self):
        if self.running:
            update_step(self.ml)
            self.updateUIGrid()
        

    def startGame(self):
        self.btn_start.setEnabled(False)
        self.btn_stop.setEnabled(True)
        self.running = True

    def stopGame(self):
        self.btn_start.setEnabled(True)
        self.btn_stop.setEnabled(False)
        self.running = False

    def changeRule(self, ruleText):
        self.ml = new_ml_instance(
            self.grid_height,
            self.grid_width,
            ruleText)
        
    def displayMessageBox(self, text):
        msg = QMessageBox(self)
        msg.setText(text)
        msg.exec()

    def resizeEvent(self, event):
        """This method is called whenever the window is resized."""
        self.stopGame()
        self.resize_timer.start()  # Restart the timer every time this event is triggered
        self.last_size = event.size()  # Store the latest size
        super().resizeEvent(event)

    def finished_resizing(self):
        """This method will be called approximately 300ms after the last resize event."""
        self.grid_width = int(self.last_size.width() / CELL_SIZE)
        self.grid_height = int(self.last_size.height() / CELL_SIZE)

        self.ml = new_ml_instance(self.grid_height,self.grid_width,RULE_STRING)
        self.grid = [[QColor(random.randint(0, 255), random.randint(0, 255), random.randint(0, 255)) for y in range(self.grid_height)] for x in range(self.grid_width)]
        self.rects = [[None for y in range(self.grid_height)] for x in range(self.grid_width)]

        for x in range(self.grid_width):
            for y in range(self.grid_height):
                rect = QGraphicsRectItem(QRectF(x * CELL_SIZE, y * CELL_SIZE, CELL_SIZE, CELL_SIZE))
                self.rects[x][y] = rect
                self.scene.addItem(rect)
                rect.setBrush(QBrush(self.grid[x][y]))
        self.updateUIGrid()

        self.resize_timer.stop()

    def updateUIGrid(self):
        grid = self.ml['lattice'][0]['data']
        for x in range(self.grid_width):
            for y in range(self.grid_height):
                color = QColor(int(grid[y][x][0]), int(grid[y][x][1]), int(grid[y][x][2]))  # assuming RGB values
                self.rects[x][y].setBrush(QBrush(color))

if __name__ == '__main__':
    app = QApplication(sys.argv)
    app.setApplicationName("Heaton's Game of Life")
    window = MergeLife()
    window.show()
    sys.exit(app.exec())

