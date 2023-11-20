rm -rf ./working
mkdir ./working
cp ./entitlements.plist ./working
cp ./heaton_ca_icon.icns ./working
cp ./heaton-ca-osx.spec ./working
cp ./build.py ./working
cp ./build.sh ./working
cp ../../*.py ./working
cp -r ../../data ./working/data
cp -r ../../jth_ui ./working/jth_ui
cp -r ../../mergelife ./working/mergelife

cd ./working
python build.py \
    --app_name "HeatonCA" \
    --version "$version" \
    --spec_file "heaton-ca-osx.spec" \
    --entitlements "entitlements.plist" \
    --provisioning_profile "$provisionprofile" \
    --app_certificate "$app_certificate" \
    --installer_certificate "$installer_certificate" \
    --output_dir "dist"
cd ..