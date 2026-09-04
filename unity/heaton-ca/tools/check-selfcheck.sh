#!/usr/bin/env bash
# check-selfcheck.sh -- shared verdict parser for the HeatonCA device gates.
#
# Every HeatonCA player runs DeterminismSelfCheck at boot and logs one
# verdict line,
#
#   [HeatonCA] SELF-CHECK PASS        or        [HeatonCA] SELF-CHECK FAIL
#
# together with the report DeterminismSelfCheck.Run renders:
#
#   HeatonCA determinism self-check:
#   PASS pcg32
#   PASS mergelife-upstream-1
#   PASS mergelife-upstream-60
#   PASS mergelife-soup50
#   PASS objective-redworld48
#   (a failing check reads "FAIL <name>: <detail>")
#
# This script watches a log (the player's -logFile, an adb logcat capture,
# `simctl launch --console` output) or stdin until a verdict line appears,
# prints the report lines it saw, and turns the verdict into an exit code.
# The per-platform gates (macos-selfcheck.sh, android-selfcheck.sh,
# ios-sim-selfcheck.sh) all end here, so the verdict rule lives in one place;
# windows-selfcheck.ps1 mirrors the same rule in PowerShell.
#
# Usage:
#   tools/check-selfcheck.sh [--timeout <s>] [--grace <s>] [--pid <pid>] [<log file> | -]
#
#   <log file>   the file to watch. It may not exist yet (the player creates
#                it); only complete lines are examined, and each poll resumes
#                where the last one stopped, so a log that is still being
#                written is fine.
#   -            read stdin instead. Also the default when no path is given
#                and stdin is not a terminal.
#   --timeout    seconds to wait for a verdict (default 120).
#   --grace      seconds to keep reading after the verdict so report lines
#                logged after it are still printed (default 2, 0 to stop at
#                the verdict line).
#   --pid        stop waiting early (after the grace period) when this process
#                has exited without producing a verdict, instead of running
#                out the timeout. Pass the player's or the log producer's pid.
#
# Exit codes:
#   0  a "[HeatonCA] SELF-CHECK PASS" line appeared
#   1  a "SELF-CHECK FAIL" line appeared (checked first: a log holding both is
#      a failure)
#   2  no verdict: the timeout expired, the input ended, or the --pid process
#      exited first
#   3  usage error
#
# Report lines go to stdout verbatim, with whatever logcat or player prefix
# they carry; status messages go to stderr prefixed "check-selfcheck:".
# Runs under the macOS default /bin/bash 3.2 (no mapfile, integer read -t).
set -euo pipefail

PASS_TOKEN='[HeatonCA] SELF-CHECK PASS'
FAIL_TOKEN='SELF-CHECK FAIL'
# Lines worth echoing: the verdict, the report header, and "PASS <name>" /
# "FAIL <name>[: detail]" check lines. The check-name alternation is a shape
# rather than a list so a sixth check needs no change here.
REPORT_RE='HeatonCA determinism self-check:|\[HeatonCA\] SELF-CHECK|(^|[^A-Za-z0-9_-])(PASS|FAIL) [a-z0-9][a-z0-9-]*(:|[[:space:]]*$)'

usage()
{
    cat <<__USAGE__
usage: tools/check-selfcheck.sh [--timeout <s>] [--grace <s>] [--pid <pid>] [<log file> | -]
       waits for "[HeatonCA] SELF-CHECK PASS|FAIL" in the log (or stdin) and prints the report lines
exit:  0 PASS, 1 FAIL, 2 no verdict (timeout, input ended, or --pid exited), 3 usage
__USAGE__
}

fail_usage()
{
    echo "check-selfcheck: $*" >&2
    usage >&2
    exit 3
}

is_uint()
{
    case "$1" in
        ''|*[!0-9]*) return 1 ;;
    esac
}

TIMEOUT=120
GRACE=2
PID=""
SOURCE=""
while [ $# -gt 0 ]; do
    case "$1" in
        --timeout)
            [ $# -ge 2 ] || fail_usage "--timeout needs a value"
            is_uint "$2" || fail_usage "--timeout must be a non-negative integer (got '$2')"
            TIMEOUT="$2"
            shift ;;
        --grace)
            [ $# -ge 2 ] || fail_usage "--grace needs a value"
            is_uint "$2" || fail_usage "--grace must be a non-negative integer (got '$2')"
            GRACE="$2"
            shift ;;
        --pid)
            [ $# -ge 2 ] || fail_usage "--pid needs a value"
            is_uint "$2" || fail_usage "--pid must be a process id (got '$2')"
            PID="$2"
            shift ;;
        -h|--help) usage; exit 0 ;;
        -) SOURCE="-" ;;
        --*) fail_usage "unknown option '$1'" ;;
        *)
            [ -z "$SOURCE" ] || fail_usage "only one log file may be given"
            SOURCE="$1" ;;
    esac
    shift
