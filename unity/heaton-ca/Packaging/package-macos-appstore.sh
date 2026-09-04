#!/usr/bin/env bash
# package-macos-appstore.sh -- Mac App Store packaging for the HeatonCA macOS player.
#
# Ported from ~/projects/heaton-life-unity/Packaging/package-macos-appstore.sh,
# itself ported from dynaface-unity's mas/package_mas.sh. Two sandboxed Unity/Mono
# apps have already been through Mac App Store review with this exact shape, so
# every detail below is a rejection somebody already earned - do not re-derive it,
# and do not "clean it up" without a submission to spend.
#
#   package-macos-appstore.sh sandbox-test [app]
#       Ad-hoc signature + the sandbox entitlements. Launchable locally, NO
#       certificates and NO provisioning profile needed. Proves the app actually
#       works inside the App Sandbox before you spend a submission finding out.
#       RUN THIS FIRST, and follow the acceptance steps it prints.
#       -> build/mas/sandbox-test/HeatonCA.app
#
#   package-macos-appstore.sh store [app]
#       Mac App Store build: sandbox, signed with "Apple Distribution", the Mac
#       App Store provisioning profile embedded, productbuild-signed installer.
#       -> build/mas/HeatonCA-<version>.pkg
#       Env: PROFILE        path to the Mac App Store .provisionprofile; default
#                           Packaging/HeatonCA-MacAppStore.provisionprofile, which
#                           is NOT in this repository - create it once in the
#                           developer portal for <team>.com.heatonresearch.heaton-ca
#                           (the script prints the steps). A profile holds no
#                           secrets (team id, app id, entitlements, the public
#                           certificate) and expires yearly; this script refuses an
#                           expired one and warns inside 30 days.
#            APP_CERT       override identity (default "Apple Distribution")
#            INSTALLER_CERT override (default "3rd Party Mac Developer Installer")
#
# [app] defaults to build/macos/HeatonCA.app - the Unity build output, already
# plist-patched by Assets/Editor/macOSPostBuild.cs (whose ad-hoc signature is for
# launching locally and is NOT submittable). The input is never modified: each mode
# copies first, then signs the copy INSIDE-OUT (nested Mach-O first, the app last,
# because signing the app seals what is inside it). Entitlements go ONLY on the
# app - macOS ignores entitlements on nested code. The app is signed SANDBOX-ONLY,
# not with the hardened runtime: the hardened runtime belongs to the Developer ID /
# notarization path, which this product does not use.
#
# Certificates are two different types and are not interchangeable: the .app is
# signed with "Apple Distribution" (the modern certificate that replaced "3rd Party
# Mac Developer Application"), the .pkg with a Mac INSTALLER certificate ("Mac
# Installer Distribution" in the portal, which appears in the keychain as "3rd Party
# Mac Developer Installer"). "Developer ID Installer" is a different thing, for
# distribution OUTSIDE the store. List what you have with:
#     security find-identity -v          # NOT -p codesigning: that hides installer certs
#
# BUILD NUMBER: App Store Connect app id 6469583429 already holds the shipped PyQt
# HeatonCA 1.2.0, and this Unity build is an UPDATE to that record. iOS and macOS
# share ONE build counter there, so CFBundleVersion must be strictly greater than
# the highest build number ever uploaded for the app on either platform - read that
# number from App Store Connect before building, then rebuild with
#     HEATONCA_BUILD_NUMBER=<n>   (ProjectIdentity.Apply, then CIBuild.MacOS)
# This script prints the version and build it is about to package and warns when
# the build number still looks like Unity's default.
#
# Upload the .pkg with Transporter.app, or:
#     xcrun altool --upload-app -f build/mas/HeatonCA-<ver>.pkg -t macos \
#         --apiKey <id> --apiIssuer <issuer>
#
# dynaface's script also carries a "developer-id" mode (hardened runtime +
# notarization for direct download). It is deliberately not ported: HeatonCA ships
# through the store, and untested packaging code is worse than none.
#
# SANDBOX: in Claude Code, run this with the Bash sandbox DISABLED, like the gates
# in tools/. Measured 2026-09-02 on this Mac: sandboxed, `mktemp` fails with
# "mkstemp failed ... Operation not permitted" and `security cms -D` fails with
# "cert import failed: UNIX[Operation not permitted]" (no keychain), so store mode
# cannot even read the profile; codesign, productbuild, and the acceptance steps
# (which launch a GUI app) need the same freedom.
set -euo pipefail

DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT="$(cd "$DIR/.." && pwd)"
BASE_ENT="$DIR/HeatonCA.entitlements"
BUNDLE_ID="com.heatonresearch.heaton-ca"
APP_NAME="HeatonCA.app"
# Only used for messages: the expected team, and the container paths the sandboxed
# app writes to (companyName / productName, as Unity lays out ~/Library/Logs).
# The team is account identity and this repository is public, so it is never
# hardcoded: export HEATONCA_APPLE_TEAM_ID to have the provisioning profile checked
# against your own team. Unset, the team the profile carries is simply reported.
TEAM_ID_EXPECTED="${HEATONCA_APPLE_TEAM_ID:-}"
TEAM_ID_DISPLAY="${TEAM_ID_EXPECTED:-<your team id>}"
COMPANY_NAME="Jeff Heaton"
PRODUCT_NAME="HeatonCA"
CONTAINER="$HOME/Library/Containers/$BUNDLE_ID/Data"
CONTAINER_LOG="$CONTAINER/Library/Logs/$COMPANY_NAME/$PRODUCT_NAME/Player.log"
CONTAINER_SNAPSHOTS="$CONTAINER/Library/Application Support/$BUNDLE_ID/Snapshots"

usage()
{
    cat <<__USAGE__
usage: Packaging/package-macos-appstore.sh sandbox-test|store [app]

  sandbox-test  ad-hoc signature + sandbox entitlements, no certificates needed.
                Run this FIRST and follow the acceptance steps it prints.
                -> build/mas/sandbox-test/$APP_NAME
  store         Apple Distribution signature + embedded Mac App Store profile,
                packaged with productbuild.
                -> build/mas/HeatonCA-<version>.pkg

  [app]         the built player (default build/macos/$APP_NAME); never modified.

env:  PROFILE (default Packaging/HeatonCA-MacAppStore.provisionprofile),
      APP_CERT (default "Apple Distribution"),
      INSTALLER_CERT (default "3rd Party Mac Developer Installer")
      List your identities with: security find-identity -v
note: run with the Claude Code Bash sandbox disabled (keychain access).
__USAGE__
}

MODE="${1:-}"
APP_IN="${2:-$PROJECT/build/macos/$APP_NAME}"
case "$MODE" in
    sandbox-test|store) ;;
    -h|--help) usage; exit 0 ;;
    *) usage >&2; exit 1 ;;
esac

if [ "$(uname -s)" != "Darwin" ]; then
    echo "error: codesign/productbuild need macOS (uname: $(uname -s))" >&2
    exit 1
fi
[ -d "$APP_IN" ] || {
    echo "error: app not found: $APP_IN (run 'tools/unity-gate.sh build-macos' first)" >&2
    exit 1
}
# Absolute from here on: verify_build reads the plist with `defaults`, which does
# not resolve relative paths (a relative [app] used to fail every key check).
APP_IN="$(cd "$APP_IN" && pwd)"
[ -f "$BASE_ENT" ] || { echo "error: entitlements not found: $BASE_ENT" >&2; exit 1; }

