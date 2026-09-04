#!/usr/bin/env bash
# vendor-vectors.sh -- copy the heaton-life conformance vectors that this
# project's EditMode tests replay into Assets/Tests/Vectors~/.
#
# The trailing tilde makes Unity skip the folder entirely, so nothing inside
# gets a .meta and the vectors never enter the asset database; the tests read
# them straight off disk (TestSupport.VendoredRoot). Assets/Tests itself IS an
# asset folder and gets a folder .meta here if it has none yet.
#
# Usage:
#   tools/vendor-vectors.sh                 copy, write README.md, verify
#   tools/vendor-vectors.sh --check         verify only: diff -r against the source, exit 1 on drift
#   tools/vendor-vectors.sh --allow-dirty   copy even when the source vectors have uncommitted changes
#
# Environment:
#   HEATON_LIFE   heaton-life checkout (default ~/projects/heaton-life)
#
# Copies these cases verbatim (17 files) and writes Vectors~/README.md with the
# source commit, spec_version, and file count:
#   evolve/mini-run-24  evolve/objective-a07f-48  evolve/objective-redworld-48  evolve/operators-seeded
#   mergelife-decode/red-world  mergelife-decode/promoted-and-negative  mergelife-decode/tied-limits
#   mergelife/redworld-48
set -euo pipefail

PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SRC="${HEATON_LIFE:-$HOME/projects/heaton-life}"
TESTS="$PROJECT/Assets/Tests"
DST="$TESTS/Vectors~"

CASES=(
    evolve/mini-run-24
    evolve/objective-a07f-48
    evolve/objective-redworld-48
    evolve/operators-seeded
    mergelife-decode/red-world
    mergelife-decode/promoted-and-negative
    mergelife-decode/tied-limits
    mergelife/redworld-48
)
EXPECTED_FILES=17

CHECK=0
ALLOW_DIRTY=0
for arg in "$@"; do
    case "$arg" in
        --check) CHECK=1 ;;
        --allow-dirty) ALLOW_DIRTY=1 ;;
        -h|--help)
            sed -n '2,24p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
            exit 0 ;;
        *)
            echo "vendor-vectors: unknown argument '$arg' (try --check or --allow-dirty)" >&2
            exit 2 ;;
    esac
done

if ! git -C "$SRC" rev-parse --git-dir >/dev/null 2>&1; then
    echo "vendor-vectors: $SRC is not a git checkout (set HEATON_LIFE)" >&2
    exit 1
fi
for c in "${CASES[@]}"; do
    if [ ! -d "$SRC/vectors/$c" ]; then
        echo "vendor-vectors: source case $SRC/vectors/$c is missing" >&2
        exit 1
    fi
done

new_guid()
{
    if command -v uuidgen >/dev/null 2>&1; then
        uuidgen | tr -d - | tr 'A-F' 'a-f'
    else
        od -An -N16 -tx1 /dev/urandom | tr -d ' \n'
    fi
}

if [ "$CHECK" -eq 0 ]; then
    COMMIT="$(git -C "$SRC" rev-parse HEAD)"
    src_paths=()
    for c in "${CASES[@]}"; do
        src_paths+=("vectors/$c")
    done
    dirty="$(git -C "$SRC" status --porcelain -- "${src_paths[@]}")"
    if [ -n "$dirty" ]; then
        if [ "$ALLOW_DIRTY" -eq 1 ]; then
            echo "vendor-vectors: WARNING: source vectors have uncommitted changes; README.md records $COMMIT but the files may differ" >&2
        else
            echo "vendor-vectors: source vectors have uncommitted changes in $SRC (commit them, or pass --allow-dirty):" >&2
            printf '%s\n' "$dirty" >&2
            exit 1
        fi
    fi

    # Assets/Tests is a real asset folder: give it a .meta once, never twice.
    mkdir -p "$TESTS"
    if [ ! -e "$TESTS.meta" ]; then
        printf 'fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$(new_guid)" > "$TESTS.meta"
        echo "vendor-vectors: wrote Assets/Tests.meta"
    fi

    echo "vendor-vectors: copying from $SRC @ $COMMIT"
    for c in "${CASES[@]}"; do
        rm -rf "$DST/$c"
        mkdir -p "$DST/$c"
        cp -R "$SRC/vectors/$c/." "$DST/$c/"
        echo "  $c ($(find "$DST/$c" -type f | wc -l | tr -d ' ') files)"
    done

    # Every params.json that declares a spec_version must agree; the
    # mergelife-decode cases carry none (their expected tables are embedded).
    SPEC_VERSION="$(python3 - "$DST" "${CASES[@]}" <<'__PY__'