done
if [ -z "$SOURCE" ]; then
    if [ -t 0 ]; then
        fail_usage "give a log file, or pipe the log into stdin"
    fi
    SOURCE="-"
fi

VERDICT=""        # "", pass, or fail
REASON=""         # why no verdict was reached
START="$(date +%s)"
DEADLINE=$((START + TIMEOUT))
GRACE_END=""
PID_GONE_END=""

# print_report_lines: echo the lines of stdin that match REPORT_RE, CR-free.
print_report_lines()
{
    tr -d '\r' | grep -E "$REPORT_RE" || true
}

# note_verdict <text>: record the first verdict found in <text> (FAIL wins).
note_verdict()
{
    [ -z "$VERDICT" ] || return 0
    case "$1" in
        *"$FAIL_TOKEN"*) VERDICT=fail ;;
        *"$PASS_TOKEN"*) VERDICT=pass ;;
    esac
}

# Returns 0 when the wait loop should stop.
should_stop()
{
    local now
    now="$(date +%s)"
    if [ -n "$VERDICT" ]; then
        if [ -z "$GRACE_END" ]; then
            GRACE_END=$((now + GRACE))
        fi
        [ "$now" -ge "$GRACE_END" ]
        return
    fi
    if [ "$now" -ge "$DEADLINE" ]; then
        REASON="no verdict within ${TIMEOUT}s"
        return 0
    fi
    if [ -n "$PID" ] && ! kill -0 "$PID" 2>/dev/null; then
        if [ -z "$PID_GONE_END" ]; then
            PID_GONE_END=$((now + GRACE))
        fi
        if [ "$now" -ge "$PID_GONE_END" ]; then
            REASON="process $PID exited without a verdict after $((now - START))s"
            return 0
        fi
    fi
    return 1
}

if [ "$SOURCE" = "-" ]; then
    # --- stdin ------------------------------------------------------------
    # read -t takes whole seconds on bash 3.2 and does not tell a timeout from
    # end of input, so the clock decides which one happened.
    while :; do
        now="$(date +%s)"
        if [ -n "$VERDICT" ]; then
            [ -n "$GRACE_END" ] || GRACE_END=$((now + GRACE))
            remaining=$((GRACE_END - now))
        else
            remaining=$((DEADLINE - now))
        fi
        if [ "$remaining" -le 0 ]; then
            [ -n "$VERDICT" ] || REASON="no verdict within ${TIMEOUT}s"
            break
        fi
        line=""
        if IFS= read -r -t "$remaining" line; then
            printf '%s\n' "$line" | print_report_lines
            note_verdict "$line"
        else
            # A final unterminated line still counts.
            if [ -n "$line" ]; then
                printf '%s\n' "$line" | print_report_lines
                note_verdict "$line"
            fi
            now="$(date +%s)"
            if [ -n "$VERDICT" ]; then
                break
            fi
            if [ "$now" -ge "$DEADLINE" ]; then
                REASON="no verdict within ${TIMEOUT}s"
            else
                REASON="input ended without a verdict after $((now - START))s"
            fi
            break
        fi
    done
    WHERE="stdin"
else
    # --- log file -----------------------------------------------------------
    SEEN=0
    while :; do
        if [ -f "$SOURCE" ]; then
            total="$(wc -l < "$SOURCE" | tr -d ' ')"
            is_uint "$total" || total=0
            if [ "$total" -gt "$SEEN" ]; then
                chunk="$(sed -n "$((SEEN + 1)),${total}p" "$SOURCE")"
                SEEN="$total"
                printf '%s\n' "$chunk" | print_report_lines
                note_verdict "$chunk"
            fi
        fi
        if should_stop; then
            break
        fi
        sleep 0.5
    done
    if [ -z "$VERDICT" ] && [ ! -f "$SOURCE" ]; then
        REASON="$REASON; the log file never appeared"
    fi
    WHERE="$SOURCE"
fi

ELAPSED=$(( $(date +%s) - START ))
case "$VERDICT" in
    pass)
        echo "check-selfcheck: PASS after ${ELAPSED}s ($WHERE)" >&2
        exit 0 ;;
    fail)
        echo "check-selfcheck: FAIL after ${ELAPSED}s ($WHERE)" >&2
        exit 1 ;;
    *)
        echo "check-selfcheck: NO VERDICT: $REASON ($WHERE)" >&2
        exit 2 ;;
esac
