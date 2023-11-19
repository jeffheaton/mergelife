import sys
from PyQt6.QtWidgets import QApplication, QMainWindow, QTabWidget, QVBoxLayout, QWidget, QLabel
from PyQt6.QtWidgets import QTableWidgetItem, QVBoxLayout, QTableWidget
from PyQt6.QtGui import QColor, QFont
import random
import mergelife.mergelife as mergelife
import ctypes

class RuleTab(QWidget):
    COLOR_NAMES = ['Black','Red','Green','Yellow','Blue','Purple','Cyan','White']
    DUMMY_HW = 10
    
    def __init__(self, rule):
        super().__init__()
        self._rule = rule
        self._ml = self._ml = mergelife.new_ml_instance(RuleTab.DUMMY_HW,RuleTab.DUMMY_HW,rule)

        # Create the QLabel for the rule
        rule_label = QLabel(f"Rule: {rule}", self)
        font = rule_label.font()
        font.setPointSize(font.pointSize() + 2)  # Increase font size by 2 points
        rule_label.setFont(font)

        # Create the QTableWidget
        self.table = QTableWidget(self)

        # Set the number of rows and columns
        self.table.setRowCount(8)
        self.table.setColumnCount(7)

        # Set the column headers
        headers = ["High (α)", "Range", "Key Color", "Percent (β)", "Index (γ)", "Octet-1", "Octet-2"]
        self.table.setHorizontalHeaderLabels(headers)

        # Populate the table with random data
        self.populate_table()

        # Set the layout and add the label and table widget
        layout = QVBoxLayout()
        layout.addWidget(rule_label)
        layout.addWidget(self.table)

        self.setLayout(layout)



    def populate_table(self):
        sorted_rule = self._ml['sorted_rule']

        low = 0
        for i, item in enumerate(sorted_rule):
            limit, pct, cidx = item

            if pct < 0:
                cidx2 = cidx + 1
                if cidx2 >= len(RuleTab.COLOR_NAMES):
                    cidx2 = 0
            else:
                cidx2 = cidx
            
            key_color = RuleTab.COLOR_NAMES[cidx2]
            color_item = QTableWidgetItem(key_color)
            color_item.setBackground(QColor(key_color))
            color_item.setForeground(QColor('white' if cidx2 == 0 else 'black'))

            self.table.setItem(i, 0, QTableWidgetItem(str(limit)))  # Ensure limit is string type
            self.table.setItem(i, 1, QTableWidgetItem(f"{low}-{limit-1}"))
            self.table.setItem(i, 2, color_item)
            self.table.setItem(i, 3, QTableWidgetItem(f"{int(pct*100)}%"))
            self.table.setItem(i, 4, QTableWidgetItem(f"{cidx}({RuleTab.COLOR_NAMES[cidx]})"))
            o1 = int(limit/8)
            if pct > 0:
                o2 = int(pct * 127)
            else:
                o2 = int(pct * 128)
            o2 = ctypes.c_byte(o2).value


            self.table.setItem(i, 5, QTableWidgetItem(f"0x{abs(o1):02x} ({o1})"))
            self.table.setItem(i, 6, QTableWidgetItem(f"0x{abs(o2):02x} ({o2})"))
            print(limit, pct, cidx)
            low = limit

        # Resize rows to content
        self.table.resizeRowsToContents()
        

    def on_close(self):
        pass

    def on_resize(self):
        pass