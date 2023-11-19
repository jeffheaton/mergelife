import logging

from PyQt6.QtCore import QRegularExpression
from PyQt6.QtGui import QRegularExpressionValidator
from PyQt6.QtWidgets import QComboBox, QPushButton, QToolBar

import utl_settings
from mergelife.mergelife import new_ml_instance, randomize_lattice, update_step
from jth_ui.tab_graphic import TabGraphic

logger = logging.getLogger(__name__)

INITIAL_GRID_WIDTH = 200
INITIAL_GRID_HEIGHT = 100

RULES = [
    "e542-5f79-9341-f31e-6c6b-7f08-8773-7068",
    "a07f-c000-0000-0000-0000-0000-ff80-807f",
    "ea44-55df-9025-bead-5f6e-45ca-6168-275a",
]

RULE_STRING = "ea44-55df-9025-bead-5f6e-45ca-6168-275a"

import sys

from PyQt6.QtWidgets import QApplication, QLabel, QMainWindow, QTabWidget


class TabSimulate(TabGraphic):
    def __init__(self, window):
        super().__init__(window=window)

        self.init_toolbar()
        self.init_fps()
        self.init_animate(target_fps=utl_settings.settings.get(utl_settings.FPS_KEY, 30))

        self._cell_size = utl_settings.settings.get(utl_settings.CELL_SIZE_KEY, 5)
        print(f"Cell size: {self._cell_size}")
        if self._cell_size < 1:
            self._cell_size = 1

    def init_toolbar(self):
        self._toolbar = QToolBar()
        self._layout.addWidget(self._toolbar)  # Add the toolbar to the layout first

        # Start Button
        self._btn_start = QPushButton("Start")
        self._btn_start.clicked.connect(self.start_game)
        self._toolbar.addWidget(self._btn_start)

        # Stop Button
        self._btn_stop = QPushButton("Stop")
        self._btn_stop.clicked.connect(self.stop_game)
        self._toolbar.addWidget(self._btn_stop)
        self._btn_stop.setEnabled(False)

        # Step Button
        self._btn_step = QPushButton("Step")
        self._btn_step.clicked.connect(self.step_game)
        self._toolbar.addWidget(self._btn_step)
        self._btn_step.setEnabled(True)

        # Step Button
        self._btn_reset = QPushButton("Reset")
        self._btn_reset.clicked.connect(self.reset_game)
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
        rule_pattern = QRegularExpression(
            r"^[a-f0-9]{4}(-[a-f0-9]{4}){7}$"
        )  # Regex pattern for the MergeLife rule string
        validator = QRegularExpressionValidator(rule_pattern, self._combo)
        self._combo.setValidator(validator)
        self._combo.addItems(RULES)
        # self._combo.currentTextChanged.connect(self.changeRule)
        self._combo.activated.connect(self.onComboBoxActivated)
        self._toolbar.addWidget(self._combo)

    def changeRule(self, ruleText):
        self._combo.setCurrentText(ruleText)
        width = self.width()
        height = self.height()

        self._grid_width = int(width / self._cell_size)
        self._grid_height = int(height / self._cell_size)

        self._ml = new_ml_instance(self._grid_height, self._grid_width, ruleText)

        render_height = self._grid_height * self._cell_size
        render_width = self._grid_width * self._cell_size
        self.create_graphic(
            render_height,
            render_width,
            fps_overlay=utl_settings.get_bool(utl_settings.FPS_OVERLAY),
        )
        self.updateUIGrid()

    def updateUIGrid(self):
        grid = self._ml["lattice"][0]["data"]
        for x in range(self._grid_width):
            for y in range(self._grid_height):
                color = grid[y][x]
                self._render_buffer[
                    y * self._cell_size : (y + 1) * self._cell_size,
                    x * self._cell_size : (x + 1) * self._cell_size,
                ] = color

        self.update_graphic()

    def timer_step(self):
        if self._force_rule:
            self.changeRule(self._force_rule)
            self._force_rule = None
            self.start_game()

        if self._force_update > 0:
            # Not crazy about this solution, but it was the only way I could find to get the scene to
            # correctly resize to the view.
            self.reset_game()
            self._force_update -= 1

    def running_step(self):
        update_step(self._ml)
        self.updateUIGrid()

    def start_game(self):
        self._btn_start.setEnabled(False)
        self._btn_stop.setEnabled(True)
        self._btn_step.setEnabled(False)
        self._running = True

    def stop_game(self):
        self._btn_start.setEnabled(True)
        self._btn_stop.setEnabled(False)
        self._btn_step.setEnabled(True)
        self._running = False

    def step_game(self):
        self._steps += 1
        update_step(self._ml)
        self.updateUIGrid()

    def reset_game(self):
        self._steps = 0
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
            if (
                self._combo.validator().validate(text, 0)[0]
                == QValidator.State.Acceptable
            ):
                self.changeRule(text)
                self._force_update = 1
                self._steps = 0
        else:
            # The user picked an item from the dropdown list
            self.changeRule(self._combo.itemText(index))
            self._force_update = 1
            self._steps = 0

    def displayRule(self):
        self._window.show_rule(self._combo.currentText())
