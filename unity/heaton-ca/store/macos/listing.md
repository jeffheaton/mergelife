# Mac App Store listing — HeatonCA for macOS (2.0.0)

Text and answers to paste into App Store Connect for the **macOS** platform of
the existing app record:

- **App**: HeatonCA — Apple ID **6469583429**
  (`https://apps.apple.com/us/app/heatonca/id6469583429`)
- **Bundle ID**: `com.heatonresearch.heaton-ca` (shared with iOS)
- **Team**: 9KJKLKXJ5G
- **This is an UPDATE.** The live Mac app is the PyQt HeatonCA (1.x); 2.0.0
  replaces it with the Unity build. The record-level fields (name, subtitle,
  privacy policy URL, category, age rating, App Privacy) are **shared with
  iOS** — edit them once, in `../ios/listing.md`'s section 1, and they change
  here too. Everything below is either that shared set restated for
  convenience or genuinely macOS-only.
- **Minimum macOS**: 12.0 (`macOSTargetOSVersion` in `ProjectSettings.asset`).
  Apple Silicon and Intel: the player is built `OSXUniversal`.
- **Localization**: English (U.S.) only.

Every field carries App Store Connect's limit and the exact length of the text
as written. Re-measure after any edit:
`python3 -c "import sys;print(len(sys.stdin.read().rstrip('\n')))" < file`.

Companion documents: `../ios/listing.md` (same record, iOS platform),
`../README.md` (screenshot plan and rules), `../../Packaging/` (the packaging
script and entitlements), `../../docs/release-runbook.md`.

---

## 1. Record-level fields (shared with iOS — see ../ios/listing.md §1)

- **Name** — limit **30**, using **8**:

```
HeatonCA
```

- **Subtitle** — limit **30**, using **28**:

```
Full Color Cellular Automata
```

- **Privacy Policy URL**:

```
https://github.com/jeffheaton/mergelife/blob/master/unity/heaton-ca/docs/privacy.md
```

- **Category**: Primary `Entertainment` (what the live listing showed on
  2026-09-02), Secondary `Education` (optional).
  **Note the second category field that only exists on macOS**: the built
  app's `Info.plist` `LSApplicationCategoryType` comes from
  `ProjectSettings.asset`'s `macAppStoreCategory`, currently
  `public.app-category.utilities`. That key drives where the app files itself
  on the system, not the store listing, and Apple has shipped mismatched pairs
  for years — but if Jeff wants them to agree, change the Info.plist side
  (`public.app-category.entertainment` in `ProjectIdentity`), not the listing,
  because a listing-category change re-enters review. Open item below.
- **Content Rights**: no third-party content.
- **Age Rating**: 4+, every questionnaire row "None"; no unrestricted web
  access (About's links open in the default browser, there is no in-app
  browser); not made for kids. Full answers in `../ios/listing.md` §1.
- **Pricing and availability**: Free, all territories, no in-app purchases.
  Because both platforms live on one record, a download is a universal
  purchase: the same listing serves Mac, iPhone and iPad.

---

## 2. macOS version information (2.0.0)

App Store Connect keeps **separate** promotional text, description, keywords,
what's-new, screenshots and support URL per platform, so these are macOS's own
copies. They are deliberately close to the iOS text, with the touch language
replaced.

- **Promotional Text** — limit **170**, using **165**. Editable without a new
  build:

```
Version 2.0 is a complete rebuild for the Mac, and the same app now runs on iPhone and iPad, with a rule decoder screen and a Finds gallery for the rules you evolve.
```

- **Description** — limit **4000**, using **2399**:

