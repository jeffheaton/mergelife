#!/usr/bin/env bash
# android-selfcheck.sh -- install the built APK on an Android emulator or
# device and gate on the determinism self-check in logcat.
#
# Android is HeatonCA's first IL2CPP target, so this is the first proof that
# the C++-compiled engine still reproduces the pinned vectors. The flow:
#
#   1. Find an Android SDK: ANDROID_SDK_ROOT, ANDROID_HOME,
#      ~/Library/Android/sdk, the SDK next to $UNITY, or Unity's bundled
#      /Applications/Unity/Hub/Editor/6000.5.0f1/PlaybackEngines/AndroidPlayer/SDK
#      (that one carries adb but no emulator and no system images). adb and
#      emulator may come from different roots.
#   2. Pick a target: ANDROID_SERIAL if set; else a device `adb devices` lists
#      as "device" (an emulator running the AVD named by AVD wins, then any
#      emulator, then a USB device); else boot the AVD named by AVD (default
#      Play_Phone) with -no-window -no-audio -no-boot-anim and wait for
#      sys.boot_completed.
#   3. adb install -r build/android/HeatonCA.apk (one automatic uninstall +
#      retry when the installed copy has a different signature or a higher
#      version code).
#   4. adb logcat -c, capture "Unity" logcat lines to
#      build/logs/android-selfcheck.log, launch with
#      `adb shell monkey -p com.heatonresearch.heatonca -c android.intent.category.LAUNCHER 1`,
#      and run tools/check-selfcheck.sh on the capture with a 90 s timeout.
#      Android apps take no command line, so the app runs normally and logs
#      the verdict at boot; nothing quits, the script force-stops it.
#   5. Clean up: stop logcat and the app; shut down an emulator this script
#      booted (KEEP_EMULATOR=1 or --keep-emulator leaves it running for the
#      next gate). An emulator that was already running is left alone.
#
# Usage:
#   tools/android-selfcheck.sh [--apk <file>] [--avd <name>] [--timeout <s>]
#                              [--boot-timeout <s>] [--keep-emulator] [--probe]
#
#   --probe   report the SDK, adb, emulator, AVDs, and attached devices found,
#             then exit (0 when a gate could run, 5 otherwise). Boots nothing.
#
# Environment:
#   ANDROID_SDK_ROOT, ANDROID_HOME   SDK roots, searched first
#   UNITY                            editor binary; its PlaybackEngines SDK is searched too
#   ANDROID_SERIAL                   use this device/emulator serial, never boot one
#   AVD                              AVD to boot when no device is attached (default Play_Phone)
#   HEATONCA_APK                     APK to install (default build/android/HeatonCA.apk)
#   HEATONCA_ANDROID_PACKAGE         application id (default com.heatonresearch.heatonca)
#   SELFCHECK_TIMEOUT                seconds to wait for the verdict (default 90)
#   BOOT_TIMEOUT                     seconds to wait for the emulator to boot (default 300)
#   KEEP_EMULATOR=1                  do not shut down an emulator this script booted
#
# Exit codes:
#   0  "[HeatonCA] SELF-CHECK PASS" seen in logcat
#   1  "SELF-CHECK FAIL" seen
#   2  no verdict within the timeout
#   3  a step failed: APK missing, emulator did not boot, install or launch failed
#   5  environment: no SDK/adb, or no device and no emulator/AVD to boot (the
#      message says what to install)
#
# Logs: build/logs/android-selfcheck.log (logcat capture, kept),
#       build/logs/android-emulator.log (emulator stdout/stderr when booted here).
#
# SANDBOX: in Claude Code run with the Bash sandbox disabled: the adb server
# socket, the emulator, and ~/.android are all outside it.
#
# Emulator quirks (from heaton-life-unity, proved 2026-08-23 on Play_Phone):
# a Play-Store AVD whose userdata predates the host's current adb key sits
# "unauthorized" forever -- relaunch it once with `emulator -avd Play_Phone
# -wipe-data`; and Android's one-time "Viewing full screen" panel steals focus
# on first launch, which under runInBackground 0 pauses the player before
# Awake -- this script pre-confirms it with
# `settings put secure immersive_mode_confirmations confirmed`.
# Runs under /bin/bash 3.2.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
PROJECT="$(cd "$SCRIPT_DIR/.." && pwd -P)"
CHECK="$SCRIPT_DIR/check-selfcheck.sh"

