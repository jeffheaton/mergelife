import os

BUNDLE_ID = "com.heatonresearch.heaton-ca"
LOG_DIR = os.path.expanduser(f"~/Library/Containers/{BUNDLE_ID}/Data/Library/Logs/")
SETTING_DIR = os.path.expanduser(f"~/Library/Containers/{BUNDLE_ID}/Data/Library/Preferences/")
SETTING_FILE = os.path.join(SETTING_DIR, f"{BUNDLE_ID}.plist")
