# HeatonCA release runbook

The order of operations for shipping HeatonCA to the App Store (iOS + macOS),
Google Play, the web, and the GitHub release page. Work top to bottom; the
early steps exist because the later ones are irreversible.

Run everything from the repository root
(`/Users/jeff/projects/mergelife`) unless a step says otherwise, and set:

```bash
R=/Users/jeff/projects/mergelife
P=$R/unity/heaton-ca
UNITY=/Applications/Unity/Hub/Editor/6000.5.0f1/Unity.app/Contents/MacOS/Unity
cd "$P"
```

> **Every Unity gate and every build in this document runs with the Claude Code
> Bash sandbox DISABLED.** Sandboxed runs fail at Unity's licensing IPC, at
> `pgrep`, and at binding `127.0.0.1`, and they report it as a license error or
> "project already open". Also: only one Unity instance may touch this project
> at a time — close the Hub before you start.

Values Jeff must supply before a first release are marked **`<TBD: …>`**. Do
not guess any of them, and never commit a guessed build number.

---

## 0. Preconditions

- [ ] Working tree clean, on `master`, at the commit you intend to ship.
- [ ] Unity 6000.5.0f1 with the macOS, iOS, Android, and WebGL modules
      (no Windows module on this Mac — Windows is step 8, on the VM).
- [ ] Release environment sourced in **every** shell that runs a step below:

      ```bash
      source unity/heaton-ca/release-env.sh
      ```

      Copy it from `release-env.sample.sh` on a new machine; it is git-ignored
      because it carries your Apple team, keystore path and publish target. It
      prints what is set and what each missing value blocks. Source, do not
      execute: a child process cannot export into this shell.
- [ ] Xcode 26.6 signed in to the developer account. The Apple team reaches the
      build only through `HEATONCA_APPLE_TEAM_ID`; it is not checked in.
- [ ] Sibling checkouts present for the sync checks:
      `~/projects/heaton-life`, `~/projects/heaton-life-unity`.
- [ ] Mac App Store provisioning profile in place:
      `Packaging/HeatonCA-MacAppStore.provisionprofile`.
      **`<TBD: obtain from the developer portal for $HEATONCA_APPLE_TEAM_ID.com.heatonresearch.heaton-ca>`**
      The packaging script refuses an expired profile and warns inside 30 days.
- [ ] Android upload keystore and its passwords available.
      **`<TBD: keystore path and credentials>`**
- [ ] Google Play app registered with package `com.heatonresearch.heatonca`.
      **`<TBD: confirm the package is registered; the first upload fixes it forever>`**
- [ ] WebGL hosting destination decided and writable.
      **`<TBD: bucket/path or Pages target, and whether gzip Content-Encoding headers can be set>`**

## 1. Read the build counters FIRST

The single most expensive mistake in this release is a build number that is not
higher than the last one: App Store Connect rejects the upload as ITMS-90189
"Redundant Binary Upload" after you have already spent the archive.

- [ ] Open App Store Connect, app id **6469583429**, and find the **highest
      build number ever uploaded on either platform**. iOS and macOS share one
      counter for this record.

      **Read on 2026-09-06:** TestFlight > iOS Builds was **empty** (2.0.0 adds
      the iOS platform to the record), and TestFlight > macOS Builds held four
      version groups -- 1.1.0 (build `1.1.0`, Expired), 1.0.1, 1.0.0, 0.0.1 --
      so the highest ever uploaded was the dotted string **`1.1.0`**. The PyQt
      builds stamped `CFBundleVersion` equal to `CFBundleShortVersionString`,
      exactly as `store/ios/listing.md` section 6 predicted. 2.0.0 therefore
      moved the counter to plain integers, which Apple orders above any of
      those component by component: **iOS = 10, macOS = 11**. The next release
      continues from 12.
- [ ] Note the record's current category and confirm it still matches
      `public.app-category.utilities`.
- [ ] Find the last `bundleVersionCode` uploaded to Google Play (its own,
      unrelated counter; `1` if this is the first upload).
      **`<TBD: last Play version code>`**

```bash
export HEATONCA_BUILD_NUMBER=<one more than the highest App Store Connect build>
export HEATONCA_ANDROID_VERSION_CODE=<one more than the last Play version code>
export HEATONCA_STORE=1     # makes the Apple builds FAIL rather than stamp a dev number
```

