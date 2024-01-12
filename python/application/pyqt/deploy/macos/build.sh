if [ -z "${app_certificate}" ]; then
    echo "Error: Environment variable app_certificate is not set."
    exit 1  # Exit with a non-zero value to indicate an error
fi

# Environment
rm -rf ./venv || true
python3.11 -m venv venv
source venv/bin/activate
pip install -r requirements.txt
cd deploy/macos

# Build it
rm -rf ./working
mkdir ./working
cp ./entitlements.plist ./working
cp ./heaton_ca_icon.icns ./working
cp ./heaton-ca-macos.spec ./working
cp ./build.py ./working
cp ./build.sh ./working
cp ../../*.py ./working
cp -r ../../data ./working/data
cp -r ../../jth_ui ./working/jth_ui
cp -r ../../mergelife ./working/mergelife

cd ./working

echo "** Pyinstaller **"
pyinstaller --clean --noconfirm --distpath dist --workpath build heaton-ca-macos.spec

echo "** Sign Deep **"
codesign --force --timestamp --deep --verbose --options runtime --sign "${app_certificate}" dist/HeatonCA.app

echo "** Sign nested **"
#codesign --force --timestamp --verbose --options runtime --entitlements entitlements-nest.plist --sign "${app_certificate}" dist/Dynaface.app/Contents/Frameworks/torch/bin/protoc
#codesign --force --timestamp --verbose --options runtime --entitlements entitlements-nest.plist --sign "${app_certificate}" dist/Dynaface.app/Contents/Frameworks/torch/bin/protoc-3.13.0.0
#codesign --force --timestamp --verbose --options runtime --entitlements entitlements-nest.plist --sign "${app_certificate}" dist/Dynaface.app/Contents/Frameworks/torch/bin/torch_shm_manager

echo "** Sign App **"
cp $provisionprofile dist/HeatonCA.app/Contents/embedded.provisionprofile
codesign --force --timestamp --verbose --options runtime --entitlements entitlements.plist --sign "${app_certificate}" dist/Heaton-CA.app/Contents/MacOS/heaton-ca

echo "** Verify Sign **"
codesign --verify --verbose dist/HeatonCA.app

echo "** Package **"
productbuild --component dist/HeatonCA.app /Applications --sign "${installer_certificate}" --version "${version}" dist/HeatonCA.pkg
