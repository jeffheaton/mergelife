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
import utl_logging
import utl_settings
import tab_settings
import tab_gallery
from tab_about import AboutTab
from tab_rule import RuleTab
from tab_evolve import EvolveTab
import webbrowser
import tab_splash
import utl_env

logger = logging.getLogger(__name__)

SIMULATOR_NAME = "Simulator"
APP_NAME = "HeatonCA"

class HeatonCA(QMainWindow):
    def __init__(self):
        super().__init__()
        self.running = False
        self.setWindowTitle(APP_NAME)
        self.setGeometry(100, 100, 1000, 500)
        
        self.render_buffer = None
        self.display_buffer = None

        self.setup_mac_menu()
        self.initUI()

    def setup_mac_menu(self):
        # Create a main menu bar
        #if platform.uname().system.startswith('Darw') :
        #    self.menubar = QMenuBar() # parentless menu bar for Mac OS
        #else:
        #    self.menubar = self.menuBar() # refer to the default one

        self.menubar = QMenuBar() #self.menuBar()

        # Create the app menu and add it to the menu bar
        app_menu = QMenu(APP_NAME, self)
  

        # Add items to the app menu
        about_action = QAction(f"About {APP_NAME}", self)
        app_menu.addAction(about_action)
        self.about_menu = QMenu("About", self)
        about_action.triggered.connect(self.show_about)


        preferences_action = QAction("Settings...", self)
        app_menu.addAction(preferences_action)
        preferences_action.triggered.connect(self.show_properties)

        exit_action = QAction("Quit", self)
        exit_action.triggered.connect(self.close)
        app_menu.addAction(exit_action)

        # File menu
        self._file_menu = QMenu('File', self)
        
        # Close Window action
        closeAction = QAction('Close Window', self)
        closeAction.setShortcut(QKeySequence(QKeySequence.StandardKey.Close))
        closeAction.triggered.connect(self.close)
        self._file_menu.addAction(closeAction)

        # Edit menu
        self._edit_menu = QMenu('Edit', self)
        cutAction = QAction('Cut', self)
        cutAction.setShortcut(QKeySequence(QKeySequence.StandardKey.Cut))
        self._edit_menu.addAction(cutAction)

        copyAction = QAction('Copy', self)
        copyAction.setShortcut(QKeySequence(QKeySequence.StandardKey.Copy))
        self._edit_menu.addAction(copyAction)

        pasteAction = QAction('Paste', self)
        pasteAction.setShortcut(QKeySequence(QKeySequence.StandardKey.Paste))
        self._edit_menu.addAction(pasteAction)

        # Simulate menu
        #self.simulator_menu = QMenu("Simulator", self)
        #simulator_action = QAction("Show Simulator", self)
        #simulator_action.triggered.connect(self.show_simulator)
        #self.simulator_menu.addAction(simulator_action)

        #gallery_action = QAction("Show Gallery", self)
        #gallery_action.triggered.connect(self.show_gallery)
        #self.simulator_menu.addAction(gallery_action)

        #evolve_action = QAction("Evolve", self)
        #evolve_action.triggered.connect(self.show_evolve)
        #self.simulator_menu.addAction(evolve_action)

        # Help menu
        self._help_menu = QMenu("Help", self)
        tutorial_action = QAction("Tutorial", self)
        tutorial_action.triggered.connect(self.open_tutorial)
        self._help_menu.addAction(tutorial_action)

        #
        self.menubar.addMenu(app_menu)
        self.menubar.addMenu(self._file_menu)
        self.menubar.addMenu(self._edit_menu)
        #self.menubar.addMenu(self.simulator_menu)
        self.menubar.addMenu(self._help_menu)

    def initUI(self):
        self._tab_widget = QTabWidget()
        self._tab_widget.setTabsClosable(True)
        self._tab_widget.tabCloseRequested.connect(self.close_tab)
        self.setCentralWidget(self._tab_widget)

        # Configure the resize timer
        self.resize_timer = QTimer(self)
        self.resize_timer.timeout.connect(self.finished_resizing)
        self.resize_timer.setInterval(300)  # 300 milliseconds

        # Configure the resize timer
        self._background_timer = QTimer(self)
        self._background_timer.timeout.connect(self.background_timer)
        self._background_timer.setInterval(1000)  # 1 second
        self._background_timer.start()

    def displayMessageBox(self, text):
        msg = QMessageBox(self)
        msg.setText(text)
        msg.exec()

    def background_timer(self):
        if self._tab_widget.count() == 0:
            window.add_tab(tab_splash.SplashTab(window), "Welcome to HeatonCA")

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
            logger.error("Error during resize", e)

        self.resize_timer.stop()

    def close_tab(self, index):
        try:
            tab = self._tab_widget.widget(index)
            tab.on_close()
            self._tab_widget.removeTab(index)
            tab.deleteLater()
        except Exception as e:
            logger.error("Error during tab close", e)

    def show_about(self):
        try:
            if not self.is_tab_open("About"):
                self.add_tab(AboutTab(), "About HeatonCA")
        except Exception as e:
            logger.error("Error during about open", e)

    def show_evolve(self):
        try:
            if not self.is_tab_open("Evolve"):
                self.add_tab(EvolveTab(), "Evolve")
        except Exception as e:
            logger.error("Error during evolve open", e)

    def show_rule(self, rule):
        try:
            if not self.is_tab_open("Rule"):
                self.add_tab(RuleTab(rule), "Rule")
        except Exception as e:
            logger.error("Error during show rule", e)
            
    def is_tab_open(self, title):
        for index in range(self._tab_widget.count()):
            if self._tab_widget.tabText(index) == title:
                self._tab_widget.setCurrentIndex(index)
                return True
        return False
    
    def add_tab(self, widget, title):
        self._tab_widget.addTab(widget, title)
        self._tab_widget.setCurrentIndex(self._tab_widget.count() - 1)
        
    def show_properties(self):
        try:
            if not window.is_tab_open("Preferences"):
                self.add_tab(tab_settings.SettingsTab(self), "Preferences")
        except Exception as e:
            logger.error("Error during show properties", e)

    def show_simulator(self):
        try:
            if not window.is_tab_open("Simulator"):
                self.add_tab(tab_simulate.TabSimulate(self), SIMULATOR_NAME)
        except Exception as e:
            logger.error("Error during resize", e)

    def show_gallery(self):
        try:
            if not window.is_tab_open("Gallery"):
                self.add_tab(tab_gallery.GalleryTab(self), "Gallery")
        except Exception as e:
            logger.error("Error during show gallery", e)

    def close_simulator_tabs(self):
        try:
            logger.info("Closing any simulator tabs due to config change")
            index = 0
            while index < self._tab_widget.count():
                if self._tab_widget.tabText(index) == SIMULATOR_NAME:
                    self.close_tab(index)
                    # Since we've removed a tab, the indices shift, so we don't increase the index in this case
                    continue
                index += 1
        except Exception as e:
            logger.error("Error forcing simulator close", e)

    def find_simulator_tab(self):
        index = 0
        while index < self._tab_widget.count():
            if self._tab_widget.tabText(index) == SIMULATOR_NAME:
                return self._tab_widget.widget(index)
            index += 1
        return None

    def close_current_tab(self):
        # Close the tab
        index = self._tab_widget.indexOf(self._tab_widget.currentWidget())
        if index != -1:
            self.close_tab(index)

    def display_rule(self, rule):
        self.close_simulator_tabs()
        sim = tab_simulate.TabSimulate(self)
        self.add_tab(sim, SIMULATOR_NAME)
        sim._force_rule = rule

    def open_tutorial(self):
        webbrowser.open('http://www.heatonresearch.com/mergelife')


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



if __name__ == '__main__':
    print(f"Logs path: {const_values.LOG_DIR}")
    print(f"Settings path: {const_values.SETTING_DIR}")
    print(f"Settings file: {const_values.SETTING_FILE}")

    try:
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
        
        app = QApplication(sys.argv)
        app.setApplicationName("Heaton's Game of Life")
        window = HeatonCA()
        window.show()
    except Exception as e:
        logger.error("Error during startup", e)
    
    try:
        level = app.exec()
    except Exception as e:
        logger.error("Error running app", e)

    try:
        utl_settings.save_settings()
        utl_logging.setup_logging()
        utl_logging.delete_old_logs()
        logging.info("Application shutting down")
        sys.exit(level)
    except Exception as e:
        logger.error("Error shutting down resize", e)

