#!/usr/bin/env bash
# NOTE: --virtual-time-budget must NOT be passed: Chrome fast-forwards virtual time and the
#       Unity wasm loader never finishes, so the run times out with no console output (2026-09-02).
# webgl-smoke.sh -- boot the WebGL build in headless Chrome and require the
# determinism self-check to pass.
#
# The wasm proof of the port. tools/webgl-serve.py serves build/webgl with the
# compressed files sent WITHOUT Content-Encoding, so the loader's decompression
# fallback is what gets exercised; Chrome opens the page headless with
# SwiftShader software WebGL; this script watches Chrome's console log
# (--enable-logging=stderr) for the line AppController prints after
# DeterminismSelfCheck runs. Exit 0 only when
#
#     [HeatonCA] SELF-CHECK PASS
#
# appears and "SELF-CHECK FAIL" does not within the timeout. It also fails on
# "popup blocked" (the jslib's console.warn when window.open returns null) and
# on the template's "[HeatonCA] LOAD FAILED" line, and before launching
# anything it checks that the built index.html carries <title>HeatonCA</title>
# (the Gate 1 expectation).
#
# Usage:
#   tools/webgl-smoke.sh
#
# Environment:
#   CHROME               browser binary; default
#                        "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
#   WEBGL_BUILD_DIR      build folder (default build/webgl)
#   WEBGL_PORT           local port (default 8765)
#   WEBGL_SMOKE_TIMEOUT  seconds to wait for a verdict (default 120)
#   WEBGL_SMOKE_RULE     rule for the ?rule= deep link (default: the red-world
#                        rule e542-5f79-9341-f31e-6c6b-7f08-8773-7068), so the
#                        same run shows the "[HeatonCA] url ?rule=..." line
#
# Exit codes: 0 PASS; 1 FAIL, popup blocked, load failure, or timeout; 2 no
# build (run tools/unity-gate.sh build-webgl first); 5 environment (Chrome,
# python3, or curl missing; the local server would not start).
#
# Logs: build/logs/webgl-smoke-<utc>.log holds Chrome's stderr (the console
# lines), build/logs/webgl-serve-<utc>.log the server's; webgl-smoke-latest.log
# points at the newest run. The [HeatonCA] console lines are echoed on exit.
# The Chrome profile lives in a temporary directory that is removed on exit,
# and the server is killed on exit, including Ctrl-C.
set -euo pipefail

DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT="$(cd "$DIR/.." && pwd)"
CHROME="${CHROME:-/Applications/Google Chrome.app/Contents/MacOS/Google Chrome}"
BUILD_DIR="${WEBGL_BUILD_DIR:-$PROJECT/build/webgl}"
PORT="${WEBGL_PORT:-8765}"
TIMEOUT_S="${WEBGL_SMOKE_TIMEOUT:-120}"
RULE="${WEBGL_SMOKE_RULE:-e542-5f79-9341-f31e-6c6b-7f08-8773-7068}"
URL="http://127.0.0.1:$PORT/?rule=$RULE"

PASS_MARKER='[HeatonCA] SELF-CHECK PASS'
FAIL_MARKER='SELF-CHECK FAIL'
POPUP_MARKER='popup blocked'
LOAD_FAIL_MARKER='[HeatonCA] LOAD FAILED'
TITLE_MARKER='<title>HeatonCA</title>'

LOG_DIR="$PROJECT/build/logs"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
LOG="$LOG_DIR/webgl-smoke-$STAMP.log"
SERVER_LOG="$LOG_DIR/webgl-serve-$STAMP.log"
PROFILE=""
SERVER_PID=""
CHROME_PID=""

# Stops a background process: TERM, a short grace period, then KILL.
stop_process()
{
    local pid="$1" waited=0
    if [ -z "$pid" ] || ! kill -0 "$pid" 2>/dev/null; then
        return 0
    fi
    kill -TERM "$pid" 2>/dev/null || true
    while kill -0 "$pid" 2>/dev/null && [ "$waited" -lt 20 ]; do
        sleep 0.25
        waited=$((waited + 1))
    done
    if kill -0 "$pid" 2>/dev/null; then
        kill -KILL "$pid" 2>/dev/null || true
    fi
    wait "$pid" 2>/dev/null || true
}

cleanup()
{
    stop_process "$CHROME_PID"
    stop_process "$SERVER_PID"
    if [ -n "$PROFILE" ] && [ -d "$PROFILE" ]; then
        rm -rf "$PROFILE"
    fi
}
trap cleanup EXIT INT TERM

# ---- environment -------------------------------------------------------------

if [ ! -x "$CHROME" ]; then
    echo "webgl-smoke: Chrome not found at '$CHROME' (set CHROME to the binary)" >&2
    exit 5
fi
for tool in python3 curl; do
    if ! command -v "$tool" >/dev/null 2>&1; then
        echo "webgl-smoke: $tool is required" >&2
        exit 5
    fi
