#!/usr/bin/env bash
# extract-icons.sh -- pull the HeatonCA app icon set out of heaton-life-unity
# history into Assets/Icons/.
#
# The HeatonCA tile (green 140,239,156 flood with the black cellular-automaton
# mark) shipped as heaton-life-unity's icon set until its commit 26b730a
# ("Change icons, preparing for app store release") replaced it with the
# leaf/circuit set. The PNGs are therefore read from 26b730a^
# (= 01e3bee4430b53374afc3d42c8afe8b985a00306) with `git show`, so this
# script needs the heaton-life-unity checkout but no working-tree state.
#
# The .meta files come from heaton-life-unity HEAD, with their GUIDs kept
# UNCHANGED on purpose: this project's ProjectSettings/ProjectSettings.asset
# was copied from that HEAD and references the icons by GUID
# (m_BuildTargetIcons + m_BuildTargetPlatformIcons), e.g.
#   Icons/heatonca_icon_1024.png   a7c31f5b9d2e4c68a1f3b5d7e9c2a4f6   default icon, every target
#   Icons/iOS/app_180.png          7a057919d1ac4b828da0af4781c593bb   plus the other five iOS sizes
# Minting fresh GUIDs here would silently detach every icon slot.
#
# Usage:
#   tools/extract-icons.sh            extract, then verify
#   tools/extract-icons.sh --check    verify only; exit 1 on any mismatch
#
# Environment:
#   HEATON_LIFE_UNITY   heaton-life-unity checkout (default ~/projects/heaton-life-unity)
#
# Writes, relative to unity/heaton-ca/Assets/Icons (heatonlife_icon_1024.png is
# renamed to heatonca_icon_1024.png; everything else keeps its path):
#   ../Icons.meta  iOS.meta  Android.meta
#   heatonca_icon_1024.png(.meta)                          1024x1024 RGBA master
#   iOS/app_{76,120,152,167,180}.png(.meta)                RGB, no alpha (Apple strips it)
#   iOS/app_store_1024.png(.meta)                          1024x1024 RGB, no alpha
#   Android/{adaptive_bg,adaptive_fg,legacy,round}.png(.meta)   1024x1024 RGBA
#
# Verification (python3 + Pillow): the six iOS PNGs are mode RGB, both 1024
# masters are 1024x1024, the four Android PNGs are 1024x1024 RGBA, and every
# .meta GUID equals the GUID at heaton-life-unity HEAD.
set -euo pipefail

PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SRC="${HEATON_LIFE_UNITY:-$HOME/projects/heaton-life-unity}"
PNG_REV="26b730a^"
META_REV="HEAD"
DST="$PROJECT/Assets/Icons"

CHECK=0
case "${1:-}" in
    "") ;;
    --check) CHECK=1 ;;
    -h|--help)
        sed -n '2,40p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
        exit 0 ;;
    *)
        echo "extract-icons: unknown argument '$1' (try --check)" >&2
        exit 2 ;;
esac

# "<source path under Assets/Icons>[:<destination path>]"
ICONS=(
    "iOS/app_76.png"
    "iOS/app_120.png"
    "iOS/app_152.png"
    "iOS/app_167.png"
    "iOS/app_180.png"
    "iOS/app_store_1024.png"
    "Android/adaptive_bg.png"
    "Android/adaptive_fg.png"
    "Android/legacy.png"
    "Android/round.png"
    "heatonlife_icon_1024.png:heatonca_icon_1024.png"
)
# Folder .meta files, relative to Assets/.
FOLDER_METAS=(
    "Icons.meta"
    "Icons/iOS.meta"
    "Icons/Android.meta"
)

if [ ! -d "$SRC/.git" ] && ! git -C "$SRC" rev-parse --git-dir >/dev/null 2>&1; then
    echo "extract-icons: $SRC is not a git checkout (set HEATON_LIFE_UNITY)" >&2
    exit 1
fi
PNG_HASH="$(git -C "$SRC" rev-parse --verify "$PNG_REV^{commit}")"
META_HASH="$(git -C "$SRC" rev-parse --verify "$META_REV^{commit}")"
if ! command -v python3 >/dev/null 2>&1 || ! python3 -c 'import PIL' 2>/dev/null; then
    echo "extract-icons: python3 with Pillow is required for verification" >&2
    exit 1
fi

# git show <rev>:<path> > <dst>, written through a temp file so a failed show
# never leaves a truncated asset behind.
extract()
{
    local rev="$1" path="$2" dst="$3" tmp
    mkdir -p "$(dirname "$dst")"
    tmp="$dst.tmp.$$"
    if ! git -C "$SRC" show "$rev:$path" > "$tmp"; then
        rm -f "$tmp"
        echo "extract-icons: git show $rev:$path failed" >&2
        exit 1
    fi
    mv -f "$tmp" "$dst"
}

