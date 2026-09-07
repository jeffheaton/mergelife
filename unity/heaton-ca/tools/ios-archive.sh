#!/usr/bin/env bash
# ios-archive.sh -- archive the generated iOS Xcode project for the App Store
# and export a signed .ipa, then audit what actually landed in the binary.
#
# `tools/unity-gate.sh build-ios` (CIBuild.IOS) writes the Xcode project to
# build/ios with iOSPostBuild already applied. This script then:
#
#   1. Refuses to start unless HEATONCA_APPLE_TEAM_ID is set -- an archive
#      signed with no team is a wasted 10 minutes.
#   2. Audits the generated Info.plist BEFORE archiving (step 5 of the release
#      runbook): the four keys that must be present, and the one that must be
#      absent. NSPhotoLibraryUsageDescription is the one that must be absent:
#      NativeGallery's post-build injects its stock placeholder, App Store
#      review rejects that as insufficient (it did, on 2.0.0 build 10), and
#      iOSPostBuild removes it again. Catching a regression here costs seconds;
#      catching it in review costs a submission.
#   3. xcodebuild archive, scheme Unity-iPhone, configuration Release,
#      -destination 'generic/platform=iOS', -allowProvisioningUpdates.
#   4. xcodebuild -exportArchive with a generated ExportOptions.plist
#      (method app-store-connect, automatic signing, symbols uploaded).
#   5. Re-audits the same plist keys inside the archived .app, where the answer
#      is authoritative, and prints the version, build number, and SHA-256 of
#      the .ipa for the runbook's artifact table.
#
# It does NOT upload. Hand build/ios-archive/HeatonCA.ipa to Transporter.app,
# or open the .xcarchive in Xcode's Organizer and Distribute App from there.
#
# Usage:
#   tools/ios-archive.sh [--skip-archive] [-- <extra xcodebuild args>]
#
#   --skip-archive   re-export and re-audit the existing .xcarchive
#
# Environment:
#   HEATONCA_APPLE_TEAM_ID   Apple developer team (required; release-env.sh)
#   HEATONCA_IOS_PROJECT     the .xcodeproj (default build/ios/Unity-iPhone.xcodeproj)
#
# Exit codes:
#   0  archive exported and audited clean
#   1  the plist audit failed, or xcodebuild did
#   2  usage error
#   5  environment problem (no Xcode, no team, no generated project)

set -euo pipefail

cd "$(dirname "$0")/.."

PROJECT=${HEATONCA_IOS_PROJECT:-build/ios/Unity-iPhone.xcodeproj}
SCHEME=Unity-iPhone
OUT=build/ios-archive
ARCHIVE=$OUT/HeatonCA.xcarchive
LOGDIR=build/logs
SKIP_ARCHIVE=0
EXTRA=()

while [ $# -gt 0 ]; do
    case "$1" in
        --skip-archive) SKIP_ARCHIVE=1; shift ;;
        --) shift; EXTRA=("$@"); break ;;
        *) echo "ios-archive: unknown argument: $1" >&2; exit 2 ;;
    esac
done

fail() { echo "ios-archive: $*" >&2; exit "${2:-1}"; }

command -v xcodebuild >/dev/null 2>&1 || fail "xcodebuild not found; install Xcode" 5
[ -d "$PROJECT" ] || fail "no Xcode project at $PROJECT -- run tools/unity-gate.sh build-ios first" 5

TEAM=${HEATONCA_APPLE_TEAM_ID:-}
[ -n "$TEAM" ] || fail "HEATONCA_APPLE_TEAM_ID is unset -- source unity/heaton-ca/release-env.sh" 5

mkdir -p "$OUT" "$LOGDIR"

# --- the plist audit, run twice: on the generated project and on the archive --
plist_get() { /usr/libexec/PlistBuddy -c "Print :$2" "$1" 2>/dev/null; }

