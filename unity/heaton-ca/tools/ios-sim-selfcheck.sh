#!/usr/bin/env bash
# ios-sim-selfcheck.sh -- build the Unity iOS Simulator Xcode project, run
# it on an iPhone simulator, and gate on the determinism self-check.
#
# `tools/unity-gate.sh build-ios-sim` (CIBuild.IOSSimulator) writes an Xcode
# project targeting the Simulator SDK to build/ios-sim. This script then:
#
#   1. Checks for Xcode, an iOS Simulator SDK, and an iOS simulator runtime
#      (exit 5 with install instructions when any is missing).
#   2. Builds it:
#        xcodebuild -project build/ios-sim/Unity-iPhone.xcodeproj -scheme Unity-iPhone
#                   -sdk iphonesimulator -configuration Release
#                   -derivedDataPath build/ios-sim/dd CODE_SIGNING_ALLOWED=NO build
#      (log: build/logs/ios-sim-xcodebuild.log; --skip-build reuses the last
#      product).
#   3. Picks a simulator: SIM_DEVICE (name or UDID) if set; else a booted
#      iPhone, else any booted device, else the first available iPhone, which
#      it boots headless with `xcrun simctl boot` (no Simulator.app window is
#      needed for the player to run).
#   4. `xcrun simctl install <udid> <app>` and
#      `xcrun simctl launch --console <udid> com.heatonresearch.heaton-ca`,
#      capturing the console to build/logs/ios-sim-selfcheck.log, which
#      tools/check-selfcheck.sh watches with the timeout. iOS apps take no
#      command line from the store, so the app runs normally and logs the
#      verdict at boot; the script terminates it afterward.
#   5. Cleans up: terminates the app and shuts down a simulator it booted
#      (KEEP_SIM=1 or --keep-booted leaves it up for the next gate). A
#      simulator that was already booted is left alone.
#
# The launch uses the chosen UDID rather than the "booted" alias so an iPad
# that happens to be up beside the iPhone cannot receive the launch.
#
# Usage:
#   tools/ios-sim-selfcheck.sh [--project <Unity-iPhone.xcodeproj>] [--device <name|udid>]
#                              [--timeout <s>] [--skip-build] [--keep-booted]
#                              [--log-source console|stream] [--probe] [-- <extra xcodebuild args>]
#
#   --probe   report Xcode, SDK, runtimes, and simulators found, then exit
#             (0 when a gate could run, 5 otherwise). Boots nothing.
#
# Environment:
#   HEATONCA_IOS_SIM_PROJECT   the .xcodeproj (default build/ios-sim/Unity-iPhone.xcodeproj)
#   HEATONCA_IOS_BUNDLE_ID     bundle id to install and launch (default com.heatonresearch.heaton-ca)
#   SIM_DEVICE                 simulator name or UDID to use (default: see step 3)
#   SELFCHECK_TIMEOUT          seconds to wait for the verdict (default 120)
#   BOOT_TIMEOUT               seconds to wait for a simulator boot (default 180)
#   KEEP_SIM=1                 do not shut down a simulator this script booted
#   SIM_LOG_SOURCE             console (default) reads the app's stdout/stderr through
#                              `simctl launch --console`; stream reads the unified log with
#                              `simctl spawn <udid> log stream` instead, for a runtime whose
#                              NSLog output no longer reaches the console
#
# Exit codes:
#   0  "[HeatonCA] SELF-CHECK PASS" seen
#   1  "SELF-CHECK FAIL" seen
#   2  no verdict within the timeout
#   3  a step failed: project missing, xcodebuild failed, no .app with the bundle id,
#      simulator did not boot, install or launch failed
#   5  environment: not macOS, no Xcode (only the Command Line Tools), no iOS
#      Simulator SDK or runtime, or no simulator device to use
#
# SANDBOX: in Claude Code run with the Bash sandbox disabled: xcrun keeps its
# cache under /var/folders and simctl talks to CoreSimulatorService over XPC,
# both of which the sandbox blocks. Runs under /bin/bash 3.2.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
PROJECT="$(cd "$SCRIPT_DIR/.." && pwd -P)"
CHECK="$SCRIPT_DIR/check-selfcheck.sh"

