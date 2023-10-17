python build.py \
    --app_name "HeatonCA" \
    --version "0.0.1" \
    --spec_file "heaton_ca.spec" \
    --entitlements "entitlements.plist" \
    --provisioning_profile "/Users/jeff/data/certs/Jeff_Heatons_Cellular_Automata.provisionprofile" \
    --app_certificate "$app_certificate" \
    --installer_certificate "$installer_certificate" \
    --output_dir "dist"