import logging
import logging.config
import logging.handlers
import os

from PyQt6.QtCore import Qt, QtMsgType, qInstallMessageHandler
from PyQt6.QtWidgets import QApplication, QFileDialog, QMainWindow, QMessageBox
from jth_ui import app_jth

logger = logging.getLogger(__name__)


class MainWindowJTH(QMainWindow):
    def __init__(self, app):
        super().__init__()
        self.app = app
        self.open_extensions = "All Files (*.*)"

        # Enable the main window to accept drops
        self.setAcceptDrops(True)

    def resizeEvent(self, event):
        """This method is called whenever the window is resized."""
        self.resize_timer.start()  # Restart the timer every time this event is triggered
        self.last_size = event.size()  # Store the latest size
        super().resizeEvent(event)

    def finished_resizing(self):
        """This method will be called approximately 300ms after the last resize event."""
        try:
            index = self._tab_widget.currentIndex()
            if index != -1:
                tab = self._tab_widget.widget(index)
                tab.on_resize()
        except Exception as e:
            logger.error("Error during resize", exc_info=True)

        self.resize_timer.stop()

    def close_tab(self, index):
        try:
            tab = self._tab_widget.widget(index)
            tab.on_close()
            self._tab_widget.removeTab(index)
            tab.deleteLater()
        except Exception as e:
            logger.error("Error during tab close", exc_info=True)

    def display_message_box(self, text):
        msg = QMessageBox(self)
        msg.setText(str(text))
        msg.exec()

    def is_tab_open(self, title):
        for index in range(self._tab_widget.count()):
            if self._tab_widget.tabText(index) == title:
                self._tab_widget.setCurrentIndex(index)
                return True
        return False

    def add_tab(self, widget, title):
        self._tab_widget.addTab(widget, title)
        self._tab_widget.setCurrentIndex(self._tab_widget.count() - 1)

    def close_current_tab(self):
        # Close the tab
        index = self._tab_widget.indexOf(self._tab_widget.currentWidget())
        if index != -1:
            self.close_tab(index)

    def dragEnterEvent(self, event):
        if event.mimeData().hasUrls():
            urls = event.mimeData().urls()
            if urls and urls[0].isLocalFile():
                file_path = urls[0].toLocalFile()
                if file_path.lower().endswith(self._drop_ext):
                    event.accept()
                    return
        event.ignore()

    def dropEvent(self, event):
        if event.mimeData().hasUrls():
            event.setDropAction(Qt.DropAction.CopyAction)
            urls = event.mimeData().urls()

            if urls and urls[0].isLocalFile():
                file_path = urls[0].toLocalFile()
                logger.info(f"Accepted drag and drop: {file_path}")
                self.open_file(file_path)
                event.accept()
            else:
                event.ignore()

    def open_file(self, file_path):
        try:
            folder = os.path.dirname(file_path)
            self.app.state[app_jth.STATE_LAST_FOLDER] = folder
        except FileNotFoundError as e:
            logger.error("Error during load (FileNotFoundError)", exc_info=True)
            self.display_message_box("Unable to load file. (FileNotFoundError)")
        except PermissionError as e:
            logger.error("Error during load (PermissionError)", exc_info=True)
            self.display_message_box("Unable to load file. (PermissionError)")
        except IsADirectoryError as e:
            logger.error("Error during load (IsADirectoryError)", exc_info=True)
            self.display_message_box("Unable to load file. (IsADirectoryError)")
        except FileExistsError as e:
            logger.error("Error during load (FileExistsError)", exc_info=True)
            self.display_message_box("Unable to load file. (FileExistsError)")
        except OSError as e:
            logger.error("Error during load (OSError)", exc_info=True)
            self.display_message_box("Unable to load file. (OSError)")
        except Exception as e:
            logger.error("Error during load", exc_info=True)
            self.display_message_box("Unable to load file.")

    def open_action(self):
        home_directory = os.path.expanduser("~")
        documents_path = os.path.join(home_directory, "Documents")
        path = self.app.state.get(app_jth.STATE_LAST_FOLDER, documents_path)
        fileName, _ = QFileDialog.getOpenFileName(
            self,
            "Open File",
            path,
            self.open_extensions,
        )
        if fileName:
            self.open_file(fileName)
        self.app.save_state()