XCPROJ="${HEATONCA_IOS_SIM_PROJECT:-$PROJECT/build/ios-sim/Unity-iPhone.xcodeproj}"
SCHEME="Unity-iPhone"
CONFIG="Release"
DERIVED="$PROJECT/build/ios-sim/dd"
BUNDLE="${HEATONCA_IOS_BUNDLE_ID:-com.heatonresearch.heaton-ca}"
SIM_DEVICE="${SIM_DEVICE:-}"
TIMEOUT="${SELFCHECK_TIMEOUT:-120}"
BOOT_TIMEOUT="${BOOT_TIMEOUT:-180}"
KEEP="${KEEP_SIM:-0}"
LOG_SOURCE="${SIM_LOG_SOURCE:-console}"
SKIP_BUILD=0
PROBE=0
LOG_DIR="$PROJECT/build/logs"
XCODEBUILD_LOG="$LOG_DIR/ios-sim-xcodebuild.log"
LOG="$LOG_DIR/ios-sim-selfcheck.log"
EXTRA=()

usage()
{
    cat <<__USAGE__
usage: tools/ios-sim-selfcheck.sh [--project <xcodeproj>] [--device <name|udid>] [--timeout <s>] [--skip-build]
                                  [--keep-booted] [--log-source console|stream] [--probe] [-- <extra xcodebuild args>]
       builds build/ios-sim for the Simulator SDK, installs it on an iPhone simulator, and reads the self-check verdict
env:   HEATONCA_IOS_SIM_PROJECT HEATONCA_IOS_BUNDLE_ID SIM_DEVICE SELFCHECK_TIMEOUT BOOT_TIMEOUT KEEP_SIM SIM_LOG_SOURCE
exit:  0 PASS, 1 FAIL, 2 no verdict, 3 step failed, 5 environment (no Xcode, SDK, runtime, or simulator)
note:  run with the Claude Code Bash sandbox disabled
__USAGE__
}

say()
{
    echo "ios-sim-selfcheck: $*"
}

warn()
{
    echo "ios-sim-selfcheck: $*" >&2
}

fail_usage()
{
    warn "$*"
    usage >&2
    exit 3
}

is_uint()
{
    case "$1" in
        ''|*[!0-9]*) return 1 ;;
    esac
}

while [ $# -gt 0 ]; do
    case "$1" in
        --project)
            [ $# -ge 2 ] || fail_usage "--project needs a value"
            XCPROJ="$2"
            shift ;;
        --device)
            [ $# -ge 2 ] || fail_usage "--device needs a value"
            SIM_DEVICE="$2"
            shift ;;
        --timeout)
            [ $# -ge 2 ] || fail_usage "--timeout needs a value"
            is_uint "$2" || fail_usage "--timeout must be a non-negative integer (got '$2')"
            TIMEOUT="$2"
            shift ;;
        --log-source)
            [ $# -ge 2 ] || fail_usage "--log-source needs console or stream"
            LOG_SOURCE="$2"
            shift ;;
        --skip-build) SKIP_BUILD=1 ;;
        --keep-booted) KEEP=1 ;;
        --probe) PROBE=1 ;;
        -h|--help) usage; exit 0 ;;
        --)
            shift
            while [ $# -gt 0 ]; do
                EXTRA+=("$1")
                shift
            done
            break ;;
        *) fail_usage "unknown argument '$1'" ;;
    esac
    shift