`HEATONCA_BUILD_NUMBER` is applied to **both** Apple targets on every build, so
the About page's build stamp agrees across platforms. `bundleVersion` stays
`2.0.0` — the store version moves on its own schedule, in
`ProjectSettings/ProjectSettings.asset`.

### The version record is named `bundleVersion`, never the build number

Creating the App Store Connect **version record** is a separate act from
uploading a build, and its "Version" field is the *marketing* version --
`bundleVersion`, i.e. **2.0.0**. The build number belongs only to the binary
and shows up on its own under the Build section.

Both platforms got this wrong on 2026-09-06 and both had to be corrected:

- **macOS** was created as version **`11`** -- the build number typed into the
  version field. Caught while still "Prepare for Submission", so the Version
  field was editable and a plain edit fixed it.
- **iOS** was created as version **`1.0`**, which is what App Store Connect
  pre-fills when you use **Add Platform > iOS**. It had already reached
  "Waiting for Review", where the field is locked, so fixing it cost a
  **Remove from Review**, an edit, and a re-submission -- and with it, the
  place in the review queue.

So: immediately after creating a version record, and **before** submitting,
confirm the version reads `2.0.0` on both platform pages. A record whose
version disagrees with the binary's `CFBundleShortVersionString` is a
contradiction the store will happily publish.

## 2. Pre-flight checks (cheap, run them all)

```bash
tools/engine-sync-check.sh                      # exit 0 only if the sole diff is the ToPng removal
tools/vendor-vectors.sh --check                 # 17 vendored files still byte-identical
tools/extract-icons.sh --check
tools/make-web-icons.py --check
tools/bootstrap.sh --verify
tools/spelling-check.sh                         # American English (and an ASCII report)
```

- [ ] All six exit 0. `bootstrap.sh --verify` in particular catches a hazard
      worth naming: an Android build run with a test keystore writes
      `AndroidKeystoreName` and `AndroidKeyaliasName` into
      `ProjectSettings/ProjectSettings.asset` and leaves them there. Those two
      fields must be **empty** in a shipping tree — signing comes from the
      `HEATONCA_ANDROID_*` environment, and a committed path to somebody's
      local keystore is both a broken build on any other machine and a leak of
      a private path.
- [ ] `git status --porcelain unity/heaton-ca` is empty apart from the
      build-churn files documented in the [README](../README.md#build-churn-to-leave-alone)
      (`UniversalRenderPipelineGlobalSettings.asset`, `Assets/Scripts/BuildInfo.cs`).
      A store build adds a third: `ProjectSettings/ProjectSettings.asset` picks
      up the build number from `CIBuild.ApplyAppleBuildNumber`. That one is not
      churn to tolerate -- revert it before committing anything (step 11).
- [ ] Every URL in `Assets/Scripts/AppLinks.cs` resolves (in-repo file or HTTP 200).

## 3. Run the full gate set green

Serially, sandbox disabled, nothing else touching the project:

```bash
tools/unity-gate.sh compile
MIN_TESTS=95 tools/unity-gate.sh editmode
MIN_TESTS=30 tools/unity-gate.sh playmode

tools/unity-gate.sh build-macos     && tools/macos-selfcheck.sh
tools/unity-gate.sh build-android   && tools/android-selfcheck.sh
tools/unity-gate.sh build-ios-sim   && tools/ios-sim-selfcheck.sh
tools/unity-gate.sh build-webgl     && tools/webgl-smoke.sh
```

- [ ] Every gate exits 0.
- [ ] All four device gates print `[HeatonCA] SELF-CHECK PASS`.
- [ ] Test totals are at or above the last release's (243 EditMode / 51 PlayMode
      as of 2026-09-02); a drop means tests were lost, not that they got faster.

Record the log paths — `build/logs/<mode>-latest.log` — in the release notes
for this run.

## 4. macOS: Mac App Store

```bash
tools/unity-gate.sh build-macos                      # store mode: HEATONCA_STORE=1
Packaging/package-macos-appstore.sh sandbox-test     # ALWAYS first
```

- [ ] `build/mas/sandbox-test/HeatonCA.app` launches.
- [ ] Its container `Player.log` shows `[HeatonCA] SELF-CHECK PASS`
      (`~/Library/Containers/com.heatonresearch.heaton-ca/Data/Library/Logs/Jeff Heaton/HeatonCA/Player.log`).
