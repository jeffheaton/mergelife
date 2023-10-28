from PyQt6.QtCore import Qt, QTimer
from PyQt6.QtWidgets import (
    QToolBar, QPushButton, QComboBox, QWidget,
    QVBoxLayout, QGraphicsView, QGraphicsScene
)
import logging
from PyQt6.QtGui import QImage, QPixmap
from mergelife import new_ml_instance, update_step
import numpy as np

logger = logging.getLogger(__name__)

CELL_SIZE = 5
INITIAL_GRID_WIDTH = 200
INITIAL_GRID_HEIGHT = 100

RULES = [
    "e542-5f79-9341-f31e-6c6b-7f08-8773-7068",
    "a07f-c000-0000-0000-0000-0000-ff80-807f",
    "ea44-55df-9025-bead-5f6e-45ca-6168-275a"
]

RULE_STRING = "ea44-55df-9025-bead-5f6e-45ca-6168-275a"

import sys
from PyQt6.QtWidgets import QApplication, QMainWindow, QTabWidget, QLabel

class TabSimulate(QWidget):
    def __init__(self, window):
        super().__init__()
        self._window = window
        self._running = False

        # Initialize central widget and layout
        layout = QVBoxLayout(self)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(0)

        # Toolbar
        self._toolbar = QToolBar()
        layout.addWidget(self._toolbar)  # Add the toolbar to the layout first

        # Start Button
        self._btn_start = QPushButton("Start")
        self._btn_start.clicked.connect(self.startGame)
        self._toolbar.addWidget(self._btn_start)

        # Stop Button
        self._btn_stop = QPushButton("Stop")
        self._btn_stop.clicked.connect(self.stopGame)
        self._toolbar.addWidget(self._btn_stop)
        self._btn_stop.setEnabled(False)

        # Combo Box
        self._combo = QComboBox()
        self._combo.addItems(RULES)
        self._combo.currentTextChanged.connect(self.changeRule)
        self._toolbar.addWidget(self._combo)

        # QGraphicsView and QGraphicsScene
        self._scene = QGraphicsScene(self)
        self._view = QGraphicsView(self._scene, self)
        layout.addWidget(self._view)  # Add the view to the layout after the toolbar
        self._scene.setBackgroundBrush(Qt.GlobalColor.black)

        # Animation timer
        self._timer = QTimer(self)
        self._timer.timeout.connect(self.nextGeneration)
        self._timer.start(10)

        self.changeRule(RULE_STRING)

    def on_close(self):
        # Your custom functionality here
        print("Simulate: The tab is closing!")
        self._timer.stop()
        self._scene.clear()


    def changeRule(self, ruleText):
        size = self.size()
        width = self.width()
        height = self.height()

        self._grid_width = int(width / CELL_SIZE)
        self._grid_height = int(height / CELL_SIZE)

        self._ml = new_ml_instance(
            self._grid_height,
            self._grid_width,
            ruleText)
                
        self._render_buffer = np.zeros((self._grid_height * CELL_SIZE, self._grid_width * CELL_SIZE, 3), 
                                        dtype=np.uint8)
        
        height, width, channel = self._render_buffer.shape
        bytes_per_line = 3 * width

        self._display_buffer = QImage(self._render_buffer.data, self._grid_width * CELL_SIZE, self._grid_height * CELL_SIZE, 3 * self._grid_width * CELL_SIZE, QImage.Format.Format_RGB888)
        self._pixmap_buffer = self._scene.addPixmap(QPixmap.fromImage(self._display_buffer))
        self.updateUIGrid()

    def updateUIGrid(self):
        grid = self._ml['lattice'][0]['data']
        for x in range(self._grid_width):
            for y in range(self._grid_height):
                color = grid[y][x]
                self._render_buffer[y*CELL_SIZE:(y+1)*CELL_SIZE, x*CELL_SIZE:(x+1)*CELL_SIZE] = color 

        # Update QPixmap for the existing QGraphicsPixmapItem
        self._pixmap_buffer.setPixmap(QPixmap.fromImage(self._display_buffer))
        self._view.fitInView(self._scene.sceneRect(), mode=Qt.AspectRatioMode.IgnoreAspectRatio)

    def nextGeneration(self):
        if self._running:
            update_step(self._ml)
            self.updateUIGrid()

    def startGame(self):
        self._btn_start.setEnabled(False)
        self._btn_stop.setEnabled(True)
        self._running = True

    def stopGame(self):
        self._btn_start.setEnabled(True)
        self._btn_stop.setEnabled(False)
        self._running = False

    def on_resize(self):
        print("Simulator resize")
        self.changeRule(RULE_STRING)
        self.updateUIGrid()

    def showEvent(self, event):
        super().showEvent(event)
        self.changeRule(RULE_STRING)
        self.updateUIGrid()
    