done
is_uint "$TIMEOUT" || fail_usage "SELFCHECK_TIMEOUT must be a non-negative integer (got '$TIMEOUT')"
is_uint "$BOOT_TIMEOUT" || fail_usage "BOOT_TIMEOUT must be a non-negative integer (got '$BOOT_TIMEOUT')"
case "$LOG_SOURCE" in
    console|stream) ;;
    *) fail_usage "SIM_LOG_SOURCE / --log-source must be console or stream (got '$LOG_SOURCE')" ;;
esac

# --- 1. environment ---------------------------------------------------------------
if [ "$(uname -s)" != "Darwin" ]; then
    warn "this gate drives the iOS Simulator and needs macOS (uname: $(uname -s))"
    exit 5
fi
if ! command -v xcrun >/dev/null 2>&1 || ! command -v xcodebuild >/dev/null 2>&1; then
    warn "xcrun/xcodebuild not found; install Xcode from the App Store"
    exit 5
fi
DEVELOPER_DIR_NOW="$(xcode-select -p 2>/dev/null || true)"
case "$DEVELOPER_DIR_NOW" in
    *Xcode*.app/Contents/Developer) ;;
    *)
        warn "xcode-select points at '${DEVELOPER_DIR_NOW:-<nothing>}', not a full Xcode; xcodebuild and simctl need one:"
        warn "  sudo xcode-select -s /Applications/Xcode.app/Contents/Developer"
        exit 5 ;;
esac
XCODE_VERSION="$(xcodebuild -version 2>/dev/null | tr '\n' ' ' | sed 's/ *$//' || true)"
if ! xcodebuild -showsdks 2>/dev/null | grep -q -- '-sdk iphonesimulator'; then
    warn "no iOS Simulator SDK in $DEVELOPER_DIR_NOW ($XCODE_VERSION)."
    warn "  fix: Xcode > Settings > Components, add the iOS platform (or: xcodebuild -downloadPlatform iOS)"
    exit 5
fi
RUNTIMES="$(xcrun simctl list runtimes 2>/dev/null | grep '^iOS ' | grep -v -i unavailable || true)"
if [ -z "$RUNTIMES" ]; then
    warn "no usable iOS simulator runtime ($XCODE_VERSION)."
    warn "  fix: Xcode > Settings > Components, download an iOS simulator runtime (or: xcodebuild -downloadPlatform iOS)"
    exit 5
fi

# DEVICES: "<udid>|<state>|<name>|<runtime>" per available device.
DEVICES=""
runtime=""
while IFS= read -r line; do
    case "$line" in
        "-- "*" --")
            runtime="${line#-- }"
            runtime="${runtime% --}"
            continue ;;
    esac
    parsed="$(printf '%s\n' "$line" | sed -nE 's/^ *(.*[^ ]) +\(([0-9A-Fa-f-]{36})\) \((Booted|Shutdown|Booting|Shutting Down)\).*$/\2|\3|\1/p')"
    if [ -n "$parsed" ]; then
        DEVICES="$DEVICES$parsed|$runtime"$'\n'
    fi
done < <(xcrun simctl list devices available 2>/dev/null || true)

field()
{
    printf '%s\n' "$1" | cut -d '|' -f "$2"
}

if [ "$PROBE" -eq 1 ]; then
    say "xcode: $XCODE_VERSION at $DEVELOPER_DIR_NOW"
    say "simulator SDK: $(xcodebuild -showsdks 2>/dev/null | grep -- '-sdk iphonesimulator' | sed 's/^[[:space:]]*//' | tr '\n' ' ')"
    say "runtimes: $(printf '%s\n' "$RUNTIMES" | sed 's/ - com.apple.*//' | tr '\n' ';' | sed 's/;$//')"
    n_all="$(printf '%s' "$DEVICES" | grep -c . || true)"
    n_iphone="$(printf '%s' "$DEVICES" | awk -F '|' '$3 ~ /^iPhone/' | grep -c . || true)"
    n_booted="$(printf '%s' "$DEVICES" | awk -F '|' '$2 == "Booted"' | grep -c . || true)"
    say "simulators: $n_all available ($n_iphone iPhone), $n_booted booted"
    printf '%s' "$DEVICES" | awk -F '|' '{ printf "ios-sim-selfcheck:   %-10s %s (%s) [%s]\n", $2, $3, $4, $1 }'
    say "project=$XCPROJ ($([ -d "$XCPROJ" ] && echo present || echo 'missing; run tools/unity-gate.sh build-ios-sim'))"
    if [ "$n_iphone" -gt 0 ] || [ "$n_booted" -gt 0 ]; then
        say "probe OK: a gate can run"
        exit 0
    fi
    warn "probe: no iPhone simulator to boot (xcrun simctl create \"iPhone 17\" com.apple.CoreSimulator.SimDeviceType.iPhone-17 <runtime id>)"
    exit 5
