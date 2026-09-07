#!/usr/bin/env python3
"""Render the Microsoft Store listing art from the HeatonCA icon master.

  python Packaging/windows/make-store-art.py            render, then verify
  python Packaging/windows/make-store-art.py --check    verify only (exit 1 on a
                                                        missing or wrong file)

Reads   Assets/Icons/heatonca_icon_1024.png (put there by tools/extract-icons.sh)
Writes  store/windows/graphics/box-art-1080.png       1080x1080  RGB
        store/windows/graphics/poster-art-720x1080.png 720x1080  RGB

The Windows sibling of Packaging/android/make-play-graphics.py, and deliberately
the same recipe: the indigo of the automaton's cells as the field, the green
rounded tile floating on it, and the wordmark split "Heaton" white / "CA" green.
Two desktop store listings that look like the same product is the whole reason
the screenshot sets share a size, and the promotional art follows.

The art lives in store/windows/graphics/, NOT store/windows/, because
store/README.md's verification map globs store/windows/*.png and requires every
file there to be exactly 2560x1600 -- listing art dropped beside the screenshots
would fail that check. Play's graphics sit in their own subdirectory for the
same reason.

Partner Center offers two sizes per slot (9:16 poster art 720x1080 or 1440x2160;
1:1 box art 1080x1080 or 2160x2160) and takes one image per slot. The smaller of
each pair is what this renders, because the icon master is 1024x1024: at 1080 the
tile is drawn at or below its native resolution, while 2160 would mean a ~2.1x
upscale of the only source that exists. Sharp at the accepted size beats soft at
the larger one. If a 2160 pair is ever wanted, re-master the icon first.

RGB with no alpha, matching the Play rule -- store listing art with a transparent
channel is rejected by some pipelines even when every pixel is opaque.

Pillow writes PNGs without timestamps, so re-running on the same master produces
byte-identical files and no spurious diffs. Nothing is written into Assets/, so
no .meta files are involved.
"""
import os
import sys

try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError:  # pragma: no cover
    sys.exit("make-store-art: Pillow is required (python -m pip install pillow)")

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MASTER = os.path.join(ROOT, "Assets", "Icons", "heatonca_icon_1024.png")
OUTDIR = os.path.join(ROOT, "store", "windows", "graphics")

# The same three colors make-play-graphics.py takes from the master.
TILE_GREEN = (140, 239, 156)
INDIGO = (74, 74, 204)
WHITE = (245, 247, 252)
TAGLINE_INK = (214, 240, 222)

WORDMARK_HEAD = "Heaton"
WORDMARK_TAIL = "CA"
TAGLINE = "Evolve and explore cellular automata"

BOX = (1080, 1080)
POSTER = (720, 1080)