APK="${HEATONCA_APK:-$PROJECT/build/android/HeatonCA.apk}"
PACKAGE="${HEATONCA_ANDROID_PACKAGE:-com.heatonresearch.heatonca}"
AVD="${AVD:-Play_Phone}"
TIMEOUT="${SELFCHECK_TIMEOUT:-90}"
BOOT_TIMEOUT="${BOOT_TIMEOUT:-300}"
KEEP="${KEEP_EMULATOR:-0}"
PROBE=0
LOG_DIR="$PROJECT/build/logs"
LOG="$LOG_DIR/android-selfcheck.log"
EMU_LOG="$LOG_DIR/android-emulator.log"
UNITY_SDK="/Applications/Unity/Hub/Editor/6000.5.0f1/PlaybackEngines/AndroidPlayer/SDK"

usage()
{
    cat <<__USAGE__
usage: tools/android-selfcheck.sh [--apk <file>] [--avd <name>] [--timeout <s>] [--boot-timeout <s>] [--keep-emulator] [--probe]
       installs the APK on an attached device or the AVD it boots, launches it, and reads the self-check verdict from logcat
env:   ANDROID_SDK_ROOT ANDROID_HOME ANDROID_SERIAL AVD HEATONCA_APK HEATONCA_ANDROID_PACKAGE SELFCHECK_TIMEOUT BOOT_TIMEOUT KEEP_EMULATOR
exit:  0 PASS, 1 FAIL, 2 no verdict, 3 step failed, 5 environment (no SDK, device, or AVD)
note:  run with the Claude Code Bash sandbox disabled
__USAGE__
}

say()
{
    echo "android-selfcheck: $*"
}

warn()
{
    echo "android-selfcheck: $*" >&2
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
        --apk)
            [ $# -ge 2 ] || fail_usage "--apk needs a value"
            APK="$2"
            shift ;;
        --avd)
            [ $# -ge 2 ] || fail_usage "--avd needs a value"
            AVD="$2"
            shift ;;
        --timeout)
            [ $# -ge 2 ] || fail_usage "--timeout needs a value"
            is_uint "$2" || fail_usage "--timeout must be a non-negative integer (got '$2')"
            TIMEOUT="$2"
            shift ;;
        --boot-timeout)
            [ $# -ge 2 ] || fail_usage "--boot-timeout needs a value"
            is_uint "$2" || fail_usage "--boot-timeout must be a non-negative integer (got '$2')"
            BOOT_TIMEOUT="$2"
            shift ;;
        --keep-emulator) KEEP=1 ;;
        --probe) PROBE=1 ;;
        -h|--help) usage; exit 0 ;;
        *) fail_usage "unknown argument '$1'" ;;
    esac
    shift
done
is_uint "$TIMEOUT" || fail_usage "SELFCHECK_TIMEOUT must be a non-negative integer (got '$TIMEOUT')"
is_uint "$BOOT_TIMEOUT" || fail_usage "BOOT_TIMEOUT must be a non-negative integer (got '$BOOT_TIMEOUT')"

# --- 1. locate the SDK ------------------------------------------------------------
ROOTS=()
[ -n "${ANDROID_SDK_ROOT:-}" ] && ROOTS+=("$ANDROID_SDK_ROOT")
[ -n "${ANDROID_HOME:-}" ] && ROOTS+=("$ANDROID_HOME")
ROOTS+=("$HOME/Library/Android/sdk")
if [ -n "${UNITY:-}" ]; then
    # .../Editor/<ver>/Unity.app/Contents/MacOS/Unity -> .../Editor/<ver>/PlaybackEngines/AndroidPlayer/SDK
    ROOTS+=("${UNITY%%/Unity.app/*}/PlaybackEngines/AndroidPlayer/SDK")
