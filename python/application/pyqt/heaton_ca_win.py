import logging
import logging.config
import logging.handlers
import webbrowser

from PyQt6.QtCore import QTimer
from PyQt6.QtGui import QAction, QKeySequence
from PyQt6.QtWidgets import QMenu, QMenuBar, QTabWidget

import tab_gallery
import tab_settings
import tab_simulate
import tab_splash
from jth_ui.window_jth import MainWindowJTH
from tab_about import AboutTab
from tab_evolve import EvolveTab
from tab_rule import RuleTab

logger = logging.getLogger(__name__)

SIMULATOR_NAME = "Simulator"


class WindowHeatonCA(MainWindowJTH):
    def __init__(self, app, app_name):
        super().__init__(app)
        self.running = False
        self.setWindowTitle(app_name)
        self.setGeometry(100, 100, 1000, 500)

        self.render_buffer = None
        self.display_buffer = None

        self._drop_ext = (".png", ".jpg", ".jpeg", ".bmp", ".gif")

        self.setup_menu()
        self.initUI()

        # Enable the main window to accept drops
        self.setAcceptDrops(True)

    def setup_menu(self):
        self.menubar = self.menuBar()

        # Add items to the app menu
        if self.app.get_system_name() == "osx":
            # Create the app menu and add it to the menu bar
            app_menu = QMenu(self.app.APP_NAME, self)
            about_action = QAction(f"About {self.app.APP_NAME}", self)
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
        self._file_menu = QMenu("File", self)

        if self.app.get_system_name() == "osx":
            # Close Window action
            closeAction = QAction("Close Window", self)
            closeAction.setShortcut(QKeySequence(QKeySequence.StandardKey.Close))
            closeAction.triggered.connect(self.close)
            self._file_menu.addAction(closeAction)
        else:
            preferences_action = QAction("Settings...", self)
            self._file_menu.addAction(preferences_action)
            preferences_action.triggered.connect(self.show_properties)

            # Create a "Exit" action
            exit_action = QAction("Exit", self)
            exit_action.triggered.connect(self.close)
            self._file_menu.addAction(exit_action)


        # Edit menu
        self._edit_menu = QMenu("Edit", self)
        cutAction = QAction("Cut", self)
        cutAction.setShortcut(QKeySequence(QKeySequence.StandardKey.Cut))
        self._edit_menu.addAction(cutAction)

        copyAction = QAction("Copy", self)
        copyAction.setShortcut(QKeySequence(QKeySequence.StandardKey.Copy))
        self._edit_menu.addAction(copyAction)

        pasteAction = QAction("Paste", self)
        pasteAction.setShortcut(QKeySequence(QKeySequence.StandardKey.Paste))
        self._edit_menu.addAction(pasteAction)

        # Help menu
        self._help_menu = QMenu("Help", self)
        tutorial_action = QAction("Tutorial", self)
        tutorial_action.triggered.connect(self.open_tutorial)
        self._help_menu.addAction(tutorial_action)

        if self.app.get_system_name() == "windows":
            about_action = QAction("About", self)
            about_action.triggered.connect(self.show_about)
            self._help_menu.addAction(about_action)
        #
        if self.app.get_system_name() == "osx":
            self.menubar.addMenu(app_menu)
        self.menubar.addMenu(self._file_menu)
        self.menubar.addMenu(self._edit_menu)
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

    def background_timer(self):
        if self._tab_widget.count() == 0:
            self.add_tab(tab_splash.SplashTab(self), "Welcome to HeatonCA")

    def show_about(self):
        try:
            if not self.is_tab_open("About"):
                self.add_tab(AboutTab(), "About HeatonCA")
        except Exception as e:
            logger.error("Error during about open", exc_info=True)

    def show_evolve(self):
        try:
            if not self.is_tab_open("Evolve"):
                self.add_tab(EvolveTab(), "Evolve")
        except Exception as e:
            logger.error("Error during evolve open", exc_info=True)

    def show_rule(self, rule):
        try:
            if not self.is_tab_open("Rule"):
                self.add_tab(RuleTab(rule), "Rule")
        except Exception as e:
            logger.error("Error during show rule", exc_info=True)

    def show_properties(self):
        try:
            if not self.is_tab_open("Preferences"):
                self.add_tab(tab_settings.SettingsTab(self), "Preferences")
        except Exception as e:
            logger.error("Error during show properties", exc_info=True)

    def show_simulator(self):
        try:
            if not self.is_tab_open("Simulator"):
                self.add_tab(tab_simulate.TabSimulate(self), SIMULATOR_NAME)
        except Exception as e:
            logger.error("Error during show simulator", exc_info=True)

    def show_gallery(self):
        try:
            if not self.is_tab_open("Gallery"):
                self.add_tab(tab_gallery.GalleryTab(self), "Gallery")
        except Exception as e:
            logger.error("Error during show gallery", exc_info=True)

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
            logger.error("Error forcing simulator close", exc_info=True)

    def find_simulator_tab(self):
        index = 0
        while index < self._tab_widget.count():
            if self._tab_widget.tabText(index) == SIMULATOR_NAME:
                return self._tab_widget.widget(index)
            index += 1
        return None

    def display_rule(self, rule):
        self.close_simulator_tabs()
        sim = tab_simulate.TabSimulate(self)
        self.add_tab(sim, SIMULATOR_NAME)
        sim._force_rule = rule

    def open_tutorial(self):
        webbrowser.open("https://www.heatonresearch.com/mergelife/heaton-ca.html")
