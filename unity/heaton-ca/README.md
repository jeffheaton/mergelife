# HeatonCA (Unity)

**HeatonCA** is a MergeLife cellular-automaton app for iOS, iPadOS, macOS,
Windows, Android, and the web. It is the Unity rewrite of the shipped PyQt6
desktop app in [`python/application/pyqt/`](../../python/application/pyqt) —
same product, same rules, same numbers, redesigned for touch as well as for a
mouse, and carrying its own C# engine and trainer so it is standalone.

Version **2.0.0** updates the existing App Store record (app id 6469583429,
bundle `com.heatonresearch.heaton-ca`); the PyQt app was 1.2.0.

- End-user documentation: [`docs/manual.md`](docs/manual.md),
  [`docs/privacy.md`](docs/privacy.md).
- Shipping: [`docs/release-runbook.md`](docs/release-runbook.md).
- Tooling protocol in full: [`tools/README.md`](tools/README.md).
- Engine provenance: [`Assets/Engine/PROVENANCE.md`](Assets/Engine/PROVENANCE.md),
  licenses in [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).

Everything below is relative to `unity/heaton-ca/` unless the path starts with
`/`, which means the repository root (`/Users/jeff/projects/mergelife` on this
Mac).

## What the app contains

Seven screens, all code-built uGUI in one scene, coordinated by
`AppController`: **Home**, **Simulator**, **Gallery** (the 30 curated rules),
**Rule decoder**, **Evolve** (a genetic-algorithm search) with its **Finds**
page, **Settings**, and **About**. The simulation engine, the objective
function, and the evolver are plain C# in `Assets/Engine` with no Unity
dependency; `Assets/SelfCheck` replays five pinned determinism checks in every
player at launch.

## Prerequisites

- **Unity 6000.5.0f1** — the version in `ProjectSettings/ProjectVersion.txt`.
  A different patch release will re-serialize assets and produce churn.
- Build modules installed on this Mac (`/Applications/Unity/Hub/Editor/6000.5.0f1/PlaybackEngines`):
  `MacStandaloneSupport`, `iOSSupport`, `AndroidPlayer`, `WebGLSupport`.
- **There is no Windows module on this Mac.** `CIBuild.Windows` and
  `tools/windows-selfcheck.ps1` run on Jeff's Parallels Win11 VM against a
  checkout of this repository; nothing in the macOS gate set builds Windows.
- Xcode 26.6 (iOS device and simulator builds, the macOS ad-hoc re-sign, and
  `xcodebuild`/`simctl` for the iOS gate).
- Android SDK + NDK + an emulator AVD named `Play_Phone` (the Android gate).
- Google Chrome (the WebGL smoke gate), `python3`, `xmllint`.
- For the engine and vector sync scripts, the sibling checkouts
  `~/projects/heaton-life` and `~/projects/heaton-life-unity`
  (override with `HEATON_LIFE` / `HEATON_LIFE_UNITY`).

## Layout

```
unity/heaton-ca/
  Assets/
    Engine/          HeatonCA.Engine — verbatim copy of HeatonLife.Core (no UnityEngine)
    SelfCheck/       HeatonCA.SelfCheck — DeterminismSelfCheck, five pinned checks
    Scripts/         HeatonCAApp — services, views, AppController, AppStrings
    Editor/          CIBuild, ProjectIdentity, CompileGate, post-build and icon scripts
    Tests/EditMode/  HeatonCA.EditorTests (25 fixtures)
    Tests/PlayMode/  HeatonCAApp.PlayTests (7 fixtures)
    Tests/Vectors~/  vendored heaton-life vectors — the tilde keeps Unity out
    Resources/       Gallery/<rule>.png x30, Art/, Icons/, ThirdPartyNotices.txt
    Icons/           app icon masters (1024) plus the iOS and Android sets
    Plugins/WebGL/   HeatonCA.jslib
    WebGLTemplates/HeatonCA/
    Settings/        URP assets and the Build Profiles
    Scenes/HeatonCAMainScene.unity, HeatonCAInput.inputactions
  Packaging/         store packaging scripts (macOS pkg, Android checks, web publish)
  store/             listing texts and store graphics
  docs/              privacy.md, manual.md, release-runbook.md
  tools/             the gate runner and every other script (see tools/README.md)
  build/             gate logs and player output — git-ignored
```