fi
ROOTS+=("$UNITY_SDK")
for d in /Applications/Unity/Hub/Editor/*/PlaybackEngines/AndroidPlayer/SDK; do
    [ -d "$d" ] && ROOTS+=("$d")
done

ADB=""
ADB_ROOT=""
EMULATOR=""
EMULATOR_ROOT=""
for root in ${ROOTS[@]+"${ROOTS[@]}"}; do
    [ -d "$root" ] || continue
    if [ -z "$ADB" ] && [ -x "$root/platform-tools/adb" ]; then
        ADB="$root/platform-tools/adb"
        ADB_ROOT="$root"
    fi
    if [ -z "$EMULATOR" ] && [ -x "$root/emulator/emulator" ]; then
        EMULATOR="$root/emulator/emulator"
        EMULATOR_ROOT="$root"
    fi
done
if [ -z "$ADB" ] && command -v adb >/dev/null 2>&1; then
    ADB="$(command -v adb)"
    ADB_ROOT="(PATH)"
fi
if [ -z "$EMULATOR" ] && command -v emulator >/dev/null 2>&1; then
    EMULATOR="$(command -v emulator)"
    EMULATOR_ROOT="(PATH)"
fi

if [ -z "$ADB" ]; then
    warn "no Android SDK with platform-tools/adb found."
    warn "  searched: ANDROID_SDK_ROOT=${ANDROID_SDK_ROOT:-<unset>} ANDROID_HOME=${ANDROID_HOME:-<unset>} $HOME/Library/Android/sdk $UNITY_SDK"
    warn "  fix: install Android Studio (or the command-line tools + 'sdkmanager platform-tools emulator'),"
    warn "       then export ANDROID_SDK_ROOT=<sdk> or install into ~/Library/Android/sdk. Unity's Android"
    warn "       module puts adb under $UNITY_SDK, which is enough for a USB device but has no emulator."
    exit 5
fi
# The emulator wants to know the SDK it belongs to; point it at its own root.
if [ -n "$EMULATOR_ROOT" ] && [ "$EMULATOR_ROOT" != "(PATH)" ]; then
    export ANDROID_SDK_ROOT="$EMULATOR_ROOT"
    export ANDROID_HOME="$EMULATOR_ROOT"
fi

# adb_devices: "<serial>\t<state>" per attached device, header and daemon
# chatter removed. Empty (not fatal) when the adb server cannot start.
adb_devices()
{
    "$ADB" devices 2>/dev/null | tr -d '\r' | awk 'NR > 1 && NF >= 2 && $1 !~ /^\*/ { print $1 "\t" $2 }' || true
}

# avd_list: one AVD name per line (the emulator prints INFO/WARNING chatter on stderr).
avd_list()
{
    [ -n "$EMULATOR" ] || return 0
    "$EMULATOR" -list-avds 2>/dev/null | tr -d '\r' | sed '/^$/d' | grep -v '^INFO\|^WARNING' || true
}

# avd_of <serial>: the AVD name an emulator serial is running ("" for a device).
avd_of()
{
    case "$1" in
        emulator-*) "$ADB" -s "$1" emu avd name 2>/dev/null | tr -d '\r' | head -n 1 ;;
        *) echo "" ;;
    esac
}

if [ "$PROBE" -eq 1 ]; then
    say "adb=$ADB (root $ADB_ROOT), $("$ADB" version 2>/dev/null | sed -n '2p' || true)"
    if [ -n "$EMULATOR" ]; then
        say "emulator=$EMULATOR (root $EMULATOR_ROOT)"
        avds="$(avd_list)"
        if [ -n "$avds" ]; then
            say "AVDs: $(printf '%s' "$avds" | tr '\n' ' ') (default AVD=$AVD$(printf '%s\n' "$avds" | grep -qx "$AVD" && echo ', present' || echo ', MISSING'))"
        else
            say "AVDs: none (create one in Android Studio > Device Manager, or avdmanager create avd)"
        fi
    else
        say "emulator: none (Unity's bundled SDK has no emulator; install it with Android Studio or 'sdkmanager emulator')"
    fi
    devs="$(adb_devices)"
    if [ -n "$devs" ]; then
        say "attached devices (adb devices):"
        printf '%s\n' "$devs" | sed 's/^/android-selfcheck:   /'
    else
        say "attached devices: none"
    fi
    say "apk=$APK ($([ -f "$APK" ] && echo present || echo 'missing; run tools/unity-gate.sh build-android'))"
    if [ -n "$devs" ] || { [ -n "$EMULATOR" ] && avd_list | grep -qx "$AVD"; }; then
        say "probe OK: a gate can run"
        exit 0
    fi
    warn "probe: no attached device and no AVD named '$AVD' to boot"
    exit 5
fi

if [ ! -x "$CHECK" ]; then
    warn "missing $CHECK"
    exit 5
fi
if [ ! -f "$APK" ]; then
    warn "no APK at $APK; run 'tools/unity-gate.sh build-android' first (or pass --apk / HEATONCA_APK)"
    exit 3
