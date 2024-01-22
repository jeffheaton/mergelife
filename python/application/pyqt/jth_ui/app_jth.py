import datetime
import glob
import json
import logging
import logging.config
import logging.handlers
import os
import platform
import plistlib
import sys

import appdirs
from PyQt6.QtCore import QUrl, QEvent
from PyQt6.QtGui import QDesktopServices
from PyQt6.QtWidgets import QApplication

logger = logging.getLogger(__name__)

STATE_LAST_FOLDER = "last_folder"
STATE_LAST_FILES = "recent"


class AppJTH(QApplication):
    def __init__(self, app_name, app_author, copyright, version, bundle_id):
        super().__init__(sys.argv)
        self.file_open_request = None
        self.BUNDLE_ID = bundle_id
        self.APP_NAME = app_name
        self.APP_AUTHOR = app_author
        self.COPYRIGHT = copyright
        self.VERSION = version
        self.APP_ID = self.BUNDLE_ID.split(".")[-1]
        self.settings = {}
        if self.get_system_name() == "osx":
            if self.is_sandboxed():
                self.LOG_DIR = os.path.join(os.path.expanduser("~"), "logs")
                self.SETTING_DIR = os.path.expanduser(f"~/preferences")
            else:
                self.LOG_DIR = os.path.expanduser(
                    f"~/Library/Application Support/{self.APP_ID}/logs/"
                )
                self.SETTING_DIR = os.path.expanduser(
                    f"~/Library/Application Support/{self.APP_ID}/"
                )
            self.SETTING_FILE = os.path.join(self.SETTING_DIR, f"{self.APP_ID}.plist")
            self.STATE_FILE = os.path.join(self.SETTING_DIR, "state.json")
        elif self.get_system_name() == "windows":
            base_dir = appdirs.user_config_dir(
                self.APP_NAME, self.APP_AUTHOR, roaming=False
            )
            self.LOG_DIR = os.path.join(base_dir, "logs")
            self.SETTING_DIR = os.path.join(base_dir, "preferences")
            self.SETTING_FILE = os.path.join(self.SETTING_DIR, f"{self.APP_ID}.json")
            self.STATE_FILE = os.path.join(self.SETTING_DIR, "state.json")
        else:
            home_dir = os.path.expanduser("~")
            base_dir = os.path.join(home_dir, self.APP_ID)
            os.makedirs(base_dir, exist_ok=True)
            self.LOG_DIR = os.path.join(base_dir, "logs")
            self.SETTING_DIR = os.path.join(base_dir, "preferences")
            self.SETTING_FILE = os.path.join(self.SETTING_DIR, f"{self.APP_ID}.json")
            self.STATE_FILE = os.path.join(self.SETTING_DIR, "state.json")

        print(f"Logs path: {self.LOG_DIR}")
        print(f"Settings path: {self.SETTING_DIR}")
        print(f"Settings file: {self.SETTING_FILE}")

        self.load_settings()
        self.setup_logging()
        self.delete_old_logs()

        logging.info("Application starting up")
        s = self.get_system_name()
        logging.info(f"System: {s}")
        logging.info(f"Pyinstaller: {self.is_pyinstaller_bundle()}")
        z = os.path.expanduser("~")
        logging.info(f"User: {z}")
        if s == "osx":
            logging.info(f"Sandbox mode: {self.is_sandboxed()}")

        self.setApplicationName(app_name)

        self.load_state()

    def exec(self):
        try:
            logger.info("Starting app main loop")
            super().exec()
            logger.info("Exited app main loop")
        except Exception as e:
            logger.error("Error running app", exc_info=True)

    def is_sandboxed(self):
        return "APP_SANDBOX_CONTAINER_ID" in os.environ

    def get_resource_path(self, relative_path, base_path):
        """Get the path to a resource, supporting both normal and bundled (PyInstaller) modes."""
        if getattr(sys, "frozen", False):
            # If the application is run as a bundle (via PyInstaller)
            base_path = sys._MEIPASS
        else:
            base_path = os.path.dirname(base_path)

        return os.path.join(base_path, relative_path)

    def get_system_name(self):
        system = platform.system().lower()
        if system == "darwin":
            return "osx"
        elif system == "windows":
            return "windows"
        else:
            # This covers Linux and other UNIX-like systems
            return "unix"

    def is_pyinstaller_bundle(self):
        return getattr(sys, "frozen", False)

    # Define a function to handle deletion of old log files
    def delete_old_logs(self):
        retention_period = 7  # days
        current_time = datetime.datetime.now()
        log_files = glob.glob(os.path.join(self.LOG_DIR, "*.log"))

        for file in log_files:
            creation_time = datetime.datetime.fromtimestamp(os.path.getctime(file))
            if (current_time - creation_time).days > retention_period:
                os.remove(file)

    # Define the logging configuration
    def setup_logging(self, level=logging.INFO):
        os.makedirs(self.LOG_DIR, exist_ok=True)

        # Create the directory if it doesn't exist
        if not os.path.exists(self.LOG_DIR):
            os.makedirs(self.LOG_DIR)

            # Create the directory if it doesn't exist
        if not os.path.exists(self.SETTING_DIR):
            os.makedirs(self.SETTING_DIR)

        # Get the current date to append to the log filename
        date_str = datetime.datetime.now().strftime("%Y-%m-%d")
        log_filename = os.path.join(self.LOG_DIR, f"{date_str}.log")

        # Set up logging
        self._logger = logging.getLogger()
        self._logger.setLevel(level)

        # Create a file handler to write log messages to the file
        self._file_handler = logging.handlers.TimedRotatingFileHandler(
            log_filename, when="midnight", interval=1, backupCount=7
        )
        self._file_handler.setLevel(level)

        # Create a formatter to format the log messages
        formatter = logging.Formatter("%(asctime)s - %(levelname)s - %(message)s")
        self._file_handler.setFormatter(formatter)

        # Add the handler to the logger
        logger.addHandler(self._file_handler)

        # Add a stream handler (console handler) for console output
        self._console_handler = logging.StreamHandler()
        self._console_handler.setLevel(level)
        self._console_handler.setFormatter(formatter)
        logger.addHandler(self._console_handler)

    def change_log_level(self, level):
        self._console_handler.setLevel(level)
        self._logger.setLevel(level)
        self._file_handler.setLevel(level)

    def shutdown(self):
        self.save_state()
        self.save_settings()
        self.setup_logging()
        self.delete_old_logs()
        logging.info("Application shutting down")

    def load_state(self):
        try:
            with open(self.STATE_FILE, "r") as fp:
                self.state = json.load(fp)
        except FileNotFoundError:
            self.init_state()
            logger.info("Failed to read state file, using defaults.")

    def save_state(self):
        try:
            with open(self.STATE_FILE, "w") as fp:
                json.dump(self.state, fp)
        except Exception as e:
            logger.error("Failed to write state file", exc_info=True)

    def init_state(self):
        home_directory = os.path.expanduser("~")
        documents_path = os.path.join(home_directory, "Documents")
        self.state = {STATE_LAST_FOLDER: documents_path, STATE_LAST_FILES: []}

    def init_settings(self):
        self.settings = {}

    # Save settings to a JSON file
    def save_settings(self):
        try:
            if self.get_system_name() == "osx":
                logger.info("Saved MacOS settings")
                with open(self.SETTING_FILE, "wb") as fp:
                    plistlib.dump(self.settings, fp)
            else:
                logger.info("Saved Windows settings")
                with open(self.SETTING_FILE, "w") as fp:
                    json.dump(self.settings, fp)
        except Exception as e:
            logging.error("Caught an exception saving settings", exc_info=True)

    def load_settings(self):
        try:
            os.makedirs(self.SETTING_DIR, exist_ok=True)

            if not os.path.exists(self.SETTING_FILE):
                logger.info("Resetting to default settings")
                self.init_settings()
            else:
                if self.get_system_name() == "osx":
                    with open(self.SETTING_FILE, "rb") as fp:
                        self.settings = plistlib.load(fp)
                    logger.info("Loaded MacOS settings")
                else:
                    with open(self.SETTING_FILE, "r") as fp:
                        print(self.SETTING_FILE)
                        self.settings = json.load(fp)
                        logger.info("Loaded Windows settings")
        except Exception as e:
            logging.error("Caught an exception loading settings", exc_info=True)
            self.init_settings()

    def open_logs(self):
        # Convert the file path to a QUrl object
        file_url = QUrl.fromLocalFile(self.LOG_DIR)

        # Open the file location in the default file explorer
        QDesktopServices.openUrl(file_url)

    def event(self, event):
        try:
            if event.type() == QEvent.Type.FileOpen:
                file_path = event.file()
                self.file_open_request = file_path
                logger.info(f"MacOS open file request: {file_path}")
                return True
            return super().event(event)
        except Exception as e:
            logger.error("Error during application event", exc_info=True)