fi

if [ ! -x "$CHECK" ]; then
    warn "missing $CHECK"
    exit 5
fi

# --- 2. build ------------------------------------------------------------------------
if [ ! -d "$XCPROJ" ]; then
    warn "no Xcode project at $XCPROJ; run 'tools/unity-gate.sh build-ios-sim' first (or pass --project / HEATONCA_IOS_SIM_PROJECT)"
    exit 3
fi
mkdir -p "$LOG_DIR"
PRODUCTS="$DERIVED/Build/Products/$CONFIG-iphonesimulator"

if [ "$SKIP_BUILD" -eq 0 ]; then
    XCB=(xcodebuild -project "$XCPROJ")
    if [ -f "$XCPROJ/xcshareddata/xcschemes/$SCHEME.xcscheme" ]; then
        XCB+=(-scheme "$SCHEME" -derivedDataPath "$DERIVED")
    else
        # Unity writes a shared scheme; if it is gone, build the target and
        # aim SYMROOT at the same products directory a scheme build would use.
        warn "no shared scheme $SCHEME in $XCPROJ; building -target $SCHEME instead"
        XCB+=(-target "$SCHEME" "SYMROOT=$DERIVED/Build/Products")
    fi
    XCB+=(-sdk iphonesimulator -configuration "$CONFIG" CODE_SIGNING_ALLOWED=NO)
    XCB+=(${EXTRA[@]+"${EXTRA[@]}"} build)
    say "xcodebuild log=$XCODEBUILD_LOG"
    printf 'ios-sim-selfcheck: running:'
    printf ' %q' "${XCB[@]}"
    echo
    build_start="$(date +%s)"
    set +e
    "${XCB[@]}" >"$XCODEBUILD_LOG" 2>&1
    build_rc=$?
    set -e
    if [ "$build_rc" -ne 0 ]; then
        warn "xcodebuild failed (exit $build_rc) after $(( $(date +%s) - build_start ))s; errors:"
        grep -E '(^|: )error:|\*\* BUILD FAILED' "$XCODEBUILD_LOG" | head -n 20 | sed 's/^/ios-sim-selfcheck:   /' >&2 || true
        warn "last 20 lines of $XCODEBUILD_LOG:"
        tail -n 20 "$XCODEBUILD_LOG" | sed 's/^/ios-sim-selfcheck:   /' >&2 || true
        exit 3
    fi
    say "xcodebuild succeeded in $(( $(date +%s) - build_start ))s"
fi

