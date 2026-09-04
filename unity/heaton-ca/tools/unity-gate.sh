#!/usr/bin/env bash
# unity-gate.sh -- run one Unity gate for the HeatonCA project and turn the
# outcome into a pass/fail exit code.
#
# This is the ONLY sanctioned way to launch the Unity editor against
# unity/heaton-ca. Worker agents never run it; the orchestrator runs it
# serially on the main checkout after merging a phase (tools/README.md has the
# full protocol).
#
# SANDBOX: in Claude Code this script must run with the Bash sandbox DISABLED
# (dangerouslyDisableSandbox, or allow it through the /sandbox command). Unity
# needs its licensing IPC (the Unity Licensing Client and Hub sockets), its
# editor preferences and caches under ~/Library, and the platform toolchains;
# the sandbox blocks those and the editor fails in ways that look like license
# or "project already open" errors.
#
# Usage:
#   tools/unity-gate.sh <mode> [-- extra unity args]
#
# Modes (every mode adds -batchmode -nographics -projectPath <project>
# -logFile build/logs/<mode>-<utc>.log):
#   compile            -quit -executeMethod HeatonCA.Editor.CompileGate.Run
#                      (passes only when the log contains "COMPILE OK")
#   editmode           -runTests -testPlatform EditMode -testResults <xml>
#   playmode           -runTests -testPlatform PlayMode -testResults <xml>
#   build-macos        -quit -buildTarget OSXUniversal -executeMethod CIBuild.MacOS
#   build-ios          -quit -buildTarget iOS -executeMethod CIBuild.IOS
#   build-ios-sim      -quit -buildTarget iOS -executeMethod CIBuild.IOSSimulator
#   build-android      -quit -buildTarget Android -executeMethod CIBuild.Android
#   build-android-aab  -quit -buildTarget Android -executeMethod CIBuild.AndroidPlayStore
#   build-webgl        -quit -buildTarget WebGL -executeMethod CIBuild.WebGL
#
# Environment:
#   UNITY                  editor binary; default is the 6000.5.0f1 Hub install
#   MIN_TESTS              minimum NUnit @total for editmode/playmode (default 1)
#   UNITY_GATE_GRAPHICS=1  omit -nographics (only if a gate turns out to need a GPU)
#   UNITY_GATE_SKIP_PGREP=1
#                          skip the running-editor process scan (the
#                          Temp/UnityLockfile check still applies); use only
#                          after confirming by hand that no editor has the
#                          project open
#
# Exit codes:
#   0  gate passed
#   1  gate failed: Unity exited non-zero, "error CS" in the log, compile log
#      without "COMPILE OK", failed tests, fewer than MIN_TESTS tests, missing
#      results XML, or "Error building Player" in a build log
#   2  usage error
#   3  another gate holds the lock directory
#   4  the project is already open in an editor (Temp/UnityLockfile exists or a
#      Unity process has this project path on its command line)
#   5  environment problem (Unity binary or xmllint missing, or pgrep cannot
#      read the process list, which means the script is running sandboxed)
#
# Only one gate runs at a time: the lock is an atomic mkdir of
# <project>/build/.unity-gate.lock, removed on exit. It lives inside the
# project (build/ is git-ignored) rather than under TMPDIR on purpose: Claude
# Code gives sandboxed and unsandboxed shells different temp directories, so a
# TMPDIR lock could not be seen by both. A lock whose recorded pid is no longer
# running is treated as stale and removed. Unity itself refuses to open a
# project twice, so the lock plus the UnityLockfile/pgrep checks keep the
# one-Unity-instance rule from ever depending on luck.
#
# Logs: build/logs/<mode>-<utc>.log (and <mode>-<utc>.xml for test modes);
# <mode>-latest.log/.xml symlinks point at the newest run. On failure the last
# 40 lines of the log are printed.
set -euo pipefail

MODES="compile identity editmode playmode build-macos build-ios build-ios-sim build-android build-android-aab build-webgl"

