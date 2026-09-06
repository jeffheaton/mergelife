# Google Play listing — HeatonCA

Everything Play Console asks for, ready to paste. The graphics beside this file
regenerate with `python3 Packaging/android/make-play-graphics.py` (add `--check`
to verify them without rewriting). Keep this file, `docs/privacy.md`, and the
iOS/macOS "Data Not Collected" labels in agreement: a Data safety answer that
contradicts the privacy policy is the most common reason a review stalls.

> **Not yet submitted.** `com.heatonresearch.heatonca` has no Play Console app
> record yet, so every field below is prepared text rather than a transcript of
> a live listing. Items marked **PLACEHOLDER** need a real value from the
> console or from Jeff before the first upload.

## App details

- **App name** (30 max): `HeatonCA`
- **Short description** (80 max):

```
Evolve and explore MergeLife cellular automata: gallery, decoder, and trainer.
```

- **Full description** (4000 max):

```
HeatonCA puts a cellular-automata research project in your hands. MergeLife is
a continuous-color cellular automaton whose entire physics is eight hexadecimal
octets: change one digit and you get a different universe. This app runs those
universes, takes them apart, and breeds new ones - entirely on your device.

SIMULATOR
Type or paste any MergeLife rule (32 hex digits, eight groups of four) and watch
it grow from a random soup. Start, stop, single-step, reset, or roll a brand-new
random rule. Pick from the presets and the named gallery rules, then save any
frame worth keeping as a PNG - exports land in a HeatonCA album in your photo
gallery. Cell size and animation
speed are yours to set, and an optional overlay reports the generation count and
frame rate while it runs.

GALLERY
Thirty curated rules, each with its own preview tile: Red World, the rule from
the paper, followed by Pen and Ink, Brushfire, Beetle Meadow, High Noon,
Lagoons, Frost, Lichen, Plankton, Neon Storm, Coral Bloom, Emeralds, Mood Ring,
and seventeen more that go by their hex alone. Tap one and it opens in the
simulator, already running.

RULE DECODER
A MergeLife rule is not a black box. The decoder compiles the current rule and
shows all eight sub-rules in one table: the promoted high limit, the neighbor
sum range each sub-rule claims, the key color it merges toward, the merge
percentage, the color index, and the two raw octets the numbers came from. It is
the fastest way to understand why a rule behaves the way it does - and to see
what your own edit actually changed.

EVOLVE
Run the genetic algorithm that found many of the gallery rules. It scores every
candidate with the aesthetic objective from the 2018 paper - rewarding worlds
that keep moving without dissolving into noise or freezing - and reports the run
number, evaluation count, evaluations per minute, the current rule and score,
and how close the run is to giving up. Rules that clear your score threshold are
kept in a finds gallery you can reopen, export as PNG, or clear. The search
pauses when you leave the app and picks up where it stopped.

THE PAPER
HeatonCA implements the automaton and the objective function described in:
Heaton, J. (2018). Evolving continuous cellular automata for aesthetic
objectives. Genetic Programming and Evolvable Machines.
https://doi.org/10.1007/s10710-018-9336-1

The same engine runs on Android, iOS, macOS, Windows, and the web, and it is
deterministic: a given rule and seed produce the same world on every one of
them.

PRIVACY
Everything happens on your device. No account, no sign-in, no ads, no in-app
purchases, no analytics, and no data collected of any kind - nothing you do here
leaves your phone. The app works offline, always.

Free and open source: https://github.com/jeffheaton/mergelife
```

- **App icon**: `graphics/icon-512.png` (512×512 PNG, RGB, no alpha)
- **Feature graphic**: `graphics/feature-graphic-1024x500.png` (1024×500 PNG, RGB, no alpha)
- **Phone screenshots**: `phone/01…08` — all eight captured on the `Play_Phone`
  AVD from the 2.0.0 `.apk`. 1080×1920 portrait PNG, alpha stripped, **maximum
  8** (Play's limit; the iOS set allows 10, so the Android set is the whole
  scene list with nothing to spare). Plan and capture procedure:
  `store/README.md`.
- **Tablet screenshots**: `tablet-10/01…08` — all eight captured on the
  `Play_Tablet` AVD from the same `.apk`. 2560×1600 landscape PNG; Play accepts
  the same set for the 7-inch and 10-inch slots. They are shot on a tablet AVD
  so the multi-column Gallery and the wide-table Rule decoder are visible -
  those two layouts are the reason the tablet set is not just upscaled phone
  shots.
- **Video**: none.

## Store settings

- **App or game**: App. (`AndroidIsGame: 0` in ProjectSettings; keep the two
  answers consistent or the console warns.)
- **Category**: Education. Simulation is the defensible second choice if
  Education ever gets pushback; the macOS listing uses Utilities, and the three
  stores do not need to match.
- **Tags**: Education, Simulation, Art & Design (choose from Play's fixed list;
  five maximum).
- **Contact email**: `jeff@jeffheaton.com` — **PLACEHOLDER**: use whatever
  address the Play developer account already publishes, and make it match
  `docs/privacy.md`.
- **Website**: `https://www.heatonresearch.com/mergelife/`
- **Support / external marketing**: `https://github.com/jeffheaton/mergelife`
- **Privacy policy URL**:
  `https://github.com/jeffheaton/mergelife/blob/master/unity/heaton-ca/docs/privacy.md`
  (must resolve publicly *before* the first review; it is the same URL the
  About screen opens.)
- **Price**: Free, all countries. No in-app products.

## Declarations

