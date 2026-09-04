#!/usr/bin/env bash
# check-16kb.sh -- prove every native library inside the HeatonCA Android
# package is 16 KB page-size compliant before it is uploaded to Google Play.
#
# Why this exists: Android 15 introduced devices whose kernel pages are 16 KB
# instead of 4 KB, and Google Play now REFUSES a new upload whose 64-bit .so
# files are not laid out for them. A library is compliant when every one of its
# LOAD program-header segments is aligned to a 16 KB boundary (Align 0x4000).
# Unity 6000.5's IL2CPP toolchain does this by default, but a Gradle template, a
# stale NDK, or a third-party .aar can silently reintroduce a 4 KB-aligned (or
# 0x1000-aligned) library, and the only feedback is a rejection at upload time,
# after a build that took minutes. This script turns that into a five-second
# local check.
#
# What it does: lists the archive, extracts every lib/<abi>/*.so entry (an .aab
# nests them under base/lib/..., an .apk does not - both are handled), runs the
# NDK's llvm-readelf -l on each, and prints one row per library with the number
# of LOAD segments and the smallest alignment found.
#
# Usage:
#   Packaging/android/check-16kb.sh [<archive>] [--abi <abi>] [--readelf <path>]
#                                   [--keep] [--verbose]
#
#   <archive>       .aab or .apk to inspect. Default build/android/HeatonCA.aab
#                   (tools/unity-gate.sh build-android-aab); build/android/HeatonCA.apk
#                   is the emulator artifact and works just as well for this check,
#                   because both carry the same compiled .so files.
#   --abi           ABI directory to check (default arm64-v8a - the project ships
#                   ARM64 only, AndroidTargetArchitectures: 2).
#   --readelf       explicit llvm-readelf binary, bypassing the search below.
#   --keep          leave the extracted libraries in the temp folder and print it.
#   --verbose       also print every LOAD segment's alignment, not just the minimum.
#
# Environment:
#   LLVM_READELF                       explicit llvm-readelf (same as --readelf)
#   ANDROID_NDK_ROOT, ANDROID_NDK_HOME, NDK_ROOT
#                                      an NDK to take the toolchain from
#   UNITY                              editor binary; its bundled NDK is searched too
#   HEATONCA_AAB                       archive to inspect (same as the positional arg)
#
# llvm-readelf search order: --readelf/LLVM_READELF, the NDK roots above, the NDK
# beside $UNITY, the Unity Hub editors under /Applications/Unity/Hub/Editor/*, then
# whatever is on PATH. Apple's command line tools ship no readelf at all, so the
# Unity-bundled NDK is the normal source on this Mac:
#   /Applications/Unity/Hub/Editor/6000.5.0f1/PlaybackEngines/AndroidPlayer/NDK/\
#     toolchains/llvm/prebuilt/darwin-x86_64/bin/llvm-readelf
#
# Compliance rule: a segment passes when its alignment is a positive multiple of
# 0x4000. 0x4000 is what Unity emits; a larger multiple (0x10000, seen on some
# prebuilt .so files) also satisfies Google's requirement, so it passes and is
# reported as the value it is. Anything else - 0x1000 above all - fails.
#
# Exit codes:
#   0  every LOAD segment of every checked library is 16 KB aligned
#   1  at least one library is not compliant (the table marks it BAD)
#   3  the archive holds no lib/<abi>/*.so entries, or unzip/llvm-readelf failed
#   5  environment: the archive or llvm-readelf is missing (the message says how
#      to produce or install it)
#
# SANDBOX: this script only reads the archive and writes into $TMPDIR, so it runs
# fine inside the Claude Code Bash sandbox. The Unity build that produces the
# .aab does not - run that with the sandbox disabled (see tools/README.md).
# Runs under /bin/bash 3.2.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
PROJECT="$(cd "$SCRIPT_DIR/../.." && pwd -P)"

ARCHIVE="${HEATONCA_AAB:-$PROJECT/build/android/HeatonCA.aab}"
ABI="arm64-v8a"
READELF="${LLVM_READELF:-}"
KEEP=0
VERBOSE=0
ARCHIVE_FROM_ARG=0

# Print this file's header block (everything between the shebang and `set -e`).
usage() {
    awk 'NR > 1 && /^#/ { sub(/^# ?/, ""); print; next } NR > 1 { exit }' "${BASH_SOURCE[0]}"
}

while [ $# -gt 0 ]; do
    case "$1" in
        --abi)
            [ $# -ge 2 ] || { echo "check-16kb: --abi needs a value" >&2; exit 5; }
            ABI="$2"
            shift 2
            ;;
        --readelf)
            [ $# -ge 2 ] || { echo "check-16kb: --readelf needs a value" >&2; exit 5; }
            READELF="$2"
            shift 2
            ;;
        --keep)
            KEEP=1
            shift
            ;;
        --verbose|-v)
            VERBOSE=1
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        -*)
            echo "check-16kb: unknown option '$1' (try --help)" >&2
            exit 5
            ;;
        *)
            if [ "$ARCHIVE_FROM_ARG" = "1" ]; then
                echo "check-16kb: only one archive may be given (got '$1' after '$ARCHIVE')" >&2
                exit 5
            fi
            ARCHIVE="$1"
            ARCHIVE_FROM_ARG=1
            shift
            ;;
    esac
