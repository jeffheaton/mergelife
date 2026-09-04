#!/usr/bin/env python3
"""Render the WebGL template's browser icons from the app icon master.

  python3 tools/make-web-icons.py            render, write missing .meta files, verify
  python3 tools/make-web-icons.py --check    verify only (exit 1 on a missing or wrong file)

Reads   Assets/Icons/heatonca_icon_1024.png (put there by tools/extract-icons.sh)
Writes  Assets/WebGLTemplates/HeatonCA/TemplateData/favicon.png            64x64  RGBA
        Assets/WebGLTemplates/HeatonCA/TemplateData/apple-touch-icon.png   180x180 RGB

Both images start from the tile cropped to its opaque bounds: the 1024 master
carries a transparent margin (bbox 94..930), and cropping first is what the
SetIOSIcons recipe does, so the touch icon matches the shipped iOS set to
within a fraction of a level. The favicon keeps its alpha so the rounded
corners stay transparent in a browser tab. The touch icon is flattened onto
the tile's modal color (140,239,156): iOS strips icon alpha and would show
black corners otherwise, the same reason Assets/Icons/iOS is RGB.

Pillow writes PNGs without timestamps, so re-running on the same master
produces byte-identical files and no spurious diffs.

Folders and .meta files: Unity imports everything under Assets/WebGLTemplates
like any other asset, so the script creates Assets/WebGLTemplates/,
HeatonCA/, and TemplateData/ with folder .meta files, plus a TextureImporter
.meta for each image (template: Assets/Icons/iOS/app_180.png.meta with
mipmaps off, uncompressed, no NPOT scaling, clamp wrap, max size 256). A .meta
that already exists is never rewritten, so GUIDs stay stable across renders.
"""
import os
import re
import sys
import uuid

try:
    from PIL import Image
except ImportError:  # pragma: no cover
    sys.exit("make-web-icons: Pillow is required (python3 -m pip install pillow)")

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MASTER = os.path.join(ROOT, "Assets", "Icons", "heatonca_icon_1024.png")
META_TEMPLATE = os.path.join(ROOT, "Assets", "Icons", "iOS", "app_180.png.meta")
TEMPLATE_DIR = os.path.join(ROOT, "Assets", "WebGLTemplates", "HeatonCA", "TemplateData")
TILE_COLOR = (140, 239, 156)  # the tile's modal color; SetIOSIcons floods corners with it

# name -> (size, mode)
OUTPUTS = {
    "favicon.png": (64, "RGBA"),
    "apple-touch-icon.png": (180, "RGB"),
}

# Import settings shared by both browser icons. Only the alpha flag differs.
TEXTURE_OVERRIDES = {
    "enableMipMap": "0",
    "sRGBTexture": "1",
    "filterMode": "1",
    "wrapU": "1",
    "wrapV": "1",
    "maxTextureSize": "256",
    "textureCompression": "0",
    "nPOTScale": "0",
    "textureType": "0",
}


def new_guid():
    """A fresh 32-hex Unity GUID."""
    return uuid.uuid4().hex


def folder_meta(guid):
    """The minimal folder .meta block Unity accepts (matches tools/new-meta.sh)."""
    return (
        "fileFormatVersion: 2\n"
        "guid: %s\n"
        "folderAsset: yes\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n" % guid
    )


_KEY_LINE = re.compile(r"^(\s*(?:- )?)([A-Za-z_][A-Za-z0-9_]*):(.*)$")


def render_texture_meta(template_text, guid, overrides):
    """Return template_text with a new guid and every `key: value` line whose
    key is in overrides rewritten (at any indentation, so maxTextureSize and
    textureCompression change in every platform block). Other lines are kept
    byte-for-byte."""
    out = []
    for line in template_text.splitlines():
        m = _KEY_LINE.match(line)
        if m:
            indent, key, _ = m.groups()
            if key == "guid" and indent == "":
                out.append("guid: %s" % guid)
                continue
            if key in overrides:
                out.append("%s%s: %s" % (indent, key, overrides[key]))
                continue
        out.append(line)
    return "\n".join(out) + "\n"


def ensure_folder(path):
    """Create the folder and, if missing, its sibling .meta. Returns True when a
    .meta was written."""
    os.makedirs(path, exist_ok=True)
    meta = path + ".meta"
    if os.path.exists(meta):
        return False
    with open(meta, "w", newline="\n") as f:
        f.write(folder_meta(new_guid()))
    return True


