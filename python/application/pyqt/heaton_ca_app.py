import json
import logging
import logging.config
import logging.handlers
import os
import plistlib
import sys

from heaton_ca_win import WindowHeatonCA
from jth_ui.app_jth import AppJTH

logger = logging.getLogger(__name__)

# Constants for settings keys
CELL_SIZE_KEY = "cell size"
FPS_KEY = "fps"
FPS_OVERLAY = "fps_overlay"


class AppHeatonCA(AppJTH):
    def __init__(self):
        try:
            super().__init__(
                app_name="HeatonCA",
                app_author="HeatonResearch",
                copyright="Copyright 2024 by Jeff Heaton, released under the <a href='https://opensource.org/license/mit/'>MIT License</a>",
                version="1.0.1",
                bundle_id="com.heatonresearch.heaton-ca",
            )

            self.BASE_DIR = os.path.dirname(os.path.abspath(__file__))
            self.DATA_DIR = os.path.join(self.BASE_DIR, "data")

            self.main_window = WindowHeatonCA(app=self, app_name=self.APP_NAME)
            self.main_window.show()

        except Exception as e:
            logger.error("Error running app", exc_info=True)

    def shutdown(self):
        try:
            super().shutdown()
            sys.exit(0)
        except Exception as e:
            logger.error("Error shutting down app", exc_info=True)

    def init_settings(self):
        self.settings = {CELL_SIZE_KEY: 5, FPS_KEY: 30, FPS_OVERLAY: True}

   