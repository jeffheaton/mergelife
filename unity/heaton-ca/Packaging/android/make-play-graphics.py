#!/usr/bin/env python3
"""Render the Google Play listing graphics from the HeatonCA icon master.

  python3 Packaging/android/make-play-graphics.py            render, then verify
  python3 Packaging/android/make-play-graphics.py --check    verify only (exit 1 on a
                                                             missing or wrong file)

Reads   Assets/Icons/heatonca_icon_1024.png (put there by tools/extract-icons.sh)
Writes  store/android/graphics/icon-512.png                  512x512  RGB
        store/android/graphics/feature-graphic-1024x500.png  1024x500 RGB

Both files are RGB with no alpha channel on purpose: Play rejects listing art
that carries transparency (the same rule Apple applies to screenshots), and a
PNG with an alpha channel is rejected even when every pixel is opaque.

icon-512.png follows the SetIOSIcons recipe: the 1024 master carries a
transparent margin (opaque bounds 94..930), so crop the rounded tile to its
bounds, scale it to fill 512x512, and let the tile's own green flood the corner
gaps. Play masks its own rounded corners over whatever it is given, so a
square, corner-to-corner tile is exactly what it wants.

feature-graphic-1024x500.png is the app's two brand colors, both taken from the
master: the indigo of the automaton's cells (74,74,204) as the field, and the
green of the tile (140,239,156) as the icon and as the second half of the
wordmark. The icon tile sits at the left with the wordmark and tagline beside
it - the same composition as heaton-life-unity's feature graphic, with the
figure/ground swapped because HeatonCA's tile is the light color of the pair.
Type is Helvetica Neue Bold, falling back to Arial Bold; both point sizes shrink
until the line fits the space left of the right margin, so no glyph metric or
font substitution can push text off the canvas.

Pillow writes PNGs without timestamps, so re-running on the same master produces
byte-identical files and no spurious diffs. Nothing here is written into
Assets/, so no .meta files are involved.
"""
import os
import sys

try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError:  # pragma: no cover
    sys.exit("make-play-graphics: Pillow is required (python3 -m pip install pillow)")

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MASTER = os.path.join(ROOT, "Assets", "Icons", "heatonca_icon_1024.png")
OUT = os.path.join(ROOT, "store", "android", "graphics")

ICON_NAME = "icon-512.png"
FEATURE_NAME = "feature-graphic-1024x500.png"

TILE_GREEN = (140, 239, 156)   # the tile's modal color (SetAndroidIcons floods with it)
INDIGO = (74, 74, 204)         # the automaton cells' accent, the feature-graphic field
WHITE = (245, 247, 252)        # wordmark, first half
TAGLINE_INK = (214, 240, 222)  # a green-tinted white, dimmer than the wordmark

WORDMARK_HEAD = "Heaton"
WORDMARK_TAIL = "CA"
TAGLINE = "Evolve and explore cellular automata"

FEATURE_SIZE = (1024, 500)
ICON_SIZE = 512

PAD = 56          # canvas margin around the whole composition
MARK_HEIGHT = 300  # icon tile height inside the feature graphic
GAP = 44          # space between the tile and the type block
LINE_GAP = 22     # space between the wordmark and the tagline

MAC_FONTS = [
    ("/System/Library/Fonts/HelveticaNeue.ttc", "Helvetica Neue Bold"),
    ("/System/Library/Fonts/Supplemental/Arial Bold.ttf", None),
    ("/Library/Fonts/Arial Bold.ttf", None),
]


def find_font():
    """Return (path, index) of a bold sans face, preferring Helvetica Neue Bold.

    A .ttc holds several faces; the wanted one is found by name rather than by a
    hard-coded index, because the index moves between macOS releases."""
    for path, wanted in MAC_FONTS:
        if not os.path.isfile(path):
            continue
        if wanted is None:
            return path, 0
        for index in range(12):
            try:
                face = ImageFont.truetype(path, 40, index=index)
            except Exception:
                break
            if " ".join(face.getname()) == wanted:
                return path, index
    sys.exit(
        "make-play-graphics: no bold sans font found. Looked for:\n  "
        + "\n  ".join(path for path, _ in MAC_FONTS)
        + "\nInstall one, or run this script on a Mac with the system fonts present."
    )


def fit_font(draw, path, index, text, max_width, start, floor):
    """Largest size in [floor, start] whose rendering of text fits max_width."""
    size = start
    while size > floor:
        font = ImageFont.truetype(path, size, index=index)
        if draw.textlength(text, font=font) <= max_width:
            return font
        size -= 2
    return ImageFont.truetype(path, floor, index=index)


def text_height(draw, font, text):
    """Height of the drawn glyphs (not the font's full line box)."""
    box = draw.textbbox((0, 0), text, font=font)
    return box[3] - box[1]


def render_icon(tile):
    """512x512 hi-res listing icon, RGB, flooded with the tile green."""
    scaled = tile.resize((ICON_SIZE, ICON_SIZE), Image.LANCZOS)
    canvas = Image.new("RGBA", (ICON_SIZE, ICON_SIZE), TILE_GREEN + (255,))
    canvas.alpha_composite(scaled)
    return canvas.convert("RGB")


