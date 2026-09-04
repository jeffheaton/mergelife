#!/usr/bin/env bash
# engine-sync-check.sh -- verify that Assets/Engine is still a verbatim copy
# of HeatonLife.Core.
#
# tools/import-engine.sh writes the seven engine files with a five-line
# provenance header, `namespace HeatonLife` renamed to
# `namespace HeatonCA.Engine`, and (MergeLife.cs only) the ToPng member
# removed. This script undoes the header and the rename and diffs each file
# against the upstream checkout. It passes only when six files are identical
# and the seventh, MergeLife.cs, differs by exactly one deletion hunk made of
# the ToPng member: its `///` summary line(s), the `public byte[] ToPng(`
# line, and at most one blank line. Anything else is drift, and drift in the
# engine is never fixed here: re-run tools/import-engine.sh, or fix upstream.
#
# It also checks the copy on its own terms: every file present, the headers
# exactly as import-engine.sh writes them and all naming the same commit, the
# namespace renamed once, no PngGrid/ToPng/HeatonLife left in any body,
# AssemblyInfo.cs the only non-upstream .cs file, and PROVENANCE.md naming the
# same commit as the headers.
#
# Usage:
#   tools/engine-sync-check.sh [--self]
#
#   --self   run only the checks on the copy itself, for machines without an
#            upstream checkout (the diff against upstream is skipped)
#
# Environment:
#   SRC, HEATONCA_UPSTREAM_ENGINE
#            the HeatonLife.Core source directory, resolved as in
#            import-engine.sh (SRC, then HEATONCA_UPSTREAM_ENGINE, then
#            ../../../heaton-life/dotnet/src/HeatonLife.Core relative to the
#            project)
#
# Exit codes: 0 in sync; 1 drift or a malformed copy; 2 usage error, or no
# upstream checkout without --self.
set -euo pipefail

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
LOCAL_CS=(AssemblyInfo.cs)
HEADER_LINES=5

usage()
{
    cat <<__USAGE__
usage: tools/engine-sync-check.sh [--self]
       verifies Assets/Engine against the HeatonLife.Core checkout
       (SRC or HEATONCA_UPSTREAM_ENGINE); --self checks only the copy itself
__USAGE__
}

SELF=0
while [ $# -gt 0 ]; do
    case "$1" in
        --self) SELF=1 ;;
        -h|--help) usage; exit 0 ;;
        *)
            echo "engine-sync-check: unknown argument: $1" >&2
            usage >&2
            exit 2 ;;
    esac
    shift
done

FAILED=0
fail()
{
    echo "engine-sync-check: FAIL: $*" >&2
    FAILED=1
}

warn()
{
    echo "engine-sync-check: WARNING: $*" >&2
}

[ -d "$DEST" ] || { echo "engine-sync-check: $DEST does not exist; run tools/import-engine.sh first" >&2; exit 1; }

# --- 1. the copy itself -----------------------------------------------------

SHA=""

# check_header <file name>: lines 1-5 exactly as import-engine.sh writes them;
# records the commit from line 1 into SHA and requires every file to agree.
check_header()
{
    local name="$1" path="$DEST/$1"
    local l1 l2 l3 l4 l5 sha mods
    l1="$(sed -n '1p' "$path")"
    l2="$(sed -n '2p' "$path")"
    l3="$(sed -n '3p' "$path")"
    l4="$(sed -n '4p' "$path")"
    l5="$(sed -n '5p' "$path")"

    sha="$(printf '%s\n' "$l1" | sed -nE 's/^.* commit ([0-9a-f]{40})\)\.$/\1/p')"
    if [ -z "$sha" ]; then
        fail "$name: line 1 does not carry a 40-hex commit: $l1"
        return
    fi
    if [ -z "$SHA" ]; then
        SHA="$sha"
    elif [ "$sha" != "$SHA" ]; then
        fail "$name: header names commit $sha but earlier files name $SHA"
    fi

    mods="namespace HeatonLife -> HeatonCA.Engine"
    if [ "$name" = "MergeLife.cs" ]; then
        mods="$mods; MergeLife.ToPng removed (PngGrid/DeflateStream not carried)"
    fi

    [ "$l1" = "// HeatonCA.Engine: copied verbatim from HeatonLife.Core ($UPSTREAM_REPO, $UPSTREAM_DIR/$name, commit $sha)." ] \
        || fail "$name: header line 1 is malformed: $l1"
    [ "$l2" = "// Copyright Jeff Heaton. Licensed under the Apache License, Version 2.0; see THIRD_PARTY_NOTICES.md." ] \
        || fail "$name: header line 2 is malformed: $l2"
    [ "$l3" = "// Modifications: $mods." ] \
        || fail "$name: header line 3 is malformed: $l3"
    [ "$l4" = "// Do not edit: re-run tools/import-engine.sh to sync." ] \
        || fail "$name: header line 4 is malformed: $l4"
    [ -z "$l5" ] || fail "$name: header line 5 must be blank, got: $l5"
}

# check_body <file name>: the text below the header has the namespace renamed
# exactly once and no trace of the old namespace or the dropped PNG code.
check_body()
{
    local name="$1" path="$DEST/$1"
    local ns
    ns="$(tail -n "+$((HEADER_LINES + 1))" "$path" | grep -c '^namespace HeatonCA.Engine$' || true)"
    [ "$ns" = "1" ] || fail "$name: expected exactly one 'namespace HeatonCA.Engine' line, found $ns"
    if tail -n "+$((HEADER_LINES + 1))" "$path" | grep -n 'namespace HeatonLife\|PngGrid\|ToPng' >&2; then
        fail "$name: body still mentions HeatonLife, PngGrid, or ToPng (lines above are relative to the body)"
    fi
}

