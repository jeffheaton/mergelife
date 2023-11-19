import os
import appdirs
import jth_ui.utl_env as utl_env

BUNDLE_ID = "com.heatonresearch.heaton-ca"
APP_ID = BUNDLE_ID.split('.')[-1]
APP_NAME = "HeatonCA"
APP_AUTHOR = "HeatonResearch"
COPYRIGHT = "Copyright 2023 by Jeff Heaton, released under the <a href='https://opensource.org/license/mit/'>MIT License</a>"
VERSION = "1.0.0"

if utl_env.get_system_name()=='osx':
    if utl_env.is_sandboxed():
        LOG_DIR = os.path.join(os.path.expanduser("~"), "logs")
        SETTING_DIR = os.path.expanduser(f"~/preferences")
    else:
        LOG_DIR = os.path.expanduser(f"~/Library/Application Support/{APP_ID}/logs/")
        SETTING_DIR = os.path.expanduser(f"~/Library/Application Support/{APP_ID}/")
    SETTING_FILE = os.path.join(SETTING_DIR, f"{APP_ID}.plist")
elif utl_env.get_system_name()=='windows':
    base_dir = appdirs.user_config_dir(APP_NAME, APP_AUTHOR, roaming=False)
    LOG_DIR = os.path.join(base_dir, "logs")
    SETTING_DIR = os.path.join(base_dir, "preferences")
    SETTING_FILE = os.path.join(SETTING_DIR, f"{APP_ID}.json")
else:
    pass

APP_NAME = "HeatonCA"
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DATA_DIR = os.path.join(BASE_DIR, 'data')

