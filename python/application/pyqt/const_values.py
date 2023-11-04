import os
import utl_env

BUNDLE_ID = "com.heatonresearch.heaton-ca"
APP_ID = BUNDLE_ID.split('.')[-1]

if utl_env.is_sandboxed():
    LOG_DIR = os.path.join(os.path.expanduser("~"), "logs")
    SETTING_DIR = os.path.expanduser(f"~/preferences")
else:
    LOG_DIR = os.path.expanduser(f"~/Library/Application Support/{APP_ID}/logs/")
    SETTING_DIR = os.path.expanduser(f"~/Library/Application Support/{APP_ID}/")

SETTING_FILE = os.path.join(SETTING_DIR, f"{APP_ID}.plist")
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DATA_DIR = os.path.join(BASE_DIR, 'data')
