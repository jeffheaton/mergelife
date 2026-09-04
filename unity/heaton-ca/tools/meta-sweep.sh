#!/usr/bin/env bash
# meta-sweep.sh -- after a Unity gate run, find the .meta files the editor
# generated or rewrote and (optionally) stage them.
#
# Unity writes a .meta for every asset it imports, rewrites hand-authored ones
# whose importer block it normalizes, and writes Packages/packages-lock.json
# the first time it resolves the manifest. Those files must travel with the
# assets they describe, so after each gate the orchestrator runs this sweep.
# It never commits.
#
# Usage:
#   tools/meta-sweep.sh            dry run: list what would be staged
#   tools/meta-sweep.sh --stage    git add the listed files
#   tools/meta-sweep.sh --strict   also exit 1 when an asset under Assets/ has
#                                  no .meta or a .meta has no asset
#
# Scope: *.meta under unity/heaton-ca/Assets/ and unity/heaton-ca/Packages/
# packages-lock.json that git reports as untracked, modified, or deleted.
#
# Warnings (never staged; fatal only with --strict):
#   - assets under Assets/ with no .meta beside them (an agent skipped
#     tools/new-meta.sh, or Unity has not imported them yet)
#   - .meta files whose asset is missing (Unity deletes those on its next run)
#   - .meta files inside a folder ending in "~" (Unity ignores such folders
#     and never wants a .meta there; these are excluded from staging)
set -euo pipefail

usage()
{
    cat <<__USAGE__
usage: tools/meta-sweep.sh [--stage] [--strict]
       lists (and with --stage, git adds) untracked/modified/deleted .meta files under
       unity/heaton-ca/Assets and Packages/packages-lock.json; never commits
__USAGE__
}

STAGE=0
STRICT=0
for arg in "$@"; do
    case "$arg" in
        --stage) STAGE=1 ;;
        --strict) STRICT=1 ;;
        -h|--help) usage; exit 0 ;;
        *)
            echo "meta-sweep: unknown argument '$arg'" >&2
            usage >&2
            exit 2 ;;
    esac
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
PROJECT="$(cd "$SCRIPT_DIR/.." && pwd -P)"
REPO="$(git -C "$PROJECT" rev-parse --show-toplevel)"
ASSETS="$PROJECT/Assets"
LOCK="$PROJECT/Packages/packages-lock.json"

# --- what git sees -------------------------------------------------------------
entries=()
while IFS= read -r -d '' entry; do
    xy="${entry:0:2}"
    path="${entry:3}"
    case "$xy" in
        R*|C*) IFS= read -r -d '' _from || true ;;   # renames carry the source path as the next record
    esac
    case "$path" in
        *.meta|*/Packages/packages-lock.json) entries+=("$xy|$path") ;;
    esac
done < <(git -C "$REPO" status --porcelain -z --untracked-files=all -- "$ASSETS" "$LOCK")

echo "meta-sweep: repo $REPO"
stage_paths=()
if [ ${#entries[@]} -eq 0 ]; then
    echo "meta-sweep: nothing to stage: no untracked, modified, or deleted .meta under ${ASSETS#$REPO/}/ and ${LOCK#$REPO/} is clean"
else
    if [ "$STAGE" -eq 1 ]; then
        echo "meta-sweep: staging:"
    else
        echo "meta-sweep: dry run; pass --stage to git add these:"
    fi
    for e in "${entries[@]}"; do
        xy="${e%%|*}"
        path="${e#*|}"
        case "$xy" in
            '??') label=untracked ;;
            *D*) label=deleted ;;
            *M*|*A*) label=modified ;;
            *) label="$xy" ;;
        esac
        note=""
        skip=0
        case "$path" in
            *.meta)
                case "$path" in
                    *~/*|*~.meta)
                        note="$note [skipped: inside a ~ folder, which Unity ignores]"
                        skip=1 ;;
                esac
                if [ "$label" != deleted ] && [ ! -e "$REPO/${path%.meta}" ]; then
                    note="$note [warning: asset ${path%.meta} is missing; Unity will delete this .meta]"
                fi ;;
        esac
        printf '  %-9s %s%s\n' "$label" "$path" "$note"
        if [ "$skip" -eq 0 ]; then
            stage_paths+=("$path")
        fi
    done
    if [ "$STAGE" -eq 1 ] && [ ${#stage_paths[@]} -gt 0 ]; then
        git -C "$REPO" add -A -- "${stage_paths[@]}"
        echo "meta-sweep: staged ${#stage_paths[@]} file(s); nothing committed"
    elif [ "$STAGE" -eq 1 ]; then
        echo "meta-sweep: nothing staged"
    else
        echo "meta-sweep: ${#stage_paths[@]} file(s) would be staged; nothing changed"
    fi
fi

# --- .meta discipline across the whole Assets tree -----------------------------
missing=()
while IFS= read -r -d '' f; do
    if [ ! -e "$f.meta" ]; then
        missing+=("${f#$REPO/}")
    fi
done < <(find "$ASSETS" -mindepth 1 \( -name '*~' -o -name '.*' \) -prune -o ! -name '*.meta' -print0)

orphans=()
while IFS= read -r -d '' f; do
    if [ ! -e "${f%.meta}" ]; then
        orphans+=("${f#$REPO/}")
    fi
done < <(find "$ASSETS" -mindepth 1 \( -name '*~' -o -name '.*' \) -prune -o -name '*.meta' -print0)

problems=0
if [ ${#missing[@]} -gt 0 ]; then
    problems=1
    echo "meta-sweep: warning: ${#missing[@]} asset(s) without a .meta (run tools/new-meta.sh, or let a gate import them):" >&2
    for m in "${missing[@]}"; do
        echo "  $m" >&2
    done
fi
if [ ${#orphans[@]} -gt 0 ]; then
    problems=1
    echo "meta-sweep: warning: ${#orphans[@]} .meta file(s) whose asset is missing:" >&2
    for o in "${orphans[@]}"; do
        echo "  $o" >&2
    done
fi
if [ "$STRICT" -eq 1 ] && [ "$problems" -ne 0 ]; then
    echo "meta-sweep: --strict: .meta discipline violated" >&2
    exit 1
fi
exit 0