# Refuse to sign an app that macOSPostBuild did not finish: each of these plist
# edits is a review item (export compliance, the privacy manifest, no Game Mode
# claim, no ATS exemption), and a build that skipped one looks exactly like a good
# build from the outside. macOSPostBuild throws BuildFailedException when an edit
# fails; this is the second net, for the app that was built before a fix, on
# another host, or by hand.
verify_build() { # $1 = app
    local plist="$1/Contents/Info" ok=1
    [ "$(defaults read "$plist" ITSAppUsesNonExemptEncryption 2>/dev/null)" = "0" ] ||
        { echo "error: ITSAppUsesNonExemptEncryption is not false" >&2; ok=0; }
    [ "$(defaults read "$plist" GCSupportsGameMode 2>/dev/null)" = "0" ] ||
        { echo "error: GCSupportsGameMode is not false" >&2; ok=0; }
    defaults read "$plist" NSHumanReadableCopyright >/dev/null 2>&1 ||
        { echo "error: NSHumanReadableCopyright is missing" >&2; ok=0; }
    if defaults read "$plist" NSAppTransportSecurity >/dev/null 2>&1; then
        echo "error: NSAppTransportSecurity is still present" >&2; ok=0
    fi
    [ "$(defaults read "$plist" CFBundleIdentifier 2>/dev/null)" = "$BUNDLE_ID" ] ||
        { echo "error: CFBundleIdentifier is not $BUNDLE_ID" >&2; ok=0; }
    local manifest="$1/Contents/Resources/PrivacyInfo.xcprivacy"
    if ! { [ -f "$manifest" ] && plutil -lint -s "$manifest" &&
           grep -q NSPrivacyAccessedAPICategoryUserDefaults "$manifest" &&
           ! grep -q NSPrivacyCollectedDataTypeUserID "$manifest"; }; then
        echo "error: PrivacyInfo.xcprivacy is missing or still Unity's template" >&2; ok=0
    fi
    [ "$ok" = 1 ] ||
        { echo "error: $1 was not post-processed by macOSPostBuild - rebuild with CIBuild.MacOS" >&2; exit 1; }
}
verify_build "$APP_IN"

OUT_ROOT="$PROJECT/build/mas"
TMP_ENT=""; TMP_PROFILE_PLIST=""
# TMP_ENT is "<mktemp file>.plist", so the bare mktemp file is removed too.
cleanup() {
    if [ -n "$TMP_ENT" ]; then rm -f "$TMP_ENT" "${TMP_ENT%.plist}"; fi
    if [ -n "$TMP_PROFILE_PLIST" ]; then rm -f "$TMP_PROFILE_PLIST"; fi
    return 0
}
trap cleanup EXIT

app_version() { defaults read "$1/Contents/Info" CFBundleShortVersionString; }
app_build()   { defaults read "$1/Contents/Info" CFBundleVersion; }

# Fresh copy of the Unity build to sign, stripped of Finder cruft.
stage_app() { # $1 = destination dir
    rm -rf "$1"; mkdir -p "$1"
    cp -R "$APP_IN" "$1/$APP_NAME"
    xattr -cr "$1/$APP_NAME"
    echo "$1/$APP_NAME"
}

# Sign every nested Mach-O (Frameworks dylibs, PlugIns bundles) so the app can be
# signed last. Entitlements are NOT passed here - macOS ignores them on nested code.
# A missing Frameworks or PlugIns folder would make `find` exit non-zero, and under
# `set -e -o pipefail` that would abort the run with no explanation, so only the
# folders that exist are searched.
sign_nested() { # $1 = app, $2... = codesign identity/option args
    local app="$1"; shift
    local dirs=() dir
    for dir in "$app/Contents/Frameworks" "$app/Contents/PlugIns"; do
        if [ -d "$dir" ]; then dirs+=("$dir"); fi
    done
    if [ "${#dirs[@]}" -eq 0 ]; then
        echo "WARNING: no Frameworks or PlugIns folder in $app - nothing nested was signed" >&2
        return 0
    fi
    find "${dirs[@]}" \
        -mindepth 1 -maxdepth 1 \( -name "*.bundle" -o -name "*.dylib" \) -print0 |
    while IFS= read -r -d '' item; do
        codesign --force "$@" "$item"
    done
}

# Anything Mach-O that sign_nested does not reach keeps the ad-hoc signature
# macOSPostBuild applied, and App Store validation rejects nested code that is not
# signed with the submission certificate. Today's player has exactly four dylibs in
# Frameworks and lib_burst_generated.bundle in PlugIns; a future Unity version or a
# native plugin landing somewhere else should show up here rather than at upload.
stray_macho() { # $1 = app
    find "$1/Contents" -type f -perm -u+x \
        ! -path "$1/Contents/Frameworks/*" \
        ! -path "$1/Contents/PlugIns/*" \
        ! -path "$1/Contents/MacOS/*" -print0 2>/dev/null |
    while IFS= read -r -d '' file; do
        case "$(file -b "$file")" in
            *Mach-O*) echo "$file" ;;
        esac
    done
}