done

# ---------------------------------------------------------------- the archive

if [ ! -f "$ARCHIVE" ]; then
    cat >&2 <<EOF
check-16kb: no archive at $ARCHIVE

Build one first (with the Claude Code Bash sandbox disabled - Unity needs it off):

  tools/unity-gate.sh build-android-aab     # release .aab, needs the HEATONCA_ANDROID_* signing env
  tools/unity-gate.sh build-android         # debug-signed .apk, same .so files, no signing env needed

then rerun, or point this script at any .aab/.apk:

  Packaging/android/check-16kb.sh build/android/HeatonCA.apk
EOF
    exit 5
fi

# ---------------------------------------------------------------- llvm-readelf

# Echo the first existing llvm-readelf under an NDK root, or nothing.
readelf_in_ndk() {
    local root="$1"
    local candidate
    [ -n "$root" ] || return 0
    for candidate in "$root"/toolchains/llvm/prebuilt/*/bin/llvm-readelf; do
        if [ -x "$candidate" ]; then
            echo "$candidate"
            return 0
        fi
    done
    return 0
}

find_readelf() {
    local root candidate unity_root

    for root in "${ANDROID_NDK_ROOT:-}" "${ANDROID_NDK_HOME:-}" "${NDK_ROOT:-}"; do
        candidate="$(readelf_in_ndk "$root")"
        if [ -n "$candidate" ]; then
            echo "$candidate"
            return 0
        fi
    done

    # The NDK that ships with the Android build support module: first the editor
    # named by $UNITY (…/Unity.app/Contents/MacOS/Unity), then every Hub editor,
    # newest path last so the glob's final match wins.
    if [ -n "${UNITY:-}" ]; then
        unity_root="$(cd "$(dirname "$UNITY")/../.." 2>/dev/null && pwd -P || true)"
        candidate="$(readelf_in_ndk "${unity_root:-}/PlaybackEngines/AndroidPlayer/NDK")"
        if [ -n "$candidate" ]; then
            echo "$candidate"
            return 0
        fi
    fi

    for root in /Applications/Unity/Hub/Editor/*/PlaybackEngines/AndroidPlayer/NDK; do
        candidate="$(readelf_in_ndk "$root")"
        if [ -n "$candidate" ]; then
            echo "$candidate"
            return 0
        fi
    done

    if command -v llvm-readelf >/dev/null 2>&1; then
        command -v llvm-readelf
        return 0
    fi

    return 0
}

if [ -z "$READELF" ]; then
    READELF="$(find_readelf)"
fi

if [ -z "$READELF" ] || [ ! -x "$READELF" ]; then
    cat >&2 <<EOF
check-16kb: llvm-readelf not found${READELF:+ at $READELF}

It comes with the NDK that Unity's Android build support module installs:

  /Applications/Unity/Hub/Editor/<version>/PlaybackEngines/AndroidPlayer/NDK/\\
    toolchains/llvm/prebuilt/<host>/bin/llvm-readelf

Install "Android Build Support" (with the OpenJDK and NDK sub-items) through Unity
Hub, or point the script at another NDK:

  export ANDROID_NDK_ROOT=\$HOME/Library/Android/sdk/ndk/<version>
  Packaging/android/check-16kb.sh --readelf /path/to/llvm-readelf

macOS ships no readelf of its own, so there is no system fallback.
EOF
    exit 5
fi

# ---------------------------------------------------------------- extraction

WORK="$(mktemp -d "${TMPDIR:-/tmp}/heatonca-16kb.XXXXXX")"
cleanup() {
    if [ "$KEEP" = "1" ]; then
        echo "check-16kb: kept extracted libraries in $WORK"
    else
        rm -rf "$WORK"
    fi
}
trap cleanup EXIT

if ! unzip -Z1 "$ARCHIVE" > "$WORK/entries.txt" 2>"$WORK/unzip-list.err"; then
    echo "check-16kb: cannot read $ARCHIVE as a zip archive:" >&2
    cat "$WORK/unzip-list.err" >&2
    exit 3
fi

# An .apk stores lib/<abi>/x.so; an .aab stores <module>/lib/<abi>/x.so. Match both.
grep -E "(^|/)lib/$ABI/[^/]+\.so$" "$WORK/entries.txt" > "$WORK/libs.txt" || true

if [ ! -s "$WORK/libs.txt" ]; then
    echo "check-16kb: $ARCHIVE contains no lib/$ABI/*.so entries." >&2
    echo "check-16kb: ABI directories present:" >&2
    grep -Eo "(^|/)lib/[^/]+/" "$WORK/entries.txt" | sed 's#^/##' | sort -u | sed 's/^/  /' >&2 || true
    echo "check-16kb: pass --abi <abi> to check one of those, or rebuild for ARM64." >&2
    exit 3
fi

