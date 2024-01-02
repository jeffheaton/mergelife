import os

from PyQt6.QtCore import Qt
from PyQt6.QtGui import QPixmap
from PyQt6.QtWidgets import (
    QGridLayout,
    QLabel,
    QPushButton,
    QSizePolicy,
    QVBoxLayout,
    QWidget,
)


class SplashTab(QWidget):
    def __init__(self, window):
        super().__init__()

        self._window = window
        self._image_filename = self._window.app.get_resource_path(
            relative_path="data/heaton_ca_title.png",
            base_path=os.path.abspath(__file__),
        )

        # Load image from the provided path
        self.original_pixmap = QPixmap(self._image_filename)

        # Create main layout
        main_layout = QVBoxLayout()

        # Display image
        self.image_label = QLabel(self)
        self.image_label.setAlignment(Qt.AlignmentFlag.AlignCenter)

        # Add image to main layout
        main_layout.addWidget(self.image_label, 3)

        # Create buttons layout
        buttons_layout = QGridLayout()

        # Create and add buttons to buttons layout
        button_gallary = QPushButton(f"Gallery")
        button_gallary.setSizePolicy(
            QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Expanding
        )
        button_gallary.clicked.connect(self._window.show_gallery)

        button_rule_viewer = QPushButton(f"Rule Viewer")
        button_rule_viewer.setSizePolicy(
            QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Expanding
        )
        button_rule_viewer.clicked.connect(self._window.show_simulator)

        button_evolve = QPushButton(f"Evolve")
        button_evolve.setSizePolicy(
            QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Expanding
        )
        button_evolve.clicked.connect(self._window.show_evolve)

        button_settings = QPushButton(f"Settings")
        button_settings.setSizePolicy(
            QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Expanding
        )
        button_settings.clicked.connect(self._window.show_properties)

        buttons_layout.addWidget(button_gallary, 0, 0)
        buttons_layout.addWidget(button_rule_viewer, 0, 1)
        buttons_layout.addWidget(button_evolve, 1, 0)
        buttons_layout.addWidget(button_settings, 1, 1)

        # Add buttons layout to main layout
        main_layout.addLayout(buttons_layout, 1)

        self.setLayout(main_layout)

        # Version label
        self.version_label = QLabel(f"v{self._window.app.VERSION}", self)
        self.version_label.setStyleSheet(
            "background-color: black; color: white; padding: 5px;"
        )
        self.version_label.setAlignment(Qt.AlignmentFlag.AlignRight)

    def on_close(self):
        pass
        # Your custom functionality here

    def on_resize(self):
        pass

    def showEvent(self, event):
        super().showEvent(event)
        # Position the version label on the upper-right hand corner
        self.version_label.move(self.width() - self.version_label.width() - 10, 10)
        # Update the image once the window is shown
        self.update_image()

    def update_image(self):
        scaled_pixmap = self.original_pixmap.scaled(
            self.image_label.size(), Qt.AspectRatioMode.KeepAspectRatio
        )
        self.image_label.setPixmap(scaled_pixmap)

    def resizeEvent(self, event):
        # Reposition the version label when the window is resized
        self.version_label.move(self.width() - self.version_label.width() - 10, 10)
        self.update_image()
        super().resizeEvent(event)