fi
mkdir -p "$LOG_DIR"

# --- cleanup ------------------------------------------------------------------------
SERIAL=""
EMU_PID=""
STARTED_EMULATOR=0
LOGCAT_PID=""
cleanup()
{
    if [ -n "$LOGCAT_PID" ]; then
        kill "$LOGCAT_PID" 2>/dev/null || true
    fi
    if [ -n "$SERIAL" ]; then
        "$ADB" -s "$SERIAL" shell am force-stop "$PACKAGE" >/dev/null 2>&1 || true
    fi
    if [ "$STARTED_EMULATOR" -eq 1 ]; then
        if [ "$KEEP" = 1 ]; then
            say "leaving emulator $SERIAL ($AVD) running (KEEP_EMULATOR=1); stop it with: $ADB -s $SERIAL emu kill"
        else
            say "shutting down emulator $SERIAL ($AVD)"
            "$ADB" -s "$SERIAL" emu kill >/dev/null 2>&1 || true
            for _ in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20; do
                kill -0 "$EMU_PID" 2>/dev/null || break
                sleep 1
            done
            if kill -0 "$EMU_PID" 2>/dev/null; then
                kill -TERM "$EMU_PID" 2>/dev/null || true
            fi
        fi
    fi
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

# --- 2. pick a device ---------------------------------------------------------------
"$ADB" start-server >/dev/null 2>&1 || true

if [ -n "${ANDROID_SERIAL:-}" ]; then
    SERIAL="$ANDROID_SERIAL"
    say "using ANDROID_SERIAL=$SERIAL"
else
    devs="$(adb_devices)"
    ready="$(printf '%s\n' "$devs" | awk -F '\t' '$2 == "device" { print $1 }')"
    others="$(printf '%s\n' "$devs" | awk -F '\t' '$2 != "device" && NF { print $1 " (" $2 ")" }')"
    if [ -n "$others" ]; then
        warn "ignoring devices not in state 'device': $(printf '%s' "$others" | tr '\n' ' ')"
        if printf '%s\n' "$others" | grep -q unauthorized; then
            warn "an 'unauthorized' emulator whose userdata predates this host's adb key never recovers; relaunch it once with: $EMULATOR -avd <name> -wipe-data"
        fi
    fi
    if [ -n "$ready" ]; then
        # Prefer the emulator running $AVD, then any emulator, then a USB device.
        for s in $ready; do
            if [ "$(avd_of "$s")" = "$AVD" ]; then
                SERIAL="$s"
                break
            fi
        done
        if [ -z "$SERIAL" ]; then
            SERIAL="$(printf '%s\n' "$ready" | grep '^emulator-' | head -n 1 || true)"
        fi
        if [ -z "$SERIAL" ]; then
            SERIAL="$(printf '%s\n' "$ready" | head -n 1)"
        fi
        say "using attached $SERIAL$( [ -n "$(avd_of "$SERIAL")" ] && echo " (AVD $(avd_of "$SERIAL"))")"
    fi
fi

if [ -z "$SERIAL" ]; then
    if [ -z "$EMULATOR" ]; then
        warn "no device attached and no emulator binary in any SDK root (adb is $ADB)."
        warn "  fix: connect a device over USB with USB debugging on (adb devices must list it as 'device'),"
        warn "       or install the emulator + a system image + an AVD (Android Studio > Device Manager,"
        warn "       or: sdkmanager emulator 'system-images;android-36;google_apis;arm64-v8a' && avdmanager create avd -n $AVD -k ...)."
        exit 5
    fi
    avds="$(avd_list)"
    if [ -z "$avds" ] || ! printf '%s\n' "$avds" | grep -qx "$AVD"; then
        warn "no device attached and no AVD named '$AVD' (emulator $EMULATOR lists: $(printf '%s' "${avds:-<none>}" | tr '\n' ' '))."
        warn "  fix: create it in Android Studio > Device Manager (a Play Store phone image is what the store"
        warn "       screenshots use), or set AVD=<one of the names above>, or attach a device over USB."
        exit 5
    fi
    before="$(adb_devices | awk -F '\t' '{ print $1 }')"
    say "booting AVD $AVD headless (log $EMU_LOG)"
    printf 'android-selfcheck: running:'
    printf ' %q' "$EMULATOR" -avd "$AVD" -no-window -no-audio -no-boot-anim
    echo
    set +e
    "$EMULATOR" -avd "$AVD" -no-window -no-audio -no-boot-anim </dev/null >"$EMU_LOG" 2>&1 &
    EMU_PID=$!
    set -e
    STARTED_EMULATOR=1
    boot_deadline=$(( $(date +%s) + BOOT_TIMEOUT ))
    while [ -z "$SERIAL" ]; do
        if ! kill -0 "$EMU_PID" 2>/dev/null; then
            warn "the emulator exited before adb saw it; last lines of $EMU_LOG:"
            tail -n 20 "$EMU_LOG" >&2 || true
            STARTED_EMULATOR=0
            exit 3
        fi
        if [ "$(date +%s)" -ge "$boot_deadline" ]; then
            warn "no new emulator serial within ${BOOT_TIMEOUT}s; last lines of $EMU_LOG:"
            tail -n 20 "$EMU_LOG" >&2 || true
            exit 3
        fi
        for s in $(adb_devices | awk -F '\t' '{ print $1 }' | grep '^emulator-' || true); do
            if ! printf '%s\n' "$before" | grep -qx "$s"; then
                SERIAL="$s"
                break
            fi
        done
        [ -n "$SERIAL" ] || sleep 2
    done
    say "emulator is $SERIAL; waiting for sys.boot_completed"
    "$ADB" -s "$SERIAL" wait-for-device
    while :; do
        booted="$("$ADB" -s "$SERIAL" shell getprop sys.boot_completed 2>/dev/null | tr -d '\r' || true)"
        [ "$booted" = "1" ] && break
        if ! kill -0 "$EMU_PID" 2>/dev/null; then
            warn "the emulator exited while booting; last lines of $EMU_LOG:"
            tail -n 20 "$EMU_LOG" >&2 || true
            STARTED_EMULATOR=0
            exit 3
        fi
        if [ "$(date +%s)" -ge "$boot_deadline" ]; then
            state="$(adb_devices | awk -F '\t' -v s="$SERIAL" '$1 == s { print $2 }')"
            warn "sys.boot_completed not set within ${BOOT_TIMEOUT}s (adb state: ${state:-gone})"
            if [ "$state" = "unauthorized" ]; then
                warn "relaunch the AVD once with: $EMULATOR -avd $AVD -wipe-data"
            fi
            exit 3
        fi
        sleep 2
    done
    say "boot completed after $(( $(date +%s) - (boot_deadline - BOOT_TIMEOUT) ))s"
fi

"$ADB" -s "$SERIAL" wait-for-device
RELEASE="$("$ADB" -s "$SERIAL" shell getprop ro.build.version.release 2>/dev/null | tr -d '\r' || true)"
SDK_LEVEL="$("$ADB" -s "$SERIAL" shell getprop ro.build.version.sdk 2>/dev/null | tr -d '\r' || true)"
ABI="$("$ADB" -s "$SERIAL" shell getprop ro.product.cpu.abi 2>/dev/null | tr -d '\r' || true)"
say "target $SERIAL: Android ${RELEASE:-?} (API ${SDK_LEVEL:-?}), ${ABI:-?}"

# Keep the launch from being parked behind system UI: pre-confirm the
# "Viewing full screen" panel, wake the screen, drop the keyguard.
"$ADB" -s "$SERIAL" shell settings put secure immersive_mode_confirmations confirmed >/dev/null 2>&1 || true
"$ADB" -s "$SERIAL" shell input keyevent KEYCODE_WAKEUP >/dev/null 2>&1 || true
"$ADB" -s "$SERIAL" shell wm dismiss-keyguard >/dev/null 2>&1 || true

# --- 3. install -----------------------------------------------------------------------
say "installing $APK"
set +e
install_out="$("$ADB" -s "$SERIAL" install -r "$APK" 2>&1)"
install_rc=$?
set -e
if [ "$install_rc" -ne 0 ] || printf '%s\n' "$install_out" | grep -q 'INSTALL_FAILED'; then
    case "$install_out" in
        *INSTALL_FAILED_UPDATE_INCOMPATIBLE*|*INSTALL_FAILED_VERSION_DOWNGRADE*|*INSTALL_FAILED_ALREADY_EXISTS*)
            warn "installed copy conflicts ($(printf '%s\n' "$install_out" | grep -o 'INSTALL_FAILED_[A-Z_]*' | head -n 1)); uninstalling $PACKAGE and retrying"
            "$ADB" -s "$SERIAL" uninstall "$PACKAGE" >/dev/null 2>&1 || true
            set +e
            install_out="$("$ADB" -s "$SERIAL" install -r "$APK" 2>&1)"
            install_rc=$?
            set -e ;;
    esac
