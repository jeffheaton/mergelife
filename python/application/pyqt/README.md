pyinstaller -y -F --icon heaton_ca_icon.png --onefile --windowed heaton-ca.py

pyinstaller -y --clean heaton-ca-osx.spec
pyinstaller -y --clean heaton-ca-win.spec

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
