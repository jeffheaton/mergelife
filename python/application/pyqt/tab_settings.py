import logging
import utl_settings
from PyQt6.QtWidgets import (
    QPushButton, QWidget, QFormLayout, QLabel, 
    QSpinBox, QVBoxLayout, QHBoxLayout, QCheckBox
)

logger = logging.getLogger(__name__)

class SettingsTab(QWidget):
    def __init__(self, window):
        super().__init__()
        self._window = window

        # Close simulator, if open
        self._window.close_simulator_tabs()

        # Create widgets
        cell_size_label = QLabel("Cell Size (1-25):", self)
        self._cell_size_spinbox = QSpinBox(self)
        self._cell_size_spinbox.setRange(1, 25)
        animation_speed_label = QLabel("Animation Speed (1-30 FPS):", self)
        self._animation_speed_spinbox = QSpinBox(self)
        self._animation_speed_spinbox.setRange(1, 30)
        self._display_fps_checkbox = QCheckBox("Display FPS/Steps", self)

        # Create button layout
        save_button = QPushButton("Save", self)
        save_button.clicked.connect(self.action_save)
        cancel_button = QPushButton("Cancel", self)
        cancel_button.clicked.connect(lambda: self.action_cancel())  
        button_layout = QHBoxLayout()
        button_layout.addWidget(save_button)
        button_layout.addWidget(cancel_button)

        # Form layout for the options
        form_layout = QFormLayout()
        form_layout.addRow(cell_size_label, self._cell_size_spinbox)
        form_layout.addRow(animation_speed_label, self._animation_speed_spinbox)
        form_layout.addRow(self._display_fps_checkbox)

        # Main layout
        layout = QVBoxLayout()
        layout.addLayout(form_layout)
        layout.addLayout(button_layout)
        self.setLayout(layout)

        window.add_tab(self, "Settings")
        self._cell_size_spinbox.setValue(utl_settings.get_int(utl_settings.CELL_SIZE_KEY))
        self._animation_speed_spinbox.setValue(utl_settings.get_int(utl_settings.FPS_KEY))
        self._display_fps_checkbox.setChecked(utl_settings.get_bool(utl_settings.FPS_OVERLAY))

    def on_close(self):
        self._window.close_simulator_tabs()

    def action_save(self):
        self.save_values()
        self._window.close_current_tab()

    def action_cancel(self):
        self._window.close_current_tab()

    def on_resize(self):
        pass

    def save_values(self):
        utl_settings.settings[utl_settings.CELL_SIZE_KEY] = int(self._cell_size_spinbox.value())
        utl_settings.settings[utl_settings.FPS_KEY] = int(self._animation_speed_spinbox.value())
        utl_settings.settings[utl_settings.FPS_OVERLAY] = bool(self._display_fps_checkbox.isChecked())
        utl_settings.save_settings()
