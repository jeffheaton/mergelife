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

Windows QT Issue
https://stackoverflow.com/questions/42863505/dll-load-failed-when-importing-pyqt5

copy  C:\Users\jeffh\AppData\Local\Programs\Python\Python311\python3.dll C:\Users\jeffh\projects\mergelife\python\application\pyqt\venv\Scripts

python3.11 -m venv venv
source venv/bin/activate
./venv/Scripts/activate

C:\Users\jeffh\AppData\Local\Programs\Python\Python312


Version:
build.sh
const_values.py
build.spec
heaton-ca-osx.spec