usage()
{
    cat <<__USAGE__
usage: tools/unity-gate.sh <mode> [-- extra unity args]
modes: $MODES
env:   UNITY=<editor binary>  MIN_TESTS=<n>  UNITY_GATE_GRAPHICS=1  UNITY_GATE_SKIP_PGREP=1
exit:  0 pass, 1 gate failed, 2 usage, 3 lock held, 4 editor has the project open, 5 environment
note:  run with the Claude Code Bash sandbox disabled (Unity needs its licensing IPC and ~/Library)
__USAGE__
}

fail()
{
    echo "unity-gate: $*" >&2
}

if [ $# -lt 1 ]; then
    usage >&2
    exit 2
fi
case "$1" in
    -h|--help) usage; exit 0 ;;
esac

MODE="$1"
shift
valid=0
for m in $MODES; do
    if [ "$m" = "$MODE" ]; then
        valid=1
    fi
done
if [ "$valid" -ne 1 ]; then
    fail "unknown mode '$MODE'"
    usage >&2
    exit 2
fi

EXTRA=()
if [ $# -gt 0 ]; then
    if [ "$1" != "--" ]; then
        fail "extra editor arguments must follow '--'"
        usage >&2
        exit 2
    fi
    shift
    EXTRA=("$@")
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
PROJECT="$(cd "$SCRIPT_DIR/.." && pwd -P)"
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.5.0f1/Unity.app/Contents/MacOS/Unity}"
MIN_TESTS="${MIN_TESTS:-1}"
BUILD_DIR="$PROJECT/build"
LOCK_DIR="$BUILD_DIR/.unity-gate.lock"

case "$MIN_TESTS" in
    ''|*[!0-9]*)
        fail "MIN_TESTS must be a non-negative integer (got '$MIN_TESTS')"
        exit 2 ;;
esac

if [ ! -x "$UNITY" ]; then
    fail "Unity binary not found or not executable: $UNITY (set UNITY=/path/to/Unity.app/Contents/MacOS/Unity)"
    exit 5
fi
case "$MODE" in
    editmode|playmode)
        if ! command -v xmllint >/dev/null 2>&1; then
            fail "xmllint is required to parse the NUnit results"
            exit 5
        fi ;;
esac

# --- one gate at a time -------------------------------------------------------
mkdir -p "$BUILD_DIR"
take_lock()
{
    if mkdir "$LOCK_DIR" 2>/dev/null; then
        return 0
    fi
    local pid="" since="" held_mode=""
    if [ -r "$LOCK_DIR/pid" ]; then
        pid="$(cat "$LOCK_DIR/pid" 2>/dev/null || true)"
    fi
    if [ -r "$LOCK_DIR/info" ]; then
        held_mode="$(sed -n 's/^mode=//p' "$LOCK_DIR/info" 2>/dev/null || true)"
        since="$(sed -n 's/^since=//p' "$LOCK_DIR/info" 2>/dev/null || true)"
    fi
    case "$pid" in
        ''|*[!0-9]*) pid="" ;;
    esac
    if [ -n "$pid" ] && ! kill -0 "$pid" 2>/dev/null; then
        echo "unity-gate: removing stale lock $LOCK_DIR (pid $pid${held_mode:+, mode $held_mode}${since:+, since $since} is no longer running)"
        rm -rf "$LOCK_DIR"
        if mkdir "$LOCK_DIR" 2>/dev/null; then
            return 0
        fi
    fi
    fail "another gate holds $LOCK_DIR${pid:+ (pid $pid}${held_mode:+, mode $held_mode}${since:+, since $since}${pid:+)}; only one gate may run at a time. Wait for it to finish; remove the directory by hand only if you have confirmed that process and its Unity child are gone."
    exit 3
}
take_lock
trap 'rm -rf "$LOCK_DIR"' EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
trap 'exit 129' HUP
printf '%s\n' "$$" > "$LOCK_DIR/pid"
printf 'mode=%s\nsince=%s\n' "$MODE" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" > "$LOCK_DIR/info"

# --- one Unity instance per project -------------------------------------------
if [ -e "$PROJECT/Temp/UnityLockfile" ]; then
    fail "$PROJECT/Temp/UnityLockfile exists: an editor has the project open (or crashed and left the file behind). Close the editor; delete the file only after confirming no Unity process is running."
    exit 4