- **Data safety**: **collects nothing, shares nothing.** Answer "No" to "Does
  your app collect or share any of the required user data types?" That single
  answer clears the whole questionnaire: no data types, therefore no sharing, no
  "encrypted in transit" question (nothing is transmitted), and no deletion
  request mechanism (nothing is held). Settings, saved finds, and exported PNGs
  stay in the app's own storage and the gallery folder you choose. This must
  match `docs/privacy.md` and the App Store "Data Not Collected" label.
- **Content rating (IARC)**: run the questionnaire and answer **no** to every
  category — no violence, no sexuality, no profanity, no controlled substances,
  no gambling or simulated gambling, no user-to-user communication, no location
  sharing, no personal information sharing, no purchases. Expected result:
  **Everyone / PEGI 3 / ESRB Everyone**. Category for the questionnaire:
  Reference, News, or Educational.
- **Target audience and content**: **13 and over.** The app is not designed for
  children, has no child-directed content, and should stay out of the Families
  policy track (which would add Teacher Approved and ad-network requirements it
  does not need).
- **Ads**: none — answer "No, my app does not contain ads".
- **In-app purchases**: none.
- **App access**: all functionality is available without an account, login, or
  special access. State that explicitly so the reviewer does not ask for
  credentials.
- **Permissions in the shipped manifest** (read out of the Phase 3 APK with
  `aapt2 dump badging`, so this is what the package actually asks for, not what
  it ought to ask for):
  - `INTERNET` — added by Unity's player, not used by any code in the app. It
    can be removed with Player Settings > Android > Internet Access = Not
    Required if its presence ever needs explaining; nothing here would break.
  - `READ_EXTERNAL_STORAGE`, `WRITE_EXTERNAL_STORAGE` — added by the
    NativeGallery package so Save PNG can write into a HeatonCA album on
    Android 9 and older. Both are inert on Android 10+.
  - `com.heatonresearch.heatonca.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION` —
    AndroidX boilerplate, signature-level, invisible to users.
  - Not present, and worth knowing because each would drag in its own console
    form: no `READ_MEDIA_IMAGES`/`READ_MEDIA_VIDEO` (so **no Photo and Video
    Permissions declaration is required**), no `MANAGE_EXTERNAL_STORAGE`, no
    location, camera, microphone, contacts, or advertising id.
- **Government app**: no. **Financial features**: none. **Health**: no.
- **Data deletion**: not applicable (no account, no server).
- **News app / COVID-19 / blockchain**: no.

## Upload notes

- **Artifact**: `build/android/HeatonCA.aab` from `CIBuild.AndroidPlayStore`
  (`tools/unity-gate.sh build-android-aab`), built with all four
  `HEATONCA_ANDROID_*` signing variables exported — see `docs/android-keystore.md`.
  Without them the build fails outright for the `.aab`, which is deliberate:
  Play rejects debug-signed bundles. `CIBuild.Android` builds the debug-signed
  `.apk` used for emulator testing.
- **Play App Signing**: accept it on the first upload and let Google generate
  the app signing key. The keystore on this Mac is then only the **upload key**;
  losing it is recoverable (Google can reset an upload key), losing an app
  signing key would not be.
- **Version code**: `AndroidBundleVersionCode` must be **higher than every
  previous upload**, every time. It is `1` in ProjectSettings and nothing has
  been uploaded yet, so the first submission is `HEATONCA_ANDROID_VERSION_CODE=1`.
  This counter is **Android's own** — it does not share the App Store Connect
  build counter that iOS and macOS split. The version users see is
  `bundleVersion` = `2.0.0`.
- **Package name**: `com.heatonresearch.heatonca` — permanent once the app
  record is created, and different from the Apple bundle id
  (`com.heatonresearch.heaton-ca`) because a dash is legal there and not here.
  **PLACEHOLDER**: the record does not exist yet; creating it is what fixes the
  package name forever.
- **Build configuration** (ProjectSettings, confirmed against the Phase 3 APK
  with `aapt2 dump badging`): `package com.heatonresearch.heatonca`,
  `versionCode 1`, `versionName 2.0.0`, `targetSdkVersion 36`
  (`AndroidTargetSdkVersion: 0` = highest installed SDK, which satisfies Play's
  current target-API floor), minSdk 26, `native-code arm64-v8a` only
  (`AndroidTargetArchitectures: 2`), IL2CPP, one base module, no split binary.
- **16 KB page compliance** (a hard Play requirement for new uploads): run

  ```
  Packaging/android/check-16kb.sh                    # build/android/HeatonCA.aab
  Packaging/android/check-16kb.sh build/android/HeatonCA.apk
  ```

  It reads the archive with the NDK's `llvm-readelf` and fails if any LOAD
  segment of any `lib/arm64-v8a/*.so` is not 16 KB aligned. Last measured
  2026-09-02 on the Phase 3 APK: all six libraries (`libmain`, `libil2cpp`,
  `libgame`, `libunity`, `libc++_shared`, `lib_burst_generated`) align every
  LOAD segment at `0x4000`. Re-run it on any Unity, NDK, or Gradle change.
- **Determinism gate**: the Android IL2CPP self-check ran green on the
  `Play_Phone` emulator (Android 17 / API 37, arm64) at every phase gate. Re-arm
  it on any Unity or scripting-backend change — `tools/android-selfcheck.sh`.
- **Release track**: start on **internal testing** with the same `.aab`, install
  it from the Play Store on a real device, confirm Save PNG lands in the gallery
  and the Evolve search survives backgrounding, then promote to production.
- **First review**: expect it to take longer than later ones, and expect Play to
  ask nothing extra as long as Data safety, the privacy policy, and the IARC
  answers agree with each other.
