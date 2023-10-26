import os
import sys
import cv2
import random
import logging
import numpy as np
from PyQt6.QtCore import Qt, QTimer, QRectF, qInstallMessageHandler
from PyQt6.QtGui import QImage, QPixmap, QBrush, QColor, QAction
from PyQt6.QtWidgets import (
    QApplication, QMainWindow, QToolBar, QPushButton,
    QComboBox, QMessageBox, QMenu, QMenuBar, QWidget,
    QVBoxLayout, QLabel, QGraphicsView, QGraphicsScene,
    QTabWidget, QSpinBox, QVBoxLayout, QHBoxLayout
)
from mergelife import new_ml_instance, update_step
from tab_simulate import show_simulator
from PyQt6.QtCore import QCoreApplication, Qt, qInstallMessageHandler
import logging.config
import const_values

import os
import logging
import logging.handlers
import os
import datetime
import glob
import json
import utl_logging
import utl_settings

logger = logging.getLogger(__name__)

print(f"Logs path: {const_values.LOG_DIR}")
print(f"Settings path: {const_values.SETTING_DIR}")
print(f"Settings file: {const_values.SETTING_FILE}")

utl_settings.load_settings()
print(utl_settings.settings)
utl_logging.setup_logging()
utl_logging.delete_old_logs()

logging.info("Application starting up")


class HeatonCA(QMainWindow):
    def __init__(self):
        super().__init__()
        self.running = False
        self.setWindowTitle("HeatonCA")
        self.setGeometry(100, 100, 1000, 500)
        
        self.render_buffer = None
        self.display_buffer = None

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
  

        # Add items to the app menu
        about_action = QAction("About HeatonCA", self)
        app_menu.addAction(about_action)
        self.about_menu = QMenu("About", self)
        about_action.triggered.connect(self.show_about)


        preferences_action = QAction("Settings...", self)
        app_menu.addAction(preferences_action)
        preferences_action.triggered.connect(self.show_properties)

        exit_action = QAction("Quit", self)
        exit_action.triggered.connect(self.close)
        app_menu.addAction(exit_action)

        self.simulator_menu = QMenu("Simulator", self)
        simulator_action = QAction("Show Simulator", self)
        simulator_action.triggered.connect(self.show_simulator)
        self.simulator_menu.addAction(simulator_action)

        self.menubar.addMenu(app_menu)
        self.menubar.addMenu(self.simulator_menu)

    def initUI(self):
        self.tab_widget = QTabWidget()
        self.tab_widget.setTabsClosable(True)
        self.tab_widget.tabCloseRequested.connect(self.close_tab)
        self.setCentralWidget(self.tab_widget)

        # Configure the resize timer
        self.resize_timer = QTimer(self)
        self.resize_timer.timeout.connect(self.finished_resizing)
        self.resize_timer.setInterval(300)  # 300 milliseconds

    def displayMessageBox(self, text):
        msg = QMessageBox(self)
        msg.setText(text)
        msg.exec()

    def resizeEvent(self, event):
        """This method is called whenever the window is resized."""
        #self.stopGame()
        self.resize_timer.start()  # Restart the timer every time this event is triggered
        self.last_size = event.size()  # Store the latest size
        super().resizeEvent(event)

    def finished_resizing(self):
        """This method will be called approximately 300ms after the last resize event."""

        #self.changeRule(RULE_STRING)
        #self.updateUIGrid()

        self.resize_timer.stop()

    def close_tab(self, index):
        self.tab_widget.removeTab(index)

    def show_about(self):
        if not self.is_tab_open("About"):
            label = QLabel("<a href='http://example.com'>Visit our website!</a>", self)
            label.setOpenExternalLinks(True)
            self.add_tab(label, "About")

    def is_tab_open(self, title):
        for index in range(self.tab_widget.count()):
            if self.tab_widget.tabText(index) == title:
                self.tab_widget.setCurrentIndex(index)
                return True
        return False
    
    def add_tab(self, widget, title):
        self.tab_widget.addTab(widget, title)
        self.tab_widget.setCurrentIndex(self.tab_widget.count() - 1)
        
    def show_properties(self):
        if not self.is_tab_open("Properties"):
            widget = QWidget()
            layout = QVBoxLayout()
            cell_size_label = QLabel("Cell Size (1-25):", widget)
            cell_size_spinbox = QSpinBox(widget)
            cell_size_spinbox.setRange(1, 25)
            animation_speed_label = QLabel("Animation Speed (1-30 FPS):", widget)
            animation_speed_spinbox = QSpinBox(widget)
            animation_speed_spinbox.setRange(1, 30)
            save_button = QPushButton("Save", widget)
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
            self.add_tab(widget, "Properties")

    def show_simulator(self):
        show_simulator(self)

def qt_message_handler(mode, context, message):
    if mode == Qt.MessageType.INFO:
        logger.info(message)
    elif mode == Qt.MessageType.WARNING:
        logger.warning(message)
    elif mode == Qt.MessageType.CRITICAL:
        logger.critical(message)
    elif mode == Qt.MessageType.FATAL:
        logger.fatal(message)
    elif mode == Qt.MessageType.DEBUG:
        logger.debug(message)

# After setting up logging and before initializing QApplication
qInstallMessageHandler(qt_message_handler)



if __name__ == '__main__':
    logger.info("Starting application")
    app = QApplication(sys.argv)
    app.setApplicationName("Heaton's Game of Life")
    window = HeatonCA()
    window.show()
    logging.info("Application shutting down")

    level = app.exec()
    utl_settings.save_settings()
    utl_logging.setup_logging()
    utl_logging.delete_old_logs()
    sys.exit(level)
