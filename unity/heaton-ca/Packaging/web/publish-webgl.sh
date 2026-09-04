#!/usr/bin/env bash
# publish-webgl.sh -- upload the HeatonCA WebGL build to a static host, with the
# per-file headers the Unity loader wants.
#
# Why a script and not `aws s3 sync`: a Unity WebGL build is NOT a folder of
# interchangeable static files. Three of them are gzip streams that Unity named
# `.unityweb` instead of `.gz`, so no tool guesses their `Content-Type` or their
# `Content-Encoding` correctly; the loader is plain JavaScript that must never be
# served as gzip; and one folder in the output (`*_DoNotShip`, Burst's debug
# symbols) must never reach the public site. `sync` would upload all of it as
# `binary/octet-stream` with no encoding, which still *works* here only because
# the build ships with Decompression Fallback on -- the loader would gunzip in
# JavaScript on every visit and start noticeably slower. This script sends the
# right four headers per file and prints exactly what it will do first.
#
#   Packaging/web/publish-webgl.sh                     dry run (default): manifest only
#   Packaging/web/publish-webgl.sh --dest s3://bucket/heatonca/2.0.0
#   Packaging/web/publish-webgl.sh --dest ... --confirm     actually upload
#   Packaging/web/publish-webgl.sh --dest ~/sites/heatonca --confirm   local mirror
#
# A run with no `--confirm` uploads nothing, ever. `--dest` may also come from
# $HEATONCA_WEB_DEST. It is either an `s3://bucket/prefix` URI (headers are set
# per object) or a local directory (a plain mirror -- the host must apply the
# headers itself; the manifest below is the table to hand it).
#
# Options:
#   --dest DEST        destination; s3://bucket/prefix or a local directory
#   --src DIR          build folder (default build/webgl)
#   --confirm          perform the upload; without it the run is a dry run
#   --dry-run          the default, stated explicitly (rejected with --confirm)
#   --no-immutable     Build/ and TemplateData/ get max-age=300 instead of
#                      immutable -- use it when you redeploy over a FLAT prefix
#                      (see "Cache-Control" below)
#   --quiet            manifest only, no per-file upload chatter
#   -h, --help         this header
#
# Environment:
#   HEATONCA_WEB_DEST      default --dest
#   HEATONCA_WEB_AWS_ARGS  extra args passed to every `aws s3 cp`
#                          (e.g. "--profile heaton --region us-east-1")
#   HEATONCA_WEB_CF_DIST   CloudFront distribution id; the script does not call
#                          it, it prints the invalidation command to run next
#
# Exit codes: 0 success (including a dry run); 1 usage error; 2 the build is
# missing, incomplete, or is not HeatonCA; 3 an upload failed; 5 the aws CLI is
# required for this destination and is not installed.
#
# Content-Type / Content-Encoding (Unity names the build files after the OUTPUT
# FOLDER, not productName, so with `build/webgl` they are `webgl.*`):
#
#   webgl.loader.js             application/javascript    (never compressed)
#   webgl.framework.js.unityweb application/javascript    Content-Encoding: gzip
#   webgl.wasm.unityweb         application/wasm          Content-Encoding: gzip
#   webgl.data.unityweb         application/octet-stream  Content-Encoding: gzip
#   index.html                  text/html; charset=utf-8
#   TemplateData/*              image/png, text/css; charset=utf-8
#
# The encoding of a `.unityweb` file is sniffed from its first two bytes rather
# than assumed, so a build switched to Brotli is labeled `br` and not mislabeled
# `gzip` (the same test tools/webgl-serve.py uses).
#
# Cache-Control: `index.html` is `no-cache` (it is the file that flips the site
# to a new build), everything under `Build/`, `TemplateData/`, and
# `StreamingAssets/` is a year and `immutable`. That split is only safe when a
# deploy lands on a NEW prefix, because `webGLNameFilesAsHashes` is 0 here: the
# payload names never change between builds. So either publish under a versioned
# prefix (`s3://bucket/heatonca/2.0.0/`) and repoint the page at it, or redeploy
# flat with `--no-immutable` plus a CDN invalidation. Uploading a new build over
# an immutable flat prefix leaves returning visitors on the old payloads for a
# year, and there is no way to take that back.
#
# Ordering: payloads first, `index.html` last, so a visitor who loads the page
# mid-deploy never gets a new index pointing at files that are not there yet.
#
# Sandbox: `aws s3 cp` needs network access, so an upload must run with the
# Claude Code Bash sandbox DISABLED. A dry run is offline and sandbox-safe.
set -euo pipefail

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$(cd "$DIR/../.." && pwd)"