# unzip has no "read names from stdin" flag (that is zip's -@), so pass the entry
# names as arguments. They are plain lib/<abi>/<name>.so paths with no globbing
# characters, so unzip's pattern matching is a no-op on them.
LIB_ENTRIES=()
while IFS= read -r entry; do
    [ -n "$entry" ] && LIB_ENTRIES+=("$entry")
done < "$WORK/libs.txt"

if ! unzip -q -o "$ARCHIVE" "${LIB_ENTRIES[@]}" -d "$WORK/x" 2>"$WORK/unzip.err"; then
    echo "check-16kb: extracting the libraries failed:" >&2
    cat "$WORK/unzip.err" >&2
    exit 3
fi

# Other ABIs are not a failure here (they may be legitimately 32-bit and exempt),
# but the project is configured ARM64-only, so their presence is worth saying.
OTHER_ABIS="$(grep -Eo "(^|/)lib/[^/]+/" "$WORK/entries.txt" \
    | sed -e 's#^/##' -e 's#^lib/##' -e 's#/$##' \
    | sort -u | grep -v "^$ABI$" || true)"

# ---------------------------------------------------------------- inspection

echo "check-16kb: archive  $ARCHIVE"
echo "check-16kb: readelf  $READELF"
echo "check-16kb: abi      $ABI"
echo
printf '  %-28s %7s %6s %-9s %s\n' "library" "size KB" "LOADs" "min align" "verdict"
printf '  %-28s %7s %6s %-9s %s\n' "----------------------------" "-------" "-----" "---------" "-------"

FAILED=0
TOTAL=0

while IFS= read -r entry; do
    [ -n "$entry" ] || continue
    so="$WORK/x/$entry"
    name="$(basename "$entry")"
    TOTAL=$((TOTAL + 1))

    if [ ! -f "$so" ]; then
        printf '  %-28s %7s %6s %-9s %s\n' "$name" "?" "?" "?" "BAD (not extracted)"
        FAILED=$((FAILED + 1))
        continue
    fi

    size_kb=$(( $(wc -c < "$so") / 1024 ))

    if ! "$READELF" -l "$so" > "$WORK/headers.txt" 2>"$WORK/headers.err"; then
        printf '  %-28s %7s %6s %-9s %s\n' "$name" "$size_kb" "?" "?" "BAD (readelf failed)"
        sed 's/^/    /' "$WORK/headers.err" >&2
        FAILED=$((FAILED + 1))
        continue
    fi

    # LOAD lines end with the alignment column, e.g.
    #   LOAD  0x000000 0x0…0 0x0…0 0x000a20 0x000a20 R   0x4000
    aligns="$(awk '$1 == "LOAD" { print $NF }' "$WORK/headers.txt")"

    if [ -z "$aligns" ]; then
        printf '  %-28s %7s %6s %-9s %s\n' "$name" "$size_kb" "0" "-" "BAD (no LOAD segments)"
        FAILED=$((FAILED + 1))
        continue
    fi

    loads=0
    min_align=""
    bad=0
    for align in $aligns; do
        loads=$((loads + 1))
        value=$(( align ))   # bash parses the 0x prefix
        if [ "$value" -le 0 ] || [ $(( value % 16384 )) -ne 0 ]; then
            bad=1
        fi
        if [ -z "$min_align" ] || [ "$value" -lt "$min_align" ]; then
            min_align="$value"
        fi
    done

    printed_align="$(printf '0x%x' "$min_align")"
    if [ "$bad" = "1" ]; then
        printf '  %-28s %7s %6s %-9s %s\n' "$name" "$size_kb" "$loads" "$printed_align" "BAD"
        FAILED=$((FAILED + 1))
    else
        printf '  %-28s %7s %6s %-9s %s\n' "$name" "$size_kb" "$loads" "$printed_align" "OK"
    fi

    if [ "$VERBOSE" = "1" ]; then
        for align in $aligns; do
            printf '      LOAD align %s\n' "$align"
        done
    fi
done < "$WORK/libs.txt"

echo

if [ -n "$OTHER_ABIS" ]; then
    echo "check-16kb: NOTE - the archive also carries these ABI folders, which this run did not check:"
    echo "$OTHER_ABIS" | sed 's/^/  lib\//'
    echo "check-16kb: HeatonCA ships ARM64 only (AndroidTargetArchitectures: 2); rerun with --abi to check one."
fi

if [ "$FAILED" -gt 0 ]; then
    cat >&2 <<EOF
check-16kb: FAIL - $FAILED of $TOTAL libraries are not 16 KB aligned.

Google Play rejects new uploads whose 64-bit native libraries are not laid out for
16 KB pages. Fixes, in the order worth trying:
  1. Rebuild with Unity 6000.5 or newer and its bundled NDK (older NDKs default to
     4 KB alignment).
  2. If a custom Gradle template is in play, drop any linker flag that pins
     -Wl,-z,max-page-size to 0x1000.
  3. If a third-party .aar contributed the library, get a 16 KB build of it from
     upstream; it cannot be fixed here.
EOF
    exit 1
fi

echo "check-16kb: OK - all $TOTAL lib/$ABI libraries have every LOAD segment 16 KB aligned."
exit 0