fi
if [ "$install_rc" -ne 0 ] || printf '%s\n' "$install_out" | grep -q 'INSTALL_FAILED'; then
    warn "adb install failed (exit $install_rc):"
    printf '%s\n' "$install_out" | sed 's/^/android-selfcheck:   /' >&2
    exit 3
fi
say "installed ($(printf '%s\n' "$install_out" | grep -v '^Performing' | tr '\n' ' ' | sed 's/ *$//'))"

# --- 4. launch and watch logcat ----------------------------------------------------------
"$ADB" -s "$SERIAL" shell am force-stop "$PACKAGE" >/dev/null 2>&1 || true
"$ADB" -s "$SERIAL" logcat -c >/dev/null 2>&1 || true
rm -f "$LOG"
# Unity logs under the "Unity" tag; AndroidRuntime/DEBUG carry Java and
# native crashes, everything else is silenced so the capture stays small.
set +e
"$ADB" -s "$SERIAL" logcat -v time Unity:V AndroidRuntime:E DEBUG:E '*:S' >"$LOG" 2>&1 </dev/null &
LOGCAT_PID=$!
set -e
sleep 1
if ! kill -0 "$LOGCAT_PID" 2>/dev/null; then
    warn "adb logcat exited at once:"
    tail -n 10 "$LOG" >&2 || true
    exit 3