- [ ] Save PNG in the Simulator writes a file and reveals it in Finder from
      inside the sandbox.
- [ ] No sandbox denials in `log stream --predicate 'sender == "Sandbox"'`.

Only then:

```bash
security find-identity -v      # NOT -p codesigning: that hides installer certs
Packaging/package-macos-appstore.sh store
```

- [ ] `build/mas/HeatonCA-2.0.0.pkg` produced.
- [ ] Upload with Transporter.app (or `xcrun altool --upload-app -f
      build/mas/HeatonCA-2.0.0.pkg -t macos --apiKey <id> --apiIssuer <issuer>`).
- [ ] App Store Connect shows the build with the number from step 1.

## 5. iOS and iPadOS

```bash
tools/unity-gate.sh build-ios        # -> build/ios (Xcode project, iOSPostBuild applied)
open build/ios/Unity-iPhone.xcodeproj
```

- [ ] Generated `Info.plist` carries `ITSAppUsesNonExemptEncryption=false`,
      `UIFileSharingEnabled`, `LSSupportsOpeningDocumentsInPlace`, and
      `NSPhotoLibraryAddUsageDescription`.
- [ ] `PrivacyInfo.xcprivacy` declares CA92.1, 35F9.1, E174.1, C617.1, no
      tracking, no collected data.
- [ ] Archive (Any iOS Device), Distribute App, App Store Connect.
- [ ] The build number is the same `HEATONCA_BUILD_NUMBER` used for macOS — the
      counter is shared, so the two platforms must not reuse one value between
      them either.

## 6. Android: Google Play

`release-env.sh` already exports the four signing variables when the keystore
exists and both passwords resolve from the keychain; set them by hand only if
you are not using it. The version code is per release and is not in the script.

```bash
export HEATONCA_ANDROID_VERSION_CODE=<TBD: above every previous upload>

tools/unity-gate.sh build-android-aab          # -> build/android/HeatonCA.aab
Packaging/android/check-16kb.sh build/android/HeatonCA.aab
```

- [ ] The build log does **not** say the package was debug-signed (Play rejects
      those).
- [ ] Every `lib/arm64-v8a/*.so` has `LOAD` alignment `0x4000` (16 KB pages —
      a Play requirement for Android 15+ targets).
- [ ] Upload to the **internal testing** track first. On the first upload,
      accept **Play App Signing**: the local keystore is only the upload key.
- [ ] Install from the internal track on a real phone and a tablet; Save PNG
      lands in the gallery.

## 7. Web

```bash
tools/unity-gate.sh build-webgl
tools/webgl-smoke.sh
Packaging/web/publish-webgl.sh --dry-run
```

- [ ] The dry run lists every file, with `Content-Encoding: gzip` on the three
      `Build/*.unityweb` files (`webgl.loader.js` is plain JavaScript and must
      not get the header) and no
      `build/webgl/HeatonCA_BurstDebugInformation_DoNotShip/` in the list.
- [ ] Publish for real, then load the page in Chrome, Safari, and Firefox.
- [ ] `?rule=e542-5f79-9341-f31e-6c6b-7f08-8773-7068` opens a running world.
- [ ] `?controls=off` gives the kiosk look.

Hosting destination: **`<TBD: final URL>`** — the same value goes in
`store/webgl/README.md` and in the tutorial page's embed links.

## 8. Windows (on the Parallels Win11 VM)

The Mac has no Windows module. On the VM, with this repository checked out:

```powershell
& $UNITY -batchmode -nographics -quit -projectPath unity\heaton-ca `
    -buildTarget Win64 -executeMethod CIBuild.Windows `
    -logFile build\logs\build-windows.log
powershell -ExecutionPolicy Bypass -File tools\windows-selfcheck.ps1
```