case "$MODE" in

sandbox-test)
    OUT_APP="$(stage_app "$OUT_ROOT/sandbox-test")"
    sign_nested "$OUT_APP" -s -
    codesign --force -s - --entitlements "$BASE_ENT" "$OUT_APP"
    codesign --verify --deep --strict "$OUT_APP"
    echo "OK: $OUT_APP"
    echo
    # The acceptance the plan requires before a submission is spent: the app has to
    # run sandboxed, prove its determinism self-check from inside the container, and
    # still show the user a saved PNG.
    cat <<__ACCEPTANCE__
Acceptance - do all four before running 'store':

 1. Launch it (double-click, or):
        open "$OUT_APP"
    The window comes up on the Home screen. First launch populates
        $CONTAINER
    That container is SHARED with the shipped PyQt HeatonCA 1.2.0 (same bundle id),
    so it may already exist and hold that app's preferences; the Unity build adds
    its own Logs and Application Support folders under it.

 2. SELF-CHECK PASS in the container log. AppController runs DeterminismSelfCheck
    at every boot - not only under -selfcheck - and logs one verdict line:
        grep -a "SELF-CHECK" "$CONTAINER_LOG"
    Expect exactly:
        [HeatonCA] SELF-CHECK PASS
    followed by the five PASS lines (pcg32, mergelife-upstream-1,
    mergelife-upstream-60, mergelife-soup50, objective-redworld48). The About
    screen shows the same report, so you can read it without the log.
    If that path is empty, find where the sandbox put it:
        find "$CONTAINER/Library" -name 'Player*.log'
    A FAIL here means the Mono player disagrees with the engine vectors: stop, do
    not submit, and take it to the determinism gate (tools/macos-selfcheck.sh).

 3. Save PNG reveals the file in Finder. Open Simulator, let a rule run, press
    "Save PNG". The status line reads
        saved heatonca-<rule>-<timestamp>.png
    and a Finder window should open with that file selected, in
        $CONTAINER_SNAPSHOTS
    The file itself must exist either way - check first:
        ls -lt "$CONTAINER_SNAPSHOTS" | head
    (If the Snapshots folder is not there, the sandbox keyed persistentDataPath
    differently: find "$CONTAINER/Library" -name 'heatonca-*.png')

 4. If the PNG is on disk but Finder never opened, the native reveal did not run.
    PngExporter.Reveal calls HeatonCA_RevealInFinder in the WP4.5 bundle, which
    uses -[NSWorkspace activateFileViewerSelectingURLs:] - a call a sandboxed app
    IS allowed to make - and falls back to Process.Start("open", "-R ...") only
    when the bundle did not load. A sandboxed process may NOT spawn that helper,
    so a silent reveal means the bundle is missing or unsigned rather than an
    entitlement being absent: revealing a file is not file access, and
    files.user-selected.* would not help. Check the bundle first:
        codesign -v "$OUT_APP/Contents/PlugIns/HeatonCAMac.bundle"
    then look for a denial:
        log show --last 5m --info --predicate \\
            'eventMessage CONTAINS "Sandbox" AND eventMessage CONTAINS "HeatonCA"' | tail -20
    Saving must keep working regardless; only the reveal changes.
    While you are in the log, check that nothing else was denied - a sandboxed
    Unity player should produce no deny lines at all.
__ACCEPTANCE__
    ;;

