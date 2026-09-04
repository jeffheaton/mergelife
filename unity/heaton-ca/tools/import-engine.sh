#!/usr/bin/env bash
# import-engine.sh -- copy the MergeLife engine from HeatonLife.Core into
# Assets/Engine as the HeatonCA.Engine assembly.
#
# Assets/Engine is a verbatim copy of seven files from the heaton-life
# repository (github.com/jeffheaton/heaton-life, dotnet/src/HeatonLife.Core).
# This script is the only sanctioned way those files change: it rewrites each
# one from the upstream checkout with
#
#   1. a five-line provenance header (four comment lines and a blank line),
#   2. the single line `namespace HeatonLife` renamed to
#      `namespace HeatonCA.Engine`, and
#   3. for MergeLife.cs only, the ToPng member removed (its XML summary line,
#      the method line, and the blank line that would otherwise double),
#      because PngGrid/DeflateStream are not carried into the Unity project.
#
# Nothing else is touched. tools/engine-sync-check.sh reverses the header and
# the namespace rename and diffs the result against the same upstream
# checkout; Assets/Engine/PROVENANCE.md records the pinned commit. Never
# hand-edit the copied files and never "fix" numerics: the conformance
# vectors are the contract.
#
# Each copied .cs file gets a sibling .meta with a fresh random GUID the first
# time it is written; an existing .meta is never overwritten (Unity references
# assets by GUID, so the GUID must stay stable across re-imports).
#
# Usage:
#   tools/import-engine.sh [--allow-dirty] [--sha <40 hex>]
#
#   --allow-dirty   import even when the seven upstream files have local
#                   modifications (the recorded commit is then approximate)
#   --sha <hex>     the upstream commit to record in the headers; required when
#                   SRC is not a git checkout (a sparse or exported copy)
#
# Environment:
#   SRC, HEATONCA_UPSTREAM_ENGINE
#                   the HeatonLife.Core source directory. SRC wins, then
#                   HEATONCA_UPSTREAM_ENGINE, then the default
#                   ../../../heaton-life/dotnet/src/HeatonLife.Core relative to
#                   the project (../heaton-life beside this repository).
#   HEATONCA_UPSTREAM_SHA
#                   same as --sha
#
# Exit codes: 0 imported; 1 upstream problem (missing file, dirty tree,
# unexpected shape); 2 usage error.
set -euo pipefail

# Treat every file as bytes: the upstream sources are UTF-8 with a few
# non-ASCII characters in comments, and sed/awk must pass them through intact.
export LC_ALL=C

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
REPO_ROOT="$(cd "$PROJECT_ROOT/../.." && pwd)"
DEST="$PROJECT_ROOT/Assets/Engine"
PROVENANCE="$DEST/PROVENANCE.md"
DEFAULT_SRC="$REPO_ROOT/../heaton-life/dotnet/src/HeatonLife.Core"

UPSTREAM_REPO="github.com/jeffheaton/heaton-life"
UPSTREAM_DIR="dotnet/src/HeatonLife.Core"
FILES=(MergeLife.cs MergeLifeObjective.cs Evolver.cs MergeLifeGallery.cs Pcg32.cs Parallelism.cs ISimulation.cs)

usage()
{
    cat <<__USAGE__
usage: tools/import-engine.sh [--allow-dirty] [--sha <40 hex>]
       copies the seven HeatonLife.Core engine files into Assets/Engine
       (SRC or HEATONCA_UPSTREAM_ENGINE selects the upstream checkout)
__USAGE__
}

die()
{
    echo "import-engine: $*" >&2
    exit 1
}

ALLOW_DIRTY=0
SHA="${HEATONCA_UPSTREAM_SHA:-}"
while [ $# -gt 0 ]; do
    case "$1" in
        --allow-dirty) ALLOW_DIRTY=1 ;;
        --sha)
            if [ $# -lt 2 ]; then
                echo "import-engine: --sha needs a value" >&2
                usage >&2
                exit 2
            fi
            SHA="$2"
            shift ;;
        -h|--help) usage; exit 0 ;;
        *)
            echo "import-engine: unknown argument: $1" >&2
            usage >&2
            exit 2 ;;
    esac
    shift
done

SRC="${SRC:-${HEATONCA_UPSTREAM_ENGINE:-$DEFAULT_SRC}}"
[ -d "$SRC" ] || die "upstream directory not found: $SRC (set SRC or HEATONCA_UPSTREAM_ENGINE)"
SRC="$(cd "$SRC" && pwd)"

for f in "${FILES[@]}"; do
    [ -f "$SRC/$f" ] || die "missing upstream file: $SRC/$f"
done

# --- upstream commit -------------------------------------------------------

if [ -z "$SHA" ]; then
    if git -C "$SRC" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
        SHA="$(git -C "$SRC" rev-parse HEAD)"
        dirty="$(cd "$SRC" && git status --porcelain -- "${FILES[@]}")"
        if [ -n "$dirty" ]; then
            echo "import-engine: upstream files have local modifications:" >&2
            echo "$dirty" >&2
            if [ "$ALLOW_DIRTY" -eq 1 ]; then
                echo "import-engine: continuing because --allow-dirty was given; the recorded commit is approximate" >&2
            else
                die "refusing to import from a dirty upstream tree (use --allow-dirty to override)"
            fi
        fi
    else
        die "$SRC is not a git checkout; pass --sha <40 hex> or set HEATONCA_UPSTREAM_SHA"
    fi
fi
case "$SHA" in
    [0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f]) ;;
    *) die "commit must be 40 lowercase hex digits, got: $SHA" ;;