def render_feature(tile, font_path, font_index):
    """1024x500 feature graphic, RGB: indigo field, icon tile, two-tone wordmark."""
    width, height = FEATURE_SIZE
    canvas = Image.new("RGBA", FEATURE_SIZE, INDIGO + (255,))

    mark_width = round(tile.width * MARK_HEIGHT / tile.height)
    mark = tile.resize((mark_width, MARK_HEIGHT), Image.LANCZOS)
    canvas.alpha_composite(mark, (PAD, (height - MARK_HEIGHT) // 2))

    draw = ImageDraw.Draw(canvas)
    text_left = PAD + mark_width + GAP
    available = width - text_left - PAD

    wordmark = WORDMARK_HEAD + WORDMARK_TAIL
    big = fit_font(draw, font_path, font_index, wordmark, available, start=96, floor=48)
    small = fit_font(draw, font_path, font_index, TAGLINE, available, start=34, floor=18)

    big_height = text_height(draw, big, wordmark)
    small_height = text_height(draw, small, TAGLINE)
    block = big_height + LINE_GAP + small_height
    top = (height - block) // 2

    # textbbox's y0 is the gap between the anchor and the first ink; subtract it
    # so the block is optically centered rather than centered on the line box.
    big_offset = draw.textbbox((0, 0), wordmark, font=big)[1]
    small_offset = draw.textbbox((0, 0), TAGLINE, font=small)[1]

    head_width = draw.textlength(WORDMARK_HEAD, font=big)
    draw.text((text_left, top - big_offset), WORDMARK_HEAD, font=big, fill=WHITE)
    draw.text((text_left + head_width, top - big_offset), WORDMARK_TAIL, font=big, fill=TILE_GREEN)
    draw.text(
        (text_left + 2, top + big_height + LINE_GAP - small_offset),
        TAGLINE,
        font=small,
        fill=TAGLINE_INK,
    )
    return canvas.convert("RGB"), big.size, small.size


def render():
    if not os.path.isfile(MASTER):
        sys.exit(
            "make-play-graphics: %s is missing; run tools/extract-icons.sh first"
            % os.path.relpath(MASTER, ROOT)
        )

    with Image.open(MASTER) as master:
        master = master.convert("RGBA")
        bbox = master.getbbox()
        tile = master.crop(bbox)
    print(
        "make-play-graphics: master %dx%d, opaque bounds %s -> tile %dx%d"
        % (master.width, master.height, bbox, tile.width, tile.height)
    )

    os.makedirs(OUT, exist_ok=True)

    icon = render_icon(tile)
    icon_path = os.path.join(OUT, ICON_NAME)
    icon.save(icon_path, format="PNG", optimize=True)
    print("  wrote %s (RGB %dx%d)" % (os.path.relpath(icon_path, ROOT), ICON_SIZE, ICON_SIZE))

    font_path, font_index = find_font()
    feature, big_size, small_size = render_feature(tile, font_path, font_index)
    feature_path = os.path.join(OUT, FEATURE_NAME)
    feature.save(feature_path, format="PNG", optimize=True)
    print(
        "  wrote %s (RGB %dx%d, %s wordmark %dpt tagline %dpt)"
        % (
            os.path.relpath(feature_path, ROOT),
            FEATURE_SIZE[0],
            FEATURE_SIZE[1],
            os.path.basename(font_path),
            big_size,
            small_size,
        )
    )


def verify():
    """Check both outputs without rewriting them. True when the set is shippable."""
    ok = True
    expected = {
        ICON_NAME: ((ICON_SIZE, ICON_SIZE), TILE_GREEN),
        FEATURE_NAME: (FEATURE_SIZE, INDIGO),
    }
    for name, (size, corner) in expected.items():
        path = os.path.join(OUT, name)
        rel = os.path.relpath(path, ROOT)
        if not os.path.isfile(path):
            print("MISSING %s" % rel, file=sys.stderr)
            ok = False
            continue
        good = True
        with Image.open(path) as im:
            mode, got_size = im.mode, im.size
            transparency = im.info.get("transparency")
            got_corner = im.convert("RGB").getpixel((0, 0))
            colors = im.convert("RGB").getcolors(1 << 20)
        if mode != "RGB" or transparency is not None:
            print(
                "BAD %s: Play rejects alpha; expected mode RGB with no transparency, got %s%s"
                % (rel, mode, " + transparency" if transparency is not None else ""),
                file=sys.stderr,
            )
            ok = good = False
        if got_size != size:
            print(
                "BAD %s: expected %dx%d, got %dx%d" % (rel, size[0], size[1], got_size[0], got_size[1]),
                file=sys.stderr,
            )
            ok = good = False
        if got_corner != corner:
            print(
                "BAD %s: top-left pixel %s, expected the flooded field %s"
                % (rel, got_corner, corner),
                file=sys.stderr,
            )
            ok = good = False
        if colors is not None and len(colors) < 8:
            print("BAD %s: only %d distinct colors; the render looks blank" % (rel, len(colors)), file=sys.stderr)
            ok = good = False
        if good:
            print("  ok %s %s %dx%d" % (rel, mode, got_size[0], got_size[1]))
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
            sys.exit("make-play-graphics: unknown argument '%s' (try --check)" % arg)
    if not check_only:
        render()
    if not verify():
        print("make-play-graphics: FAIL", file=sys.stderr)
        return 1
    print("make-play-graphics: OK")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