if [ "$CHECK" -eq 0 ]; then
    echo "extract-icons: PNGs from $SRC @ $PNG_HASH ($PNG_REV), .meta from $META_HASH ($META_REV)"
    for spec in "${ICONS[@]}"; do
        src="${spec%%:*}"
        dst="${spec#*:}"
        extract "$PNG_REV" "Assets/Icons/$src" "$DST/$dst"
        extract "$META_REV" "Assets/Icons/$src.meta" "$DST/$dst.meta"
        echo "  Icons/$dst"
    done
    for meta in "${FOLDER_METAS[@]}"; do
        extract "$META_REV" "Assets/$meta" "$PROJECT/Assets/$meta"
        echo "  $meta"
    done
fi

# ---------------------------------------------------------------------------
# Verify.
# ---------------------------------------------------------------------------
status=0

for spec in "${ICONS[@]}"; do
    src="${spec%%:*}"
    dst="${spec#*:}"
    for f in "$DST/$dst" "$DST/$dst.meta"; do
        if [ ! -s "$f" ]; then
            echo "MISSING $f" >&2
            status=1
        fi
    done
    [ -s "$DST/$dst.meta" ] || continue
    want="$(git -C "$SRC" show "$META_REV:Assets/Icons/$src.meta" | awk '/^guid:/ {print $2; exit}')"
    have="$(awk '/^guid:/ {print $2; exit}' "$DST/$dst.meta")"
    if [ "$want" != "$have" ]; then
        echo "GUID MISMATCH Icons/$dst.meta: have $have, heaton-life-unity HEAD has $want" >&2
        status=1
    fi
done
for meta in "${FOLDER_METAS[@]}"; do
    f="$PROJECT/Assets/$meta"
    if [ ! -s "$f" ]; then
        echo "MISSING $f" >&2
        status=1
        continue
    fi
    want="$(git -C "$SRC" show "$META_REV:Assets/$meta" | awk '/^guid:/ {print $2; exit}')"
    have="$(awk '/^guid:/ {print $2; exit}' "$f")"
    if [ "$want" != "$have" ]; then
        echo "GUID MISMATCH $meta: have $have, heaton-life-unity HEAD has $want" >&2
        status=1
    fi
    if ! grep -q '^folderAsset: yes' "$f"; then
        echo "NOT A FOLDER META $meta" >&2
        status=1
    fi
done

if [ "$status" -eq 0 ]; then
    python3 - "$DST" <<'__PY__' || status=1
import os
import sys
from PIL import Image

root = sys.argv[1]
expect = {
    # path: (mode, size)
    "iOS/app_76.png": ("RGB", (76, 76)),
    "iOS/app_120.png": ("RGB", (120, 120)),
    "iOS/app_152.png": ("RGB", (152, 152)),
    "iOS/app_167.png": ("RGB", (167, 167)),
    "iOS/app_180.png": ("RGB", (180, 180)),
    "iOS/app_store_1024.png": ("RGB", (1024, 1024)),
    "Android/adaptive_bg.png": ("RGBA", (1024, 1024)),
    "Android/adaptive_fg.png": ("RGBA", (1024, 1024)),
    "Android/legacy.png": ("RGBA", (1024, 1024)),
    "Android/round.png": ("RGBA", (1024, 1024)),
    "heatonca_icon_1024.png": ("RGBA", (1024, 1024)),
}
bad = 0
for rel, (mode, size) in expect.items():
    path = os.path.join(root, rel)
    with Image.open(path) as im:
        got = (im.mode, im.size)
        has_alpha = im.mode in ("RGBA", "LA", "PA") or "transparency" in im.info
    problems = []
    if got != (mode, size):
        problems.append("expected %s %dx%d, got %s %dx%d" % (mode, size[0], size[1], got[0], got[1][0], got[1][1]))
    if rel.startswith("iOS/") and has_alpha:
        problems.append("iOS icon carries an alpha channel")
    if problems:
        bad += 1
        print("BAD Icons/%s: %s" % (rel, "; ".join(problems)), file=sys.stderr)
    else:
        print("  ok Icons/%-28s %s %dx%d" % (rel, got[0], got[1][0], got[1][1]))
sys.exit(1 if bad else 0)
__PY__
fi

png_count="$(find "$DST" -name '*.png' | wc -l | tr -d ' ')"
meta_count="$(find "$DST" -name '*.meta' | wc -l | tr -d ' ')"
echo "extract-icons: $png_count PNGs, $meta_count .meta under Assets/Icons (+ Assets/Icons.meta)"
if [ "$png_count" -ne 11 ] || [ "$meta_count" -ne 13 ]; then
    echo "extract-icons: expected 11 PNGs and 13 .meta (11 PNG + iOS + Android)" >&2
    status=1
fi

if [ "$status" -ne 0 ]; then
    echo "extract-icons: FAIL" >&2
    exit 1
fi
echo "extract-icons: OK"
