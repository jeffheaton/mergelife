# HeatonCA gate evidence

Measured on Jeff's Mac (Apple Silicon, Unity 6000.5.0f1, Xcode 26.6) on 2026-09-02 while building
the Unity port. Every number here came from a real run, not from a plan. Nothing has been committed:
the whole `unity/` tree is untracked and the five touched repo files are modified in place.

## Gate 0 (2026-09-02)
- compile: PASS (`COMPILE OK`, zero `error CS`).
- ProjectIdentity.Apply: run twice, second run `changed=0 mismatches=0`, ProjectSettings byte-identical.
- Build profiles: macOS, iOS, Android exist; WebGL.asset created by ProjectIdentity.CreateWebGLBuildProfile
  (platform id 84a3bb9e7420477f885e98145999eb20, display name "Web").
- engine-sync-check: PASS, 7 files in sync with heaton-life 46a117baf33a0f916d39e2c05991604ae8a1b424.
- extract-icons --check, vendor-vectors --check (17 files, 8 cases), make-web-icons --check, bootstrap --verify: all PASS.

## Gate 1 (2026-09-02)
- EditMode: 111 tests, 0 failed, 10 s (MIN_TESTS=55).
  Fixtures: RepoConformanceTests 18 (12 upstream vectors + harness pins), Pcg32Tests 6,
  EvolveConformanceTests 4, MergeLifeDecodeTests 3, MergeLifeSoupVectorTests 4, MergeLifeEngineTests 24,
  EvolveOperatorTests 9, FeaturedRuleTests 7, SelfCheckTests 9, UIDensityTests 8, AppJsonTests 10, AppSettingsTests 7.
  (EngineBenchmarkTests is [Explicit] and reports as skipped.)
- macOS (Mono): build 35 s; `-selfcheck` player exits 0 with `[HeatonCA] SELF-CHECK PASS`.
  Info.plist: com.heatonresearch.heaton-ca, 2.0.0, ITSAppUsesNonExemptEncryption false,
  GCSupportsGameMode false, "© 2018-2026 Jeff Heaton", LSMinimumSystemVersion 12.0, no NSAppTransportSecurity.
  PrivacyInfo.xcprivacy: tracking false, 0 collected types, reasons CA92.1 / 35F9.1 / E174.1 / C617.1. Ad-hoc signed.
- WebGL (wasm): build 142 s; headless Chrome smoke PASS in 2 s with `[HeatonCA] SELF-CHECK PASS`
  and `[HeatonCA] url ?rule=e542-...` (deep link reaches the app). Build/ is 17 MB on disk
  (webgl.data 6.7 MB, webgl.wasm 11.1 MB, both gzip .unityweb).
- Android (IL2CPP arm64): build 109 s; self-check PASS on the Play_Phone emulator
  (Android 17 / API 37, arm64-v8a, 4 s). This is the IL2CPP determinism proof: the engine's
  integer and double math survives ahead-of-time compilation.

- iOS simulator (IL2CPP arm64): build 18 s, xcodebuild 50 s; self-check PASS on iPhone 17 Pro / iOS 26.5
  (all five checks). Second IL2CPP proof.
- PlayMode: 2 tests, 0 failed.

## Gate 2 (2026-09-02, after Phase 2 UI-free services)
- compile PASS; EditMode 243 tests / 0 failed (25 fixtures); PlayMode 2 / 0.
- Seam greps clean: `Task.Run` only in EvolveScheduler.cs, `NativeGallery` only in PngExporter.cs
  (plus the asmdef reference), `PlayerPrefs` only in AppSettings.cs / AppStore.cs apart from three
  `PlayerPrefs.Save()` flushes in AppController.
- AppStrings.cs carries exactly three non-ASCII characters (U+03B1/03B2/03B3, the rule-decoder
  headers), UTF-8 without BOM; ASCII fallbacks exist for all three.

## Gate 3 (2026-09-02, after Phase 3 screens)
- compile PASS; EditMode 243 / 0; PlayMode 51 / 0 (all seven screens covered).
- Two failures found and fixed rather than suppressed:
  * SimulatorView only painted its texture on the first Tick, so a world opened paused exported an
    empty PNG. OpenAt now blits the seeded lattice immediately (the house OpenWorld pattern).
  * AppSmokeTests.NoErrorLogsDuringBoot used LogAssert.NoUnexpectedReceived(), which flags every
    unexpected log INCLUDING the healthy self-check's Debug.Log. It now watches
    Application.logMessageReceived and fails only on Error/Assert/Exception, which is the plan's rule.
  * SimulatorViewTests re-pins PngExporter.SnapshotsRoot after TestBoot.Boot(), because
    AppController.UseStorageRoot deliberately re-points the exporter at <storage root>/Snapshots.
- Android 16 KB page compliance (a hard Google Play requirement) verified directly on the built APK
  with the Unity-bundled NDK's llvm-readelf: all six arm64-v8a libraries
  (libmain, libil2cpp, libgame, libunity, libc++_shared, lib_burst_generated) have every LOAD
  segment aligned 0x4000. Single ABI: arm64-v8a. APK 33 MB.

### iOS Xcode project audit (generated build, 2026-09-02)
- Info.plist: CFBundleShortVersionString 2.0.0, ITSAppUsesNonExemptEncryption false,
  UIFileSharingEnabled true, LSSupportsOpeningDocumentsInPlace true (both load-bearing: without them
  persistentDataPath is invisible in the Files app), NSPhotoLibraryAddUsageDescription set.
- PrivacyInfo.xcprivacy present AND wired into the build phase (8 pbxproj references): tracking
  false, zero collected data types, required-reason APIs CA92.1 / 35F9.1 / E174.1 / C617.1.