fi
if [ "${UNITY_GATE_SKIP_PGREP:-0}" != 1 ]; then
    # The Hub launches editors with lowercase "-projectpath", hence -i. pgrep
    # exits 1 for "no match" and 2 or more when it cannot read the process
    # list, which is exactly what happens inside the Claude Code sandbox.
    set +e
    editors="$(pgrep -if "projectpath.*(unity/heaton-ca|$PROJECT)" 2>/dev/null)"
    pgrep_exit=$?
    set -e
    if [ "$pgrep_exit" -ge 2 ]; then
        fail "pgrep cannot list processes (exit $pgrep_exit); this is what the Claude Code Bash sandbox looks like. Rerun with the sandbox disabled, or set UNITY_GATE_SKIP_PGREP=1 after confirming by hand that no editor has the project open."
        exit 5
    fi
    editors="$(printf '%s\n' "$editors" | grep -vx "$$" | grep -vx "$PPID" | sed '/^$/d' || true)"
    if [ -n "$editors" ]; then
        fail "a Unity editor already has this project open (pid $(echo "$editors" | tr '\n' ' ')); close it first. Only one Unity instance may touch the project."
        exit 4
    fi
fi

# --- logs -----------------------------------------------------------------------
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
LOG_DIR="$BUILD_DIR/logs"
mkdir -p "$LOG_DIR"
LOG="$LOG_DIR/$MODE-$STAMP.log"
RESULTS="$LOG_DIR/$MODE-$STAMP.xml"
if [ -e "$LOG" ] || [ -e "$RESULTS" ]; then
    # A second run inside the same second (only plausible when the editor
    # dies instantly): keep the paths unique so a stale results file from the
    # earlier run can never be read as this run's verdict.
    STAMP="$STAMP-$$"
    LOG="$LOG_DIR/$MODE-$STAMP.log"
    RESULTS="$LOG_DIR/$MODE-$STAMP.xml"
fi

# --- editor command line ----------------------------------------------------------
UNITY_ARGS=(-batchmode)
if [ "${UNITY_GATE_GRAPHICS:-0}" != 1 ]; then
    UNITY_ARGS+=(-nographics)
fi
UNITY_ARGS+=(-projectPath "$PROJECT")
case "$MODE" in
    compile)           UNITY_ARGS+=(-quit -executeMethod HeatonCA.Editor.CompileGate.Run) ;;
    identity)          UNITY_ARGS+=(-quit -executeMethod HeatonCA.Editor.ProjectIdentity.Apply) ;;
    editmode)          UNITY_ARGS+=(-runTests -testPlatform EditMode -testResults "$RESULTS") ;;
    playmode)          UNITY_ARGS+=(-runTests -testPlatform PlayMode -testResults "$RESULTS") ;;
    build-macos)       UNITY_ARGS+=(-quit -buildTarget OSXUniversal -executeMethod CIBuild.MacOS) ;;
    build-ios)         UNITY_ARGS+=(-quit -buildTarget iOS -executeMethod CIBuild.IOS) ;;
    build-ios-sim)     UNITY_ARGS+=(-quit -buildTarget iOS -executeMethod CIBuild.IOSSimulator) ;;
    build-android)     UNITY_ARGS+=(-quit -buildTarget Android -executeMethod CIBuild.Android) ;;
    build-android-aab) UNITY_ARGS+=(-quit -buildTarget Android -executeMethod CIBuild.AndroidPlayStore) ;;
    build-webgl)       UNITY_ARGS+=(-quit -buildTarget WebGL -executeMethod CIBuild.WebGL) ;;
esac
UNITY_ARGS+=(-logFile "$LOG")

echo "unity-gate: mode=$MODE project=$PROJECT"
echo "unity-gate: log=$LOG"
printf 'unity-gate: running:'
printf ' %q' "$UNITY" "${UNITY_ARGS[@]}" ${EXTRA[@]+"${EXTRA[@]}"}
echo

start="$(date +%s)"
set +e
"$UNITY" "${UNITY_ARGS[@]}" ${EXTRA[@]+"${EXTRA[@]}"}
UNITY_EXIT=$?
set -e
elapsed=$(( $(date +%s) - start ))

# --- verdict ---------------------------------------------------------------------
reasons=()
if [ "$UNITY_EXIT" -ne 0 ]; then
    reasons+=("Unity exited $UNITY_EXIT")