done
if [ ! -f "$BUILD_DIR/index.html" ]; then
    echo "webgl-smoke: no build at $BUILD_DIR/index.html; run tools/unity-gate.sh build-webgl first" >&2
    exit 2
fi
mkdir -p "$LOG_DIR"

# ---- static check: the HeatonCA template was used ----------------------------

if ! grep -q -F "$TITLE_MARKER" "$BUILD_DIR/index.html"; then
    echo "webgl-smoke: FAIL: $BUILD_DIR/index.html lacks $TITLE_MARKER (PlayerSettings WebGL template must be PROJECT:HeatonCA)" >&2
    exit 1
fi

# ---- local server ------------------------------------------------------------

python3 "$DIR/webgl-serve.py" --dir "$BUILD_DIR" --port "$PORT" --quiet 2>"$SERVER_LOG" &
SERVER_PID=$!
attempts=0
until curl -s -f -o /dev/null "http://127.0.0.1:$PORT/index.html"; do
    if ! kill -0 "$SERVER_PID" 2>/dev/null; then
        echo "webgl-smoke: the local server exited before answering:" >&2
        cat "$SERVER_LOG" >&2
        SERVER_PID=""
        exit 5
    fi
    attempts=$((attempts + 1))
    if [ "$attempts" -ge 50 ]; then
        echo "webgl-smoke: the local server did not answer on port $PORT within 10 s" >&2
        exit 5
    fi
    sleep 0.2
done

# ---- headless Chrome ---------------------------------------------------------

PROFILE="$(mktemp -d "${TMPDIR:-/tmp}/heatonca-chrome.XXXXXX")"
echo "webgl-smoke: $URL  (log: $LOG)"
"$CHROME" \
    --headless=new \
    --disable-gpu \
    --use-angle=swiftshader \
    --enable-unsafe-swiftshader \
    --enable-logging=stderr \
    --v=0 \
    --user-data-dir="$PROFILE" \
    --no-first-run \
    --no-default-browser-check \
    --disable-extensions \
    --mute-audio \
    --window-size=1280,800 \
    "$URL" >"$LOG" 2>&1 &
CHROME_PID=$!

# ---- watch the console -------------------------------------------------------

verdict="TIMEOUT"
elapsed=0
while [ "$elapsed" -lt "$TIMEOUT_S" ]; do
    if grep -q -F "$FAIL_MARKER" "$LOG"; then
        verdict="FAIL"
        break
    fi
    if grep -q -i -F "$POPUP_MARKER" "$LOG"; then
        verdict="POPUP"
        break
    fi
    if grep -q -F "$LOAD_FAIL_MARKER" "$LOG"; then
        verdict="LOAD"
        break
    fi
    if grep -q -F "$PASS_MARKER" "$LOG"; then
        # Let any trailing FAIL or popup line land before trusting the PASS.
        sleep 2
        if grep -q -F "$FAIL_MARKER" "$LOG"; then
            verdict="FAIL"
        elif grep -q -i -F "$POPUP_MARKER" "$LOG"; then
            verdict="POPUP"
        else
            verdict="PASS"
        fi
        break
    fi
    if ! kill -0 "$CHROME_PID" 2>/dev/null; then
        verdict="EXITED"
        break
    fi
    sleep 1
    elapsed=$((elapsed + 1))
done

ln -sfn "$(basename "$LOG")" "$LOG_DIR/webgl-smoke-latest.log"

# Echo the app's console lines without Chrome's log prefix.
echo "webgl-smoke: [HeatonCA] console lines:"
if grep -F '[HeatonCA]' "$LOG" >/dev/null 2>&1; then
    grep -F '[HeatonCA]' "$LOG" | sed -E 's/^.*CONSOLE[^"]*"//; s/", source: .*$//' | sed 's/^/    /'
else
    echo "    (none)"
fi

case "$verdict" in
    PASS)
        echo "webgl-smoke: PASS after ${elapsed}s (log: $LOG)"
        exit 0 ;;
    FAIL)
        echo "webgl-smoke: FAIL: the self-check reported SELF-CHECK FAIL (log: $LOG)" >&2 ;;
    POPUP)
        echo "webgl-smoke: FAIL: a popup was blocked (log: $LOG)" >&2 ;;
    LOAD)
        echo "webgl-smoke: FAIL: the build did not load (log: $LOG)" >&2 ;;
    EXITED)
        echo "webgl-smoke: FAIL: Chrome exited before printing a verdict (log: $LOG)" >&2 ;;
    *)
        echo "webgl-smoke: FAIL: no verdict within ${TIMEOUT_S}s (log: $LOG)" >&2 ;;
esac
echo "webgl-smoke: last log lines:" >&2
tail -n 20 "$LOG" >&2 || true
exit 1