APP=""
for candidate in "$PRODUCTS"/*.app; do
    [ -d "$candidate" ] || continue
    id="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$candidate/Info.plist" 2>/dev/null || true)"
    if [ "$id" = "$BUNDLE" ]; then
        APP="$candidate"
        break
    fi
done
if [ -z "$APP" ]; then
    warn "no .app with CFBundleIdentifier $BUNDLE under $PRODUCTS"
    warn "  found: $(ls -d "$PRODUCTS"/*.app 2>/dev/null | tr '\n' ' ' || echo '<nothing>')"
    warn "  (override the id with HEATONCA_IOS_BUNDLE_ID, or drop --skip-build to rebuild)"
    exit 3
fi
EXE="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' "$APP/Info.plist" 2>/dev/null || echo HeatonCA)"
say "app=$APP (executable $EXE)"

# --- 3. pick a simulator --------------------------------------------------------------
pick=""
if [ -n "$SIM_DEVICE" ]; then
    pick="$(printf '%s' "$DEVICES" | awk -F '|' -v d="$SIM_DEVICE" '$1 == d || $3 == d' | sort -t '|' -k2,2 | head -n 1 || true)"
    if [ -z "$pick" ]; then
        warn "no available simulator named or identified '$SIM_DEVICE'; xcrun simctl list devices available shows:"
        printf '%s' "$DEVICES" | awk -F '|' '{ printf "ios-sim-selfcheck:   %-10s %s (%s) [%s]\n", $2, $3, $4, $1 }' >&2
        exit 5
    fi
else
    pick="$(printf '%s' "$DEVICES" | awk -F '|' '$2 == "Booted" && $3 ~ /^iPhone/' | head -n 1 || true)"
    [ -n "$pick" ] || pick="$(printf '%s' "$DEVICES" | awk -F '|' '$2 == "Booted"' | head -n 1 || true)"
    [ -n "$pick" ] || pick="$(printf '%s' "$DEVICES" | awk -F '|' '$3 ~ /^iPhone/' | head -n 1 || true)"
    if [ -z "$pick" ]; then
        warn "no iPhone simulator available (runtimes: $(printf '%s\n' "$RUNTIMES" | sed 's/ - com.apple.*//' | tr '\n' ';'))."
        warn "  fix: xcrun simctl create \"iPhone 17\" com.apple.CoreSimulator.SimDeviceType.iPhone-17 <runtime id from 'xcrun simctl list runtimes'>"
        warn "       or add one in Xcode > Window > Devices and Simulators, or set SIM_DEVICE to an iPad."
        exit 5
    fi
fi
UDID="$(field "$pick" 1)"
STATE="$(field "$pick" 2)"
NAME="$(field "$pick" 3)"
RUNTIME="$(field "$pick" 4)"
say "simulator: $NAME ($RUNTIME) [$UDID] state=$STATE"