- [ ] The script exits 0 and the log holds `[HeatonCA] SELF-CHECK PASS`.
- [ ] Zip `build\windows-x64\` as `HeatonCA-2.0.0-win-x64.zip`.
- [ ] Attach the zip to the GitHub release; the listing notes that the build is
      unsigned and SmartScreen will warn on first run.

Microsoft Store / MSIX packaging is deliberately out of scope for 2.0.0.

## 9. Device verification (before releasing any of the uploads)

- [ ] **A real iPhone:** the rule decoder headers render the Greek letters
      `High (α)`, `Percent (β)`, `Index (γ)` — not the ASCII fallback and not
      boxes.
- [ ] **iPad:** rotate while the simulator runs; the world re-cuts and keeps
      the overlapping cells instead of wiping. Split View works.
- [ ] **Android tablet:** same rotation check.
- [ ] **iOS and Android:** Save PNG asks for the photo permission once, then
      writes to the HeatonCA album; the file is also reachable in Files.
- [ ] **Mac:** Save PNG reveals in Finder from the sandboxed build.
- [ ] Every platform's About screen reads `Version 2.0.0 (build <n>)`, a
      `Built` stamp later than the commit you are shipping, and
      `Determinism self-check: PASS`.

## 10. Record the artifacts

Hash everything that leaves this machine, and keep the table with the release
notes. `shasum -a 256 <file>` on macOS, `Get-FileHash` on Windows.

```bash
shasum -a 256 \
  build/mas/HeatonCA-2.0.0.pkg \
  build/android/HeatonCA.aab \
  build/webgl/Build/webgl.wasm.unityweb \
  build/webgl/Build/webgl.data.unityweb
```

| Artifact | Path | Build number | Size | SHA-256 | Uploaded |
| --- | --- | --- | --- | --- | --- |
| macOS `.pkg` | `build/mas/HeatonCA-2.0.0.pkg` | 11 | 46 MB | `39ee330c06bbdcb21ab42482925cb254600488edb6ff05ad26ee782cb32c08dc` | 2026-09-06 |
| iOS archive | Xcode Organizer | 10 | n/a | n/a | 2026-09-06 |
| Android `.aab` | `build/android/HeatonCA.aab` | | | | |
| WebGL `webgl.wasm.unityweb` | `build/webgl/Build/` | n/a | | | |
| WebGL `webgl.data.unityweb` | `build/webgl/Build/` | n/a | | | |
| Windows zip | `HeatonCA-2.0.0-win-x64.zip` | n/a | | | |

Also record, for the next release: the App Store Connect build number used, the
Play version code used, the Unity version, and the engine commit
(`Assets/Engine/PROVENANCE.md`).

## 11. Release and tag

- [ ] Submit the App Store version for review (iOS and macOS together on the
      one record). Privacy policy URL and support URL point at
      `docs/privacy.md` and `docs/manual.md` in this repository.
- [ ] Promote the Play internal build to production when review passes.
- [ ] Tag the shipping commit `heaton-ca-v2.0.0`.
- [ ] Flip the HeatonCA rows in `/binaries.md` from pending to live, with the
      real store and hosting URLs.
- [ ] File anything learned during this run back into this document — that is
      what keeps the next release cheap.

## Failure modes worth recognizing

| Symptom | Cause | Fix |
| --- | --- | --- |
| ITMS-90189 "Redundant Binary Upload" | build number not above the shared iOS/macOS counter | bump `HEATONCA_BUILD_NUMBER`, rebuild, re-archive |
| ITMS-91053 (undeclared required-reason API) | `PrivacyInfo.xcprivacy` missing or overwritten | confirm `iOSPostBuild` / `macOSPostBuild` ran; both throw on a failed edit, so check the build log |
| ITMS-91109 (quarantined file in payload) | the downloaded `.provisionprofile` reintroduced `com.apple.quarantine` | the packaging script runs `xattr -cr` *after* copying the profile; do not reorder |
| Play rejects the bundle as debug-signed | one of the four `HEATONCA_ANDROID_*` variables unset | export all four, rebuild |
| Play rejects for 16 KB page size | a native library not `0x4000`-aligned | `Packaging/android/check-16kb.sh` before uploading, not after |
| WebGL page loads to a blank canvas | the host does not send `Content-Encoding: gzip` and the decompression fallback is off | the fallback is on in `ProjectSettings`; if it was turned off, turn it back on and rebuild |
| The WebGL smoke run times out with no console output | `--virtual-time-budget` was passed to headless Chrome | never pass it; it stalls the wasm loader (see the README's gotchas) |
| `simctl install` fails "Failed to find matching arch" | `iOSSimulatorArchitecture` is not arm64 | it must be 1; `ProjectIdentity.CheckYaml` asserts it |
