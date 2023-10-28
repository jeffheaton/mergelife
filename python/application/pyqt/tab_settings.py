import logging
import utl_settings
from PyQt6.QtWidgets import (
    QPushButton, QWidget, QFormLayout, QLabel, 
    QSpinBox, QVBoxLayout, QHBoxLayout
)

logger = logging.getLogger(__name__)

class SettingsTab(QWidget):
    def __init__(self, window):
        super().__init__()
        self._window = window
        layout = QVBoxLayout()
        cell_size_label = QLabel("Cell Size (1-25):", self)
        self._cell_size_spinbox = QSpinBox(self)
        self._cell_size_spinbox.setRange(1, 25)
        animation_speed_label = QLabel("Animation Speed (1-30 FPS):", self)
        self._animation_speed_spinbox = QSpinBox(self)
        self._animation_speed_spinbox.setRange(1, 30)
        save_button = QPushButton("Save", self)
        save_button.clicked.connect(self.action_save)
        cancel_button = QPushButton("Cancel", self)
        cancel_button.clicked.connect(lambda: self.action_cancel())  
        button_layout = QHBoxLayout()
        button_layout.addWidget(save_button)
        button_layout.addWidget(cancel_button)
        layout.addWidget(cell_size_label)
        layout.addWidget(self._cell_size_spinbox)
        layout.addWidget(animation_speed_label)
        layout.addWidget(self._animation_speed_spinbox)
        layout.addLayout(button_layout)
        self.setLayout(layout)
        window.add_tab(self, "Settings")
        self._cell_size_spinbox.setValue(utl_settings.settings[utl_settings.CELL_SIZE_KEY])
        self._animation_speed_spinbox.setValue(utl_settings.settings[utl_settings.FPS_KEY])



    def on_close(self):
        # Your custom functionality here
        print("The tab is closing!")

    def action_save(self):
        # Save the values
        utl_settings.settings[utl_settings.CELL_SIZE_KEY] = int(self._cell_size_spinbox.value())
        utl_settings.settings[utl_settings.FPS_KEY] = int(self._animation_speed_spinbox.value())

        # Close the tab
        index = self._window.tab_widget.indexOf(self._window.tab_widget.currentWidget())
        if index != -1:
            self._window.close_tab(index)

    def action_cancel(self):
        index = self._window.tab_widget.indexOf(self._window.tab_widget.currentWidget())
        if index != -1:
            self._window.close_tab(index)

    def on_resize(self):
        pass

