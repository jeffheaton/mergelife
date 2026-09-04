#!/usr/bin/env bash
# macos-selfcheck.sh -- run the built macOS player headless and gate on its
# determinism self-check.
#
# `tools/unity-gate.sh build-macos` (CIBuild.MacOS) writes
# build/macos/HeatonCA.app. This script launches the player binary inside it
# with
#
#   -batchmode -nographics -selfcheck -logFile build/logs/mac-selfcheck.log
#
# AppController honors -selfcheck by running DeterminismSelfCheck, logging
# "[HeatonCA] SELF-CHECK PASS|FAIL" plus the report, and calling
# Application.Quit(passed ? 0 : 1). Two things must agree for a pass: the
# player's exit code is 0 AND tools/check-selfcheck.sh finds the PASS line in
# the log. A player that exits 0 without logging a verdict, or that logs PASS
# and then crashes on the way out, fails the gate.
#
# Usage:
#   tools/macos-selfcheck.sh [--app <HeatonCA.app>] [--timeout <s>] [--log <file>]
#
# Environment:
#   HEATONCA_APP        the .app bundle (default build/macos/HeatonCA.app)
#   SELFCHECK_TIMEOUT   seconds to wait for the verdict and the exit (default 120)
#
# Exit codes:
#   0  player exited 0 and the log holds "[HeatonCA] SELF-CHECK PASS"
#   1  self-check FAIL (the player's exit code when it is 1..255, else 1)
#   2  no verdict: the player did not exit or log a verdict within the timeout
#   3  the .app or its executable is missing (run the build-macos gate first)
#   5  environment problem (not macOS, or the player could not be started)
#
# SANDBOX: in Claude Code run this with the Bash sandbox disabled, like
# tools/unity-gate.sh. The player needs the window server even in -batchmode,
# and writes PlayerPrefs and its Player.log under ~/Library.
#
# macOS ships no `timeout`, so the watchdog is a bash loop: TERM at the
# deadline, KILL five seconds later. Runs under /bin/bash 3.2.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
PROJECT="$(cd "$SCRIPT_DIR/.." && pwd -P)"
CHECK="$SCRIPT_DIR/check-selfcheck.sh"

APP="${HEATONCA_APP:-$PROJECT/build/macos/HeatonCA.app}"
TIMEOUT="${SELFCHECK_TIMEOUT:-120}"
LOG="$PROJECT/build/logs/mac-selfcheck.log"

usage()
{
    cat <<__USAGE__
usage: tools/macos-selfcheck.sh [--app <HeatonCA.app>] [--timeout <s>] [--log <file>]
       runs <app>/Contents/MacOS/<exe> -batchmode -nographics -selfcheck -logFile <log>
env:   HEATONCA_APP  SELFCHECK_TIMEOUT
exit:  0 PASS, 1 FAIL, 2 no verdict within the timeout, 3 app missing, 5 environment
note:  run with the Claude Code Bash sandbox disabled
__USAGE__
}

fail_usage()
{
    echo "macos-selfcheck: $*" >&2
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
        --app)
            [ $# -ge 2 ] || fail_usage "--app needs a value"
            APP="$2"
            shift ;;
        --timeout)
            [ $# -ge 2 ] || fail_usage "--timeout needs a value"
            is_uint "$2" || fail_usage "--timeout must be a non-negative integer (got '$2')"
            TIMEOUT="$2"
            shift ;;
        --log)
            [ $# -ge 2 ] || fail_usage "--log needs a value"
            LOG="$2"
            shift ;;
        -h|--help) usage; exit 0 ;;
        *) fail_usage "unknown argument '$1'" ;;
    esac
    shift
done
is_uint "$TIMEOUT" || fail_usage "SELFCHECK_TIMEOUT must be a non-negative integer (got '$TIMEOUT')"

if [ "$(uname -s)" != "Darwin" ]; then
    echo "macos-selfcheck: this gate runs the macOS player and needs macOS (uname: $(uname -s))" >&2
    exit 5
fi
if [ ! -x "$CHECK" ]; then
    echo "macos-selfcheck: missing $CHECK" >&2
    exit 5
fi
if [ ! -d "$APP" ]; then
    echo "macos-selfcheck: no app bundle at $APP; run 'tools/unity-gate.sh build-macos' first (or pass --app / HEATONCA_APP)" >&2
    exit 3
fi

