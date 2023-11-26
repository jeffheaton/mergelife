import logging

import numpy as np
from PyQt6.QtCore import QDateTime, Qt, QTimer
from PyQt6.QtGui import QColor, QFont, QFontMetrics, QImage, QPainter, QPixmap
from PyQt6.QtWidgets import QGraphicsScene, QGraphicsView, QVBoxLayout, QWidget

logger = logging.getLogger(__name__)


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


class TabGraphic(QWidget):
    def __init__(self, window):
        super().__init__()
        self._window = window
        self._target_fps = None
        self._running = False
        self._force_rule = False
        self._force_update = 0
        self._frame_count = 0
        self._scene = None
        self._view = None

    def init_graphics(self, layout=None):
        if layout is None:
            # Initialize central widget and layout
            self._layout = QVBoxLayout(self)
            self._layout.setContentsMargins(0, 0, 0, 0)
            self._layout.setSpacing(0)
        else:
            self._layout = layout

    def init_fps(self):
        # FPS
        self._frame_count = 0
        self._fps = 0
        self._fps_timer = QTimer(self)
        self._fps_timer.timeout.connect(self.computeFPS)
        self._fps_timer.start(1000)  # Every second
        self._steps = 0

    def init_animate(self, target_fps):
        # Animation timer
        self._target_fps = target_fps
        self._timer_interval = int(1000 / self._target_fps)
        self._last_event_time = QDateTime.currentMSecsSinceEpoch()
        self._timer = QTimer(self)
        self._timer.timeout.connect(self._timer_proc)
        self._timer.start(self._timer_interval)
        self._force_update = 0

    def on_close(self):
        self._timer.stop()
        self._scene.clear()
        logger.info("Closed graphic tab")

    def create_graphic(self, height=None, width=None, buffer=None, fps_overlay=False):
        if buffer is None:
            self._render_buffer = np.zeros((height, width, 3), dtype=np.uint8)
        else:
            self._render_buffer = buffer

        height, width, _ = self._render_buffer.shape

        self._display_buffer = QImage(
            self._render_buffer.data,
            width,
            height,
            3 * width,
            QImage.Format.Format_RGB888,
        )
        # close out old scene, if there is one
        if self._scene:
            self._scene.clear()
            self._view.close()
            self._layout.removeWidget(self._view)

        # QGraphicsView and QGraphicsScene
        self._scene = QGraphicsScene(self)
        if fps_overlay:
            self._view = FPSGraphicsView(self._scene, self)
        else:
            self._view = QGraphicsView(self._scene, self)

        self._view.setHorizontalScrollBarPolicy(Qt.ScrollBarPolicy.ScrollBarAlwaysOff)
        self._view.setVerticalScrollBarPolicy(Qt.ScrollBarPolicy.ScrollBarAlwaysOff)

        self._layout.addWidget(
            self._view
        )  # Add the view to the layout after the toolbar
        self._scene.setBackgroundBrush(Qt.GlobalColor.black)

        self._pixmap_buffer = self._scene.addPixmap(
            QPixmap.fromImage(self._display_buffer)
        )

    def update_graphic(self, resize=True):
        # Update QPixmap for the existing QGraphicsPixmapItem
        self._pixmap_buffer.setPixmap(QPixmap.fromImage(self._display_buffer))

        if resize:
            self._view.fitInView(
                self._scene.sceneRect(), mode=Qt.AspectRatioMode.IgnoreAspectRatio
            )

        # FPS
        self._frame_count += 1

    def _timer_proc(self):
        # Throttle to the requested FPS rate
        current_time = QDateTime.currentMSecsSinceEpoch()
        elapsed_time = current_time - self._last_event_time

        if (
            elapsed_time < self._timer_interval * 10
        ):  # if not too much time has passed since last event
            self.timer_step()
            if self._running:
                self.running_step()
                self._steps += 1

        self._last_event_time = current_time

    def timer_step():
        """Called for every frame, regardless of if running."""
        pass

    def running_step():
        """Called for every frame, only if running."""
        pass

    def computeFPS(self):
        self._fps = self._frame_count
        self._frame_count = 0
        self._view.viewport().update()  # Trigger a redraw to show the updated FPS

    def start_game(self):
        self._running = True

    def stop_game(self):
        self._running = False