def ensure_texture_meta(image_path, template_text, alpha_is_transparency):
    meta = image_path + ".meta"
    if os.path.exists(meta):
        return False
    overrides = dict(TEXTURE_OVERRIDES)
    overrides["alphaIsTransparency"] = "1" if alpha_is_transparency else "0"
    with open(meta, "w", newline="\n") as f:
        f.write(render_texture_meta(template_text, new_guid(), overrides))
    return True


def render():
    if not os.path.isfile(MASTER):
        sys.exit("make-web-icons: %s is missing; run tools/extract-icons.sh first" % os.path.relpath(MASTER, ROOT))
    if not os.path.isfile(META_TEMPLATE):
        sys.exit("make-web-icons: %s is missing; run tools/extract-icons.sh first" % os.path.relpath(META_TEMPLATE, ROOT))
    with open(META_TEMPLATE) as f:
        template_text = f.read()

    with Image.open(MASTER) as master:
        master = master.convert("RGBA")
        bbox = master.getbbox()
        tile = master.crop(bbox)
    print("make-web-icons: master %dx%d, opaque bounds %s -> tile %dx%d" % (master.width, master.height, bbox, tile.width, tile.height))

    for folder in (
        os.path.dirname(os.path.dirname(TEMPLATE_DIR)),
        os.path.dirname(TEMPLATE_DIR),
        TEMPLATE_DIR,
    ):
        if ensure_folder(folder):
            print("  wrote %s" % os.path.relpath(folder + ".meta", ROOT))

    for name, (size, mode) in OUTPUTS.items():
        scaled = tile.resize((size, size), Image.LANCZOS)
        if mode == "RGB":
            flat = Image.new("RGBA", (size, size), TILE_COLOR + (255,))
            flat.alpha_composite(scaled)
            scaled = flat.convert("RGB")
        path = os.path.join(TEMPLATE_DIR, name)
        scaled.save(path, format="PNG", optimize=True)
        print("  wrote %s (%s %dx%d)" % (os.path.relpath(path, ROOT), scaled.mode, size, size))
        if ensure_texture_meta(path, template_text, alpha_is_transparency=(mode == "RGBA")):
            print("  wrote %s" % os.path.relpath(path + ".meta", ROOT))


def verify():
    ok = True
    for folder in (
        os.path.dirname(os.path.dirname(TEMPLATE_DIR)),
        os.path.dirname(TEMPLATE_DIR),
        TEMPLATE_DIR,
    ):
        for path in (folder, folder + ".meta"):
            if not os.path.exists(path):
                print("MISSING %s" % os.path.relpath(path, ROOT), file=sys.stderr)
                ok = False
    for name, (size, mode) in OUTPUTS.items():
        path = os.path.join(TEMPLATE_DIR, name)
        if not os.path.isfile(path):
            print("MISSING %s" % os.path.relpath(path, ROOT), file=sys.stderr)
            ok = False
            continue
        with Image.open(path) as im:
            got = (im.mode, im.size)
        if got != (mode, (size, size)):
            print("BAD %s: expected %s %dx%d, got %s %dx%d" % (os.path.relpath(path, ROOT), mode, size, size, got[0], got[1][0], got[1][1]), file=sys.stderr)
            ok = False
        else:
            print("  ok %s %s %dx%d" % (os.path.relpath(path, ROOT), got[0], got[1][0], got[1][1]))
        meta = path + ".meta"
        if not os.path.isfile(meta):
            print("MISSING %s" % os.path.relpath(meta, ROOT), file=sys.stderr)
            ok = False
        else:
            with open(meta) as f:
                text = f.read()
            if "TextureImporter:" not in text or not re.search(r"^guid: [0-9a-f]{32}$", text, re.M):
                print("BAD %s: not a TextureImporter .meta with a 32-hex guid" % os.path.relpath(meta, ROOT), file=sys.stderr)
                ok = False
    return ok


def main(argv):
    check_only = False
    for arg in argv[1:]:
        if arg == "--check":
            check_only = True
        elif arg in ("-h", "--help"):
            print(__doc__.strip())
            return 0
        else:
            sys.exit("make-web-icons: unknown argument '%s' (try --check)" % arg)
    if not check_only:
        render()
    if not verify():
        print("make-web-icons: FAIL", file=sys.stderr)
        return 1
    print("make-web-icons: OK")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
