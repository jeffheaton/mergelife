import sys
from PyQt6.QtWidgets import QApplication, QMainWindow, QTabWidget, QVBoxLayout, QWidget, QLabel

class AboutTab(QWidget):
    def __init__(self):
        super().__init__()

        # Create the QLabel with the hyperlink
        self.label = QLabel("<a href='http://example.com'>Visit our website!</a>", self)
        self.label.setOpenExternalLinks(True)
        
        # Set a layout for the CustomTab and add the label to it
        layout = QVBoxLayout(self)
        layout.addWidget(self.label)

    def on_close(self):
        # Your custom functionality here
        print("The tab is closing!")

    def on_resize(self):
        pass