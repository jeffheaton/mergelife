from PyQt6.QtWidgets import (
    QToolBar, QPushButton, QComboBox, QWidget,
    QVBoxLayout, QGraphicsView, QGraphicsScene
)

class TabGraphic(QWidget):
    def __init__(self, window):
        super().__init__()
        self._window = window