DEST="${HEATONCA_WEB_DEST:-}"
SRC="$PROJECT/build/webgl"
CONFIRM=0
DRY_RUN_FLAG=0
IMMUTABLE=1
QUIET=0

usage() { sed -n '2,76p' "$0"; }

while [ $# -gt 0 ]; do
    case "$1" in
        --dest)          [ $# -ge 2 ] || { echo "error: --dest needs a value" >&2; exit 1; }
                         DEST="$2"; shift 2 ;;
        --dest=*)        DEST="${1#--dest=}"; shift ;;
        --src)           [ $# -ge 2 ] || { echo "error: --src needs a value" >&2; exit 1; }
                         SRC="$2"; shift 2 ;;
        --src=*)         SRC="${1#--src=}"; shift ;;
        --confirm)       CONFIRM=1; shift ;;
        --dry-run)       DRY_RUN_FLAG=1; shift ;;
        --no-immutable)  IMMUTABLE=0; shift ;;
        --quiet)         QUIET=1; shift ;;
        -h|--help)       usage; exit 0 ;;
        *)               echo "error: unknown argument: $1" >&2; echo >&2; usage >&2; exit 1 ;;
    esac
done

if [ "$CONFIRM" = 1 ] && [ "$DRY_RUN_FLAG" = 1 ]; then
    echo "error: --dry-run and --confirm contradict each other" >&2
    exit 1
fi
if [ "$CONFIRM" = 1 ] && [ -z "$DEST" ]; then
    echo "error: --confirm needs a destination: --dest s3://bucket/prefix (or \$HEATONCA_WEB_DEST)" >&2
    exit 1
fi

# ---------------------------------------------------------------- build checks
# Refuse early on anything that would publish a broken or foreign site. The
# title check is the cheap proof that this folder is a HeatonCA build and not
# some other project's output left in build/webgl by a hand-run editor build.
[ -d "$SRC" ] || {
    echo "error: no build folder: $SRC" >&2
    echo "       run tools/unity-gate.sh build-webgl first" >&2
    exit 2
}
SRC="$(cd "$SRC" && pwd)"
INDEX="$SRC/index.html"
[ -f "$INDEX" ] || {
    echo "error: $INDEX is missing -- that is not a finished WebGL build" >&2
    exit 2
}
TITLE="$(grep -o '<title>[^<]*</title>' "$INDEX" | head -1 || true)"
if [ "$TITLE" != "<title>HeatonCA</title>" ]; then
    echo "error: $INDEX has title '${TITLE:-<none>}', expected '<title>HeatonCA</title>'" >&2
    echo "       refusing to publish a build that is not HeatonCA" >&2
    exit 2
fi
[ -d "$SRC/Build" ] || {
    echo "error: $SRC/Build is missing -- the build did not finish" >&2
    exit 2
}

DEST_KIND="none"
case "$DEST" in
    "")     DEST_KIND="none" ;;
    s3://*) DEST_KIND="s3" ;;
    *)      DEST_KIND="local" ;;
esac

# The aws CLI is what applies the headers, so an s3 destination without it is a
# dead end -- report that before printing a manifest that could not be uploaded.
if [ "$DEST_KIND" = "s3" ] && ! command -v aws >/dev/null 2>&1; then
    echo "error: the aws CLI is not installed, so an s3:// destination cannot be published" >&2
    echo "       install it (brew install awscli) and configure credentials, or publish" >&2
    echo "       to a local directory and upload that with your host's own tool" >&2
    exit 5
fi

# --------------------------------------------------------------- header tables
CACHE_SHORT="no-cache, max-age=0, must-revalidate"
if [ "$IMMUTABLE" = 1 ]; then
    CACHE_LONG="public, max-age=31536000, immutable"
else
    CACHE_LONG="public, max-age=300"
fi

