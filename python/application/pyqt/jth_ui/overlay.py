import logging

from PyQt6.QtGui import QColor, QFont, QFontMetrics, QPainter
from PyQt6.QtWidgets import QGraphicsView

logger = logging.getLogger(__name__)


class OverlayGraphicsView(QGraphicsView):
    def __init__(self, scene, parent=None):
        super().__init__(scene, parent)
        self._parent = parent
        self.message = ""

    def paintEvent(self, event):
        super().paintEvent(event)

        if len(self.message) < 1:
            return

        painter = QPainter(self.viewport())

        # Setting up text properties
        font = QFont("Arial", 20)
        font.setBold(True)
        painter.setFont(font)

        # Get text dimensions
        font_metrics = QFontMetrics(font)
        text_width = font_metrics.horizontalAdvance(self.message)
        text_height = font_metrics.height()

        # Rectangle properties
        padding = 5  # You can adjust the padding if needed
        rect_x = 10  # self.width() - text_width - 2 * padding
        rect_y = 0
        rect_width = text_width + 2 * padding
        rect_height = text_height + padding

        # Draw the black rectangle
        painter.setBrush(QColor(0, 0, 0))
        painter.setPen(QColor(0, 0, 0))  # Set rectangle border color to black
        painter.drawRect(rect_x, rect_y, rect_width, rect_height)

        # Draw the FPS text
        painter.setPen(QColor(255, 0, 0))  # Set text color to white
        painter.drawText(rect_x + padding, text_height, self.message)