# The executable name follows productName; read it from the bundle rather
# than assuming, falling back to HeatonCA.
EXE="HeatonCA"
if [ -f "$APP/Contents/Info.plist" ]; then
    EXE="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' "$APP/Contents/Info.plist" 2>/dev/null || echo HeatonCA)"
fi
BIN="$APP/Contents/MacOS/$EXE"
if [ ! -x "$BIN" ]; then
    echo "macos-selfcheck: player executable not found or not executable: $BIN" >&2
    exit 3
fi

mkdir -p "$(dirname "$LOG")"
rm -f "$LOG"   # never read a previous run's verdict

echo "macos-selfcheck: app=$APP"
echo "macos-selfcheck: log=$LOG"
printf 'macos-selfcheck: running:'
printf ' %q' "$BIN" -batchmode -nographics -selfcheck -logFile "$LOG"
echo

START="$(date +%s)"
set +e
"$BIN" -batchmode -nographics -selfcheck -logFile "$LOG" </dev/null >/dev/null 2>&1 &
PLAYER_PID=$!
set -e

# Watch the log while the player runs; --pid ends the wait early if the player
# exits without a verdict (a binary refused at launch lands there too, with
# an empty log and a non-zero exit code below).
set +e
"$CHECK" --timeout "$TIMEOUT" --pid "$PLAYER_PID" "$LOG"
CHECK_EXIT=$?
set -e

# -selfcheck makes the player quit on its own; give it the rest of the
# timeout to do so, then put it down. The group runs with the shell's stderr
# on /dev/null so bash's own "Terminated: 15" job notice (printed when it
# reaps a child the watchdog killed) cannot be mistaken for a script crash;
# this script's messages go to the saved fd 3 instead, and the exit status
# (128 + signal) still comes through wait.
TIMED_OUT=0
DEADLINE=$((START + TIMEOUT))
PLAYER_EXIT=0
exec 3>&2
{
    while kill -0 "$PLAYER_PID" 2>/dev/null; do
        if [ "$(date +%s)" -ge "$DEADLINE" ]; then
            TIMED_OUT=1
            echo "macos-selfcheck: player pid $PLAYER_PID still running after ${TIMEOUT}s; sending TERM" >&3
            kill -TERM "$PLAYER_PID" 2>/dev/null || true
            for _ in 1 2 3 4 5; do
                kill -0 "$PLAYER_PID" 2>/dev/null || break
                sleep 1
            done
            if kill -0 "$PLAYER_PID" 2>/dev/null; then
                echo "macos-selfcheck: sending KILL" >&3
                kill -KILL "$PLAYER_PID" 2>/dev/null || true
            fi
            break
        fi
        sleep 1
    done
    set +e
    wait "$PLAYER_PID"
    PLAYER_EXIT=$?
    set -e
} 2>/dev/null
exec 3>&-
ELAPSED=$(( $(date +%s) - START ))

if [ "$TIMED_OUT" -eq 1 ]; then
    echo "macos-selfcheck: FAIL: the player did not exit within ${TIMEOUT}s (check exit $CHECK_EXIT, log $LOG)" >&2
    exit 2
fi
if [ "$PLAYER_EXIT" -ne 0 ]; then
    echo "macos-selfcheck: FAIL: player exit=$PLAYER_EXIT check=$CHECK_EXIT elapsed=${ELAPSED}s log=$LOG" >&2
    if [ ! -s "$LOG" ]; then
        echo "macos-selfcheck: the log is empty; the player may have been refused before Unity started (Gatekeeper quarantine? try: xattr -dr com.apple.quarantine $APP)" >&2
    fi
    if [ "$PLAYER_EXIT" -ge 1 ] && [ "$PLAYER_EXIT" -le 255 ]; then
        exit "$PLAYER_EXIT"
    fi
    exit 1
fi
case "$CHECK_EXIT" in
    0) echo "macos-selfcheck: PASS player exit=0 check=0 elapsed=${ELAPSED}s log=$LOG" ;;
    1) echo "macos-selfcheck: FAIL: the player exited 0 but the log says SELF-CHECK FAIL (elapsed ${ELAPSED}s, log $LOG)" >&2 ;;
    *) echo "macos-selfcheck: FAIL: the player exited 0 but logged no verdict (check exit $CHECK_EXIT, elapsed ${ELAPSED}s, log $LOG)" >&2 ;;
esac
exit "$CHECK_EXIT"
