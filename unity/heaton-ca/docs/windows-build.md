# Building HeatonCA for Windows

Windows is HeatonCA's only unsigned, storeless channel. There is no Microsoft
Store submission and no installer: the deliverable is one zip, published on a
GitHub release with its SHA-256 beside it. Everything here happens on Jeff's
Parallels Windows 11 VM — the Mac cannot finish this channel on its own.

Contents: [why the VM](#why-the-vm) · [one-time VM setup](#one-time-vm-setup) ·
[the repo on the VM](#getting-the-repo-onto-the-vm) · [the build](#the-build) ·
[the self-check](#the-self-check) · [the zip](#the-release-zip) ·
[Standalone stays Mono](#standalone-stays-mono) ·
[SmartScreen](#smartscreen-and-the-unsigned-exe) ·
[where the app keeps data](#where-the-app-keeps-data) ·
[release checklist](#release-checklist) · [troubleshooting](#troubleshooting)

## Why the VM

The Mac's editor has Android, iOS, macOS and WebGL build support and **no
Windows module** (`/Applications/Unity/Hub/Editor/6000.5.0f1/modules.json`:
`windows-mono` is `selected: false`, and `PlaybackEngines/` holds only
`AndroidPlayer`, `MacStandaloneSupport`, `WebGLSupport`, `iOSSupport`).

The Hub would happily add `windows-mono` to the Mac install, and a Mono Win64
player does cross-compile from macOS. That is not the plan, for three reasons:

1. **The determinism gate has to run on Windows anyway.** Every new
   platform/scripting-backend combination must pass `DeterminismSelfCheck` in a
   real player before it ships, and only Windows can run a Windows player.
   Packaging (`Get-FileHash`, robocopy, the version resource) is Windows-side too.
   Cross-compiling would move one step and leave the other two.
2. **The Mac's editor is the gated install.** Every other channel's evidence was
   measured against it; adding a module changes the machine that all of them
   depend on, for a target it will never verify.
3. **heaton-life-unity's Windows channel was proved out this way** (2026-08-21,
   the same Parallels VM, the same editor version), so the path is known-good.

## One-time VM setup

The VM wants roughly **8 GB RAM, 4 vCPU and 60 GB free disk**. For scale: the
Mac's editor install is 28 GB *with* the Android, iOS and WebGL modules (a
Windows-only install is a fraction of that) and this project's `Library/` on the
Mac is 9.5 GB; packaging then briefly holds a second copy of the player.

1. **Unity Hub**, then the editor at **exactly 6000.5.0f1**
   (`ProjectSettings/ProjectVersion.txt`, revision `88b47c5e7076`). A different
   patch release upgrades the project's serialized settings and asset metadata
   on open, which turns into churn in every later diff and can disagree with the
   Mac's gated install. From the Hub UI: Installs → Install Editor → Archive →
   6000.5.0f1. From a command line:

   ```
   "C:\Program Files\Unity Hub\Unity Hub.exe" -- --headless install ^
     --version 6000.5.0f1 --changeset 88b47c5e7076
   ```

2. **Build support.** On a *Windows* host the Windows Standalone (Mono) player is
   part of the base editor — there is nothing extra to add, and the Hub's module
   list for a Windows install offers only *Windows Build Support (IL2CPP)*.
   **Do not install the IL2CPP module.** See
   [Standalone stays Mono](#standalone-stays-mono). (If your Hub does list a
   *Windows Build Support (Mono)* module, add that one and nothing else.)

3. **Sign in to the Hub once** with the Unity account and activate the Personal
   license. A `-batchmode` build on an unlicensed editor fails with a licensing
   error that looks nothing like a build error.

4. **Git for Windows**, configured so a Windows checkout does not rewrite the
   repo:

   ```
   git config --global core.autocrlf input
   git config --global core.longpaths true
   ```

   `core.autocrlf input` keeps the shell scripts and `.ps1` files LF, which is
   how they are checked in (there is no `.gitattributes`); PowerShell reads LF
   scripts perfectly well. `core.longpaths` matters because Unity's `Library/`
   nests past the old 260-character limit. Enable the Windows-side limit too,
   from an elevated PowerShell:

   ```
   Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem' `
     -Name LongPathsEnabled -Value 1
   ```

5. **Optional but worth it:** exclude the checkout from Microsoft Defender
   real-time scanning (Windows Security → Virus & threat protection → Exclusions).
   Unity writes tens of thousands of small files into `Library/`; scanning each
   one roughly doubles a cold build.

## Getting the repo onto the VM

Clone onto the VM's **own disk**. Do not build out of a Parallels shared folder
(`\\Mac\Home\projects\mergelife`): SMB round-trips make Unity's asset import
crawl, file watching is unreliable, and — worse — the Mac and Windows editors
would share one `Library/` and one project path, which is exactly the
one-editor-at-a-time rule that `tools/unity-gate.sh` exists to enforce.

```
git clone https://github.com/jeffheaton/mergelife.git C:\src\mergelife
cd C:\src\mergelife
git checkout <the release commit>
```

Everything below is run from `C:\src\mergelife\unity\heaton-ca`, and every
relative path in this document is relative to that folder.

The VM is a **build machine, not a work machine**: never commit or push from it.
A Unity build legitimately dirties the tree — `Assets/Scripts/BuildInfo.cs` is
stamped before the build (and restored after a *successful* one), and
`Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` collects renderer
list entries. Pull, build, and throw the checkout's changes away:

```
git status --porcelain          # expect BuildInfo.cs / URP settings churn only
git checkout -- .
```

## The build

No editor may have the project open — the same rule the Mac gate enforces. Then:

```
"C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe" -batchmode -quit ^
  -projectPath C:\src\mergelife\unity\heaton-ca ^
  -buildTarget Win64 ^
  -executeMethod CIBuild.Windows ^
  -logFile C:\src\mergelife\unity\heaton-ca\build\logs\build-windows.log
```

`CIBuild.Windows` builds the one enabled scene
(`Assets/Scenes/HeatonCAMainScene.unity`) to
**`build\windows-x64\HeatonCA.exe`**. It calls `ApplyAppleBuildNumber` with
`storeMode: false`, so `HEATONCA_BUILD_NUMBER` is irrelevant here: Windows has no
store build counter and an unset variable only logs `DEV BUILD NUMBER`. The
first build on a fresh clone imports every asset and takes many minutes; later
builds are a fraction of that (the Mac's macOS player, for scale, builds in 35 s
warm).

**The exit-code trap.** `Unity.exe` is a GUI-subsystem executable: launched from
`cmd` or PowerShell it detaches immediately and `%ERRORLEVEL%` / `$LASTEXITCODE`
is meaningless. To gate on the real exit code, hold the process:

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe'
$proj  = 'C:\src\mergelife\unity\heaton-ca'
$p = Start-Process -FilePath $unity -Wait -PassThru -NoNewWindow -ArgumentList @(
    '-batchmode', '-quit',
    '-projectPath', $proj,
    '-buildTarget', 'Win64',
    '-executeMethod', 'CIBuild.Windows',
    '-logFile', "$proj\build\logs\build-windows.log")
"Unity exit $($p.ExitCode)"
```

A good build ends with exit code 0 and these lines in the log:

```
CIBuild StandaloneWindows64: building Assets/Scenes/HeatonCAMainScene.unity -> build/windows-x64/HeatonCA.exe
CIBuild StandaloneWindows64: Succeeded -> build/windows-x64/HeatonCA.exe (<n> bytes, <n> s, <n> warnings)
```

Check the log for `Error building Player` and for `error CS` before believing a
zero exit code — that is the same rule `tools/unity-gate.sh` applies on the Mac.

**Expect `error CS` here even though the Mac's compile gate is green.** Two
pieces of app code live behind `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` and
are compiled by *nothing else in the project* — the Windows half of the native
menu bar (`Assets/Scripts/NativeMenus.Win.cs`) and the Explorer reveal in
`PngExporter.Reveal`. This build is their first compile, so a typo in either
surfaces here and only here. Fix it on the Mac, push, pull on the VM, rebuild:
do not patch the VM's checkout.

The player folder that comes out holds `HeatonCA.exe`, `UnityPlayer.dll`,
`UnityCrashHandler64.exe`, `HeatonCA_Data\` (with `Managed\` — the Mono
assemblies), `MonoBleedingEdge\`, and one folder that must **not** ship:
`HeatonCA_BurstDebugInformation_DoNotShip\`, Burst's debug dump, which every
HeatonCA build on every platform produces. `make-zip.ps1` excludes it; if you
ever hand-zip the folder, exclude it by hand.

## The self-check

The determinism device gate. Run it on the player you just built, before
packaging:

```
powershell -ExecutionPolicy Bypass -File tools\windows-selfcheck.ps1
```

It launches `HeatonCA.exe -batchmode -nographics -selfcheck -logFile
build\logs\win-selfcheck.log`, waits for the player to exit (120 s default,
`-TimeoutSec` to change), and requires **both** a zero exit code and the PASS
line in the log — the same two-part rule as `tools/macos-selfcheck.sh`. A pass
looks like this (the elapsed time varies):

```
windows-selfcheck: exe=C:\src\mergelife\unity\heaton-ca\build\windows-x64\HeatonCA.exe
windows-selfcheck: log=C:\src\mergelife\unity\heaton-ca\build\logs\win-selfcheck.log
windows-selfcheck: running: "...\HeatonCA.exe" -batchmode -nographics -selfcheck -logFile "...\win-selfcheck.log"
[HeatonCA] SELF-CHECK PASS
HeatonCA determinism self-check:
PASS pcg32
PASS mergelife-upstream-1
PASS mergelife-upstream-60
PASS mergelife-soup50
PASS objective-redworld48
windows-selfcheck: PASS player exit=0 check=0 elapsed=4s log=C:\src\...\win-selfcheck.log
```

Exit codes: `0` pass, `1` the self-check said FAIL, `2` no verdict (timeout, or
the player exited 0 without logging one), `3` usage error or the player is
missing/incomplete, `5` environment (not Windows, or the player would not
start). Anything but 0 stops the release: a red check means the platform broke
the engine's integer or floating-point math, and the fix is to diagnose it, never
to ship it.

Two harmless VM prompts on a first run: Windows Firewall may ask about the Unity
player (**deny** — this run needs no network, and the privacy policy's "never
connects on its own" should stay true), and SmartScreen does **not** appear,
because it only guards launches from Explorer, not from a console.

If a driverless VM ever refuses the null graphics device, `-Graphics` drops
`-nographics` from the player's command line — the same escape hatch as
`UNITY_GATE_GRAPHICS=1` on the Mac. Reach for it only after reading the log:
the self-check runs during `Awake`, before anything renders, so a graphics
device is not normally involved.

## The release zip

```
powershell -ExecutionPolicy Bypass -File Packaging\windows\make-zip.ps1
```

Output:

```
build\dist\HeatonCA-2.0.0-windows-x64.zip
build\dist\HeatonCA-2.0.0-windows-x64.zip.sha256
```

The version comes from `bundleVersion` in
`ProjectSettings/ProjectSettings.asset`, so the name follows a version bump
automatically. The zip has one top-level folder,
`HeatonCA-<version>-windows-x64\`, so extracting it into Downloads does not
scatter two hundred files.

The script refuses to package unless all of this holds:

| Check | Why |
| --- | --- |
| `HeatonCA.exe`, `UnityPlayer.dll`, `HeatonCA_Data\`, `globalgamemanagers` all present | an incomplete folder zips silently and fails on the downloader's machine |
| `HeatonCA_Data\Managed\HeatonCAApp.dll` and `HeatonCA.Engine.dll` present, `GameAssembly.dll` and `HeatonCA_Data\il2cpp_data` absent | the Mono tripwire, stated both ways |
| the exe's PE machine type is `0x8664` | an arm64 player must not ship under an x64 name |
| the exe's version resource matches `bundleVersion` | catches a player built before a version bump (a missing resource only warns) |
| `tools\windows-selfcheck.ps1` exits 0 | a player whose engine math disagrees with the conformance vectors is not a release |

It runs the self-check itself. Pass `-ReuseSelfCheck` to accept the log you
already produced — accepted only if it holds the PASS line **and** is newer than
`HeatonCA.exe`, so a verdict on a previous build cannot be recycled.

`*_DoNotShip` and `*_ButDontShipItWithYourGame` folders are excluded from the
staged copy, and the stage is re-scanned afterward in case one slipped through.
`THIRD_PARTY_NOTICES.md` is copied in beside the player: there is no installer
and no license screen on this channel, so the notices ride with the binary.

The `.sha256` is written in `sha256sum` format — lowercase hex, two spaces, the
zip's bare name, one LF, no BOM — so `shasum -a 256 -c` verifies it on the Mac
(`sha256sum -c` on Linux) and `Get-FileHash` matches it, case-insensitively, on
Windows. The script prints the release-note lines to paste.

## Standalone stays Mono

`ProjectSettings/ProjectSettings.asset` sets `scriptingBackend:` with a single
entry, `Android: 1` (IL2CPP). Every other target, Windows included, uses the
default: **Mono**. Keep it that way.

Switching Windows to IL2CPP is not a build-flag change. It is a
**scripting-backend change**, and this project's rule is that any new
platform/scripting-backend combination re-arms the determinism device gate: the
engine's `long`/`double` math has to be re-proved under a different compiler
before anything ships on it. IL2CPP would also drag in the Visual C++ build tools
and a much longer build. The IL2CPP proofs the project already has are Android
(arm64, on the Play_Phone emulator) and the iOS simulator (arm64) — both green,
both recorded in the release runbook. Mono on Windows needs no C++ toolchain at
all, which is why the VM setup above is as short as it is.

If Windows ever does move to IL2CPP: install the module, rebuild, run
`tools\windows-selfcheck.ps1`, and record the verdict as a new line of gate
evidence before packaging. `make-zip.ps1` will refuse the player until its Mono
tripwire is updated deliberately — which is the point.

## SmartScreen and the unsigned exe

HeatonCA for Windows is **not code-signed**. The first time someone runs
`HeatonCA.exe` from Explorer, Microsoft Defender SmartScreen shows

> Windows protected your PC — Microsoft Defender SmartScreen prevented an
> unrecognized app from starting.

with a **Run anyway** button behind **More info**. This is expected for any
unsigned executable that has no download reputation yet, and it is not a virus
warning.

**The mitigation is publishing the SHA-256** that `make-zip.ps1` writes, in the
GitHub release notes next to the download, with the command to check it:

```
Get-FileHash -Algorithm SHA256 .\HeatonCA-2.0.0-windows-x64.zip
```

Tell users to **Unblock the zip before extracting** (right-click → Properties →
Unblock, or `Unblock-File .\HeatonCA-*.zip`). A downloaded zip carries the Mark
of the Web, Explorer copies that mark onto every extracted file, and a marked
`HeatonCA.exe` re-triggers the prompt on every launch instead of just the first.

An Authenticode certificate would remove the prompt (an EV certificate removes it
immediately; a standard one only after the download builds reputation), but it is
a **new recurring expense and is out of scope** for this release. Do not treat
that as a decision to revisit casually — revisit it when Windows downloads are
numerous enough to justify the annual cost.

## Where the app keeps data

There is no installer, so there is no uninstaller: "uninstalling" is deleting the
extracted folder. Two things are **not** in that folder and survive it.

| What | Where | Survives folder deletion |
| --- | --- | --- |
| Snapshots (Save PNG), the evolve finds store, `Player.log` | `%USERPROFILE%\AppData\LocalLow\Jeff Heaton\HeatonCA\` | yes |
| Settings — cell size, speed, FPS overlay (`PlayerPrefs`) | `HKCU\Software\Jeff Heaton\HeatonCA` | yes |

`Application.persistentDataPath` on Windows is
`%USERPROFILE%\AppData\LocalLow\<companyName>\<productName>`, which is
`...\Jeff Heaton\HeatonCA` for this app (`companyName` and `productName` are
pinned by `ProjectIdentity`), and `PlayerPrefs` on Windows is a registry key, not
a file. Deleting the app folder leaves both behind; a fresh download picks the
old settings and finds right back up.

Say so plainly in `docs/privacy.md` and the manual: on Windows, "deleting the app
deletes this data" is **not** true, and the two paths above are what a user has
to remove by hand to erase everything. Nothing leaves the machine either way —
the app has no network code and no analytics.

## Release checklist

1. On the Mac, everything green through `docs/release-runbook.md`; the release
   commit pushed.
2. On the VM: `git fetch && git checkout <release commit>`, confirm
   `git status --porcelain` is empty before building.
3. Build (`CIBuild.Windows`), exit code 0, no `Error building Player` in the log.
4. `tools\windows-selfcheck.ps1` → PASS.
5. **Launch the player by hand once.** The headless self-check proves the engine,
   not the app: open the window, run the simulator, open the gallery, save a PNG,
   and check the About screen shows version `2.0.0` and a build stamp that is not
   `dev`. The window opens at 1280×800 and is resizable (`fullscreenMode: 3`,
   windowed). Exercise the two Windows-only code paths deliberately, because no
   other platform's gate has ever run them: **Save PNG** (the file lands in
   `...\LocalLow\Jeff Heaton\HeatonCA\Snapshots\` and `PngExporter.Reveal` then
   runs `explorer.exe /select,<path>`) and the **native menu bar** from
   `NativeMenus.Win.cs` — File → Settings…, File → Exit, Help → Tutorial,
   Help → About. Both sit behind `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR`.
6. `Packaging\windows\make-zip.ps1` → zip + `.sha256`.
7. Copy both files off the VM, verify the hash on the Mac
   (`shasum -a 256 -c HeatonCA-<ver>-windows-x64.zip.sha256`).
8. Attach the zip to the GitHub release, paste the hash and the SmartScreen note
   into the release notes, and update the Windows row in the repo's
   `binaries.md`.
9. Record the zip's size and SHA-256 with the release's other artifact hashes.

## Troubleshooting

| Symptom | Cause and fix |
| --- | --- |
| Unity exits at once, log mentions licensing | The editor is not activated on the VM. Sign in through the Hub once, then rerun. |
| `Multiple Unity instances cannot open the same project` | An editor has the project open, or `Temp\UnityLockfile` is stale. Close the editor; delete the lockfile only after confirming no `Unity.exe` is running. |
| `%ERRORLEVEL%` is 0 but there is no player | `Unity.exe` detached from the console. Use the `Start-Process -Wait -PassThru` form above and read `$p.ExitCode`. |
| Build fails only on the VM, with `UnityEditor.iOS` / `UnityEditor.Android` errors | An editor script referenced a platform module the VM lacks. Every such `using` in `Assets/Editor` must sit behind a `#if UNITY_IOS` / `#if UNITY_ANDROID` guard — this is exactly how heaton-life's first Windows build died. |
| `windows-selfcheck: player incomplete: ... UnityPlayer.dll is missing` | A partial or moved player folder. Rebuild; do not copy the exe out of its folder. |
| `windows-selfcheck` exit 2, empty log | The player never reached Unity. Check the exe is not blocked (`Get-ChildItem -Recurse build\windows-x64 \| Unblock-File`) and that the log path is on the VM's own disk, not a `\\Mac\...` share. |
| `windows-selfcheck` exit 5, "could not be started" | Wrong architecture for the VM (an arm64 player on an x64 VM or the reverse), or an antivirus quarantine. `Packaging\windows\make-zip.ps1` reports the exe's PE machine type. |
| `does not look like a Mono player` / `is an IL2CPP player` | The Windows scripting backend was switched to IL2CPP. See [Standalone stays Mono](#standalone-stays-mono) — this is a deliberate stop, not a bug. |
| `HeatonCA.exe reports version ... but ProjectSettings says ...` | The player predates a `bundleVersion` bump. Rebuild. |
| `robocopy failed (8)` or worse | Something holds the staged files open — usually the app itself, launched from `build\dist\...`. Close it and rerun. |
| Long-path errors during import or build | `core.longpaths` / `LongPathsEnabled` were not set. See [one-time VM setup](#one-time-vm-setup), then delete `Library\` and re-import. |
| The tree is dirty after a *failed* build | `BuildInfoGenerator` restores `Assets/Scripts/BuildInfo.cs` only after a success. `git checkout -- Assets/Scripts/BuildInfo.cs`, or run the editor once with `-executeMethod BuildInfoGenerator.Reset`. |
