pyinstaller -y -F --icon heaton_ca_icon.png --onefile --windowed heaton-ca.py

pyinstaller -y --clean heaton-ca-osx.spec
pyinstaller -y --clean heaton-ca-win.spec

pip install pyinstaller
pip install pyqt6
pip install numpy
pip install scipy
pip install opencv-python-headless
pip install pillow
pip install appdirs

python3.11 -m venv .venv
source .venv/bin/activate

C:\Users\jeffh\AppData\Local\Programs\Python\Python312

.\.venv\Scripts\activate.bat