fi

say "launching $PACKAGE"
set +e
monkey_out="$("$ADB" -s "$SERIAL" shell monkey -p "$PACKAGE" -c android.intent.category.LAUNCHER 1 2>&1 | tr -d '\r')"
monkey_rc=$?
set -e
if [ "$monkey_rc" -ne 0 ] || ! printf '%s\n' "$monkey_out" | grep -q 'Events injected: 1'; then
    warn "launch failed (monkey exit $monkey_rc):"
    printf '%s\n' "$monkey_out" | sed 's/^/android-selfcheck:   /' >&2
    warn "is the APK's applicationId '$PACKAGE'? (override with HEATONCA_ANDROID_PACKAGE)"
    exit 3
fi

say "waiting up to ${TIMEOUT}s for the verdict in $LOG"
START="$(date +%s)"
set +e
"$CHECK" --timeout "$TIMEOUT" --pid "$LOGCAT_PID" "$LOG"
CHECK_EXIT=$?
set -e
ELAPSED=$(( $(date +%s) - START ))

case "$CHECK_EXIT" in
    0) say "PASS device=$SERIAL android=${RELEASE:-?}/API${SDK_LEVEL:-?} abi=${ABI:-?} elapsed=${ELAPSED}s log=$LOG" ;;
    1) warn "FAIL: the player logged SELF-CHECK FAIL (device $SERIAL, elapsed ${ELAPSED}s, log $LOG)" ;;
    *)
        warn "FAIL: no verdict within ${TIMEOUT}s (device $SERIAL, log $LOG)"
        pid="$("$ADB" -s "$SERIAL" shell pidof "$PACKAGE" 2>/dev/null | tr -d '\r' || true)"
        focus="$("$ADB" -s "$SERIAL" shell dumpsys window 2>/dev/null | tr -d '\r' | grep -E 'mCurrentFocus|mFocusedApp' | head -n 2 | sed 's/^ *//' || true)"
        warn "  process: ${pid:-not running}; window focus: ${focus:-unknown}"
        if [ ! -s "$LOG" ]; then
            warn "  the logcat capture is empty: the app produced no Unity log lines at all"
        fi
        if [ -z "$pid" ]; then
            warn "  the app is not running; look for a crash in the capture (AndroidRuntime/DEBUG lines) or run: $ADB -s $SERIAL logcat -d | grep -i -E 'heatonca|crash'"
        elif ! printf '%s' "$focus" | grep -q "$PACKAGE"; then
            warn "  the app runs but is not focused; with runInBackground 0 it pauses before logging. Something else holds the screen (a system dialog or the launcher)."
        fi ;;
esac
exit "$CHECK_EXIT"