```
MergeLife is a continuous-color cellular automaton whose entire physics fits in sixteen bytes: a rule written as eight groups of four hexadecimal digits. Change one digit and you change the universe.

HeatonCA is the laboratory for those rules. It runs them, decodes them, and breeds new ones.

SIMULATOR
Type or paste any 32-digit rule and watch it run. Start, Stop, Step and Reset give frame-level control, Random rolls a fresh rule, and an optional overlay reports the generation count and frame rate. Cell size (1 to 25) and speed (1 to 60 generations per second) are yours to set, the lattice fills the window at whole cells with no stretching, and resizing the window re-cuts the world instead of wiping it. Save PNG writes the current frame to disk and reveals it in the Finder.

RULE GALLERY
Thirty curated rules ship with the app, thirteen of them named and described: Red World from the paper, Beetle Meadow, Pen and Ink, Brushfire, High Noon, Lagoons, Frost, Lichen, Plankton, Neon Storm, Coral Bloom and more. Click a tile and the rule opens in the simulator, already running.

RULE DECODER
Every rule is eight sub-rules. The decoder shows each one the way the paper does: the high threshold (alpha), the neighbor range it fires on, the key color it merges toward, the blend percentage (beta), the color index (gamma), and the raw octets behind them. It is the fastest way to see why a rule looks the way it does.

EVOLVE
The built-in genetic algorithm is the one from the paper: a population of one hundred rules scored by an aesthetic objective, tournament selection, crossover and digit-swap mutation, restarting whenever a run stops improving. Watch the run, evaluation, score and rate readouts, keep every rule that beats your score threshold in the Finds gallery, and open any find in the simulator or export it as a PNG. The search uses several cores, and opening a find does not interrupt it.

THE PAPER
HeatonCA implements the algorithm described in Heaton, J. (2018), "Evolving continuous cellular automata for aesthetic objectives", Genetic Programming and Evolvable Machines, DOI 10.1007/s10710-018-9336-1. About links to the paper, the tutorial, the manual and the open-source repository.

NO STRINGS
No account, no ads, no in-app purchases, no analytics, and no network connection. HeatonCA collects nothing and runs entirely offline; everything you save stays on your Mac.
```

- **Keywords** — limit **100**, using **96** (4 to spare). Comma-separated,
  **no spaces**; words already in the name and subtitle are indexed for free,
  so "cellular", "automata" and "color" are deliberately absent:

```
mergelife,emergence,generative,evolution,genetic,algorithm,simulator,pixel,rule,pattern,life,art
```

- **What's New in This Version** — limit **4000**, using **1605**. Required on
  every update:

```
HeatonCA 2.0 is a complete rebuild.

REBUILT FOR THE MAC, AND NOW ON IPHONE AND IPAD
The Mac app has been rewritten from the ground up, and the same app now runs on iPhone and iPad from this listing.

ONE VERIFIED ENGINE
The simulator, the rule decoder and the genetic algorithm all run on one deterministic engine. Every launch replays the reference vectors published with the paper, so the same rule produces the same world on every device; About reports the result.

SIMULATOR
The lattice fills the window at whole cells with no stretching, resizing re-cuts the world instead of wiping it, speed runs to 60 generations per second, presets and the thirty gallery rules are one click away, and Save PNG exports the frame you are looking at and reveals it in the Finder.

RULE DECODER
The decoded rule table is now a screen of its own, opened from the simulator, showing the high threshold, range, key color, blend percentage, color index and the raw octets for each of a rule's eight sub-rules.

FINDS GALLERY
Evolve keeps every rule that beats your score threshold, with a thumbnail, its score and the run that found it. Open a find in the simulator, export it as a PNG, or delete it. The list survives quitting the app, and opening a find does not stop the search.

SETTINGS AND ABOUT
Cell size, animation speed and the FPS/steps overlay apply live and can be restored to their defaults. About carries the version and build, the 2018 paper and its DOI, the tutorial, the manual, the privacy policy, the source repository and the third-party notices.

Everything still runs offline and collects nothing.
```

- **Version**: `2.0.0` (from `bundleVersion`; the same string the iOS platform
  uses).
- **Copyright**:

```
2026 Jeff Heaton
```

  The built app's `NSHumanReadableCopyright` is `© 2018-2026 Jeff Heaton`,
  written by `Assets/Editor/macOSPostBuild.cs`. App Store Connect's Copyright
  field takes the plain form above.

- **Support URL** (the manual):

```
https://github.com/jeffheaton/mergelife/blob/master/unity/heaton-ca/docs/manual.md
```