fi
if [ ! -f "$LOG" ]; then
    reasons+=("Unity wrote no log at $LOG")
    : > "$LOG"
fi
cs_errors="$(grep -c 'error CS' "$LOG" || true)"
case "$cs_errors" in
    ''|*[!0-9]*) cs_errors=0 ;;
esac
if [ "$cs_errors" -ne 0 ]; then
    reasons+=("$cs_errors 'error CS' line(s) in the log")
fi

tests_total="-"
tests_failed="-"
failed_names=""
case "$MODE" in
    compile)
        if ! grep -q 'COMPILE OK' "$LOG"; then
            reasons+=("log lacks 'COMPILE OK'")
        fi ;;
    identity)
        # ProjectIdentity.Apply ends with "[ProjectIdentity] DONE changed=N mismatches=M".
        # Anything but mismatches=0 means ProjectSettings.asset drifted from the
        # constants (or, for appleDeveloperTeamID, that a personal team leaked in).
        if ! grep -q 'DONE changed=' "$LOG"; then
            reasons+=("log lacks the ProjectIdentity DONE line")
        elif ! grep -q 'DONE changed=[0-9]* mismatches=0' "$LOG"; then
            reasons+=("$(grep -o 'DONE changed=[0-9]* mismatches=[0-9]*' "$LOG" | tail -1)")
        fi ;;
    editmode|playmode)
        if [ -f "$RESULTS" ]; then
            tests_total="$(xmllint --xpath 'string(/test-run/@total)' "$RESULTS" 2>/dev/null || true)"
            tests_failed="$(xmllint --xpath 'string(/test-run/@failed)' "$RESULTS" 2>/dev/null || true)"
            numeric=1
            case "$tests_total" in ''|*[!0-9]*) numeric=0 ;; esac
            case "$tests_failed" in ''|*[!0-9]*) numeric=0 ;; esac
            if [ "$numeric" -eq 1 ]; then
                if [ "$tests_failed" -ne 0 ]; then
                    reasons+=("$tests_failed test(s) failed")
                    failed_names="$(xmllint --xpath '//test-case[@result="Failed"]/@fullname' "$RESULTS" 2>/dev/null | grep -o 'fullname="[^"]*"' | sed -e 's/^fullname="//' -e 's/"$//' | head -n 20 || true)"
                fi
                if [ "$tests_total" -lt "$MIN_TESTS" ]; then
                    reasons+=("only $tests_total test(s) ran; MIN_TESTS=$MIN_TESTS")
                fi
            else
                reasons+=("could not read /test-run/@total and @failed from $RESULTS")
                tests_total="?"
                tests_failed="?"
            fi
        else
            reasons+=("no results XML at $RESULTS")
            tests_total="?"
            tests_failed="?"
        fi ;;
    build-*)
        if grep -q 'Error building Player' "$LOG"; then
            reasons+=("log contains 'Error building Player'")
        fi ;;
esac

# Convenience pointers to the newest run of each mode (build/ is git-ignored).
ln -sfn "${LOG##*/}" "$LOG_DIR/$MODE-latest.log"
if [ -f "$RESULTS" ]; then
    ln -sfn "${RESULTS##*/}" "$LOG_DIR/$MODE-latest.xml"
fi

if [ ${#reasons[@]} -eq 0 ]; then
    echo "unity-gate: PASS mode=$MODE exit=$UNITY_EXIT tests=$tests_total/$tests_failed (total/failed) elapsed=${elapsed}s log=$LOG"
    exit 0
fi

echo "unity-gate: FAIL mode=$MODE exit=$UNITY_EXIT tests=$tests_total/$tests_failed (total/failed) elapsed=${elapsed}s log=$LOG" >&2
for r in "${reasons[@]}"; do
    echo "unity-gate:   - $r" >&2
done
if [ -n "$failed_names" ]; then
    echo "unity-gate: failed tests (first 20):" >&2
    echo "$failed_names" | sed 's/^/unity-gate:     /' >&2
fi
echo "unity-gate: last 40 lines of $LOG:" >&2
tail -n 40 "$LOG" >&2
exit 1