# Bold sans, in preference order. Windows first (this script runs on the build VM
# beside package-msix.ps1), then the macOS faces make-play-graphics.py uses, so the
# two scripts can produce matching type on either machine.
FONTS = [
    (r"C:\Windows\Fonts\segoeuib.ttf", 0),
    (r"C:\Windows\Fonts\arialbd.ttf", 0),
    ("/System/Library/Fonts/HelveticaNeue.ttc", 1),
    ("/Library/Fonts/Arial Bold.ttf", 0),
    ("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 0),
]


def find_font():
    for path, index in FONTS:
        if os.path.exists(path):
            return path, index
    sys.exit(
        "make-store-art: no bold sans font found. Looked for:\n  "
        + "\n  ".join(p for p, _ in FONTS)
    )


def fit_font(draw, path, index, text, max_width, start, floor):
    """Largest size at or below `start` whose `text` fits `max_width`."""
    size = start
    while size > floor:
        font = ImageFont.truetype(path, size, index=index)
        if draw.textlength(text, font=font) <= max_width:
            return font
        size -= 2
    return ImageFont.truetype(path, floor, index=index)


def text_height(draw, font, text):
    box = draw.textbbox((0, 0), text, font=font)
    return box[3] - box[1]


def load_tile():
    """The rounded tile cropped to its opaque bounds (the master carries a margin)."""
    master = Image.open(MASTER).convert("RGBA")
    bbox = master.getbbox()
    return master.crop(bbox)


def draw_wordmark(draw, font, cx, y, head, tail):
    """Two-color wordmark centered on cx. Returns the height drawn."""
    w_head = draw.textlength(head, font=font)
    w_tail = draw.textlength(tail, font=font)
    x = cx - (w_head + w_tail) / 2
    draw.text((x, y), head, font=font, fill=WHITE)
    draw.text((x + w_head, y), tail, font=font, fill=TILE_GREEN)
    return text_height(draw, font, head + tail)


def render(size, tile, font_path, font_index, with_tagline):
    w, h = size
    canvas = Image.new("RGB", size, INDIGO)
    draw = ImageDraw.Draw(canvas)
    margin = int(w * 0.09)
    inner = w - 2 * margin

    tile_px = int(w * 0.58)
    scaled = tile.resize((tile_px, tile_px), Image.LANCZOS)

    wm_font = fit_font(draw, font_path, font_index, WORDMARK_HEAD + WORDMARK_TAIL,
                       inner, int(w * 0.17), 24)
    wm_h = text_height(draw, wm_font, WORDMARK_HEAD + WORDMARK_TAIL)

    tag_font = None
    tag_h = 0
    if with_tagline:
        tag_font = fit_font(draw, font_path, font_index, TAGLINE, inner, int(w * 0.052), 14)
        tag_h = text_height(draw, tag_font, TAGLINE)

    gap_tile = int(w * 0.06)
    gap_tag = int(w * 0.035)
    block = tile_px + gap_tile + wm_h + (gap_tag + tag_h if with_tagline else 0)
    top = (h - block) // 2

    canvas.paste(scaled, ((w - tile_px) // 2, top), scaled)
    y = top + tile_px + gap_tile
    # textbbox offsets: draw from the ascender, not the glyph top.
    off = draw.textbbox((0, 0), WORDMARK_HEAD + WORDMARK_TAIL, font=wm_font)[1]
    draw_wordmark(draw, wm_font, w / 2, y - off, WORDMARK_HEAD, WORDMARK_TAIL)
    if with_tagline:
        y += wm_h + gap_tag
        toff = draw.textbbox((0, 0), TAGLINE, font=tag_font)[1]
        tw = draw.textlength(TAGLINE, font=tag_font)
        draw.text((w / 2 - tw / 2, y - toff), TAGLINE, font=tag_font, fill=TAGLINE_INK)
    return canvas


WANT = [
    ("box-art-1080.png", BOX, False),
    ("poster-art-720x1080.png", POSTER, True),
]


def check():
    bad = 0
    for name, size, _ in WANT:
        path = os.path.join(OUTDIR, name)
        if not os.path.exists(path):
            print("MISSING %s" % path)
            bad += 1
            continue
        im = Image.open(path)
        ok = im.size == size and im.mode == "RGB"
        print("%-28s %-11s %-4s %s" % (name, "%dx%d" % im.size, im.mode, "OK" if ok else "FAIL"))
        if not ok:
            bad += 1
    return bad


def main():
    if "--check" in sys.argv:
        sys.exit(1 if check() else 0)
    if not os.path.exists(MASTER):
        sys.exit("make-store-art: icon master not found: %s" % MASTER)
    os.makedirs(OUTDIR, exist_ok=True)
    tile = load_tile()
    font_path, font_index = find_font()
    print("make-store-art: font %s" % font_path)
    for name, size, with_tagline in WANT:
        img = render(size, tile, font_path, font_index, with_tagline)
        img.save(os.path.join(OUTDIR, name))
        print("wrote %s" % os.path.join(OUTDIR, name))
    sys.exit(1 if check() else 0)


if __name__ == "__main__":
    main()