# The type a browser must see. Ordered longest-suffix first: a `.unityweb` file
# is typed by what is INSIDE it (framework -> JavaScript, wasm -> wasm), so the
# generic `*.unityweb` case has to come last among those.
content_type_for() { # $1 = file name
    local name
    name="$(printf '%s' "${1##*/}" | tr '[:upper:]' '[:lower:]')"
    case "$name" in
        *.gz) name="${name%.gz}" ;;
        *.br) name="${name%.br}" ;;
    esac
    case "$name" in
        *.framework.js.unityweb)  echo "application/javascript" ;;
        *.wasm.unityweb)          echo "application/wasm" ;;
        *.data.unityweb)          echo "application/octet-stream" ;;
        *.symbols.json.unityweb)  echo "application/json" ;;
        *.unityweb)               echo "application/octet-stream" ;;
        *.js|*.mjs)               echo "application/javascript" ;;
        *.wasm)                   echo "application/wasm" ;;
        *.data)                   echo "application/octet-stream" ;;
        *.html|*.htm)             echo "text/html; charset=utf-8" ;;
        *.css)                    echo "text/css; charset=utf-8" ;;
        *.json)                   echo "application/json" ;;
        *.webmanifest)            echo "application/manifest+json" ;;
        *.png)                    echo "image/png" ;;
        *.jpg|*.jpeg)             echo "image/jpeg" ;;
        *.svg)                    echo "image/svg+xml" ;;
        *.ico)                    echo "image/x-icon" ;;
        *.txt)                    echo "text/plain; charset=utf-8" ;;
        *)                        echo "application/octet-stream" ;;
    esac
}

# gzip / br / empty. `.unityweb` carries no hint in its name, so read the magic
# bytes: 1f 8b is gzip, anything else from a Unity build is Brotli.
content_encoding_for() { # $1 = full path
    local name magic
    name="$(printf '%s' "${1##*/}" | tr '[:upper:]' '[:lower:]')"
    case "$name" in
        *.gz) echo "gzip"; return ;;
        *.br) echo "br"; return ;;
        *.unityweb) ;;
        *) echo ""; return ;;
    esac
    magic="$(od -An -N2 -tx1 "$1" 2>/dev/null | tr -d ' \n')"
    if [ "$magic" = "1f8b" ]; then echo "gzip"; else echo "br"; fi
}