store)
    PROFILE="${PROFILE:-$DIR/HeatonCA-MacAppStore.provisionprofile}"
    APP_CERT="${APP_CERT:-Apple Distribution}"
    INSTALLER_CERT="${INSTALLER_CERT:-3rd Party Mac Developer Installer}"
    [ -f "$PROFILE" ] || {
        cat >&2 <<__NO_PROFILE__
error: store mode needs a Mac App Store provisioning profile, and there is none at
           $PROFILE

       This repository does not carry one yet. Create it once:

         https://developer.apple.com/account/resources/profiles  (team $TEAM_ID_DISPLAY)
           Profiles > + > macOS > "Mac App Store Connect"   (older portals: "Mac App Store")
           App ID:      $TEAM_ID_DISPLAY.$BUNDLE_ID   (HeatonCA)
           Certificate: your "Apple Distribution" certificate

       If that App ID is not listed, confirm $BUNDLE_ID is still
       registered to team $TEAM_ID_DISPLAY after the 2026-08-19 heaton-ca /
       heaton-life split before creating a new one - the App Store record
       (app id 6469583429) depends on it.

       Download the profile, then either save it as
           $PROFILE
       or point the script at it:
           PROFILE=/path/to/HeatonCA-MacAppStore.provisionprofile $0 store

       A profile holds no secrets - team id, app id, entitlements, the public
       certificate - so committing it (as heaton-life-unity does) is fine and puts
       its expiry date in git history. It expires yearly; this script refuses an
       expired one and warns inside 30 days.
__NO_PROFILE__
        exit 1
    }

    # Team ID comes from the profile itself, so the entitlements cannot drift from it.
    TMP_PROFILE_PLIST="$(mktemp)"
    # A .provisionprofile is a CMS-signed plist; `security cms -D` unwraps it. A
    # truncated download or the wrong file lands here, so say so plainly instead of
    # letting the next PlistBuddy read fail on an empty file.
    if ! security cms -D -i "$PROFILE" > "$TMP_PROFILE_PLIST" 2>/dev/null; then
        echo "error: could not decode $PROFILE" >&2
        echo "       it must be a .provisionprofile downloaded from the developer portal" >&2
        exit 1
    fi
    TEAM_ID="$(/usr/libexec/PlistBuddy -c 'Print :TeamIdentifier:0' "$TMP_PROFILE_PLIST")"
    echo "Team ID from profile: $TEAM_ID"
    if [ -z "$TEAM_ID_EXPECTED" ]; then
        echo "         (export HEATONCA_APPLE_TEAM_ID to check this against your own team)"
    elif [ "$TEAM_ID" != "$TEAM_ID_EXPECTED" ]; then
        echo "WARNING: profile team is $TEAM_ID, expected $TEAM_ID_EXPECTED (\$HEATONCA_APPLE_TEAM_ID)" >&2
    fi

    # A profile for the wrong app id signs cleanly and is rejected at upload with a
    # message about the application-identifier, so compare here instead.
    PROFILE_APP_ID="$(/usr/libexec/PlistBuddy \
        -c 'Print :Entitlements:com.apple.application-identifier' "$TMP_PROFILE_PLIST" 2>/dev/null || echo '')"
    case "$PROFILE_APP_ID" in
        "$TEAM_ID.$BUNDLE_ID") ;;
        "$TEAM_ID."\*)
            echo "WARNING: wildcard profile ($PROFILE_APP_ID); a Mac App Store profile is normally explicit" >&2 ;;
        *)
            echo "error: profile is for '$PROFILE_APP_ID', not '$TEAM_ID.$BUNDLE_ID': $PROFILE" >&2
            exit 1 ;;
    esac
    if /usr/libexec/PlistBuddy -c 'Print :ProvisionedDevices' "$TMP_PROFILE_PLIST" >/dev/null 2>&1; then
        echo "WARNING: this profile lists provisioned devices, so it looks like a DEVELOPMENT" >&2
        echo "         profile; store submissions need the Mac App Store distribution profile." >&2
    fi

    # Profiles expire a year after issue (or with the certificate), and an expired
    # embedded profile fails at upload with an unhelpful message.
    if command -v python3 >/dev/null 2>&1; then
        DAYS_LEFT="$(python3 - "$TMP_PROFILE_PLIST" <<'PY'