STARTED_SIM=0
LAUNCH_PID=""
cleanup()
{
    if [ -n "$LAUNCH_PID" ]; then
        kill "$LAUNCH_PID" 2>/dev/null || true
    fi
    xcrun simctl terminate "$UDID" "$BUNDLE" >/dev/null 2>&1 || true
    if [ "$STARTED_SIM" -eq 1 ]; then
        if [ "$KEEP" = 1 ]; then
            say "leaving $NAME booted (KEEP_SIM=1); stop it with: xcrun simctl shutdown $UDID"
        else
            say "shutting down $NAME"
            xcrun simctl shutdown "$UDID" >/dev/null 2>&1 || true
        fi
    fi
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

if [ "$STATE" != "Booted" ]; then
    say "booting $NAME headless"
    if ! xcrun simctl boot "$UDID" 2>"$LOG_DIR/ios-sim-boot.err"; then
        # "Unable to boot device in current state: Booted" is a race, not a failure.
        if ! grep -q 'current state: Booted' "$LOG_DIR/ios-sim-boot.err"; then
            warn "simctl boot failed:"
            sed 's/^/ios-sim-selfcheck:   /' "$LOG_DIR/ios-sim-boot.err" >&2
            exit 3
        fi
    fi
    STARTED_SIM=1
    # bootstatus -b blocks until the device finishes booting; bound it.
    set +e
    xcrun simctl bootstatus "$UDID" -b >/dev/null 2>&1 &
    boot_pid=$!
    set -e
    boot_deadline=$(( $(date +%s) + BOOT_TIMEOUT ))
    while kill -0 "$boot_pid" 2>/dev/null; do
        if [ "$(date +%s)" -ge "$boot_deadline" ]; then
            kill "$boot_pid" 2>/dev/null || true
            warn "$NAME did not finish booting within ${BOOT_TIMEOUT}s"
            exit 3
        fi
        sleep 1
    done
    set +e
    wait "$boot_pid"
    boot_rc=$?
    set -e
    if [ "$boot_rc" -ne 0 ]; then
        warn "simctl bootstatus exited $boot_rc for $NAME"
        exit 3
    fi
    say "booted after $(( $(date +%s) - (boot_deadline - BOOT_TIMEOUT) ))s"
fi

# --- 4. install, launch, watch -------------------------------------------------------------
say "installing $APP"
if ! install_out="$(xcrun simctl install "$UDID" "$APP" 2>&1)"; then
    warn "simctl install failed:"
    printf '%s\n' "$install_out" | sed 's/^/ios-sim-selfcheck:   /' >&2
    exit 3
fi
xcrun simctl terminate "$UDID" "$BUNDLE" >/dev/null 2>&1 || true
rm -f "$LOG"

say "launching $BUNDLE (log source: $LOG_SOURCE, log $LOG)"
case "$LOG_SOURCE" in
    console)
        # simctl blocks for the app's lifetime and relays its stdout/stderr;
        # it goes to a file rather than a pipe so a PASS that ends the watch
        # early cannot SIGPIPE it into a misleading exit status.
        set +e
        xcrun simctl launch --console "$UDID" "$BUNDLE" >"$LOG" 2>&1 </dev/null &
        LAUNCH_PID=$!
        set -e ;;
    stream)
        set +e
        xcrun simctl spawn "$UDID" log stream --style compact --predicate "process == \"$EXE\"" >"$LOG" 2>&1 </dev/null &
        LAUNCH_PID=$!
        set -e
        sleep 2
        if ! launch_out="$(xcrun simctl launch "$UDID" "$BUNDLE" 2>&1)"; then
            warn "simctl launch failed:"
            printf '%s\n' "$launch_out" | sed 's/^/ios-sim-selfcheck:   /' >&2
            exit 3
        fi
        say "$launch_out" ;;
esac
sleep 1
if ! kill -0 "$LAUNCH_PID" 2>/dev/null; then
    warn "simctl exited at once; its output:"
    tail -n 20 "$LOG" | sed 's/^/ios-sim-selfcheck:   /' >&2 || true
    exit 3
fi

START="$(date +%s)"
set +e
"$CHECK" --timeout "$TIMEOUT" --pid "$LAUNCH_PID" "$LOG"
CHECK_EXIT=$?
set -e
ELAPSED=$(( $(date +%s) - START ))

case "$CHECK_EXIT" in
    0) say "PASS simulator=\"$NAME\" runtime=\"$RUNTIME\" udid=$UDID elapsed=${ELAPSED}s log=$LOG" ;;
    1) warn "FAIL: the player logged SELF-CHECK FAIL (simulator $NAME, elapsed ${ELAPSED}s, log $LOG)" ;;
    *)
        warn "FAIL: no verdict within ${TIMEOUT}s (simulator $NAME, log $LOG)"
        if [ ! -s "$LOG" ]; then
            if [ "$LOG_SOURCE" = console ]; then
                warn "  the console capture is empty. If the app is running (xcrun simctl spawn $UDID launchctl list | grep -i heaton), its"
                warn "  log output is not reaching the console on this runtime: rerun with SIM_LOG_SOURCE=stream (or --log-source stream)."
            else
                warn "  the log stream capture is empty; check that the app launched: xcrun simctl launch $UDID $BUNDLE"
            fi
        else
            warn "  last lines of the capture:"
            tail -n 15 "$LOG" | sed 's/^/ios-sim-selfcheck:   /' >&2 || true
        fi ;;
esac
exit "$CHECK_EXIT"