for f in "${FILES[@]}"; do
    if [ ! -f "$DEST/$f" ]; then
        fail "missing $DEST/$f"
        continue
    fi
    check_header "$f"
    check_body "$f"
done

# Only the seven upstream files and AssemblyInfo.cs may be C# in Assets/Engine.
for path in "$DEST"/*.cs; do
    [ -e "$path" ] || continue
    name="$(basename "$path")"
    allowed=0
    for f in "${FILES[@]}" "${LOCAL_CS[@]}"; do
        [ "$name" = "$f" ] && allowed=1
    done
    [ "$allowed" -eq 1 ] || fail "unexpected C# file in Assets/Engine: $name (only the upstream files and AssemblyInfo.cs belong there)"
done

if [ -n "$SHA" ]; then
    if [ ! -f "$PROVENANCE" ]; then
        fail "missing $PROVENANCE"
    elif ! grep -q "$SHA" "$PROVENANCE"; then
        fail "PROVENANCE.md does not name commit $SHA recorded in the headers"
    fi
fi

if [ "$FAILED" -ne 0 ]; then
    echo "engine-sync-check: FAIL (copy checks)" >&2
    exit 1
fi

if [ "$SELF" -eq 1 ]; then
    echo "engine-sync-check: PASS (--self): ${#FILES[@]} files well formed, commit $SHA (upstream diff skipped)"
    exit 0
fi

# --- 2. diff against upstream -----------------------------------------------

SRC="${SRC:-${HEATONCA_UPSTREAM_ENGINE:-$DEFAULT_SRC}}"
if [ ! -d "$SRC" ]; then
    echo "engine-sync-check: upstream directory not found: $SRC" >&2
    echo "engine-sync-check: set SRC or HEATONCA_UPSTREAM_ENGINE, or pass --self to check only the copy" >&2
    exit 2
fi
SRC="$(cd "$SRC" && pwd)"

if git -C "$SRC" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    head_sha="$(git -C "$SRC" rev-parse HEAD)"
    if [ "$head_sha" != "$SHA" ]; then
        warn "upstream checkout is at $head_sha but the headers name $SHA (content is still compared)"
    fi
    dirty="$(cd "$SRC" && git status --porcelain -- "${FILES[@]}")"
    if [ -n "$dirty" ]; then
        warn "upstream files have local modifications; the comparison is against the working tree"
    fi
fi

tmpdir="$(mktemp -d "${TMPDIR:-/tmp}/engine-sync-check.XXXXXX")"
trap 'rm -rf "$tmpdir"' EXIT

# mergelife_diff_ok <diff output>: exactly one deletion hunk consisting of the
# ToPng member (one `public byte[] ToPng(` line, at least one `///` line, at
# most one blank line) and nothing else.
mergelife_diff_ok()
{
    local out="$1"
    local hunks topng doc blank total
    hunks="$(grep -cE '^[0-9]+(,[0-9]+)?d[0-9]+$' "$out" || true)"
    topng="$(grep -cE '^< [[:space:]]*public byte\[\] ToPng\(' "$out" || true)"
    doc="$(grep -cE '^< [[:space:]]*///' "$out" || true)"
    blank="$(grep -cE '^< [[:space:]]*$' "$out" || true)"
    total="$(wc -l < "$out" | tr -d ' ')"
    [ "$hunks" = "1" ] && [ "$topng" = "1" ] && [ "$doc" -ge 1 ] && [ "$blank" -le 1 ] \
        && [ $((hunks + topng + doc + blank)) -eq "$total" ]
}

for f in "${FILES[@]}"; do
    if [ ! -f "$SRC/$f" ]; then
        fail "missing upstream file: $SRC/$f"
        continue
    fi
    recon="$tmpdir/$f"
    tail -n "+$((HEADER_LINES + 1))" "$DEST/$f" | sed 's/^namespace HeatonCA.Engine$/namespace HeatonLife/' > "$recon"

    out="$tmpdir/$f.diff"
    set +e
    diff "$SRC/$f" "$recon" > "$out"
    rc=$?
    set -e
    case "$rc" in
        0)
            if [ "$f" = "MergeLife.cs" ]; then
                fail "$f: identical to upstream, but the header says ToPng was removed"
            else
                echo "engine-sync-check: $f OK (identical to upstream)"
            fi ;;
        1)
            if [ "$f" = "MergeLife.cs" ] && mergelife_diff_ok "$out"; then
                echo "engine-sync-check: $f OK (differs only by the removed ToPng member)"
            else
                fail "$f: differs from upstream beyond the permitted change:"
                cat "$out" >&2
            fi ;;
        *)
            fail "$f: diff failed (exit $rc)" ;;
    esac
done

if [ "$FAILED" -ne 0 ]; then
    echo "engine-sync-check: FAIL: Assets/Engine has drifted from $SRC; re-run tools/import-engine.sh, never hand-edit" >&2
    exit 1
fi

echo "engine-sync-check: PASS: ${#FILES[@]} files in sync with $SRC, commit $SHA"
