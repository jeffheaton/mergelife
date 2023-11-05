import json
import const_values
import os
import plistlib
import logging
import utl_env

logger = logging.getLogger(__name__)

# Constants for settings keys
CELL_SIZE_KEY = "cell size"
FPS_KEY = "fps"
FPS_OVERLAY = "fps_overlay"

settings = {}

def init_settings():
    global settings
    settings = {
        CELL_SIZE_KEY: 5,
        FPS_KEY: 30,
        FPS_OVERLAY: True
    }

# Save settings to a JSON file
def save_settings():
    try:
        global settings
        if utl_env.get_system_name()=='osx':
            with open(const_values.SETTING_FILE, "wb") as fp:
                plistlib.dump(settings, fp)
        else:
            with open(const_values.SETTING_FILE, "w") as fp:
                json.dump(settings, fp)
    except Exception as e:
        logging.error("Caught an exception saving settings", exc_info=True)

# Load settings from a JSON file
def load_settings():
    try:
        global settings
        os.makedirs(const_values.SETTING_DIR, exist_ok=True)

        if not os.path.exists(const_values.SETTING_FILE):
            init_settings()
        else:
            if utl_env.get_system_name()=='osx':
                with open(const_values.SETTING_FILE, "rb") as fp:
                    settings = plistlib.load(fp)
            else:
                with open(const_values.SETTING_FILE, "r") as fp:
                    SETTINGS = json.load(fp)
    except Exception as e:
        logging.error("Caught an exception loading settings", exc_info=True)

def get_bool(key):
    result = settings.get(key,False)
    if not result: result = False
    return result

def get_int(key):
    result = settings.get(key,1)
    if not result: result = 1
    return result

