import os
os.environ['QT_MAC_WANTS_LAYER'] = '1'
import sys
import cv2
import random
from PyQt6.QtCore import Qt
from PyQt6.QtWidgets import QApplication, QMainWindow, \
    QToolBar, QPushButton, QComboBox, QMessageBox, \
    QMenu, QMenuBar, QWidget, QVBoxLayout, QLabel, \
    QGraphicsView, QGraphicsScene
from PyQt6 import QtWidgets
from PyQt6.QtGui import QImage, QPixmap
from PyQt6.QtGui import QAction
from PyQt6.QtCore import QTimer, QRectF
from PyQt6.QtGui import QBrush, QColor
from mergelife import new_ml_instance, update_step
import numpy as np

CELL_SIZE = 5
INITIAL_GRID_WIDTH = 200
INITIAL_GRID_HEIGHT = 100

RULES = [
    "e542-5f79-9341-f31e-6c6b-7f08-8773-7068",
    "a07f-c000-0000-0000-0000-0000-ff80-807f",
    "ea44-55df-9025-bead-5f6e-45ca-6168-275a"
]

RULE_STRING = "ea44-55df-9025-bead-5f6e-45ca-6168-275a"  # One of the known MergeLife rule strings. You can replace this.

class HeatonCA(QMainWindow):
    def __init__(self):
        super().__init__()
        self.running = False
        self.setWindowTitle("HeatonCA")
        self.setGeometry(100, 100, 
            INITIAL_GRID_WIDTH * CELL_SIZE, 
            INITIAL_GRID_HEIGHT * CELL_SIZE)
        
        self.render_buffer = None
        self.display_buffer = None

        self.setup_mac_menu()
        self.initUI()
        self.changeRule(RULE_STRING)


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

        # Initialize central widget and layout
        central_widget = QWidget(self)
        self.layout = QVBoxLayout(central_widget)
        self.layout.setContentsMargins(0, 0, 0, 0)
        self.layout.setSpacing(0)
        self.setCentralWidget(central_widget)

        # QGraphicsView and QGraphicsScene
        self.scene = QGraphicsScene()
        self.view = QGraphicsView(self.scene, self)
        self.layout.addWidget(self.view)

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

        self.changeRule(RULE_STRING)
        self.updateUIGrid()

        self.resize_timer.stop()

    def changeRule(self, ruleText):
        size = self.size()
        width = size.width()
        height = size.height()
    
        self.grid_width = int(width / CELL_SIZE)
        self.grid_height = int(height / CELL_SIZE)
    
        self.ml = new_ml_instance(
            self.grid_height,
            self.grid_width,
            ruleText)
                
        self.render_buffer = np.zeros((self.grid_height * CELL_SIZE, self.grid_width * CELL_SIZE, 3), 
                                      dtype=np.uint8)
        
        height, width, channel = self.render_buffer.shape
        bytes_per_line = 3 * width

        self.display_buffer = QImage(self.render_buffer.data, self.grid_width * CELL_SIZE, self.grid_height * CELL_SIZE, 3 * self.grid_width * CELL_SIZE, QImage.Format.Format_RGB888)
        self.pixmap_buffer = self.scene.addPixmap(QPixmap.fromImage(self.display_buffer))

    def updateUIGrid(self):
        grid = self.ml['lattice'][0]['data']
        for x in range(self.grid_width):
            for y in range(self.grid_height):
                color = grid[y][x]
                self.render_buffer[y*CELL_SIZE:(y+1)*CELL_SIZE, x*CELL_SIZE:(x+1)*CELL_SIZE] = color 

        # Update QPixmap for the existing QGraphicsPixmapItem
        self.pixmap_buffer.setPixmap(QPixmap.fromImage(self.display_buffer))
        
        self.view.fitInView(self.scene.sceneRect(), mode=Qt.AspectRatioMode.IgnoreAspectRatio)
        


if __name__ == '__main__':
    app = QApplication(sys.argv)
    app.setApplicationName("Heaton's Game of Life")
    window = HeatonCA()
    window.show()
    sys.exit(app.exec())