- DEVELOPMENT_TEAM 9KJKLKXJ5G on the app and UnityFramework targets; the one empty value is on
  Unity's auxiliary com.unity3d.framework test target, which is never archived.

### WebGL payload
- 17 MB with all seven screens (webgl.wasm 10.8 MB, webgl.data 6.5 MB, both gzip .unityweb) —
  unchanged from the shell-only build, because the UI is code-built and ships no prefabs or atlases.
- macOS .app is 118 MB uncompressed.

### Google Play submission path (exercised 2026-09-02, throwaway key)
- CIBuild.AndroidPlayStore builds a release-signed .aab (33.8 MB) with a single `base` module,
  which is what Play expects. Exercised with a THROWAWAY keystore created by Unity's bundled JDK
  (/Applications/Unity/Hub/Editor/6000.5.0f1/PlaybackEngines/AndroidPlayer/OpenJDK/bin/keytool —
  there is no system Java on this Mac, which the keystore doc must say). The signed artifact was
  DELETED afterwards: a real release must rebuild with Jeff's own upload key.
- 16 KB page compliance re-verified on the .aab itself: all six base/lib/arm64-v8a libraries align
  every LOAD segment at 0x4000.
- Manifest (from the APK): package com.heatonresearch.heatonca, versionName 2.0.0, versionCode 1,
  minSdkVersion 26, targetSdkVersion 36 (meets Play's current floor), label HeatonCA, arm64-v8a only.

## Gate 4 (2026-09-02, packaging, store text, docs, CI)
- compile PASS; EditMode 243 / 0; PlayMode 51 / 0 after the native menus landed.
- Seam greps clean (comment-aware): Task.Run only in EvolveScheduler.cs, NativeGallery only in
  PngExporter.cs, PlayerPrefs only in AppSettings/AppStore plus three PlayerPrefs.Save() flushes,
  and no platform #if outside the five designated seam files.
- CI workflow parses; jobs are python, javascript, java, c, unity-lint, unity (the four original
  jobs untouched).
- tools/spelling-check.sh PASSES and is proven live (planting the British spelling of "color"
  makes it exit 1). Three
  rules: no British spellings; no non-ASCII in Assets/Scripts code (comments and test data exempt,
  since neither renders); AppStrings.cs ASCII apart from U+03B1/03B2/03B3.
- engine-sync-check PASS (still byte-identical to upstream apart from the recorded ToPng removal).
- publish-webgl.sh --dry-run lists 10 files / 17.3 MB with correct content types, gzip encoding on
  the three .unityweb payloads, an immutable cache policy for Build/ and TemplateData/, no-cache for
  index.html, and it excludes the *_DoNotShip folder.
- make-play-graphics.py --check: icon-512.png and feature-graphic-1024x500.png are RGB with no alpha.
- Mac App Store: package-macos-appstore.sh sandbox-test ran end to end and produced a sandboxed,
  ad-hoc-signed HeatonCA.app whose codesign verification passes with exactly the three entitlements.
  Its verify_build gate was proven to REFUSE a build missing the macOSPostBuild edits, and store mode
  correctly refuses a mismatched provisioning profile.
- All project docs, store listings and packaging files present; repo README, binaries.md and
  CLAUDE.md all mention HeatonCA.

### Native macOS menu bundle
- Assets/Plugins/macOS/HeatonCAMac.bundle is a real universal Mach-O (arm64 + x86_64) compiled from
  Source~/HeatonCAMac.mm, exporting HeatonCA_InstallMenuBar, _RegisterCallback, _RevealInFinder and
  _UninstallMenuBar — exactly the four the C# side imports. It ships into
  Contents/PlugIns/ and is ad-hoc signed by macOSPostBuild. The menu code only compiles into the
  standalone player (#if UNITY_STANDALONE_OSX && !UNITY_EDITOR), so the editor compile gate cannot
  see it: a macOS build plus its self-check is the only proof it compiles and does not crash.

## Gotchas discovered while gating (keep in the runbook)
1. `--virtual-time-budget` must NOT be passed to headless Chrome: it fast-forwards virtual time and the
   Unity wasm loader never finishes, so the smoke run times out with no console output. Removed from
   tools/webgl-smoke.sh with a note; without it the verdict lands in ~2 s.
2. Unity names the WebGL build files after the OUTPUT FOLDER, not productName: `webgl.loader.js`,
   `webgl.{framework.js,wasm,data}.unityweb`. Packaging/webgl/README.md was corrected to match.
3. `build/webgl/HeatonCA_BurstDebugInformation_DoNotShip/` is produced by every build and must be
   excluded by the publish script (WP4.3) as well as by the MSIX packer.
4. `[UnityCache] Could not connect to cache: Database timeout` is normal in headless Chrome
   (no IndexedDB in that profile) and does not affect the verdict.
5. Every Unity gate must run with the Claude Code Bash sandbox DISABLED: sandboxed runs fail at
   licensing IPC, at `pgrep` (unity-gate exits 5), and at binding 127.0.0.1 for webgl-serve.
6. `iOSSimulatorArchitecture` must be 1 (arm64). Unity's default 0 (x86_64) builds a simulator app
   that cannot install on an Apple Silicon Mac (`simctl install` fails with "Failed to find matching
   arch"), so the iOS determinism gate never runs. Set in ProjectSettings and asserted by
   ProjectIdentity.CheckYaml. The public API is `PlayerSettings.iOS.simulatorSdkArchitecture`
   (type AppleMobileArchitectureSimulator), not the `simulatorArchitecture` name one would guess.
