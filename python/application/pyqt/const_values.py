import os
import utl_env

BUNDLE_ID = "com.heatonresearch.heaton-ca"
APP_ID = BUNDLE_ID.split('.')[-1]
APP_NAME = "HeatonCA"
COPYRIGHT = "Copyright 2023 by Jeff Heaton, released under the <a href='https://opensource.org/license/mit/'>MIT License</a>"
VERSION = "1.0.0"

if utl_env.is_sandboxed():
    LOG_DIR = os.path.join(os.path.expanduser("~"), "logs")
    SETTING_DIR = os.path.expanduser(f"~/preferences")
else:
    LOG_DIR = os.path.expanduser(f"~/Library/Application Support/{APP_ID}/logs/")
    SETTING_DIR = os.path.expanduser(f"~/Library/Application Support/{APP_ID}/")

SETTING_FILE = os.path.join(SETTING_DIR, f"{APP_ID}.plist")
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DATA_DIR = os.path.join(BASE_DIR, 'data')
