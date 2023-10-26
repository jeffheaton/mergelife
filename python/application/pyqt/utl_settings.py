import json
import const_values
import os
import plistlib

# Constants for settings keys
CELL_SIZE_KEY = "cell size"
FPS_KEY = "fps"

settings = {}

def init_settings():
    global settings
    settings = {
        CELL_SIZE_KEY: 5,
        FPS_KEY: 30
    }

# Save settings to a JSON file
def save_settings():
    global settings
    with open(const_values.SETTING_FILE, "wb") as fp:
        plistlib.dump(settings, fp)

# Load settings from a JSON file
def load_settings():
    global settings
    os.makedirs(const_values.SETTING_DIR, exist_ok=True)

    if not os.path.exists(const_values.SETTING_FILE):
        # You might want to set default values or handle this situation differently
        init_settings()
    else:
        #with open(const_values.SETTING_FILE, 'r') as file:
        #    return json.load(file)

        with open(const_values.SETTING_FILE, "rb") as fp:
            settings = plistlib.load(fp)