cache_control_for() { # $1 = path relative to the build root
    case "$1" in
        Build/*|TemplateData/*|StreamingAssets/*) echo "$CACHE_LONG" ;;
        *)                                        echo "$CACHE_SHORT" ;;
    esac
}

file_size() { # $1 = full path
    stat -f%z "$1" 2>/dev/null || stat -c%s "$1"
}

human() { # $1 = bytes
    awk -v b="$1" 'BEGIN {
        if (b >= 1048576) printf "%.1f MB", b / 1048576;
        else if (b >= 1024) printf "%.1f KB", b / 1024;
        else printf "%d B", b;
    }'
}

# ------------------------------------------------------------------ file lists
# `-name "*_DoNotShip" -prune` drops build/webgl/HeatonCA_BurstDebugInformation_DoNotShip
# and any sibling Unity invents later. Burst writes it on EVERY build; it is
# internal symbol text, it is useless to a visitor, and Unity's own folder name
# is the instruction. .DS_Store is Finder litter that would otherwise ship.
#
# Read with a null-delimited loop rather than `mapfile`: macOS ships bash 3.2,
# which has neither `mapfile` nor `readarray` (tools/check-selfcheck.sh notes the
# same constraint), and paths are null-delimited so a space could not split one.
ALL_FILES=()
while IFS= read -r -d '' file; do
    ALL_FILES+=("$file")
done < <(find "$SRC" -name '*_DoNotShip' -prune -o -type f ! -name '.DS_Store' -print0 | sort -z)

SKIPPED=()
while IFS= read -r -d '' file; do
    SKIPPED+=("$file")
done < <(find "$SRC" -name '*_DoNotShip' -type d -exec find {} -type f -print0 \;)

if [ "${#ALL_FILES[@]}" -eq 0 ]; then
    echo "error: $SRC contains no publishable files" >&2
    exit 2
fi

# index.html goes last so a mid-deploy visitor never sees a new page pointing at
# payloads that have not landed yet.
ORDERED=()
for file in "${ALL_FILES[@]}"; do
    [ "$file" = "$INDEX" ] || ORDERED+=("$file")
done
ORDERED+=("$INDEX")

# --------------------------------------------------------------------- manifest
DEST_SHOWN="$DEST"
[ -n "$DEST_SHOWN" ] || DEST_SHOWN="(none given -- dry run only)"
DEST_PREFIX="${DEST%/}"

echo "HeatonCA WebGL publish"
echo "  source      : $SRC"
echo "  destination : $DEST_SHOWN"
if [ "$CONFIRM" = 1 ]; then
    echo "  mode        : UPLOAD (--confirm)"
else
    echo "  mode        : DRY RUN (nothing is uploaded)"
fi
echo
printf '%10s  %-26s  %-6s  %-36s  %s\n' "SIZE" "CONTENT-TYPE" "ENCODE" "CACHE-CONTROL" "PATH"

TOTAL=0
for file in "${ORDERED[@]}"; do
    rel="${file#"$SRC"/}"
    size="$(file_size "$file")"
    TOTAL=$((TOTAL + size))
    printf '%10s  %-26s  %-6s  %-36s  %s\n' \
        "$(human "$size")" \
        "$(content_type_for "$file")" \
        "$(content_encoding_for "$file")" \
        "$(cache_control_for "$rel")" \
        "$rel"
done

echo
echo "  ${#ORDERED[@]} files, $(human "$TOTAL") ($TOTAL bytes)"
if [ "${#SKIPPED[@]}" -gt 0 ]; then
    echo "  excluded (*_DoNotShip, never published): ${#SKIPPED[@]} file(s)"
    for file in "${SKIPPED[@]}"; do
        echo "    ${file#"$SRC"/}"
    done
fi
if [ "$IMMUTABLE" = 1 ]; then
    echo "  note: Build/ and TemplateData/ are immutable for a year and their names do NOT"
    echo "        carry a hash -- publish to a versioned prefix, or use --no-immutable."
fi

# ----------------------------------------------------------------------- upload
if [ "$CONFIRM" != 1 ]; then
    echo
    echo "DRY RUN: nothing was uploaded."
    if [ -z "$DEST" ]; then
        echo "To publish: --dest s3://bucket/prefix (or \$HEATONCA_WEB_DEST) --confirm"
    else
        echo "To publish: re-run the same command with --confirm"
    fi
    exit 0
fi

AWS_EXTRA=()
if [ -n "${HEATONCA_WEB_AWS_ARGS:-}" ]; then
    read -r -a AWS_EXTRA <<< "$HEATONCA_WEB_AWS_ARGS"
fi

echo
echo "Uploading to $DEST_PREFIX ..."
for file in "${ORDERED[@]}"; do
    rel="${file#"$SRC"/}"
    ctype="$(content_type_for "$file")"
    cenc="$(content_encoding_for "$file")"
    cache="$(cache_control_for "$rel")"

    if [ "$DEST_KIND" = "s3" ]; then
        args=(s3 cp "$file" "$DEST_PREFIX/$rel"
              --content-type "$ctype" --cache-control "$cache")
        [ -z "$cenc" ] || args+=(--content-encoding "$cenc")
        [ "$QUIET" = 0 ] || args+=(--only-show-errors)
        [ "${#AWS_EXTRA[@]}" -eq 0 ] || args+=("${AWS_EXTRA[@]}")
        if ! aws "${args[@]}"; then
            echo "error: upload failed for $rel -- the site may be half-published" >&2
            echo "       fix the cause and re-run; index.html is uploaded last, so a" >&2
            echo "       failure before it leaves the live page pointing at the old build" >&2
            exit 3
        fi
    else
        target="$DEST_PREFIX/$rel"
        mkdir -p "$(dirname "$target")"
        if ! cp -p "$file" "$target"; then
            echo "error: copy failed for $rel" >&2
            exit 3
        fi
        [ "$QUIET" = 1 ] || echo "copy: $rel"
    fi
done

echo
echo "OK: ${#ORDERED[@]} files published to $DEST_PREFIX"
if [ "$DEST_KIND" = "local" ]; then
    echo "This was a plain file copy: a local directory carries no headers. Configure the"
    echo "host with the Content-Type / Content-Encoding / Cache-Control columns above, or"
    echo "leave it alone -- the build's decompression fallback works without them, just"
    echo "slower to start. See store/webgl/README.md."
fi
if [ -n "${HEATONCA_WEB_CF_DIST:-}" ]; then
    echo
    echo "CloudFront: invalidate the cached copies next (not run for you):"
    echo "  aws cloudfront create-invalidation --distribution-id $HEATONCA_WEB_CF_DIST --paths '/*'"
fi
echo
echo "Verify: load the page, confirm '[HeatonCA] SELF-CHECK PASS' in the browser console,"
echo "and open ?rule=e542-5f79-9341-f31e-6c6b-7f08-8773-7068 (store/webgl/README.md)."