- **Marketing URL** (optional, the tutorial page):

```
https://www.heatonresearch.com/mergelife/
```

- **Version Release**: manual, so macOS and iOS 2.0.0 can go live together.

---

## 3. App Privacy — Data Not Collected

Record-level and therefore identical to iOS: answer "Do you or your
third-party partners collect data from this app?" with **No**, which yields
the **Data Not Collected** label with no data types and no tracking. The macOS
build backs it up the same way, with one extra step worth knowing:

- `Assets/Editor/macOSPostBuild.cs` **overwrites**
  `Contents/Resources/PrivacyInfo.xcprivacy` in the built app. Unity's own
  manifest (merged with the Insights module's) declares six *collected* data
  types and no required-reason APIs — it would contradict the Data Not
  Collected label and trip ITMS-91053 at the same time. The replacement
  declares `NSPrivacyTracking false`, no collected types, and the four
  required-reason APIs the player actually uses: UserDefaults **CA92.1**,
  SystemBootTime **35F9.1**, DiskSpace **E174.1**, FileTimestamp **C617.1**.
  Verified on the 2026-09-02 macOS build: tracking false, 0 collected types,
  those four reasons.
- Unity engine telemetry is off (`submitAnalytics 0`,
  `UnityConnectSettings.m_Enabled 0`, `m_EngineDiagnosticsEnabled 0`), which is
  what makes "never connects on its own" literally true.
- Keep this label, `docs/privacy.md`, and the Play Data safety form in
  `../android/listing.md` in agreement.

---

## 4. Export compliance

- **No encryption beyond exempt.** The app performs no cryptography and opens
  no connections of its own.
- `Assets/Editor/macOSPostBuild.cs` sets **`ITSAppUsesNonExemptEncryption =
  false`** in `Contents/Info.plist` (verified on the 2026-09-02 build), so App
  Store Connect skips the encryption questions and no French self-declaration
  or CCATS is needed. If it asks: "Does your app use encryption?" → **No**.

---

## 5. App Review information

- **Sign-in required**: No.
- **Contact**: Jeff Heaton, `jeff@jeffheaton.com`.
- **Notes** — limit **4000**, using **1165** (paste as-is):

```
HeatonCA implements the cellular automata and genetic algorithm from Heaton, J. (2018), "Evolving continuous cellular automata for aesthetic objectives", Genetic Programming and Evolvable Machines (DOI 10.1007/s10710-018-9336-1). The author of the app is the author of the paper.

No account, no server, no network use: the app runs entirely offline and collects no data. The only outbound action is opening a link in the default browser from the About screen.

The app is sandboxed. Save PNG writes into the app's own container (~/Library/Containers/com.heatonresearch.heaton-ca/Data/Library/Application Support/com.heatonresearch.heaton-ca/Snapshots) and opens the Finder on it; the app never asks for access to files outside its container. The entitlements are app-sandbox plus allow-jit and allow-unsigned-executable-memory, which the Mono scripting backend requires.

Evolve runs a genetic algorithm on the machine and is deliberately CPU-intensive while a search is running; that is the feature, not a defect.

To reproduce the screenshots: Gallery, then click any tile (the simulator starts), or Evolve, then Start to watch the search, then the Finds button.
```

---

## 6. Build number policy (iOS and macOS share ONE counter)

Identical rule to `../ios/listing.md` §6, restated because this is where a Mac
upload dies:

- Record 6469583429 keeps **one build-number sequence across both
  platforms**. A number already used on either platform is rejected as
  **ITMS-90189 "Redundant Binary Upload"**.
- The current highest value is only visible in App Store Connect. The PyQt
  releases stamped `CFBundleVersion` from an environment variable, and
  `python/application/pyqt/deploy/macos/heaton-ca-macos.spec` set it equal to
  `CFBundleShortVersionString`, so the historical numbers look like version
  strings (`1.1.0`, `1.2.0`) rather than counters. Read the real value; never
  guess it.
- Procedure:
  ```bash
  export HEATONCA_BUILD_NUMBER=<higher than the highest ever uploaded, either platform>
  export HEATONCA_STORE=1          # store mode: CIBuild FAILS if the number is unset
  tools/unity-gate.sh build-macos
  Packaging/package-macos-appstore.sh sandbox-test   # always first
  Packaging/package-macos-appstore.sh store
  ```
  `CIBuild` writes the value to `PlayerSettings.macOS.buildNumber` **and**
  `PlayerSettings.iOS.buildNumber` — the shared counter expressed in code.
- **The iOS upload of 2.0.0 needs a different, higher number.** Ship one
  platform, bump by one, ship the other.
- Never commit a build number; the repo's value is 1, and a dev build logs
  `DEV BUILD NUMBER`.

---

## 7. Packaging and upload (macOS only)

The Mac binary does not come out of Unity ready to submit. In order:

1. `HEATONCA_STORE=1 HEATONCA_BUILD_NUMBER=N tools/unity-gate.sh build-macos`
   → `build/macos/HeatonCA.app` (ad-hoc signed by `macOSPostBuild`, which is
   for launching locally and is **not submittable**).
2. `Packaging/package-macos-appstore.sh sandbox-test` — ad-hoc signature plus
   the sandbox, no certificates. Run this **first**, every time: it proves the
   app still works sandboxed before a submission finds out for you. Launch it,
   confirm `SELF-CHECK PASS` in the container's `Player.log`, and confirm Save
   PNG reveals the file in the Finder.
3. `Packaging/package-macos-appstore.sh store` — signs with `Apple
   Distribution`, embeds
   `Packaging/HeatonCA-MacAppStore.provisionprofile` as
   `Contents/embedded.provisionprofile`, and builds the `.pkg` with `3rd Party
   Mac Developer Installer`. List both certificates with `security
   find-identity -v` (`-p codesigning` hides installer certs).
4. Upload the `.pkg` with **Transporter**, wait for processing, then pick the
   build on the macOS 2.0.0 page.
5. On the archived build confirm About reads `Version 2.0.0 (build N)`, a
   `Built` stamp newer than the release commit, and `Determinism self-check:
   PASS`.

Entitlements are `Packaging/HeatonCA.entitlements`: `app-sandbox`,
`allow-jit`, `allow-unsigned-executable-memory` (both JIT entitlements are
required by the Mono backend), and nothing else — no user-selected file
access, because Save PNG stays inside the container.

---

## 8. Screenshots

Sizes, scene list, capture commands and the no-alpha rule are in
`../README.md`. For macOS App Store Connect needs:

- **Mac** — `store/macos/*.png`, **2560x1600** (Apple also accepts 1280x800,
  1440x900 and 2880x1800; pick one size and stay in it), 1 to 10 shots, no
  alpha channel.
- Shoot the Mac player windowed, not full screen, and without other apps or
  the desktop visible.
- App preview videos: none.
- **Re-shoot for 2.0.** The screenshots on the record are the PyQt app.

---

## 9. Submission checklist

1. `HEATONCA_BUILD_NUMBER` read from App Store Connect and exceeded (§6).
2. Build, `sandbox-test`, `store`, Transporter (§7).
3. Paste §1, §2 (macOS version), §3, §4, §5; upload the Mac screenshot set.
4. Answer the IDFA question (**No**) at upload.
5. Submit; hold the release until iOS 2.0.0 is approved as well.
6. After release: update `/binaries.md`.

## Open items (Jeff only)

- The highest build number ever uploaded on either platform (§6).
- A current Mac App Store provisioning profile for
  `9KJKLKXJ5G.com.heatonresearch.heaton-ca` at
  `Packaging/HeatonCA-MacAppStore.provisionprofile`. Confirm the App ID is
  clean after the 2026-08-19 heaton-ca / heaton-life split.
- Confirm the live subtitle and primary category in App Store Connect (the
  values here were read from the public App Store page on 2026-09-02, which
  also showed version 1.1.0 while this repo's PyQt app is 1.2.0), and decide
  whether `LSApplicationCategoryType` should be moved from
  `public.app-category.utilities` to match the listing category.