audit_plist() {
    local plist=$1 label=$2 bad=0 v
    echo "ios-archive: auditing $label"
    for key in ITSAppUsesNonExemptEncryption UIFileSharingEnabled \
               LSSupportsOpeningDocumentsInPlace NSPhotoLibraryAddUsageDescription; do
        if v=$(plist_get "$plist" "$key"); then
            printf '  present  %-36s %s\n' "$key" "$v"
        else
            printf '  MISSING  %s\n' "$key"
            bad=1
        fi
    done
    # Add-only is the whole story: the app never reads the photo library, so a
    # read-access purpose string is both untrue and a review rejection.
    if v=$(plist_get "$plist" NSPhotoLibraryUsageDescription); then
        printf '  PRESENT  %-36s %s\n' "NSPhotoLibraryUsageDescription" "$v"
        echo "  ^ must be absent -- iOSPostBuild is supposed to remove NativeGallery's placeholder"
        bad=1
    else
        printf '  absent   %s (correct)\n' "NSPhotoLibraryUsageDescription"
    fi
    [ "$bad" -eq 0 ] || fail "plist audit failed on $label"
}

PROJECT_DIR=$(dirname "$PROJECT")
audit_plist "$PROJECT_DIR/Info.plist" "$PROJECT_DIR/Info.plist (generated)"

[ -f "$PROJECT_DIR/PrivacyInfo.xcprivacy" ] \
    || fail "PrivacyInfo.xcprivacy missing from $PROJECT_DIR -- iOSPostBuild did not run"

# --- archive ------------------------------------------------------------------
if [ "$SKIP_ARCHIVE" -eq 0 ]; then
    rm -rf "$ARCHIVE"
    ALOG=$LOGDIR/ios-archive-$(date -u +%Y%m%dT%H%M%SZ).log
    echo "ios-archive: archiving (log: $ALOG)"
    xcodebuild archive \
        -project "$PROJECT" \
        -scheme "$SCHEME" \
        -configuration Release \
        -destination 'generic/platform=iOS' \
        -archivePath "$ARCHIVE" \
        -allowProvisioningUpdates \
        DEVELOPMENT_TEAM="$TEAM" \
        "${EXTRA[@]+"${EXTRA[@]}"}" >"$ALOG" 2>&1 \
        || { tail -40 "$ALOG"; fail "xcodebuild archive failed; full log: $ALOG"; }
    ln -sf "$(basename "$ALOG")" "$LOGDIR/ios-archive-latest.log"
fi

[ -d "$ARCHIVE" ] || fail "no archive at $ARCHIVE"

# --- export -------------------------------------------------------------------
OPTS=$OUT/ExportOptions.plist
cat > "$OPTS" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>method</key>            <string>app-store-connect</string>
  <key>teamID</key>            <string>$TEAM</string>
  <key>signingStyle</key>      <string>automatic</string>
  <key>destination</key>       <string>export</string>
  <key>uploadSymbols</key>     <true/>
  <key>manageAppVersionAndBuildNumber</key> <false/>
</dict>
</plist>
PLIST

ELOG=$LOGDIR/ios-export-$(date -u +%Y%m%dT%H%M%SZ).log
echo "ios-archive: exporting (log: $ELOG)"
rm -rf "$OUT/export"
xcodebuild -exportArchive \
    -archivePath "$ARCHIVE" \
    -exportPath "$OUT/export" \
    -exportOptionsPlist "$OPTS" \
    -allowProvisioningUpdates >"$ELOG" 2>&1 \
    || { tail -40 "$ELOG"; fail "xcodebuild -exportArchive failed; full log: $ELOG"; }
ln -sf "$(basename "$ELOG")" "$LOGDIR/ios-export-latest.log"

# --- audit what actually shipped ---------------------------------------------
APP=$(find "$ARCHIVE/Products/Applications" -maxdepth 1 -name '*.app' | head -1)
[ -n "$APP" ] || fail "no .app inside $ARCHIVE"
audit_plist "$APP/Info.plist" "$(basename "$APP")/Info.plist (archived binary)"

IPA=$(find "$OUT/export" -maxdepth 1 -name '*.ipa' | head -1)
[ -n "$IPA" ] || fail "no .ipa in $OUT/export"

VERSION=$(plist_get "$APP/Info.plist" CFBundleShortVersionString)
BUILD=$(plist_get "$APP/Info.plist" CFBundleVersion)

echo
echo "ios-archive: PASS  version=$VERSION build=$BUILD"
echo "  archive  $ARCHIVE"
echo "  ipa      $IPA ($(du -h "$IPA" | cut -f1))"
echo "  sha256   $(shasum -a 256 "$IPA" | cut -d' ' -f1)"
echo
echo "Upload with Transporter.app, or open the .xcarchive in Xcode's Organizer"
echo "and use Distribute App. This script deliberately does not upload."
