import sys
import os
import logging
from PyQt6.QtCore import Qt, QTimer, qInstallMessageHandler, QtMsgType
from PyQt6.QtGui import QAction, QKeySequence
from PyQt6.QtWidgets import (
    QApplication, QMainWindow, QMessageBox, QMenu, 
    QMenuBar, QLabel, QTabWidget, QLineEdit
)

import tab_simulate
from PyQt6.QtCore import QCoreApplication, Qt, qInstallMessageHandler
import logging.config
import const_values

import logging
import logging.handlers
import jth_ui.utl_logging as utl_logging
import utl_settings as utl_settings
import tab_settings
import tab_gallery
from tab_about import AboutTab
from tab_rule import RuleTab
from tab_evolve import EvolveTab
import webbrowser
import tab_splash
import jth_ui.utl_env as utl_env

logger = logging.getLogger(__name__)

class MainWindowJTH(QMainWindow):
    def __init__(self):
        super().__init__()

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

    def displayMessageBox(self, text):
        msg = QMessageBox(self)
        msg.setText(text)
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
                event.accept()
            else:
                event.ignore()



def qt_message_handler(mode, context, message):
    if mode == QtMsgType.QtInfoMsg:
        logger.info(message)
    elif mode == QtMsgType.QtWarningMsg:
        logger.warning(message)
    elif mode == QtMsgType.QtCriticalMsg:
        logger.critical(message)
    elif mode == QtMsgType.QtFatalMsg:
        logger.fatal(message)
    elif mode == QtMsgType.QtDebugMsg:
        logger.debug(message)

# After setting up logging and before initializing QApplication
qInstallMessageHandler(qt_message_handler)

def app_startup(app_name):
    global _app
    print(f"Logs path: {const_values.LOG_DIR}")
    print(f"Settings path: {const_values.SETTING_DIR}")
    print(f"Settings file: {const_values.SETTING_FILE}")

    utl_settings.load_settings()
    utl_logging.setup_logging()
    utl_logging.delete_old_logs()

    logging.info("Application starting up")
    s = utl_env.get_system_name()
    logging.info(f"System: {s}")
    logging.info(f"Pyinstaller: {utl_env.is_pyinstaller_bundle()}")
    z=os.path.expanduser("~")
    logging.info(f"User: {z}")
    if s=="osx":
        logging.info(f"Sandbox mode: {utl_env.is_sandboxed()}")
    
    _app = QApplication(sys.argv)
    _app.setApplicationName(app_name)
    return _app

def app_shutdown():
    utl_settings.save_settings()
    utl_logging.setup_logging()
    utl_logging.delete_old_logs()
    logging.info("Application shutting down")

    





