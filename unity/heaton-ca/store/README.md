# HeatonCA store assets

Everything the stores need that is not the binary: listing text, graphics, and
the screenshot sets. This file is the **screenshot plan** — what to shoot, at
what size, on which platform, and the rules a shot has to satisfy before it is
committed.

## What lives here

```
store/
  README.md                      this file: the screenshot plan and rules
  ios/listing.md                 App Store Connect text and answers, iOS platform
  ios/iphone-6.9/NN-*.png        iPhone 6.9-inch screenshots, 1320x2868
  ios/ipad-13/NN-*.png           iPad 13-inch screenshots, 2064x2752
  macos/listing.md               App Store Connect text and answers, macOS platform
  macos/NN-*.png                 Mac screenshots, 2560x1600
  android/listing.md             Google Play Console text and declarations
  android/graphics/              icon-512.png, feature-graphic-1024x500.png
  android/phone/NN-*.png         Play phone screenshots, 1080x1920 (max 8)
  android/tablet-10/NN-*.png     Play tablet screenshots, 2560x1600
  webgl/README.md                hosting notes for the web build
```

The listings are the source of truth for store text; this file is the source
of truth for the pictures. Nothing here is built by the app or by CI — the
screenshots are shot by hand from finished builds and committed.

## Required sizes

| Set | Directory | Pixels | Orientation | Count | Shot on |
|---|---|---|---|---|---|
| iPhone 6.9-inch | `ios/iphone-6.9/` | **1320x2868** | portrait | 1-10 (ship 8) | iOS Simulator, iPhone 17 Pro Max |
| iPad 13-inch | `ios/ipad-13/` | **2064x2752** | portrait | 1-10 (ship 8) | iOS Simulator, iPad Pro 13-inch (M4) |
| Mac | `macos/` | **2560x1600** | landscape | 1-10 (ship 8) | macOS player, windowed on a Retina display |
| Play phone | `android/phone/` | **1080x1920** | portrait | 2-8 (**max 8**) | `Play_Phone` AVD |
| Play tablet | `android/tablet-10/` | **2560x1600** | landscape | 2-8 | `Play_Tablet` AVD |

Notes on the sizes:

- App Store Connect scales the **6.9-inch** iPhone set down for every smaller
  iPhone display size, so that one set covers all iPhones. The **13-inch**
  iPad set is required on its own because the app supports iPad; upscaled
  iPhone shots in the iPad slot are a rejection.
- The Mac App Store also accepts 1280x800, 1440x900 and 2880x1800. Pick one
  size for the whole set; 2560x1600 is the 1280x800 window captured on a
  Retina display, which is what the sibling Heaton Life listing ships.
- The two Android sizes are exactly what the committed AVDs produce:
  `Play_Phone` is 1080x1920 at density 420, `Play_Tablet` is 2560x1600 at
  density 276 (`~/.android/avd/*/config.ini`), so `adb exec-out screencap`
  needs no resizing. Play accepts the tablet set for both the 7-inch and
  10-inch slots.
