import logging
import os
from PyQt6.QtCore import QDir, QTimer,QThread
from PyQt6.QtWidgets import (QFileDialog, QGridLayout, QHBoxLayout, QLabel, 
                             QLineEdit, QPushButton, QVBoxLayout, QWidget)
import ml_evolve
import time

logger = logging.getLogger(__name__)

config = {
    "config": {
        "rows": 50,
        "cols": 50,
        "populationSize": 100,
        "crossover": 0.75,
        "tournamentCycles": 5,
        "zoom": 5,
        "renderSteps": 250,
        "evalCycles": 5,
        "patience": 250,
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

class Worker(QThread):
    def __init__(self, report_target, path):
        super().__init__()
        self._report_target = report_target
        self.is_running = True
        self._evolve = ml_evolve.Evolve(report_target=report_target, path=path)
        
    def run(self):
        try:
            i = 1
            while self.is_running:
                # Your CPU-bound process code goes here
                # For demonstration, let's just print a message
                print(f"Processing... {i}")
                self._evolve.evolve(config)
                time.sleep(1)
                i+=1
            self._report_target.stop_complete()
        except Exception as e:
            logger.error("Error during startup", exc_info=True)
        

    def stop(self):
        logging.info("Requesting evolve to stop")
        self._evolve.requestStop = True
        self.is_running = False

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

        no_improve_label = QLabel("No improve/Max allowed:")
        self._no_improve_value = QLineEdit()
        self._no_improve_value.setReadOnly(True)
        grid_layout.addWidget(no_improve_label, 5, 0)
        grid_layout.addWidget(self._no_improve_value, 5, 1)

        rules_found_label = QLabel("Rules found:")
        self._rules_found_value = QLineEdit()
        self._rules_found_value.setReadOnly(True)
        grid_layout.addWidget(rules_found_label, 6, 0)
        grid_layout.addWidget(self._rules_found_value, 6, 1)

        status_label = QLabel("Status:")
        self._status_value = QLineEdit()
        self._status_value.setReadOnly(True)
        grid_layout.addWidget(status_label, 7, 0)
        grid_layout.addWidget(self._status_value, 7, 1)

        output_dir_label = QLabel("Output Directory:")
        self._output_dir_value = QLineEdit()
        self._output_dir_value.setText("")
        self._output_dir_value.setReadOnly(True)
        grid_layout.addWidget(output_dir_label, 8, 0)
        grid_layout.addWidget(self._output_dir_value, 8, 1)

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


    def on_close(self):
        logger.info("Closed Evolve tab.")
        if self._evolve:
            self._evolve.stop()

    def on_resize(self):
        pass

    def action_start(self):
        path = QFileDialog.getExistingDirectory(self, "Select Output Directory")
        if path:
            self._output_dir_value.setText(path)

        logger.info("Checking if output directory exists")
        if not os.path.exists(path):
            self._status_value.setText("Output directory does not exist")
            return
        
        logger.info("Checking if output directory is a directory")
        if not os.path.isdir(path):
            self._status_value.setText("Must specify a valid output directory (not a directory)")
            return
        
        logger.info("Checking for write access to directory")
        if not os.access(path, os.W_OK):
            self._status_value.setText("Must have write access to output directory")
            return
        
        self._start_button.setEnabled(False)
        self._stop_button.setEnabled(True)

        self.thread = Worker(report_target=self,path=path)
        self.thread.start()
        logging.info("Evolve started")

    def action_stop(self):
        try:
            logger.info("Stop requested for evolve")
            self._status_value.setText("Stopping...")
            self.thread.stop()
            self._start_button.setEnabled(False)
            self._stop_button.setEnabled(False)
        except Exception as e:
            logger.error("Error during stop", exc_info=True)

    def report(self, evolve):
        self._run_number_value.setText(f"{evolve.runCount:,}")
        self._eval_number_value.setText(f"{evolve.evalCount:,}")
        self._evals_per_min_value.setText(f"{evolve.perMin:,.2f}")
        if evolve.bestGenome:
            self._current_rule_value.setText(str(evolve.bestGenome['rule']))
            self._current_score_value.setText(f"{evolve.bestGenome['score']:,.2f}/{evolve.score_threshold:,.2f}")
        else:
            self._current_rule_value.setText("")
            self._current_score_value.setText("")
        self._status_value.setText(evolve.status)
        self._no_improve_value.setText(f"{evolve.noImprovement:,}/{evolve.patience:,}")
        self._rules_found_value.setText(f"{evolve.rules_found:,}")

    def stop_complete(self):
        logger.info("Stop complete")
        self._start_button.setEnabled(True)
        self._stop_button.setEnabled(False)
        self._status_value.setText("Stopped")
        
                
