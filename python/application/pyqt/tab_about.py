from PyQt6.QtWidgets import QApplication, QMainWindow, QTabWidget, QVBoxLayout, QWidget, QLabel
from PyQt6.QtCore import Qt
import const_values

class AboutTab(QWidget):
    def __init__(self):
        super().__init__()

        text = f"""
<H1>{const_values.APP_NAME} {const_values.VERSION}</H1>
{const_values.COPYRIGHT}
<br>
<hr>
This program implements the cellular automata described in the paper:<br>
Heaton, J. March (2018). Evolving continuous cellular automata for aesthetic objectives. <i>Genetic Programming Evolvable Machines<i>.<br><a href='https://doi.org/10.1007/s10710-018-9336-1'>https://doi.org/10.1007/s10710-018-9336-1</a>
<hr>
Log path: {const_values.LOG_DIR}
""" 

        # Create the QLabel with the hyperlink
        self.label = QLabel(text, self)
        self.label.setOpenExternalLinks(True)
        
        # Set a layout for the CustomTab and add the label to it
        layout = QVBoxLayout(self)
        layout.addWidget(self.label)

        # Align the content to the top
        layout.setAlignment(Qt.AlignmentFlag.AlignTop)

    def on_close(self):
        # Your custom functionality here
        print("The tab is closing!")

    def on_resize(self):
        pass