- Windows ships as a zip from a GitHub release and the WebGL build is
  self-hosted; neither has a store listing, so neither needs a screenshot set.
  (If a Windows set is ever wanted, Partner Center's floor is 1366x768.)

## Rules every screenshot must satisfy

1. **No alpha channel.** Apple rejects screenshots that carry one, and both
   the iOS Simulator and macOS `screencapture` produce RGBA PNGs. Strip it —
   see "Stripping alpha" below — and verify with `sips -g hasAlpha`. Strip it
   on the Android sets too, for consistency.
2. **Shot from the real build of the version being submitted** — the 2.0.0
   store builds, not an Editor Play Mode window and not a development build.
   The Home screen's version badge reads `v2.0.0` and the About screen reads
   `Version 2.0.0 (build N)`; if the About shot shows a build number, shoot it
   *after* the final `HEATONCA_BUILD_NUMBER` is set, or the picture and the
   binary disagree.
3. **Exact pixel size for the slot.** No resizing, no cropping to fit, no
   letterboxing.
4. **The app only.** No device frames, no added marketing text or drop
   shadows, no desktop, no other windows, no cursor over a control, no
   notification banners. Straight captures of what the app draws.
5. **One orientation per set** (portrait for phone and iPad, landscape for
   Mac and Play tablet). Do not mix.
6. **No personal data** — the app has none to leak, but check the Mac shots
   for a visible user name in a revealed Finder window.
7. **Determinism note:** the self-check line on the About shot must read
   `Determinism self-check: PASS`. A `FAIL` in a store screenshot would be
   both a bad look and a real bug.

## The scene list

Eight scenes, numbered in **upload order** — App Store Connect and Play both
show the first few without any swiping, so the color goes first and the
housekeeping screens go last. Keep the file numbers stable across sets so the
same number means the same scene everywhere.

| # | File name | Screen | How to get there | What the shot must show |
|---|---|---|---|---|
| 01 | `01-simulator-red-world.png` | Simulator, running | Home > Gallery > tap/click the first tile (`e542-…`, "Red World (paper)") | The lattice filling the canvas, the toolbar, and the `Steps: n, FPS: n` overlay. Let it run a few hundred generations first so the world is developed, not fresh soup. |
| 02 | `02-rule-gallery.png` | Gallery | Home > Gallery | A full page of the 30 tiles, scrolled to the top, hex captions and the named subtitles legible. |
| 03 | `03-evolve-running.png` | Evolve, mid-search | Home > Evolve > Start | Status `Running...`, a non-zero Run Number, Eval Number and Current Score, the 96x96 best-rule preview populated, and the threshold slider. Let it run past 60 s so `Evals/min:` is not 0 (it is recomputed once a minute and reads 0 for the first one). |
| 04 | `04-finds-gallery.png` | Finds | Evolve > Finds | At least four or five find cards with thumbnails, scores and run numbers. To fill it quickly, drop the threshold slider to about 1.5 before starting the search, then let it run; the cards render their own thumbnails, so give it a few seconds after opening the page. |
| 05 | `05-rule-decoder.png` | Rule decoder | Simulator > Rule | All eight sub-rule rows with the Greek headers (`High (α)`, `Percent (β)`, `Index (γ)`) and the color swatches. Table layout on iPad/Mac/tablet, cards on phones. Verify on a real iPhone once that the Greek headers render rather than falling back to `alpha/beta/gamma`. |
| 06 | `06-home.png` | Home | app launch | The title art, the `v2.0.0` badge, and the five buttons. Shoot it while a search is *not* running so the "Evolving..." chip is absent. |
| 07 | `07-settings.png` | Settings | Home > Settings | Cell size, animation speed, the FPS/steps toggle, and Save / Cancel / Restore Defaults. |
| 08 | `08-about.png` | About | Home > About | Version and build, the copyright line, the 2018 paper citation with the DOI button, the Tutorial / Manual / Privacy / Source buttons, and `Determinism self-check: PASS`. |

Per-set guidance:

- **All five sets shoot all eight scenes.** Play's phone slot caps at 8, which
  is exactly the list; if a ninth shot is ever wanted there, drop `07-settings`
  first.
- **Cell size** is what makes or breaks scenes 01 and 04. The default 5 is
  right on a phone; on the Mac and the Android tablet, raise it in Settings
  (7-9) before shooting so the cells read as structure rather than noise, then
  restore the default afterward. The value is a user setting, not a code
  change — see `docs/manual.md`.
- **Rule choice** for scene 01: the default `e542-5f79-9341-f31e-6c6b-7f08-8773-7068`
  ("Red World (paper)") is the paper's own rule and the honest lead. Good
  alternates from the gallery if a set needs a second simulator shot:
  `ea44-…` ("Beetle Meadow"), `6007-…` ("Coral Bloom"), `cb97-…` ("Neon
  Storm").
- The **iPad and Mac** sets show the wide layouts (decoder as a table, gallery
  at more columns); the **phone** sets show the stacked ones. That contrast is
  the point of having both — do not shoot the phone sets from a tablet.
- **Scene 03 on the Play phone** cannot show everything. At 1080x1920 / density
  420 the Evolve page is 731 dp tall and its content overflows by about 160 dp,
  so the threshold slider and the Finds button sit below the fold while the
  preview and the counters are on screen. Shoot it scrolled to the top: the
  preview tile, Run/Eval/Evals-per-minute, the rule, the score and
  `Status: Running...` all read, and the slider is the one item that gives way.
  Every other set — the two tablets included — fits the whole page, slider and
  all, so this concession is the Play phone's alone.

## Capturing

Every command below assumes the repository root's `unity/heaton-ca` as the
working directory. **Run them with the Claude Code Bash sandbox disabled** —
simulator, emulator and window capture all need IPC the sandbox blocks.

### iPhone 6.9-inch and iPad 13-inch (iOS Simulator)

The simulator build is the one the determinism gate already uses.

```bash
tools/unity-gate.sh build-ios-sim
# boot the device you want and leave it up:
SIM_DEVICE="iPhone 17 Pro Max" KEEP_SIM=1 tools/ios-sim-selfcheck.sh
# ...drive the app by hand in Simulator.app, then per shot:
xcrun simctl io booted screenshot --type=png store/ios/iphone-6.9/01-simulator-red-world.png
```

If more than one simulator is booted, pass the UDID (`xcrun simctl list
devices booted`) instead of `booted`, exactly as `ios-sim-selfcheck.sh` does,
so an iPad that happens to be up cannot receive the capture.

Repeat with `SIM_DEVICE="iPad Pro 13-inch (M4)"` for `store/ios/ipad-13/`.
Both device types are installed on this Mac
(`/Library/Developer/CoreSimulator/Profiles/DeviceTypes/`).

`ios-sim-selfcheck.sh` boots the simulator **headless** (no window) and only
proves the build runs; `--keep-booted` leaves it up so it can do the build and
install for you, but to drive the app you need the window: `open -a Simulator`
after it finishes. `simctl io screenshot` writes the framebuffer at native
size — 1320x2868 and 2064x2752 — with an alpha channel to strip. Only the
framebuffer is captured, so the simulator's own bezel never appears in the
file. Keep the device in portrait (`Device > Rotate` back if you rotated it).

### Mac (2560x1600)

```bash
tools/unity-gate.sh build-macos
open build/macos/HeatonCA.app --args -screen-width 1280 -screen-height 800 -screen-fullscreen 0
```

Move the window fully onto the **built-in Retina display** (an external 1x
monitor captures at 1x and yields 1280x800), then capture the content area in
points; on a 2x display the file comes back at 2560x1600:

```bash
# interactive: space, then click the window; -o drops the shadow
screencapture -o -x -w store/macos/01-simulator-red-world.png
# or by rectangle, in points: -R<x>,<y>,<w>,<h>
screencapture -o -x -R100,100,1280,800 store/macos/01-simulator-red-world.png
sips -g pixelWidth -g pixelHeight store/macos/01-simulator-red-world.png   # expect 2560 x 1600
```

`-w` captures the whole window including the title bar, so the file comes out
taller than 1600 px; either crop the bar off or use `-R` over the content
rect. If the capture comes back 1280x800, the window was on a 1x display —
move it and re-shoot.

### Play phone and tablet (Android emulator)

```bash
tools/unity-gate.sh build-android
~/Library/Android/sdk/emulator/emulator -avd Play_Phone &      # or Play_Tablet
adb install -r build/android/HeatonCA.apk
adb shell monkey -p com.heatonresearch.heatonca -c android.intent.category.LAUNCHER 1
# ...drive the app, then per shot:
adb exec-out screencap -p > store/android/phone/01-simulator-red-world.png
```

The Play sets come from the `.apk` of the same commit as the submitted
`.aab` — an AAB cannot be installed on a device, and the two are built from
identical player content. `Play_Phone` captures at 1080x1920 and `Play_Tablet`
at 2560x1600 with no resizing needed. The tablet AVD boots landscape; leave it there. On first
launch dismiss Android's one-time "Viewing full screen" education panel before
shooting — it steals focus, and under `runInBackground 0` it pauses the
player. Launch the emulator yourself as above rather than through
`tools/android-selfcheck.sh`: that script boots it with `-no-window`, which is
right for the gate and useless for driving the app by hand (it does accept
`AVD=Play_Tablet` and `--keep-emulator` if you want it to do the install).

## Stripping alpha and verifying

Strip alpha by compositing over black, so the rounded-corner pixels an iOS
capture leaves transparent become opaque black rather than white
(ImageMagick is at `/opt/homebrew/bin/magick`):

```bash
magick in.png -background black -alpha remove -alpha off out.png
```

Pillow does the same without ImageMagick (it drops the channel rather than
compositing, which is equivalent when nothing is actually translucent):

```bash
python3 -c "from PIL import Image;import sys;Image.open(sys.argv[1]).convert('RGB').save(sys.argv[1])" out.png
```

`sips` alone cannot remove an alpha channel; do not try. Verify the whole tree
before committing — this checks every set's size, mode and count at once:

```bash
python3 - <<'PY'
from PIL import Image
import glob, sys
want = {
    "store/ios/iphone-6.9": (1320, 2868, 10),
    "store/ios/ipad-13":    (2064, 2752, 10),
    "store/macos":          (2560, 1600, 10),
    "store/android/phone":  (1080, 1920, 8),
    "store/android/tablet-10": (2560, 1600, 8),
}
bad = 0
for d, (w, h, cap) in want.items():
    files = sorted(glob.glob(d + "/*.png"))
    if len(files) > cap:
        print(f"{d}: {len(files)} files, store maximum is {cap}"); bad += 1
    for f in files:
        im = Image.open(f)
        if im.size != (w, h) or im.mode != "RGB":
            print(f"{f}: {im.size} {im.mode}, want ({w}, {h}) RGB"); bad += 1
print("OK" if not bad else f"{bad} problem(s)")
sys.exit(1 if bad else 0)
PY
```

`mode == "RGB"` is the alpha check: `RGBA` or `P` with transparency fails.

## After shooting

1. Commit the sets with the listing they belong to; the store text and the
   pictures should land together.
2. Upload: `ios/iphone-6.9` and `ios/ipad-13` to the iOS 2.0.0 page,
   `macos/*.png` to the macOS 2.0.0 page (both under App Store Connect app id
   **6469583429**), `android/phone` and `android/tablet-10` to the Play
   listing for `com.heatonresearch.heatonca`.
3. The App Store screenshots currently on the record are of the PyQt 1.x app.
   Replace all of them; a 2.0 listing showing the old user interface is a
   review comment waiting to happen.
