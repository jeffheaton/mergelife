import logging
import sys
import os
from PyQt6.QtCore import QDir, QTimer
from PyQt6.QtWidgets import (QFileDialog, QGridLayout, QHBoxLayout, QLabel, 
                             QLineEdit, QPushButton, QVBoxLayout, QWidget)
import ml_evolve


logger = logging.getLogger(__name__)

config = {
    "config": {
        "rows": 100,
        "cols": 100,
        "populationSize": 100,
        "crossover": 0.75,
        "tournamentCycles": 5,
        "zoom": 5,
        "renderSteps": 250,
        "evalCycles": 5,
        "patience": 1000,
        "scoreThreshold": 3.5,
        "maxRuns": 1000000
    },
    "objective": [
        {
            "stat": "steps",
            "min": 300,
            "max": 1000,
            "weight": 1,
            "min_weight": -1,
            "max_weight": 1
        },
        {
            "stat": "foreground",
            "min": 0.001,
            "max": 0.1,
            "weight": 1,
            "min_weight": -0.1,
            "max_weight": -1
        },
        {
            "stat": "active",
            "min": 0.001,
            "max": 0.1,
            "weight": 1,
            "min_weight": -1,
            "max_weight": -1
        },
        {
            "stat": "rect",
            "min": 0.02,
            "max": 0.25,
            "weight": 2,
            "min_weight": -2,
            "max_weight": 2
        },
        {
            "stat": "mage",
            "min": 5,
            "max": 10,
            "weight": 0,
            "min_weight": -5,
            "max_weight": 0
        }
    ]
}

class EvolveTab(QWidget):
    def __init__(self):
        super().__init__()
        self._evolve = None

# Main layout
        main_layout = QVBoxLayout(self)

        # Grid layout for labels and fields
        grid_layout = QGridLayout()

        run_number_label = QLabel("Run Number:")
        self._run_number_value = QLineEdit()
        self._run_number_value.setReadOnly(True)
        grid_layout.addWidget(run_number_label, 0, 0)
        grid_layout.addWidget(self._run_number_value, 0, 1)

        eval_number_label = QLabel("Eval Number:")
        self._eval_number_value = QLineEdit()
        self._eval_number_value.setReadOnly(True)
        grid_layout.addWidget(eval_number_label, 1, 0)
        grid_layout.addWidget(self._eval_number_value, 1, 1)

        evals_per_min_label = QLabel("Evals/min:")
        self._evals_per_min_value = QLineEdit()
        self._evals_per_min_value.setReadOnly(True)
        grid_layout.addWidget(evals_per_min_label, 2, 0)
        grid_layout.addWidget(self._evals_per_min_value, 2, 1)

        current_rule_label = QLabel("Current Rule:")
        self._current_rule_value = QLineEdit()
        self._current_rule_value.setReadOnly(True)
        grid_layout.addWidget(current_rule_label, 3, 0)
        grid_layout.addWidget(self._current_rule_value, 3, 1)

        current_score_label = QLabel("Current Score:")
        self._current_score_value = QLineEdit()
        self._current_score_value.setReadOnly(True)
        grid_layout.addWidget(current_score_label, 4, 0)
        grid_layout.addWidget(self._current_score_value, 4, 1)

        output_dir_label = QLabel("Output Directory:")
        self._output_dir_value = QLineEdit()
        default_path = os.path.expanduser("~/Documents/HeatonCA")
        self._output_dir_value.setText(default_path)
        self._browse_button = QPushButton("Browse...")
        self._browse_button.clicked.connect(self.browse_directory)
        grid_layout.addWidget(output_dir_label, 5, 0)
        grid_layout.addWidget(self._output_dir_value, 5, 1)
        grid_layout.addWidget(self._browse_button, 5, 2)

        # Extend the fields all the way to the right
        grid_layout.setColumnStretch(1, 1)

        # Buttons layout
        buttons_layout = QHBoxLayout()
        self._start_button = QPushButton("Start")
        self._stop_button = QPushButton("Stop")
        buttons_layout.addWidget(self._start_button)
        buttons_layout.addWidget(self._stop_button)
        self._start_button.clicked.connect(self.action_start)
        self._stop_button.clicked.connect(self.action_stop)
        self._stop_button.setEnabled(False)

        # Add grid layout and buttons layout to the main layout
        main_layout.addLayout(grid_layout)
        main_layout.addLayout(buttons_layout)
        
        # Push all items to the top
        main_layout.addStretch()

        # Set the layout to the QWidget
        self.setLayout(main_layout)

        # Adjust the window size to fit contents
        self.adjustSize()

        # Configure the resize timer
        self._update_timer = QTimer(self)
        self._update_timer.timeout.connect(self.timer_event)
        self._update_timer.setInterval(1000)  # 300 milliseconds
        self._update_timer.start()
        

    def on_close(self):
        # Your custom functionality here
        print("The tab is closing!")

    def on_resize(self):
        pass

    def browse_directory(self):
        # Get the selected directory from the file dialog
        dir_path = QFileDialog.getExistingDirectory(self, "Select Output Directory", self._output_dir_value.text())
        # Update the directory text box if a directory was chosen
        if dir_path:
            self._output_dir_value.setText(dir_path)

    def action_start(self):
        self._start_button.setEnabled(False)
        self._stop_button.setEnabled(True)
        self._output_dir_value.setReadOnly(True)
        self._browse_button.setEnabled(False)
        self._evolve = ml_evolve.Evolve()
        self._evolve.start(config)
        logging.info("Evolve started")

    def action_stop(self):
        self._start_button.setEnabled(False)
        self._stop_button.setEnabled(False)
        self._evolve.stop()

    def timer_event(self):
        if self._evolve:
            if self._evolve.stop_mode.value==ml_evolve.STOP_STAGE_STOPPED:
                self._start_button.setEnabled(True)
                self._stop_button.setEnabled(False)
                self._output_dir_value.setReadOnly(False)
                self._browse_button.setEnabled(True)
                self._evolve.stop_mode.value=ml_evolve.STOP_STAGE_RUNNING
