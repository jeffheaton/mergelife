from PyQt6.QtCore import Qt, QTimer
from PyQt6.QtWidgets import (
    QToolBar, QPushButton, QComboBox, QWidget,
    QVBoxLayout, QGraphicsView, QGraphicsScene
)
import logging
from PyQt6.QtGui import QImage, QPixmap, QFont, QColor, QPainter, QFontMetrics
from mergelife import new_ml_instance, update_step, randomize_lattice
import numpy as np
from PyQt6.QtCore import QRegularExpression, QDateTime
from PyQt6.QtGui import QRegularExpressionValidator
import utl_settings
from tab_graphic import TabGraphic

logger = logging.getLogger(__name__)

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

class FPSGraphicsView(QGraphicsView):
    def __init__(self, scene, parent=None):
        super().__init__(scene, parent)
        self._parent = parent

    def paintEvent(self, event):
        super().paintEvent(event)
        painter = QPainter(self.viewport())
        
        # Setting up text properties
        font = QFont("Arial", 20)
        font.setBold(True)
        painter.setFont(font)
        fps_text = f"Steps: {self._parent._steps:,}, FPS: {self._parent._fps}"
        
        # Get text dimensions
        font_metrics = QFontMetrics(font)
        text_width = font_metrics.horizontalAdvance(fps_text)
        text_height = font_metrics.height()

        # Rectangle properties
        padding = 5  # You can adjust the padding if needed
        rect_x = self.width() - text_width - 2 * padding
        rect_y = 0
        rect_width = text_width + 2 * padding
        rect_height = text_height + padding

        # Draw the black rectangle
        painter.setBrush(QColor(0, 0, 0))
        painter.setPen(QColor(0, 0, 0))  # Set rectangle border color to black
        painter.drawRect(rect_x, rect_y, rect_width, rect_height)

        # Draw the FPS text
        painter.setPen(QColor(255, 255, 255))  # Set text color to white
        painter.drawText(rect_x + padding, text_height, fps_text)

