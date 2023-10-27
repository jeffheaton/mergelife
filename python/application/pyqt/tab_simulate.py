from PyQt6.QtCore import Qt, QTimer
from PyQt6.QtWidgets import (
    QToolBar, QPushButton, QComboBox, QWidget,
    QVBoxLayout, QGraphicsView, QGraphicsScene,
    QVBoxLayout
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

RULE_STRING = "ea44-55df-9025-bead-5f6e-45ca-6168-275a"  # One of the known MergeLife rule strings. You can replace this.

_grid_width = None
_grid_height = None
_ml = None
_render_buffer = None
_pixmap_buffer = None
_scene = None
_widget = None
_btn_start = None
_btn_stop = None
_combo = None
_view = None
_running = False
_window = None

def changeRule(ruleText):
    global _grid_width, _grid_height, _ml, _render_buffer, _pixmap_buffer, _display_buffer
    size = _widget.size()
    width = _widget.width()
    height = _widget.height()

    _grid_width = int(width / CELL_SIZE)
    _grid_height = int(height / CELL_SIZE)

    _ml = new_ml_instance(
        _grid_height,
        _grid_width,
        ruleText)
            
    _render_buffer = np.zeros((_grid_height * CELL_SIZE, _grid_width * CELL_SIZE, 3), 
                                    dtype=np.uint8)
    
    height, width, channel = _render_buffer.shape
    bytes_per_line = 3 * width

    _display_buffer = QImage(_render_buffer.data, _grid_width * CELL_SIZE, _grid_height * CELL_SIZE, 3 * _grid_width * CELL_SIZE, QImage.Format.Format_RGB888)
    _pixmap_buffer = _scene.addPixmap(QPixmap.fromImage(_display_buffer))
    updateUIGrid()

def updateUIGrid():
    grid = _ml['lattice'][0]['data']
    for x in range(_grid_width):
        for y in range(_grid_height):
            color = grid[y][x]
            _render_buffer[y*CELL_SIZE:(y+1)*CELL_SIZE, x*CELL_SIZE:(x+1)*CELL_SIZE] = color 

    # Update QPixmap for the existing QGraphicsPixmapItem
    _pixmap_buffer.setPixmap(QPixmap.fromImage(_display_buffer))
    _view.fitInView(_scene.sceneRect(), mode=Qt.AspectRatioMode.IgnoreAspectRatio)


def nextGeneration():
    if _running:
        update_step(_ml)
        updateUIGrid()
    

def startGame():
    global _running
    _btn_start.setEnabled(False)
    _btn_stop.setEnabled(True)
    _running = True

def stopGame():
    global _running
    _btn_start.setEnabled(True)
    _btn_stop.setEnabled(False)
    _running = False

def show_simulator(window):
    global _scene, _window, _widget, _view, _toolbar, _btn_start, _btn_stop, _combo

    if window.is_tab_open("Simulator"):
        return

    _window = window

    # Initialize central widget and layout
    _widget = QWidget()
    layout = QVBoxLayout(_widget)
    layout.setContentsMargins(0, 0, 0, 0)
    layout.setSpacing(0)

    # Toolbar
    _toolbar = QToolBar()
    layout.addWidget(_toolbar)  # Add the toolbar to the layout first

    # Start Button
    _btn_start = QPushButton("Start")
    _btn_start.clicked.connect(startGame)
    _toolbar.addWidget(_btn_start)

    # Stop Button
    _btn_stop = QPushButton("Stop")
    _btn_stop.clicked.connect(stopGame)
    _toolbar.addWidget(_btn_stop)
    _btn_stop.setEnabled(False)

    # Combo Box
    _combo = QComboBox()
    _combo.addItems(RULES)
    _combo.currentTextChanged.connect(changeRule)
    _toolbar.addWidget(_combo)

    # QGraphicsView and QGraphicsScene
    _scene = QGraphicsScene()
    _view = QGraphicsView(_scene, _widget)
    layout.addWidget(_view)  # Add the view to the layout after the toolbar
    _scene.setBackgroundBrush(Qt.GlobalColor.black)

    window.add_tab(_widget, "Simulator")

    # Animation timer
    _timer = QTimer(_widget)
    _timer.timeout.connect(nextGeneration)
    _timer.start(10)

    changeRule(RULE_STRING)