import json, os, sys
root, cases = sys.argv[1], sys.argv[2:]
versions = set()
for c in cases:
    with open(os.path.join(root, c, "params.json")) as f:
        v = json.load(f).get("spec_version")
    if v is not None:
        versions.add(str(v))
if len(versions) != 1:
    sys.exit("vendor-vectors: expected exactly one spec_version across the cases, got %s" % sorted(versions))
print(versions.pop())
__PY__
)"
    FILE_COUNT="$(find "$DST" -type f ! -name README.md | wc -l | tr -d ' ')"

    {
        echo "# Vendored conformance vectors"
        echo
        echo "Golden test data replayed by the EditMode tests in \`Assets/Tests/EditMode\`"
        echo "(\`TestSupport.VendoredRoot()\` points here)."
        echo
        echo "- Source repo: https://github.com/jeffheaton/heaton-life (\`vectors/\`)"
        echo "- Commit: \`$COMMIT\`"
        echo "- spec_version: $SPEC_VERSION (from \`params.json\`; the \`mergelife-decode\` cases declare none)"
        echo "- Files: $FILE_COUNT across ${#CASES[@]} cases (excluding this README)"
        echo
        echo "These files are copied verbatim, never edited here; re-run \`tools/vendor-vectors.sh\`"
        echo "to update. The trailing tilde on \`Vectors~\` keeps Unity from importing the folder,"
        echo "so nothing inside has a \`.meta\`."
        echo
        echo "| Case | Files | Spec |"
        echo "|---|---|---|"
        for c in "${CASES[@]}"; do
            files="$(cd "$DST/$c" && find . -type f | sed 's|^\./||' | sort | tr '\n' ' ' | sed 's/ $//; s/ /, /g')"
            case "$c" in
                evolve/*) spec="spec/evolve.md" ;;
                mergelife-decode/*) spec="spec/mergelife.md (decode)" ;;
                mergelife/*) spec="spec/mergelife.md" ;;
                *) spec="" ;;
            esac
            echo "| \`$c\` | $files | $spec |"
        done
        echo
        echo "Specs live in the source repo under \`spec/\`; \`spec/rng.md\` defines the PCG32 stream"
        echo "every seeded case depends on."
    } > "$DST/README.md"
    echo "vendor-vectors: wrote Vectors~/README.md (commit $COMMIT, spec_version $SPEC_VERSION, $FILE_COUNT files)"
fi

# ---------------------------------------------------------------------------
# Verify: every case is byte-identical to the source and the count is right.
# ---------------------------------------------------------------------------
status=0
for c in "${CASES[@]}"; do
    if [ ! -d "$DST/$c" ]; then
        echo "MISSING $DST/$c" >&2
        status=1
        continue
    fi
    if diff -r "$SRC/vectors/$c" "$DST/$c" >/dev/null; then
        echo "  ok $c"
    else
        echo "DRIFT $c:" >&2
        diff -r "$SRC/vectors/$c" "$DST/$c" >&2 || true
        status=1
    fi
done
count="$(find "$DST" -type f ! -name README.md | wc -l | tr -d ' ')"
if [ "$count" -ne "$EXPECTED_FILES" ]; then
    echo "vendor-vectors: expected $EXPECTED_FILES vendored files, found $count" >&2
    status=1
fi
if find "$DST" -name '*.meta' | grep -q .; then
    echo "vendor-vectors: stray .meta inside Vectors~ (Unity must never import it):" >&2
    find "$DST" -name '*.meta' >&2
    status=1
fi
if [ ! -s "$DST/README.md" ]; then
    echo "MISSING $DST/README.md" >&2
    status=1
fi
if [ ! -s "$TESTS.meta" ]; then
    echo "MISSING $TESTS.meta" >&2
    status=1
fi
for extra in "$DST"/*/; do
    fam="$(basename "$extra")"
    case " ${CASES[*]} " in
        *" $fam/"*) ;;
        *) echo "vendor-vectors: WARNING: $fam/ is not one of the vendored cases (stale?)" >&2 ;;
    esac
done

if [ "$status" -ne 0 ]; then
    echo "vendor-vectors: FAIL" >&2
    exit 1
fi
echo "vendor-vectors: OK ($count files, ${#CASES[@]} cases)"
