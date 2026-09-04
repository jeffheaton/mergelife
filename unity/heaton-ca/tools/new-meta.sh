#!/usr/bin/env bash
# new-meta.sh -- write a hand-authored Unity .meta file with a fresh GUID.
#
# Worker agents never run Unity, so every text asset they add under Assets/
# (C# scripts, .asmdef, csc.rsp, .md/.txt/.json, .jslib, and the folders that
# hold them) needs a .meta written by hand before the orchestrator's gate opens
# the project. This script writes the minimal importer block for each kind.
# Unity may later normalize the block; that is fine, because the GUID is the
# part that matters: scenes, prefabs, and ProjectSettings reference assets by
# GUID, and a missing .meta makes Unity mint a new one, breaking those links.
#
# Usage:
#   tools/new-meta.sh [--stdout] [--guid <32 hex>] <path> [more paths]
#
#   <path>      the ASSET path (not the .meta path). A trailing slash, an
#               existing directory, or a missing path with no extension is
#               treated as a folder.
#   --stdout    print the .meta content instead of writing <path>.meta
#   --guid      use this GUID instead of a random one (single path only), for
#               the reserved GUIDs listed in tools/README.md
#
# Importer blocks:
#   folder                        folderAsset: yes + DefaultImporter
#   .cs                           MonoImporter
#   .asmdef                       AssemblyDefinitionImporter
#   .md .txt .json .rsp .jslib    DefaultImporter (also .xml .csv .bytes .yaml
#   .html .css .js .entitlements  .yml, all plain text)
#
# Anything else (.png, .unity, .asset, .inputactions, ...) is refused: those
# importers carry settings the editor must generate. Let the next gate run
# create the .meta and stage it with tools/meta-sweep.sh.
#
# Never overwrites an existing .meta (exit 1). Exit 2 on usage errors.
set -euo pipefail

usage()
{
    cat <<__USAGE__
usage: tools/new-meta.sh [--stdout] [--guid <32 hex>] <path> [more paths]
       writes <path>.meta with a fresh random GUID (folder, .cs, .asmdef, or plain-text importer)
__USAGE__
}

STDOUT=0
GUID=""
PATHS=()
while [ $# -gt 0 ]; do
    case "$1" in
        --stdout) STDOUT=1 ;;
        --guid)
            if [ $# -lt 2 ]; then
                echo "new-meta: --guid needs a value" >&2
                usage >&2
                exit 2
            fi
            GUID="$2"
            shift ;;
        -h|--help) usage; exit 0 ;;
        --)
            shift
            while [ $# -gt 0 ]; do
                PATHS+=("$1")
                shift
            done
            break ;;
        -*)
            echo "new-meta: unknown option '$1'" >&2
            usage >&2
            exit 2 ;;
        *) PATHS+=("$1") ;;
    esac
    shift
done

if [ ${#PATHS[@]} -eq 0 ]; then
    usage >&2
    exit 2
fi
if [ -n "$GUID" ]; then
    case "$GUID" in
        *[!0-9a-f]*|'')
            echo "new-meta: --guid must be 32 lowercase hex digits (got '$GUID')" >&2
            exit 2 ;;
    esac
    if [ ${#GUID} -ne 32 ]; then
        echo "new-meta: --guid must be 32 lowercase hex digits (got ${#GUID})" >&2
        exit 2
    fi
    if [ ${#PATHS[@]} -ne 1 ]; then
        echo "new-meta: --guid applies to exactly one path" >&2
        exit 2
    fi
fi

new_guid()
{
    if command -v uuidgen >/dev/null 2>&1; then
        uuidgen | tr -d - | tr 'A-F' 'a-f'
    else
        od -An -N16 -tx1 /dev/urandom | tr -d ' \n'
    fi
}

# Prints the importer kind for an asset path: folder, mono, asmdef, default,
# or nothing when the type needs an editor-generated importer block.
kind_for()
{
    local p="$1" base ext
    case "$p" in
        */) echo folder; return ;;
    esac
    if [ -d "$p" ]; then
        echo folder
        return
    fi
    base="${p##*/}"
    case "$base" in
        *.*) ext="${base##*.}" ;;
        *)
            # No extension: an existing regular file is plain text, otherwise
            # assume the caller is about to create a folder.
            if [ -f "$p" ]; then echo default; else echo folder; fi
            return ;;
    esac
    ext="$(printf '%s' "$ext" | tr 'A-Z' 'a-z')"
    case "$ext" in
        cs) echo mono ;;
        asmdef) echo asmdef ;;
        md|txt|json|rsp|jslib|xml|csv|bytes|yaml|yml|html|css|js|entitlements) echo default ;;
        *) echo "" ;;
    esac
}

render()
{
    local kind="$1" guid="$2"
    printf 'fileFormatVersion: 2\nguid: %s\n' "$guid"
    case "$kind" in
        folder)
            printf 'folderAsset: yes\n'
            printf 'DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' ;;
        mono)
            printf 'MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' ;;
        asmdef)
            printf 'AssemblyDefinitionImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' ;;
        default)
            printf 'DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' ;;
    esac
}

# Pass 1: validate everything before writing anything.
KINDS=()
METAS=()
problems=0
for p in "${PATHS[@]}"; do
    kind=""
    meta="${p%/}.meta"
    case "$p" in
        *.meta)
            echo "new-meta: '$p' is a .meta path; give the asset path instead" >&2
            problems=1 ;;
        *)
            kind="$(kind_for "$p")"
            if [ -z "$kind" ]; then
                echo "new-meta: refusing '$p': no hand-written template for this asset type; let a Unity gate run generate its .meta, then stage it with tools/meta-sweep.sh" >&2
                problems=1
            fi
            if [ "$STDOUT" -ne 1 ] && [ -e "$meta" ]; then
                echo "new-meta: refusing to overwrite existing $meta" >&2
                problems=1
            fi ;;
    esac
    KINDS+=("$kind")
    METAS+=("$meta")
done
if [ "$problems" -ne 0 ]; then
    exit 1
fi

# Pass 2: write (or print).
i=0
for p in "${PATHS[@]}"; do
    kind="${KINDS[$i]}"
    meta="${METAS[$i]}"
    i=$((i + 1))
    if [ -n "$GUID" ]; then
        guid="$GUID"
    else
        guid="$(new_guid)"
    fi
    if [ "$STDOUT" -eq 1 ]; then
        if [ ${#PATHS[@]} -gt 1 ]; then
            echo "# $meta"
        fi
        render "$kind" "$guid"
    else
        dir="$(dirname "$meta")"
        if [ ! -d "$dir" ]; then
            echo "new-meta: directory $dir does not exist; create the asset (or its folder) first" >&2
            exit 1
        fi
        if [ ! -e "${p%/}" ]; then
            echo "new-meta: note: ${p%/} does not exist yet; writing its .meta anyway" >&2
        fi
        render "$kind" "$guid" > "$meta"
        echo "new-meta: wrote $meta ($kind, guid $guid)"
    fi
done
