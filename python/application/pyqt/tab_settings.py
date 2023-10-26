import os
import json
import logging
import const_values

logger = logging.getLogger(__name__)

from PyQt6.QtWidgets import (
    QApplication, QMainWindow, QToolBar, QPushButton,
    QComboBox, QMessageBox, QMenu, QMenuBar, QWidget,
    QVBoxLayout, QLabel, QGraphicsView, QGraphicsScene,
    QTabWidget, QSpinBox, QVBoxLayout, QHBoxLayout
)

def show_settings(window):
    if window.is_tab_open("Properties"):
        return
    logger.info("Opened settings")
    widget = QWidget()
    layout = QVBoxLayout()
    cell_size_label = QLabel("Cell Size (1-25):", widget)
    cell_size_spinbox = QSpinBox(widget)
    cell_size_spinbox.setRange(1, 25)
    animation_speed_label = QLabel("Animation Speed (1-30 FPS):", widget)
    animation_speed_spinbox = QSpinBox(widget)
    animation_speed_spinbox.setRange(1, 30)
    save_button = QPushButton("Save", widget)
    save_button.clicked.connect(action_save)
    cancel_button = QPushButton("Cancel", widget)
    button_layout = QHBoxLayout()
    button_layout.addWidget(save_button)
    button_layout.addWidget(cancel_button)
    layout.addWidget(cell_size_label)
    layout.addWidget(cell_size_spinbox)
    layout.addWidget(animation_speed_label)
    layout.addWidget(animation_speed_spinbox)
    layout.addLayout(button_layout)
    widget.setLayout(layout)
    window.add_tab(widget, "Settings")


def action_save():
    logger.info("save")
