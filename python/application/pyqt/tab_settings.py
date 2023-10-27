import logging
import utl_settings
from PyQt6.QtWidgets import (
    QPushButton, QWidget, QVBoxLayout, QLabel, 
    QSpinBox, QVBoxLayout, QHBoxLayout
)

logger = logging.getLogger(__name__)

_window = None
_cell_size_spinbox = None
_animation_speed_spinbox = None

def action_save():
    # Save the values
    utl_settings.settings[utl_settings.CELL_SIZE_KEY] = int(_cell_size_spinbox.value())
    utl_settings.settings[utl_settings.FPS_KEY] = int(_animation_speed_spinbox.value())

    # Close the tab
    index = _window.tab_widget.indexOf(_window.tab_widget.currentWidget())
    if index != -1:
        _window.tab_widget.removeTab(index)

def action_cancel():
    index = _window.tab_widget.indexOf(_window.tab_widget.currentWidget())
    if index != -1:
        _window.tab_widget.removeTab(index)

def show_settings(window):
    global _window, _cell_size_spinbox, _animation_speed_spinbox



    _window = window
    logger.info("show settings")
    if window.is_tab_open("Properties"):
        return
    logger.info("Opened settings")
    widget = QWidget()
    layout = QVBoxLayout()
    cell_size_label = QLabel("Cell Size (1-25):", widget)
    _cell_size_spinbox = QSpinBox(widget)
    _cell_size_spinbox.setRange(1, 25)
    animation_speed_label = QLabel("Animation Speed (1-30 FPS):", widget)
    _animation_speed_spinbox = QSpinBox(widget)
    _animation_speed_spinbox.setRange(1, 30)
    save_button = QPushButton("Save", widget)
    save_button.clicked.connect(action_save)
    cancel_button = QPushButton("Cancel", widget)
    cancel_button.clicked.connect(lambda: action_cancel())  
    button_layout = QHBoxLayout()
    button_layout.addWidget(save_button)
    button_layout.addWidget(cancel_button)
    layout.addWidget(cell_size_label)
    layout.addWidget(_cell_size_spinbox)
    layout.addWidget(animation_speed_label)
    layout.addWidget(_animation_speed_spinbox)
    layout.addLayout(button_layout)
    widget.setLayout(layout)
    window.add_tab(widget, "Settings")
    _cell_size_spinbox.setValue(utl_settings.settings[utl_settings.CELL_SIZE_KEY])
    _animation_speed_spinbox.setValue(utl_settings.settings[utl_settings.FPS_KEY])


