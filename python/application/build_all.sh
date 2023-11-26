cd ./pyqt
rm -rf ./venv || true
python3.11 -m venv venv
source venv/bin/activate
pip install -r requirements.txt
cd deploy/macos
rm -rf ./working || true
./build.sh