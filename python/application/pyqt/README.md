pyinstaller -y -F --icon heaton_ca_icon.png --onefile --windowed heaton-ca.py

pyinstaller -y --clean heaton-ca-osx.spec
pyinstaller -y --clean heaton-ca-win.spec

pip install pyinstaller==6.1.0
pip install pyqt6==6.5.3
pip install numpy==1.26.1
pip install scipy==1.11.3
pip install opencv-python-headless==4.8.1.78
pip install pillow==10.1.0
pip install appdirs==1.4.4

pip install -r requirements.txt

python3.11 -m venv .venv
source .venv/bin/activate

C:\Users\jeffh\AppData\Local\Programs\Python\Python312

.\.venv\Scripts\activate.bat

Version:
build.sh
const_values.py
build.spec
heaton-ca-osx.spec
