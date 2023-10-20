pyinstaller -y -F --icon heaton_ca.png --onefile --windowed heaton_ca.py

pyinstaller -y --clean heaton_ca.spec

pip install pyinstaller
pip install pyqt6
pip install numpy
pip install scipy
pip install opencv-python-headless

python3.11 -m venv .venv
source .venv/bin/activate