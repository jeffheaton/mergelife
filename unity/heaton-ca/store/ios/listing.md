# App Store listing — HeatonCA for iPhone and iPad (2.0.0)

Text and answers to paste into App Store Connect for the **iOS** platform of
the existing app record:

- **App**: HeatonCA — Apple ID **6469583429**
  (`https://apps.apple.com/us/app/heatonca/id6469583429`)
- **Bundle ID**: `com.heatonresearch.heaton-ca` (shared with macOS)
- **Team**: 9KJKLKXJ5G
- **This is an UPDATE**, not a new app. The record already exists and already
  ships the macOS build of the PyQt HeatonCA; 2.0.0 replaces it with the Unity
  app and *adds the iOS platform* to the same record (Add Platform > iOS in
  App Store Connect, which starts an "iOS 2.0.0 Prepare for Submission" page
  alongside the macOS one).
- **Localization**: English (U.S.) only. Every field below is that
  localization; nothing here is translated.

Every field is annotated with App Store Connect's limit and the exact length
of the text as written. Counts were measured on the literal block contents
(no trailing newline). Re-measure after any edit:
`python3 -c "import sys;print(len(sys.stdin.read().rstrip('\n')))" < file`.

Companion documents: `../macos/listing.md` (the same record's macOS platform),
`../README.md` (screenshot plan and rules), `../android/listing.md` (Play),
`../../docs/release-runbook.md` (the order the uploads happen in).

---

## 1. App information (record-level, shared by iOS and macOS)

These live on the record, not on a version, so editing them changes both
platforms at once.

- **Name** — limit **30**, using **8**:

```
HeatonCA
```

  Do not rename. The record is already indexed under this name and a rename on
  an update invites a metadata review round trip.

- **Subtitle** — limit **30**, using **28**:

```
Full Color Cellular Automata
```

  This is what the live listing already carries (read from the public App
  Store page on 2026-09-02 — confirm in App Store Connect, which is the
  authority). It is accurate for 2.0 and costs nothing to keep. If Jeff wants
  it refreshed, `Evolve Color Cellular Automata` is 30 exactly.

- **Privacy Policy URL**:

```
https://github.com/jeffheaton/mergelife/blob/master/unity/heaton-ca/docs/privacy.md
```

  Same URL the app opens from About > Privacy policy (`AppLinks.PrivacyUrl`),
  so the two can never drift.

- **Category**: Primary `Entertainment`, Secondary `Education` (optional).
  The public listing page showed **Entertainment** on 2026-09-02 and there is
  no reason to move it — a category change is a listing-level change that
  re-enters review. Note that this is *not* the same field as the built app's
  `LSApplicationCategoryType`, which `ProjectSettings.asset`
  (`macAppStoreCategory`) sets to `public.app-category.utilities`; that one is
  a macOS Info.plist key and only matters for the Mac build. See the open item
  in `../macos/listing.md` if the two should be reconciled.

- **Content Rights**: "Does your app contain, show, or access third-party
  content?" → **No**. All art, text, and rules are Jeff's own; the
  open-source components (Unity, NativeGallery, the Apache-2.0 engine) are
  code dependencies, listed in the app's Third-party notices, not displayed
  third-party content.

- **Age Rating**: expect **4+**, which is what the live record already shows.
  Answer every questionnaire row **None / No**: no violence of any kind, no
  profanity or crude humor, no horror or fear themes, no sexual content or
  nudity, no alcohol/tobacco/drug references, no simulated gambling, no
  contests, no medical or treatment information. Also:
  - *Unrestricted Web Access* → **No**. The app has no in-app browser; About's
    Tutorial / Manual / Privacy / Source / DOI buttons hand the URL to Safari
    through `Application.OpenURL`.
  - *User-generated content, chat, or sharing with other users* → **No**.
  - *Made for Kids / Kids Category* → **No** (do not opt in; it drags in the
    Kids privacy track for no benefit).
  - *Gambling and contests, in-app purchases, advertising* → **No / None**.
  If Apple's questionnaire has changed shape since this was written, the rule
  is unchanged: everything is "None", and the result must stay 4+.

- **Pricing and availability**: Free, all territories, no in-app purchases.

---

## 2. iOS version information (2.0.0)

- **Promotional Text** — limit **170**, using **164**. Editable any time
  without submitting a new build, so this is the field to reuse for a "now on
  iPhone and iPad" push after launch:

```
Version 2.0 is a complete rebuild. HeatonCA now runs on iPhone and iPad as well as the Mac, with a rule decoder screen and a Finds gallery for the rules you evolve.
```

- **Description** — limit **4000**, using **2422**:

```
MergeLife is a continuous-color cellular automaton whose entire physics fits in sixteen bytes: a rule written as eight groups of four hexadecimal digits. Change one digit and you change the universe.

HeatonCA is the laboratory for those rules. It runs them, decodes them, and breeds new ones.

SIMULATOR
Type or paste any 32-digit rule and watch it run. Start, Stop, Step and Reset give frame-level control, Random rolls a fresh rule, and an optional overlay reports the generation count and frame rate. Cell size (1 to 25) and speed (1 to 60 generations per second) are yours to set, the lattice fills the screen at whole cells with no stretching, and rotating the device re-cuts the world instead of wiping it. Save PNG writes the current frame to your photo library.

RULE GALLERY
Thirty curated rules ship with the app, thirteen of them named and described: Red World from the paper, Beetle Meadow, Pen and Ink, Brushfire, High Noon, Lagoons, Frost, Lichen, Plankton, Neon Storm, Coral Bloom and more. Tap a tile and the rule opens in the simulator, already running.

RULE DECODER
Every rule is eight sub-rules. The decoder shows each one the way the paper does: the high threshold (alpha), the neighbor range it fires on, the key color it merges toward, the blend percentage (beta), the color index (gamma), and the raw octets behind them. It is the fastest way to see why a rule looks the way it does.

EVOLVE
The built-in genetic algorithm is the one from the paper: a population of one hundred rules scored by an aesthetic objective, tournament selection, crossover and digit-swap mutation, restarting whenever a run stops improving. Watch the run, evaluation, score and rate readouts, keep every rule that beats your score threshold in the Finds gallery, and open any find in the simulator or export it as a PNG. The search runs on your device and works it hard; expect it to warm the device and use battery while it runs.

THE PAPER
HeatonCA implements the algorithm described in Heaton, J. (2018), "Evolving continuous cellular automata for aesthetic objectives", Genetic Programming and Evolvable Machines, DOI 10.1007/s10710-018-9336-1. About links to the paper, the tutorial, the manual and the open-source repository.

NO STRINGS
No account, no ads, no in-app purchases, no analytics, and no network connection. HeatonCA collects nothing and runs entirely offline; everything you save stays on your device.
```

- **Keywords** — limit **100**, using **96** (4 to spare). Comma-separated, **no spaces**
  (a space costs a character and buys nothing). Words already in the app name
  and subtitle are indexed for free, so "cellular", "automata" and "color" are
  deliberately absent:

```
mergelife,emergence,generative,evolution,genetic,algorithm,simulator,pixel,rule,pattern,life,art
```

  Runners-up if a term needs swapping: `conway`, `sandbox`, `science`,
  `wallpaper`, `chaos`, `complexity`.

- **What's New in This Version** — limit **4000**, using **1665**. Required on
  every update:

```
HeatonCA 2.0 is a complete rebuild.

NOW ON IPHONE AND IPAD
HeatonCA runs on iPhone and iPad as well as the Mac, with layouts built for touch: full-screen views, a Back button on every screen, a gallery that re-columns for the device it is on, and support for every orientation.

ONE VERIFIED ENGINE
The simulator, the rule decoder and the genetic algorithm all run on one deterministic engine. Every launch replays the reference vectors published with the paper, so the same rule produces the same world on every device; About reports the result.

SIMULATOR
The lattice fills the screen at whole cells with no stretching, rotating or resizing re-cuts the world instead of wiping it, speed runs to 60 generations per second, presets and the thirty gallery rules are one tap away, and Save PNG exports the frame you are looking at.

RULE DECODER
The decoded rule table is now a screen of its own, opened from the simulator, showing the high threshold, range, key color, blend percentage, color index and the raw octets for each of a rule's eight sub-rules.

FINDS GALLERY
Evolve keeps every rule that beats your score threshold, with a thumbnail, its score and the run that found it. Open a find in the simulator, export it as a PNG, or delete it. The list survives quitting the app, and opening a find does not stop the search.

SETTINGS AND ABOUT
Cell size, animation speed and the FPS/steps overlay apply live and can be restored to their defaults. About carries the version and build, the 2018 paper and its DOI, the tutorial, the manual, the privacy policy, the source repository and the third-party notices.

Everything still runs offline and collects nothing.
```

- **Version**: `2.0.0` (matches `bundleVersion` in `ProjectSettings.asset`
  and the app's About screen).
- **Copyright**:

```
2026 Jeff Heaton
```

- **Support URL** (the manual):

```
https://github.com/jeffheaton/mergelife/blob/master/unity/heaton-ca/docs/manual.md
```

- **Marketing URL** (optional, the tutorial page):

```
https://www.heatonresearch.com/mergelife/
```

- **Routing App Coverage File**: none (not a maps app).
- **Version Release**: manual release, so the iOS and macOS builds of 2.0.0
  can be released together once both are approved.

---

## 3. App Privacy — Data Not Collected

Answer the App Privacy questionnaire (record-level, applies to both platforms)
as:

- "Do you or your third-party partners collect data from this app?" → **No**.
  That single answer produces the **Data Not Collected** label; no data types,
  no purposes, and no tracking questions follow.
- **Tracking**: none. The app does not use the Advertising Identifier
  (IDFA) — answer **No** to the IDFA question at upload time.
- This is true, not aspirational, and the build backs it up:
  - Unity engine telemetry is off in `ProjectSettings.asset`
    (`submitAnalytics 0`, `UnityConnectSettings.m_Enabled 0`,
    `m_EngineDiagnosticsEnabled 0`), so the player makes no `unity3d.com`
    calls at launch.
  - The app opens no sockets at all. The only network activity a user can
    cause is handing a URL to Safari from About.
  - The app-level `PrivacyInfo.xcprivacy` written by `Assets/Editor/iOSPostBuild.cs`
    declares `NSPrivacyTracking false`, an empty
    `NSPrivacyCollectedDataTypes`, and exactly four required-reason APIs:
    UserDefaults **CA92.1**, SystemBootTime **35F9.1**, DiskSpace **E174.1**,
    FileTimestamp **C617.1**. Undeclared uses are ITMS-91053, and Unity's own
    manifest declares only the file-timestamp reason — which is why the app
    ships its own.
- Keep `docs/privacy.md`, this label, and the Play Data safety form in
  `../android/listing.md` in agreement. Changing one without the others is how
  a listing becomes false.
- **Account deletion**: not applicable — the app has no accounts and no
  server, so the "account deletion" requirement does not apply.

---

## 4. Export compliance

- The app uses **no encryption beyond what is exempt**: it performs no
  cryptography of its own, opens no TLS connections of its own, and only hands
  URLs to the system browser.
- `Assets/Editor/iOSPostBuild.cs` sets **`ITSAppUsesNonExemptEncryption =
  false`** in the built `Info.plist` (verified in the macOS counterpart on
  2026-09-02), so App Store Connect does not ask the encryption questions per
  upload and no annual French self-classification report or CCATS is needed.
- If App Store Connect ever asks anyway (a stale plist, or a build produced
  outside `CIBuild`): "Does your app use encryption?" → **No**.

---

## 5. App Review information

- **Sign-in required**: No. Every feature is reachable with no account.
- **Contact**: Jeff Heaton, `jeff@jeffheaton.com` (phone number as held on the
  developer account).
- **Attachment**: none needed.
- **Notes** — limit **4000**, using **1146** (paste as-is; it heads off the
  questions this app actually attracts):

```
HeatonCA implements the cellular automata and genetic algorithm from Heaton, J. (2018), "Evolving continuous cellular automata for aesthetic objectives", Genetic Programming and Evolvable Machines (DOI 10.1007/s10710-018-9336-1). The author of the app is the author of the paper.

No account, no server, no network use: the app runs entirely offline and collects no data. The only outbound action is opening a link in Safari from the About screen.

Evolve runs a genetic algorithm on the device and is deliberately CPU-intensive while a search is running; that is the feature, not a defect. It suspends itself when the app goes to the background and resumes when it returns.

Save PNG writes the image to the photo library through the add-only permission (NSPhotoLibraryAddUsageDescription: "HeatonCA saves the PNG images you export to your photo library."), and also keeps a copy in the app's Documents folder, which is why UIFileSharingEnabled is set. The app never reads the photo library.

To reproduce the screenshots: Home > Gallery > tap any tile (the simulator starts), or Home > Evolve > Start to watch the search, then the Finds button.
```

---

## 6. Build number policy (iOS and macOS share ONE counter)

**This is the step that fails first if it is skipped.**

- App Store Connect record 6469583429 keeps **one build-number sequence for
  both platforms**. Uploading a build whose number was already used on
  *either* platform is rejected as **ITMS-90189 "Redundant Binary Upload"**.
- Nobody can guess the current value: the PyQt releases stamped
  `CFBundleVersion` from an environment variable, and
  `python/application/pyqt/deploy/macos/heaton-ca-macos.spec` set
  `CFBundleVersion` equal to `CFBundleShortVersionString`, so historical build
  numbers look like version strings (`1.1.0`, `1.2.0`), not counters.
- **Procedure, before the first 2.0.0 upload:**
  1. In App Store Connect, open the app > **TestFlight > All Builds** (and the
     macOS builds list) and read the **highest build number ever uploaded on
     either platform**, including expired and rejected builds.
  2. Choose a value strictly greater than it. A plain integer is the sane
     choice; if the highest existing value is a dotted string, pick an integer
     that Apple orders above it (Apple compares component by component, so
     `2` beats `1.2.0`) — when in doubt go higher, the number is free.
  3. Export it and build:
     ```bash
     export HEATONCA_BUILD_NUMBER=<that value>
     export HEATONCA_STORE=1     # store mode: CIBuild FAILS if the number is unset
     tools/unity-gate.sh build-ios
     ```
     `CIBuild` writes the same value to `PlayerSettings.iOS.buildNumber` and
     `PlayerSettings.macOS.buildNumber`, which is exactly the shared-counter
     rule expressed in code.
  4. The macOS upload of 2.0.0 must use a **different, higher** number than
     the iOS one (or vice versa). Ship one platform, bump by one, ship the
     other. `../macos/listing.md` says the same thing from the other side.
  5. Never commit a build number. It lives in the environment; the repo's
     checked-in `buildNumber` is `2.0.0` for both Apple targets, and dev builds
     log `DEV BUILD NUMBER`. A **store** build does write the real number into
     `ProjectSettings/ProjectSettings.asset`, because `ApplyAppleBuildNumber`
     assigns `PlayerSettings.{macOS,iOS}.buildNumber` and Unity serializes it --
     `git checkout --` that file afterwards. The generated Xcode project already
     carries the number, so reverting cannot affect an archive.
- The version string (`2.0.0`, from `bundleVersion`) is shared and stays the
  same for both platforms.

---

## 7. Screenshots

Sizes, scene list, capture commands and the no-alpha rule are in
`../README.md`. For this platform App Store Connect needs:

- **iPhone 6.9-inch** — `iphone-6.9/`, 1320x2868 portrait, up to 10, at least 1.
- **iPad 13-inch** — `ipad-13/`, 2064x2752 portrait, up to 10, at least 1.
  Required because the app supports iPad; an iPad set of upscaled iPhone shots
  is a rejection.
- App Store Connect scales the 6.9-inch set down for the smaller iPhone
  display sizes, so only these two sets have to exist.
- App preview videos: none.
- **Re-shoot for 2.0.** The screenshots currently on the record are the PyQt
  Mac app and show a user interface this build no longer has.

---

## 8. Submission checklist

1. `HEATONCA_BUILD_NUMBER` read from App Store Connect and exceeded (section 6).
2. `HEATONCA_STORE=1 tools/unity-gate.sh build-ios`, then Xcode:
   Product > Archive > Distribute App > App Store Connect.
3. Confirm on the archived build: About reads `Version 2.0.0 (build N)` with
   the N you just used, a `Built` stamp newer than the last commit, and
   `Determinism self-check: PASS`.
4. Paste sections 1, 2 (iOS version), 3, 4 and 5; upload the screenshot sets
   from section 7.
5. Answer the IDFA question (**No**) at upload.
6. Submit for review; hold the release until macOS 2.0.0 is approved too.
7. After release: update `/binaries.md` and the App Store badge/links.

## Open items (Jeff only)

- ~~The highest build number ever uploaded on either platform (section 6).~~
  **Resolved 2026-09-06.** TestFlight > iOS Builds was empty; TestFlight >
  macOS Builds held 1.1.0 (build `1.1.0`, Expired), 1.0.1, 1.0.0 and 0.0.1, so
  the highest ever was the dotted string `1.1.0` -- confirming this section's
  prediction that the PyQt builds stamped `CFBundleVersion` from the version
  string. **iOS 2.0.0 builds as `10`; macOS 2.0.0 takes `11`.** The counter is
  now plain integers; the next release continues from `12`.
- Confirm the record's live subtitle and primary category in App Store
  Connect. The values in section 1 were read from the public App Store page on
  2026-09-02, which also showed version **1.1.0** while this repo's PyQt app
  is 1.2.0 — so either 1.2.0 was never shipped to iOS/macOS or the page was
  stale. Neither changes what 2.0.0 needs, but it is worth knowing before the
  build-number hunt.
- Whether the tutorial page (`heatonresearch.com/mergelife/`,
  the Marketing URL and About > Tutorial) gets refreshed for 2.0.