Assembly direction: `HeatonCA.Engine` ← `HeatonCA.SelfCheck` ← `HeatonCAApp` ←
`HeatonCAApp.PlayTests`; `HeatonCA.EditorTests` references all three runtime
assemblies. `conformance/vectors.txt` is read from the repository root at test
time (`HEATONCA_CONFORMANCE_VECTORS` overrides), never duplicated here.

## The gate protocol

Agents write code and hand-authored `.meta` files and **never launch Unity**.
Gates run serially on the main checkout, one at a time, through one script:

```bash
tools/unity-gate.sh <mode> [-- extra unity args]
```

| Mode | What it runs |
| --- | --- |
| `compile` | `-quit -executeMethod HeatonCA.Editor.CompileGate.Run`; passes only on `COMPILE OK` |
| `editmode` | `-runTests -testPlatform EditMode -testResults <xml>` |
| `playmode` | `-runTests -testPlatform PlayMode -testResults <xml>` |
| `build-macos` | `-buildTarget OSXUniversal -executeMethod CIBuild.MacOS` |
| `build-ios` | `-buildTarget iOS -executeMethod CIBuild.IOS` |
| `build-ios-sim` | `-buildTarget iOS -executeMethod CIBuild.IOSSimulator` |
| `build-android` | `-buildTarget Android -executeMethod CIBuild.Android` |
| `build-android-aab` | `-buildTarget Android -executeMethod CIBuild.AndroidPlayStore` |
| `build-webgl` | `-buildTarget WebGL -executeMethod CIBuild.WebGL` |

Every mode adds `-batchmode -nographics -projectPath <project> -logFile
build/logs/<mode>-<utc>.log`. The underlying editor invocations, if you ever
need to run one by hand from the repository root:

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.5.0f1/Unity.app/Contents/MacOS/Unity

"$UNITY" -batchmode -nographics -quit -projectPath unity/heaton-ca \
  -executeMethod HeatonCA.Editor.CompileGate.Run -logFile build/logs/compile.log

"$UNITY" -batchmode -nographics -projectPath unity/heaton-ca \
  -runTests -testPlatform EditMode \
  -testResults build/logs/editmode.xml -logFile build/logs/editmode.log

"$UNITY" -batchmode -nographics -projectPath unity/heaton-ca \
  -runTests -testPlatform PlayMode \
  -testResults build/logs/playmode.xml -logFile build/logs/playmode.log

"$UNITY" -batchmode -nographics -quit -projectPath unity/heaton-ca \
  -buildTarget OSXUniversal -executeMethod CIBuild.MacOS \
  -logFile build/logs/build-macos.log