esac

# --- helpers ---------------------------------------------------------------

new_guid()
{
    uuidgen | tr -d - | tr 'A-F' 'a-f'
}

# write_cs_meta <asset path>: MonoImporter block, only if no .meta exists yet.
write_cs_meta()
{
    local meta="$1.meta"
    if [ -f "$meta" ]; then
        return 1
    fi
    # Unity's own serializer leaves a trailing space after the empty values;
    # printf keeps that exact form (a heredoc would invite editors to trim it).
    printf '%s\n' \
        'fileFormatVersion: 2' \
        "guid: $(new_guid)" \
        'MonoImporter:' \
        '  externalObjects: {}' \
        '  serializedVersion: 2' \
        '  defaultReferences: []' \
        '  executionOrder: 0' \
        '  icon: {instanceID: 0}' \
        '  userData: ' \
        '  assetBundleName: ' \
        '  assetBundleVariant: ' > "$meta"
    return 0
}

# header <file name>: the provenance header, four comment lines and a blank.
header()
{
    local name="$1"
    local mods="namespace HeatonLife -> HeatonCA.Engine"
    if [ "$name" = "MergeLife.cs" ]; then
        mods="$mods; MergeLife.ToPng removed (PngGrid/DeflateStream not carried)"
    fi
    printf '%s\n' \
        "// HeatonCA.Engine: copied verbatim from HeatonLife.Core ($UPSTREAM_REPO, $UPSTREAM_DIR/$name, commit $SHA)." \
        "// Copyright Jeff Heaton. Licensed under the Apache License, Version 2.0; see THIRD_PARTY_NOTICES.md." \
        "// Modifications: $mods." \
        "// Do not edit: re-run tools/import-engine.sh to sync." \
        ""
}

# ends_with_newline <file>: the transforms below are line based, so a source
# without a trailing newline would gain one and the sync check would fail.
ends_with_newline()
{
    [ "$(tail -c 1 "$1" | od -An -tx1 | tr -d ' ')" = "0a" ]
}

# remove_topng: drop the ToPng member from MergeLife.cs on stdin. Exactly one
# `public byte[] ToPng(` line must exist; the `///` lines directly above it go
# with it, and when the member sits between two blank lines one of them goes
# too so the surrounding members stay one blank line apart.
remove_topng()
{
    awk '
        { lines[NR] = $0 }
        END {
            n = NR
            idx = 0
            count = 0
            for (i = 1; i <= n; i++)
            {
                if (lines[i] ~ /^[[:space:]]*public byte\[\] ToPng\(/)
                {
                    idx = i
                    count++
                }
            }
            if (count != 1)
            {
                print "import-engine: expected exactly one ToPng method line in MergeLife.cs, found " count > "/dev/stderr"
                exit 1
            }
            start = idx
            while (start > 1 && lines[start - 1] ~ /^[[:space:]]*\/\/\//)
                start--
            end = idx
            if (start > 1 && lines[start - 1] ~ /^[[:space:]]*$/ && end < n && lines[end + 1] ~ /^[[:space:]]*$/)
                end++
            for (i = 1; i <= n; i++)
                if (i < start || i > end)
                    print lines[i]
        }'
}

# --- import ----------------------------------------------------------------

mkdir -p "$DEST"
tmp="$(mktemp "$DEST/.import-engine.XXXXXX")"
trap 'rm -f "$tmp"' EXIT

echo "import-engine: source $SRC"
echo "import-engine: commit $SHA"

for f in "${FILES[@]}"; do
    src="$SRC/$f"
    ends_with_newline "$src" || die "$f does not end with a newline; the line-based transforms would alter it"

    ns_count="$(grep -c '^namespace HeatonLife$' "$src" || true)"
    [ "$ns_count" = "1" ] || die "$f: expected exactly one 'namespace HeatonLife' line, found $ns_count"

    {
        header "$f"
        if [ "$f" = "MergeLife.cs" ]; then
            sed 's/^namespace HeatonLife$/namespace HeatonCA.Engine/' "$src" | remove_topng
        else
            sed 's/^namespace HeatonLife$/namespace HeatonCA.Engine/' "$src"
        fi
    } > "$tmp"

    # Post-conditions the sync check will also enforce. The header (lines 1-5)
    # legitimately names the old namespace and the removed member, so only the
    # body below it is scanned.
    [ "$(grep -c '^namespace HeatonCA.Engine$' "$tmp")" = "1" ] || die "$f: namespace rename did not produce exactly one HeatonCA.Engine namespace"
    if tail -n +6 "$tmp" | grep -q 'namespace HeatonLife\|PngGrid\|ToPng'; then
        die "$f: body still mentions HeatonLife, PngGrid, or ToPng"
    fi

    dest="$DEST/$f"
    if [ -f "$dest" ] && cmp -s "$tmp" "$dest"; then
        status="unchanged"
    else
        status="written"
    fi
    # mktemp creates 0600 files; the checked-in sources are plain 0644.
    chmod 644 "$tmp"
    mv -f "$tmp" "$dest"
    tmp="$(mktemp "$DEST/.import-engine.XXXXXX")"

    if write_cs_meta "$dest"; then
        status="$status, new .meta"
    fi
    echo "import-engine: $f ($status)"
done

if [ -f "$PROVENANCE" ] && ! grep -q "$SHA" "$PROVENANCE"; then
    echo "import-engine: WARNING: $PROVENANCE does not mention commit $SHA; update its pinned commit" >&2
fi

echo "import-engine: done; run tools/engine-sync-check.sh to verify"
