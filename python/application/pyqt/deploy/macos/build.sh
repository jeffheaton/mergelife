if [ -z "${app_certificate}" ]; then
    echo "Error: Environment variable app_certificate is not set."
    exit 1  # Exit with a non-zero value to indicate an error
fi

# Environment
cd ../..
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
appname="HeatonCA-${arch}.app"
cp $provisionprofile dist/${appname}/Contents/embedded.provisionprofile
codesign --force --timestamp --deep --verbose --options runtime --sign "${app_certificate}" dist/${appname}

echo "** Sign nested **"
#codesign --force --timestamp --verbose --options runtime --entitlements entitlements-nest.plist --sign "${app_certificate}" dist/Dynaface.app/Contents/Frameworks/torch/bin/protoc
#codesign --force --timestamp --verbose --options runtime --entitlements entitlements-nest.plist --sign "${app_certificate}" dist/Dynaface.app/Contents/Frameworks/torch/bin/protoc-3.13.0.0
#codesign --force --timestamp --verbose --options runtime --entitlements entitlements-nest.plist --sign "${app_certificate}" dist/Dynaface.app/Contents/Frameworks/torch/bin/torch_shm_manager

echo "** Sign App **"
codesign --force --timestamp --verbose --options runtime --entitlements entitlements.plist --sign "${app_certificate}" dist/${appname}/Contents/MacOS/heaton-ca

echo "** Verify Sign **"
codesign --verify --verbose dist/${appname}

# Set permissions, sometimes the transport app will complain about this
echo "** Set Permissions **"
find dist/${appname} -type f -exec chmod a=u {} \;
find dist/${appname} -type d -exec chmod a=u {} \;

echo "** Package **"
productbuild --component dist/${appname} /Applications --sign "${installer_certificate}" --version "${version}" dist/HeatonCA-${arch}.pkg
