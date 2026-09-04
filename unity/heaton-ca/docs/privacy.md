# HeatonCA Privacy Policy

*Effective September 2, 2026. Applies to HeatonCA 2.0 and later on iOS, iPadOS,
macOS, Windows, and Android, and to the browser (web) build.*

**HeatonCA does not collect, store, or transmit any personal information.**
There are no accounts, no analytics, no advertising, no crash reporting, and no
tracking of any kind. The app never connects to the internet on its own.

Everything the app shows is computed on your own device. The rules, the
gallery, the images, and the evolutionary search all run locally; nothing you
type or discover is uploaded anywhere.

## What stays on your device

HeatonCA saves three kinds of things, and only on the device you are using:

1. **Three preferences** — cell size, animation speed, and whether the
   FPS/Steps overlay is shown.
2. **Your evolve finds** — the list of rules your searches turned up, each with
   its score and run number. This is a short list of text; the thumbnails you
   see are recomputed from the rule each time, never stored.
3. **PNG images you export** — the pictures you choose to save with **Save
   PNG** in the Simulator or on the Finds page.

That is the complete list. There is no history, no telemetry, no identifier,
and no log sent anywhere.

Where those files live:

- **iPhone and iPad:** inside the app's own container. Saved PNGs also appear
  in the Files app under *On My iPhone* (or *On My iPad*) → *HeatonCA*, so you
  can open, share, or delete them yourself.
- **Mac:** in the app's Application Support folder — inside the app's sandbox
  container for the App Store version.
- **Windows:** under your `AppData\LocalLow` folder.
- **Android:** in the app's private storage, which no other app can read.

On iPhone, iPad, and Android, deleting the app deletes this data. **On Windows,
uninstalling HeatonCA leaves its `AppData\LocalLow\Jeff Heaton\HeatonCA` folder
on your computer** — delete that folder too if you want everything gone. On a
Mac, removing the App Store version removes its container; a directly
downloaded build leaves its Application Support folder behind. Your device's
own backups (iCloud or computer backups on iOS, for example) may include this
data; that is governed by your backup settings, not by the app.

## The web build

The version of HeatonCA that runs in a browser keeps the same three
preferences and the same finds list in your **browser's site data** (an
IndexedDB store belonging to the page's address). It never leaves your
browser, it is not an account, and it does not follow you to another browser,
another device, or a private window. Clearing the site's data — or browsing
privately — clears it. Saved PNGs go to your browser's normal downloads
location.

The page itself is served as static files. Whoever hosts it may keep ordinary
web-server logs, the same as any web page you visit; the app sends nothing of
its own.

## Permissions

HeatonCA asks for no camera, microphone, location, contacts, or notification
access.

The one prompt you may see is on **iPhone and iPad**: the first time you use
**Save PNG**, iOS asks for permission to add photos to your photo library. It
is used for exactly that — writing the image you just asked to save into a
"HeatonCA" album — and for nothing else. HeatonCA never *reads* your photo
library, and declining only means the image is saved inside the app (where the
Files app can still reach it) instead of in Photos. On **Android**, saving an
image may likewise ask for the permission the system requires to place a file
in your gallery, and only when you tap Save PNG.

## Things the app does only when you ask

- **Copy rule** puts the current rule code on the clipboard. HeatonCA never
  reads your clipboard.
- The **Tutorial**, **Manual**, **Privacy policy**, **Source code**, and
  **DOI** buttons on the About screen open a web page in your browser. Those
  visits are governed by the privacy policies of the sites involved
  (heatonresearch.com, GitHub, doi.org); HeatonCA sends no data along with
  them.

## Children

HeatonCA collects no data from anyone, including children.

## Third-party components

HeatonCA is built with the Unity engine, with every Unity service — analytics,
advertising, crash reporting, and engine diagnostics — switched off in the
project settings. The photo-library helper on iOS and Android is a
permissions-only wrapper around the operating system's own gallery API. No
third-party component in the app collects data. The licenses of the components
used are listed in the app under About → Third-party notices.

## Changes

Any change to this policy will be posted at this address with a new effective
date.

## Contact

Questions about this policy: [jeff@jeffheaton.com](mailto:jeff@jeffheaton.com),
or [open an issue](https://github.com/jeffheaton/mergelife/issues) on the
project's repository.

See also the [HeatonCA user manual](manual.md).