```

**A gate fails** when Unity exits non-zero, the log contains `error CS`, a
compile log lacks `COMPILE OK`, a build log contains `Error building Player`,
the NUnit results carry `@failed != 0`, or `@total < MIN_TESTS`.

**`MIN_TESTS`** is the floor on `/test-run/@total` for the test modes (default
1). A run that silently compiles zero tests is a failure, not a pass. The
pinned values:

```bash
MIN_TESTS=55  tools/unity-gate.sh editmode    # Gate 1
MIN_TESTS=95  tools/unity-gate.sh editmode    # Gates 2, 3, 4
MIN_TESTS=30  tools/unity-gate.sh playmode    # Gates 3, 4
```

Exit codes: `0` pass, `1` gate failed, `2` usage, `3` another gate holds the
lock, `4` the project is open in an editor, `5` environment.

### Every Unity gate runs with the Claude Code Bash sandbox DISABLED

This is a hard rule, not a preference. Sandboxed runs fail at the licensing
IPC, at `pgrep` (the gate exits 5), and at binding `127.0.0.1` for
`tools/webgl-serve.py` — and the failures are reported as misleading license or
"project already open" errors. Run gates with `dangerouslyDisableSandbox`, or
allow them through `/sandbox`.

### One Unity instance, ever

Unity refuses to open a project another editor already has open, and two
batchmode runs against one `Library/` corrupt it. The gate takes an atomic
`mkdir build/.unity-gate.lock`, refuses when `Temp/UnityLockfile` exists, and
refuses when `pgrep` finds an editor holding this project path. Never open
`unity/heaton-ca` in the Unity Hub while gates may run, and never run two gates
at once.

### Device gates

Run each after the matching `build-*` gate, also with the sandbox disabled:

```bash
tools/macos-selfcheck.sh                 # build/macos/HeatonCA.app, Mono
tools/android-selfcheck.sh               # build/android/HeatonCA.apk on the Play_Phone AVD
tools/ios-sim-selfcheck.sh               # build/ios-sim/Unity-iPhone.xcodeproj on an iPhone simulator
tools/webgl-smoke.sh                     # build/webgl in headless Chrome
pwsh tools/windows-selfcheck.ps1         # on the Windows VM, after CIBuild.Windows
```

All five run the player as `-batchmode -nographics -selfcheck` (or, on WebGL,
load the page) and gate on `[HeatonCA] SELF-CHECK PASS` plus a zero exit code;
`tools/check-selfcheck.sh` is the shared verdict parser (0 PASS, 1 FAIL, 2
timeout). `--probe` on the Android and iOS scripts lists the environment
without running anything.

### `.meta` discipline

Every asset under `Assets/` has a sibling `.meta`, because Unity references
assets by the GUID inside it. Author text assets with
`tools/new-meta.sh <path>`; let Unity generate the `.meta` for binary and
Unity-native assets and stage them afterwards with `tools/meta-sweep.sh
--stage`. Folders ending in `~` take no `.meta`. Reserved GUIDs that must never
change are listed in [`tools/README.md`](tools/README.md).

## Engine provenance — the rules that keep the numbers honest

`Assets/Engine` is a **verbatim copy** of seven files from
`HeatonLife.Core` in the sibling `heaton-life` repository, pinned at commit
`46a117baf33a0f916d39e2c05991604ae8a1b424`. It is not a fork.

- `tools/import-engine.sh` performs the copy: it prepends the five-line
  provenance header, renames `namespace HeatonLife` to `namespace
  HeatonCA.Engine`, and removes the two `ToPng` lines from `MergeLife.cs` (the
  only `DeflateStream` user). That removal is the single recorded modification.
- `tools/engine-sync-check.sh` reverses the header and the namespace and diffs
  against upstream. **Exit 0 only when the sole difference is the `ToPng`
  removal.** `--self` checks the headers alone, for CI machines without the
  sibling checkout.
- **Never edit the numerics.** Not to "fix" the per-cycle evaluation bleed, not
  the `mage` off-by-one, not `RandomRule`'s 0..255 range byte. Those are the
  vector-pinned C# behavior, and the published scores came from them.
- The contract is the vectors, not the code: `/conformance/vectors.txt` at the
  repository root plus the files vendored into `Assets/Tests/Vectors~/`
  (17 files, 8 cases, same pinned commit, refreshed by
  `tools/vendor-vectors.sh --check`). If a change makes a vector move, the
  change is wrong.
- The one non-upstream C# file in the folder is `AssemblyInfo.cs`
  (`InternalsVisibleTo("HeatonCA.EditorTests")`), which lets the EditMode
  convergence pins drive the internal `RunEvaluator`.

The objective function follows the **2018 reference trainer** (stable
background held for more than 50 generations; exits on a dead world, a frozen
background, or the strict 1000-step cap, so an unconverged run records 1001
steps). Aligning it with the paper's Section 4.1 prose is a regression, not a
fix.

## Test inventory

Counts are current at the time of writing (2026-09-02, end of Phase 3); they
only grow. Run `tools/unity-gate.sh editmode` and read
`build/logs/editmode-latest.xml` for today's numbers.

**EditMode — 243 tests, 0 failed, 25 fixtures** (2 skipped: `EngineBenchmarkTests`
is `[Explicit]`).

| Fixture | Tests | What it pins |
| --- | --- | --- |
| `RepoConformanceTests` | 18 | the 12 vectors of `/conformance/vectors.txt` replayed through an LCG and FNV-1a-64 written inside the test (no engine code shared), plus a corrupted-digest negative test |
| `MergeLifeEngineTests` | 24 | canonicalization, 31/33-digit and non-hex rejection, 2040 to 2048 promotion, 127/128 scaling, tie stability, soup determinism, the decode table, and the Python convergence pins (uniform 20x20 freezes at 153, exploded 16x16 dies at 101, a capped run records 501) |
| `AppStoreTests` | 14 | `FileStore`/`PrefsStore` round trips and the 60-find store-size ceiling |
| `SimulationHostTests` | 14 | 1..60 gen/s, cell-budgeted steps per tick, backlog drop |
| `CellGeometryTests` | 13 | the density model on phone, tablet, desktop, Retina, and WebGL |
| `AppStringsTests` | 13 | the string table stays Latin-1 apart from the three Greek headers |
| `UrlParamsTests` | 13 | `?rule=`, `?size=`, `?controls=` |
| `PngExporterTests` | 12 | file naming and the injected snapshot root |
| `RuleParserTests` | 11 | the canonicalizing parser and its rejections |
| `AppJsonTests` / `GalleryCatalogTests` | 10 each | the tiny JSON codec; 30 unique rules in PyQt order, 13 named, default `e542` |
| `AppContractsTests` / `EvolveOperatorTests` / `SelfCheckTests` | 9 each | view/navigator contracts; GA operators; the five determinism checks against recomputed constants |
| `EvolveHostTests` / `EvolveSchedulerTests` / `UIDensityTests` | 8 each | finds persistence and readouts; both chunk runners; the density model |
| `AppSettingsTests` / `FeaturedRuleTests` / `LatticeResizerTests` | 7 each | the three preferences; the 15 named rules; the re-cut that keeps overlapping cells |
| `Pcg32Tests` | 6 | PCG32 known answers |
| `EvolveConformanceTests` / `MergeLifeSoupVectorTests` | 4 each | the vendored evolve vectors at workers 1 and 5; the redworld-48 soup checkpoints |
| `MergeLifeDecodeTests` | 3 | the three decode vectors |
| `EngineBenchmarkTests` | 2 (`[Explicit]`) | ms per 512² step and ms per 50x50 `RunOnce(1001)` — run it by name when you want numbers |

**PlayMode — 51 tests, 0 failed, 7 fixtures** (the Phase 3 screen suite):
`SimulatorViewTests` 12, `RuleDecoderViewTests` 9, `AppSmokeTests` 8,
`EvolveViewTests` 7, `GalleryViewTests` 6, `SettingsViewTests` 5,
`AboutViewTests` 4. Every PlayMode test runs under `LogAssert`: any
`Debug.LogError` during a test fails it.

## Build matrix

| Target | Gate | Output | Backend |
| --- | --- | --- | --- |
| macOS | `build-macos` | `build/macos/HeatonCA.app` | Mono, universal |
| Windows (VM) | `CIBuild.Windows` on the VM | `build/windows-x64/HeatonCA.exe` | Mono |
| iOS device | `build-ios` | `build/ios` (Xcode project) | IL2CPP arm64 |
| iOS simulator | `build-ios-sim` | `build/ios-sim` (Xcode project) | IL2CPP arm64 |
| Android APK | `build-android` | `build/android/HeatonCA.apk` | IL2CPP arm64 |
| Android AAB | `build-android-aab` | `build/android/HeatonCA.aab` | IL2CPP arm64 |
| WebGL | `build-webgl` | `build/webgl` | wasm, gzip |

Identity, fixed by `HeatonCA.Editor.ProjectIdentity.Apply` and asserted by its
`CheckYaml`: productName `HeatonCA`, companyName `Jeff Heaton`, bundleVersion
`2.0.0`, Apple bundle id `com.heatonresearch.heaton-ca` (iOS **and** macOS),
Android application id `com.heatonresearch.heatonca`
(hyphens are illegal there), `macAppStoreCategory public.app-category.utilities`,
minSdk 26 / targetSdk highest installed / ARM64 only, iOS 15.0, macOS 12.0,
WebGL gzip with the decompression fallback on and threads off, template
`PROJECT:HeatonCA`.

Release environment. `release-env.sample.sh` is the committed template for the
variables below; copy it to `release-env.sh` (git-ignored, so your own Apple
team, keystore path and publish target never reach the repository) and
**source** it -- exports made by a child process vanish, so executing it would
appear to work and change nothing:

```bash
cp release-env.sample.sh release-env.sh   # then edit in your values
```

```bash
source unity/heaton-ca/release-env.sh
```

It sets no store mode by default, reads the Android passwords from the login
keychain rather than holding them, exports the four Android signing variables
only when all four resolve (a partial keystore fails the build by design), and
prints which values are set and what each missing one blocks. Sourcing it is
safe to repeat and safe in an everyday shell.

Build-number environment (see `Assets/Editor/CIBuild.cs`):

| Variable | Meaning |
| --- | --- |
| `HEATONCA_BUILD_NUMBER` | the **shared** iOS + macOS `buildNumber`. One App Store Connect counter serves both platforms; it must exceed the highest build ever uploaded to app 6469583429 |
| `HEATONCA_ANDROID_VERSION_CODE` | Android `bundleVersionCode`, its own monotonic counter |
| `HEATONCA_STORE=1` | store mode: `MacOS` and `IOS` **fail** without a build number instead of logging `DEV BUILD NUMBER`. `AndroidPlayStore` is always store mode |
| `HEATONCA_ANDROID_KEYSTORE`, `_KEYSTORE_PASS`, `_KEYALIAS`, `_KEYALIAS_PASS` | Android release signing; all four together, or the build is debug-signed and warns |
| `HEATONCA_APPLE_TEAM_ID` | Apple developer team for a device or App Store build. Account identity, so it is deliberately **not** checked in: `ProjectIdentity.Apply` holds `appleDeveloperTeamID` empty and `iOSPostBuild` stamps this value onto the app and `UnityFramework` targets of the generated Xcode project. Unset, `DEVELOPMENT_TEAM` is empty — WebGL, Windows, macOS, Android and the iOS **simulator** gate are unaffected, and a device or store build stops with Xcode's "requires a development team". `package-macos-appstore.sh` also checks the provisioning profile against it |

Recorded sizes and timings from the Phase 3 gate run on this Mac (2026-09-02):
macOS build 35 s; Android 109 s, 33 MB APK; iOS simulator 18 s plus 50 s of
`xcodebuild`; WebGL 142 s, 17 MB on disk (`webgl.data.unityweb` 6.7 MB,
`webgl.wasm.unityweb` 11.1 MB, both gzip).

## Where the app stores things, per platform

The app writes three preferences (`heatonca.cellSize`,
`heatonca.stepsPerSecond`, `heatonca.showOverlay`), the evolve finds list
(`evolve-finds.json`), and any PNGs the user exports (`Snapshots/`). Nothing
else, and nothing leaves the device.

| Platform | Data folder (`Application.persistentDataPath`) | Preferences |
| --- | --- | --- |
| macOS, direct build | `~/Library/Application Support/Jeff Heaton/HeatonCA` | `~/Library/Preferences/unity.Jeff Heaton.HeatonCA.plist` |
| macOS, Mac App Store | the sandbox container keyed by the bundle id: `~/Library/Containers/com.heatonresearch.heaton-ca/Data/Library/Application Support/Jeff Heaton/HeatonCA` | the matching `Data/Library/Preferences` inside that container |
| iOS / iPadOS | the app container's `Documents/`, published to the Files app by `UIFileSharingEnabled` + `LSSupportsOpeningDocumentsInPlace` | `NSUserDefaults` in the container |
| Android | app-private internal storage, `/data/user/0/com.heatonresearch.heatonca/files` (`ForceSDCardPermission: 0`) | `SharedPreferences`, `com.heatonresearch.heatonca.v2.playerprefs.xml` |
| Windows | `%USERPROFILE%\AppData\LocalLow\Jeff Heaton\HeatonCA` — **this folder survives an uninstall** | registry, `HKCU\Software\Jeff Heaton\HeatonCA` |
| WebGL | browser site data: PlayerPrefs backed by IndexedDB, scoped to the page origin | the same IndexedDB store |

Two consequences worth remembering. On WebGL the whole store must fit inside
the browser's roughly 1 MB PlayerPrefs budget, which is why find thumbnails are
derived on demand and never stored, and why the finds list is capped at 60.
On Windows, "delete the app and its data goes" is false; the LocalLow folder
stays behind, and `docs/privacy.md` says so.

## Release order (summary)

The full checklist with commands is [`docs/release-runbook.md`](docs/release-runbook.md).
The order matters:

1. **Read App Store Connect first.** Open app 6469583429 and find the highest
   build number ever uploaded on **either** iOS or macOS. Export
   `HEATONCA_BUILD_NUMBER` above it — for **both** Apple targets, because they
   share one counter. A repeat is rejected as ITMS-90189. Never guess it and
   never commit a guessed value.
2. Export `HEATONCA_ANDROID_VERSION_CODE` higher than the last Play upload
   (its own counter, unrelated to Apple's).
3. Run the whole gate set green on the commit you are shipping.
4. macOS: `CIBuild.MacOS`, then package **`sandbox-test` first** — it proves the
   app works sandboxed before a submission finds out — then `store`, then
   Transporter.
5. iOS: `CIBuild.IOS`, archive in Xcode, upload to App Store Connect.
6. Android: `CIBuild.AndroidPlayStore` with the four signing variables set,
   check 16 KB page alignment, upload the `.aab` to the internal track.
7. WebGL: `CIBuild.WebGL`, then the publish script (dry run first).
8. Windows: on the Parallels VM, `CIBuild.Windows`, `tools/windows-selfcheck.ps1`,
   zip, attach to the GitHub release.
9. Record the artifact hashes in the runbook's table, then tag.

## Build churn to leave alone

- `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` gains two
  `rid:` entries on a macOS build and loses them again on an iOS build: its
  `RenderPipelineGraphicsSettings` list is serialized per build target, so the
  diff flips with whatever was built last. Do not commit it as part of a
  change, and do not chase it.
- `Assets/Scripts/BuildInfo.cs` is rewritten by `BuildInfoGenerator` on every
  player build (it is the About page's build stamp). Committing it is harmless;
  a diff there means someone built, not that someone edited.

## Gotchas earned while gating

Two defects cost real time during Phase 1 and are fixed in the tooling. Do not
reintroduce either.

1. **Never pass `--virtual-time-budget` to headless Chrome.** It fast-forwards
   virtual time, the Unity wasm loader never finishes, and the smoke run times
   out with no console output at all — which reads exactly like a broken build.
   It is removed from `tools/webgl-smoke.sh` with a note; without it the
   verdict lands in about 2 seconds.
2. **`iOSSimulatorArchitecture` must be 1 (arm64).** Unity's default 0
   (x86_64) builds a simulator app that cannot install on an Apple Silicon Mac
   — `simctl install` fails with "Failed to find matching arch" — so the iOS
   determinism gate silently never runs. It is set in `ProjectSettings` and
   asserted by `ProjectIdentity.CheckYaml`. The public API is
   `PlayerSettings.iOS.simulatorSdkArchitecture` (type
   `AppleMobileArchitectureSimulator`), not the `simulatorArchitecture` name
   one would guess.

Three more that are noise, not failures: Unity names the WebGL output files
after the **output folder**, not `productName`, so they are `webgl.loader.js`
and `webgl.{framework.js,wasm,data}.unityweb`;
`build/<target>/HeatonCA_BurstDebugInformation_DoNotShip/` is produced by every
build and must be excluded from anything that ships; and
`[UnityCache] Could not connect to cache: Database timeout` is normal in the
headless Chrome profile (no IndexedDB there) and does not affect the verdict.

## Conventions

- **American English everywhere** — identifiers, comments, log and UI strings,
  and these documents. `tools/spelling-check.sh` runs in CI.
- Every user-visible string lives in `Assets/Scripts/AppStrings.cs`. Numeric
  readouts format with `CultureInfo.InvariantCulture`, matching the locale-free
  PyQt originals.
- The only `#if` platform sites are the seam files: `EvolveScheduler.cs`,
  `AppStore.cs`, `PngExporter.cs`, the `NativeMenus` partials, and the editor
  scripts. Editor code that touches `UnityEditor.iOS` / `.Android` / macOS must
  be guarded by the matching define — an unguarded `using` does not merely
  disable itself on a machine lacking that module, it fails to compile and
  takes every unrelated build on that machine down with it.
- Nothing in `tools/` assumes the Claude Code sandbox; each script says in its
  header when the sandbox must be disabled.