class TabSimulate(TabGraphic):
    def __init__(self, window):
        super().__init__(window)
        self._running = False
        self._force_rule = False

        # Initialize central widget and layout
        self._layout = QVBoxLayout(self)
        self._layout.setContentsMargins(0, 0, 0, 0)
        self._layout.setSpacing(0)

        # Toolbar
        self._toolbar = QToolBar()
        self._layout.addWidget(self._toolbar)  # Add the toolbar to the layout first

        # Start Button
        self._btn_start = QPushButton("Start")
        self._btn_start.clicked.connect(self.startGame)
        self._toolbar.addWidget(self._btn_start)

        # Stop Button
        self._btn_stop = QPushButton("Stop")
        self._btn_stop.clicked.connect(self.stopGame)
        self._toolbar.addWidget(self._btn_stop)
        self._btn_stop.setEnabled(False)

        # Step Button
        self._btn_step = QPushButton("Step")
        self._btn_step.clicked.connect(self.stepGame)
        self._toolbar.addWidget(self._btn_step)
        self._btn_step.setEnabled(True)

        # Step Button
        self._btn_reset = QPushButton("Reset")
        self._btn_reset.clicked.connect(self.resetGame)
        self._toolbar.addWidget(self._btn_reset)
        self._btn_reset.setEnabled(True)

        # Rule Button
        self._btn_rule = QPushButton("Rule:")
        self._btn_rule.clicked.connect(self.displayRule)
        self._toolbar.addWidget(self._btn_rule)
        self._btn_rule.setEnabled(True)

        # Combo Box
        self._combo = QComboBox()
        self._combo.setEditable(True)  # Make the combo box editable
        # Set up validation for the combo box input
        rule_pattern = QRegularExpression(r"^[a-f0-9]{4}(-[a-f0-9]{4}){7}$")  # Regex pattern for the MergeLife rule string
        validator = QRegularExpressionValidator(rule_pattern, self._combo)
        self._combo.setValidator(validator)
        self._combo.addItems(RULES)
        #self._combo.currentTextChanged.connect(self.changeRule)
        self._combo.activated.connect(self.onComboBoxActivated)
        self._toolbar.addWidget(self._combo)

        self._scene = None
        self._view = None

        # FPS
        self._frame_count = 0
        self._fps = 0
        self._fps_timer = QTimer(self)
        self._fps_timer.timeout.connect(self.computeFPS)
        self._fps_timer.start(1000)  # Every second
        self._target_fps = utl_settings.settings.get(utl_settings.FPS_KEY, 30)

        self._steps = 0

        # Animation timer
        self._last_event_time = QDateTime.currentMSecsSinceEpoch()
        self._timer_interval = 16  # for example, roughly 60fps
        self._timer = QTimer(self)
        self._timer.timeout.connect(self.nextGeneration)
        self._timer.start(self._timer_interval)

        self._force_update = 0
        self._cell_size = utl_settings.settings.get(utl_settings.CELL_SIZE_KEY, 5)
        
        if self._cell_size<1: self._cell_size = 1

    def on_close(self):
        self._timer.stop()
        self._scene.clear()
        logger.info("Closed simulator tab")


    def changeRule(self, ruleText):
        self._combo.setCurrentText(ruleText)
        width = self.width()
        height = self.height()
        
        self._grid_width = int(width / self._cell_size)
        self._grid_height = int(height / self._cell_size)

        self._ml = new_ml_instance(
            self._grid_height,
            self._grid_width,
            ruleText)
                
        self._render_buffer = np.zeros((self._grid_height * self._cell_size, self._grid_width * self._cell_size, 3), 
                                        dtype=np.uint8)
        
        height, width, channel = self._render_buffer.shape
        bytes_per_line = 3 * width

        self._display_buffer = QImage(self._render_buffer.data, self._grid_width * self._cell_size, 
                        self._grid_height * self._cell_size, 3 * self._grid_width * self._cell_size, 
                        QImage.Format.Format_RGB888)
        # close out old scene, if there is one
        if self._scene:
            self._scene.clear()
            self._view.close()
            self._layout.removeWidget(self._view)

        # QGraphicsView and QGraphicsScene
        self._scene = QGraphicsScene(self)
        if utl_settings.get_bool(utl_settings.FPS_OVERLAY):
            self._view = FPSGraphicsView(self._scene, self)
        else:
            self._view = QGraphicsView(self._scene, self)
        
        self._view.setHorizontalScrollBarPolicy(Qt.ScrollBarPolicy.ScrollBarAlwaysOff)
        self._view.setVerticalScrollBarPolicy(Qt.ScrollBarPolicy.ScrollBarAlwaysOff)

        self._layout.addWidget(self._view)  # Add the view to the layout after the toolbar
        self._scene.setBackgroundBrush(Qt.GlobalColor.black)


        self._pixmap_buffer = self._scene.addPixmap(QPixmap.fromImage(self._display_buffer))
        self.updateUIGrid()

    def updateUIGrid(self):
        grid = self._ml['lattice'][0]['data']
        #self._render_buffer.fill(0) # maybe not needed?
        for x in range(self._grid_width):
            for y in range(self._grid_height):
                color = grid[y][x]
                self._render_buffer[y*self._cell_size:(y+1)*self._cell_size, x*self._cell_size:(x+1)*self._cell_size] = color 

        # Update QPixmap for the existing QGraphicsPixmapItem
        self._pixmap_buffer.setPixmap(QPixmap.fromImage(self._display_buffer))
        self._view.fitInView(self._scene.sceneRect(), mode=Qt.AspectRatioMode.IgnoreAspectRatio)

        # FPS
        self._frame_count += 1

    def nextGeneration(self):
        current_time = QDateTime.currentMSecsSinceEpoch()
        elapsed_time = current_time - self._last_event_time

        if self._force_rule:
            self.changeRule(self._force_rule)
            self._force_rule = None
            self.startGame()

        if self._force_update>0:
            # Not crazy about this solution, but it was the only way I could find to get the scene to
            # correctly resize to the view.
            self.resetGame()
            self._force_update-=1

        if elapsed_time < self._timer_interval * 10:  # if not too much time has passed since last event
            if self._running:
                update_step(self._ml)
                self._steps+=1
                self.updateUIGrid()

        self._last_event_time = current_time

    def startGame(self):
        self._btn_start.setEnabled(False)
        self._btn_stop.setEnabled(True)
        self._btn_step.setEnabled(False)
        self._running = True

    def stopGame(self):
        self._btn_start.setEnabled(True)
        self._btn_stop.setEnabled(False)
        self._btn_step.setEnabled(True)
        self._running = False

    def stepGame(self):
        self._steps+=1
        update_step(self._ml)
        self.updateUIGrid()

    def resetGame(self):
        self._steps=0
        randomize_lattice(self._ml)
        if not self._running:
            self.updateUIGrid()
            

    def on_resize(self):
        print("Simulator resize")
        self.changeRule(self._combo.currentText())
        self._view.update()
        self.updateUIGrid()

    def showEvent(self, event):
        super().showEvent(event)
        self.changeRule(self._combo.currentText())
        self.updateUIGrid()
        self._force_update = 1

    def onComboBoxActivated(self, index):
        # index is the position of the activated item in the dropdown list
        # For editable combo boxes, the index is -1 if the user edited the text and pressed Enter.
        if index == -1:
            # The user edited the text and pressed Enter
            text = self._combo.currentText()
            if self._combo.validator().validate(text, 0)[0] == QValidator.State.Acceptable:
                self.changeRule(text)
                self._force_update = 1
                self._steps = 0
        else:
            # The user picked an item from the dropdown list
            self.changeRule(self._combo.itemText(index))
            self._force_update = 1
            self._steps = 0


    def computeFPS(self):
        self._fps = self._frame_count
        self._frame_count = 0
        self._view.viewport().update()  # Trigger a redraw to show the updated FPS

    def displayRule(self):
        self._window.show_rule(self._combo.currentText())
        

    