import datetime, plistlib, sys
profile = plistlib.load(open(sys.argv[1], "rb"))
now = datetime.datetime.now(datetime.timezone.utc).replace(tzinfo=None)
print((profile["ExpirationDate"] - now).days)
PY
)"
        if [ "$DAYS_LEFT" -lt 0 ]; then
            echo "error: profile expired $(( -DAYS_LEFT )) days ago: $PROFILE" >&2
            echo "       download a renewed Mac App Store profile from the developer portal" >&2
            exit 1
        elif [ "$DAYS_LEFT" -lt 30 ]; then
            echo "WARNING: profile expires in $DAYS_LEFT days - renew it in the developer portal: $PROFILE" >&2
        fi
        echo "Profile: $(/usr/libexec/PlistBuddy -c 'Print :Name' "$TMP_PROFILE_PLIST") (expires in $DAYS_LEFT days)"
    else
        echo "WARNING: python3 not found - profile expiry NOT checked; an expired profile" >&2
        echo "         fails at upload with an unhelpful message." >&2
        echo "Profile: $(/usr/libexec/PlistBuddy -c 'Print :Name' "$TMP_PROFILE_PLIST")"
    fi

    TMP_ENT="$(mktemp -t heatonca-entitlements).plist"
    cp "$BASE_ENT" "$TMP_ENT"
    /usr/libexec/PlistBuddy \
        -c "Add :com.apple.application-identifier string $TEAM_ID.$BUNDLE_ID" \
        -c "Add :com.apple.developer.team-identifier string $TEAM_ID" "$TMP_ENT"

    OUT_APP="$(stage_app "$OUT_ROOT/store")"
    cp "$PROFILE" "$OUT_APP/Contents/embedded.provisionprofile"
    # The profile is usually a browser download, so the copy above reintroduces
    # com.apple.quarantine AFTER stage_app's strip - App Store validation rejects
    # any quarantined file in the payload (ITMS-91109). Strip once everything is in.
    xattr -cr "$OUT_APP"

    STRAY="$(stray_macho "$OUT_APP" || true)"
    if [ -n "$STRAY" ]; then
        echo "WARNING: Mach-O files outside Frameworks/PlugIns keep their ad-hoc signature and" >&2
        echo "         will be rejected as nested code signed with a different certificate:" >&2
        echo "$STRAY" | sed 's|^|         |' >&2
        echo "         sign them explicitly or widen sign_nested." >&2
    fi

    # Inside-out: nested Mach-O first, then the app, which seals them. No
    # --options runtime anywhere: a Mac App Store build is sandbox-only.
    sign_nested "$OUT_APP" --timestamp -s "$APP_CERT"
    codesign --force --timestamp -s "$APP_CERT" --entitlements "$TMP_ENT" "$OUT_APP"
    codesign --verify --deep --strict "$OUT_APP"

    if ! codesign -d --entitlements - --xml "$OUT_APP" 2>/dev/null | grep -q "app-sandbox"; then
        echo "error: app-sandbox entitlement missing after signing - the store will reject this." >&2
        exit 1
    fi

    VERSION="$(app_version "$OUT_APP")"
    BUILD="$(app_build "$OUT_APP")"
    echo "Packaging HeatonCA $VERSION (CFBundleVersion $BUILD)"
    if [ "$BUILD" = "1" ] || [ "$BUILD" = "0" ]; then
        echo "WARNING: CFBundleVersion is $BUILD, which is Unity's default and almost certainly" >&2
        echo "         NOT greater than the highest build already uploaded to App Store Connect" >&2
        echo "         app id 6469583429 (iOS and macOS share that counter). Read the live value" >&2
        echo "         there, then rebuild: HEATONCA_BUILD_NUMBER=<n> ProjectIdentity.Apply +" >&2
        echo "         CIBuild.MacOS. Uploading a build number that is not higher is refused." >&2
    fi

    PKG="$OUT_ROOT/HeatonCA-$VERSION.pkg"
    rm -f "$PKG"
    productbuild --component "$OUT_APP" /Applications --sign "$INSTALLER_CERT" "$PKG"
    echo "OK: $PKG"
    echo "Upload with Transporter.app (or xcrun altool --upload-app -f \"$PKG\" -t macos ...)."
    echo "This is an UPDATE to App Store Connect app id 6469583429 - confirm the build number"
    echo "($BUILD) is higher than every build ever uploaded there for iOS or macOS."
    ;;
esac
