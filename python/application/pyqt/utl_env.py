import os
import sys
import platform

def is_sandboxed():
    return 'APP_SANDBOX_CONTAINER_ID' in os.environ

def get_resource_path(relative_path):
    """ Get the path to a resource, supporting both normal and bundled (PyInstaller) modes."""
    if getattr(sys, 'frozen', False):
        # If the application is run as a bundle (via PyInstaller)
        base_path = sys._MEIPASS
    else:
        base_path = os.path.dirname(os.path.abspath(__file__))

    return os.path.join(base_path, relative_path)

def get_system_name():
    system = platform.system().lower()
    if system == "darwin":
        return "osx"
    elif system == "windows":
        return "windows"
    else:
        # This covers Linux and other UNIX-like systems
        return "unix"
    
def is_pyinstaller_bundle():
    return getattr(sys, 'frozen', False)

