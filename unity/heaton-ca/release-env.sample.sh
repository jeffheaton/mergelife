# HeatonCA release environment -- SAMPLE
#
# Copy to release-env.sh (git-ignored) and fill in your own values:
#
#     cp release-env.sample.sh release-env.sh
#     $EDITOR release-env.sh
#
# Then SOURCE it -- do not execute it. Exports made in a child process vanish
# when it exits, so `./release-env.sh` would appear to work and change nothing:
#
#     source unity/heaton-ca/release-env.sh
#
# Sourcing is safe to repeat and safe to do in an everyday shell: nothing here
# runs a build, and every value is either yours or absent. It sets no store
# mode by default, so a stale HEATONCA_STORE cannot leak into a dev build.
#
# Deliberately NOT `set -e` / `set -u` / `exit`: this file runs inside your
# interactive shell, where any of those would close the window on a typo.

# --- sourced, or executed by mistake? ----------------------------------------
_hca_sourced=0
if [ -n "${ZSH_VERSION:-}" ]; then
    case "${ZSH_EVAL_CONTEXT:-}" in *:file*) _hca_sourced=1 ;; esac
elif [ -n "${BASH_VERSION:-}" ]; then
    [ "${BASH_SOURCE[0]}" != "$0" ] && _hca_sourced=1
fi
if [ "$_hca_sourced" -ne 1 ]; then
    echo "release-env.sh must be SOURCED, not executed:" >&2
    echo "    source ${BASH_SOURCE[0]:-unity/heaton-ca/release-env.sh}" >&2
    unset _hca_sourced
    exit 1
fi
unset _hca_sourced

_hca_keychain() { security find-generic-password -a "$USER" -s "$1" -w 2>/dev/null; }

# =============================================================================
# 1. Apple developer team
# =============================================================================
# The only variable needed for ordinary work: signing a build for a physical
# iPhone or iPad. Never checked in -- ProjectIdentity.Apply holds
# appleDeveloperTeamID empty and iOSPostBuild stamps this value onto the app and
# UnityFramework targets of the generated Xcode project.
export HEATONCA_APPLE_TEAM_ID=""   # <-- your 10-character Apple developer team id

# =============================================================================
# 2. Apple store release
# =============================================================================
# iOS and macOS share ONE App Store Connect build counter. Read the highest
# build ever uploaded (App Store Connect > your app > every platform) and set a
# higher one. Never guess: a reused number is rejected as ITMS-90189.
#
# Uncomment BOTH lines only for a store build, and re-comment them afterwards.
# HEATONCA_STORE=1 makes CIBuild.MacOS and CIBuild.IOS fail without a build
# number instead of quietly stamping a dev one.
#
# export HEATONCA_STORE=1
# export HEATONCA_BUILD_NUMBER=<TBD: higher than any build ever uploaded>

# =============================================================================
# 3. Android release signing
# =============================================================================
# Create the keystore once (see docs/android-keystore.md), then store the two
# passwords in the login keychain so they never sit in a file:
#
#     security add-generic-password -a "$USER" -s heatonca-android-keystore -w
#     security add-generic-password -a "$USER" -s heatonca-android-keyalias  -w
#
# All four signing variables must be set together. CIBuild fails a partially
# configured keystore on purpose, so this block exports nothing at all unless
# the keystore file exists AND both passwords come back from the keychain --
# otherwise the build debug-signs and warns, which is the honest fallback.
HEATONCA_ANDROID_KEYSTORE_PATH=""  # <-- /path/to/heatonca-upload.keystore
HEATONCA_ANDROID_KEYALIAS_NAME="upload"

if [ -f "$HEATONCA_ANDROID_KEYSTORE_PATH" ]; then
    _hca_store_pass="$(_hca_keychain heatonca-android-keystore)"
    _hca_alias_pass="$(_hca_keychain heatonca-android-keyalias)"
    if [ -n "$_hca_store_pass" ] && [ -n "$_hca_alias_pass" ]; then
        export HEATONCA_ANDROID_KEYSTORE="$HEATONCA_ANDROID_KEYSTORE_PATH"
        export HEATONCA_ANDROID_KEYALIAS="$HEATONCA_ANDROID_KEYALIAS_NAME"
        export HEATONCA_ANDROID_KEYSTORE_PASS="$_hca_store_pass"
        export HEATONCA_ANDROID_KEYALIAS_PASS="$_hca_alias_pass"
    fi
    unset _hca_store_pass _hca_alias_pass
fi

# Play needs a version code higher than every previous upload (1 for the first).
# export HEATONCA_ANDROID_VERSION_CODE=1

# =============================================================================
# 4. WebGL publish target
# =============================================================================
# s3://bucket/prefix, or a local directory. publish-webgl.sh needs --confirm as
# well, so an accidental source cannot publish anything.
# export HEATONCA_WEB_DEST=<TBD: s3://bucket/heatonca/2.0.0>
# export HEATONCA_WEB_AWS_ARGS="--profile <aws-profile> --region us-east-1"
# export HEATONCA_WEB_CF_DIST=<TBD: CloudFront distribution id>

# =============================================================================
# What is ready, and what each value unlocks
# =============================================================================
_hca_row() {
    # $1 label, $2 value, $3 what it unlocks, $4 = "secret" to mask the value
    if [ -n "$2" ]; then
        case "$4" in
            secret) printf '  set    %-30s %s\n' "$1" "(from keychain)" ;;
            *)      printf '  set    %-30s %s\n' "$1" "$2" ;;
        esac
    else
        printf '  unset  %-30s %s\n' "$1" "$3"
    fi
}

echo "HeatonCA release environment:"
_hca_row HEATONCA_APPLE_TEAM_ID       "${HEATONCA_APPLE_TEAM_ID:-}"       "device and store builds cannot sign"
_hca_row HEATONCA_STORE               "${HEATONCA_STORE:-}"               "Apple builds stamp a dev build number"
_hca_row HEATONCA_BUILD_NUMBER        "${HEATONCA_BUILD_NUMBER:-}"        "required in store mode (iOS + macOS share it)"
_hca_row HEATONCA_ANDROID_KEYSTORE    "${HEATONCA_ANDROID_KEYSTORE:-}"    "the AAB is debug-signed and Play rejects it"
_hca_row HEATONCA_ANDROID_KEYALIAS    "${HEATONCA_ANDROID_KEYALIAS:-}"    "as above"
_hca_row HEATONCA_ANDROID_KEYSTORE_PASS "${HEATONCA_ANDROID_KEYSTORE_PASS:-}" "as above" secret
_hca_row HEATONCA_ANDROID_KEYALIAS_PASS "${HEATONCA_ANDROID_KEYALIAS_PASS:-}" "as above" secret
_hca_row HEATONCA_ANDROID_VERSION_CODE "${HEATONCA_ANDROID_VERSION_CODE:-}" "Play needs a code above every previous upload"
_hca_row HEATONCA_WEB_DEST            "${HEATONCA_WEB_DEST:-}"            "publish-webgl.sh has nowhere to publish"
echo "Unset values are fine for WebGL, Windows, local macOS, a local Android APK,"
echo "and the iOS simulator: those build with an empty environment."

unset -f _hca_row _hca_keychain
