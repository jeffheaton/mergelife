# HeatonCA Unity tooling

Scripts under `unity/heaton-ca/tools/` that keep the Unity port buildable
while several agents write into the same checkout. Paths below are relative to
`unity/heaton-ca/` unless they start with `/`.

## The protocol in one paragraph

Agents write code and hand-authored `.meta` files; they **never run Unity**.
After a phase is merged, the orchestrator runs the gates **serially on the
main checkout** with `tools/unity-gate.sh <mode>`, then runs
`tools/meta-sweep.sh --stage` so the `.meta` files Unity generated travel with
their assets. Nothing here commits: commits happen only when Jeff authorizes
them for the run.

## Gates: `tools/unity-gate.sh`

```
tools/unity-gate.sh <compile|editmode|playmode|build-macos|build-ios|build-ios-sim|build-android|build-android-aab|build-webgl> [-- extra unity args]
```

- **Sandbox.** In Claude Code this script must run with the Bash sandbox
  **disabled** (`dangerouslyDisableSandbox`, or allow it through `/sandbox`).
  Unity needs its licensing IPC, its preferences and caches under `~/Library`,
  and the platform toolchains; sandboxed runs fail with misleading license or
  "project already open" errors.
- **Editor binary.** `UNITY` (default
  `/Applications/Unity/Hub/Editor/6000.5.0f1/Unity.app/Contents/MacOS/Unity`,
  matching `ProjectSettings/ProjectVersion.txt`).
- **Modes.** Every mode adds `-batchmode -nographics -projectPath <project>
  -logFile <log>`. `compile` runs `-quit -executeMethod
  HeatonCA.Editor.CompileGate.Run` and passes only when the log contains
  `COMPILE OK`. `editmode` / `playmode` run `-runTests -testPlatform
  EditMode|PlayMode -testResults <xml>`. The `build-*` modes run `-quit
  -buildTarget <target> -executeMethod CIBuild.<Method>` (macOS
  `OSXUniversal`/`MacOS`; iOS `IOS` and `IOSSimulator`; Android `Android` and
  `AndroidPlayStore`; WebGL `WebGL`).
- **Verdict.** The gate fails (exit 1) when Unity exits non-zero, the log
  contains `error CS`, a compile log lacks `COMPILE OK`, a build log contains
  `Error building Player`, the NUnit results have `@failed != 0`, or
  `@total < MIN_TESTS`. On failure it prints the reasons, the first failed test
  names, and the last 40 lines of the log. On success it prints one summary
  line: mode, Unity exit code, tests total/failed, log path.
- **`MIN_TESTS`.** Minimum `/test-run/@total` for the test modes, default 1.
  The plan pins it per gate: Gate 1 `editmode` `MIN_TESTS=55`, Gate 2 and 3
  `editmode` `MIN_TESTS=95`, Gate 3 `playmode` `MIN_TESTS=30`. A run that
  silently compiles zero tests is a failure, not a pass.
- **Logs.** `build/logs/<mode>-<utcstamp>.log` and, for test modes,
  `build/logs/<mode>-<utcstamp>.xml`; `<mode>-latest.log` / `.xml` symlinks
  point at the newest run. `build/` is git-ignored.
- **Exit codes.** 0 pass; 1 gate failed; 2 usage; 3 another gate holds the
  lock; 4 the project is open in an editor; 5 environment (Unity binary or
  `xmllint` missing, or `pgrep` cannot read the process list, which is the
  symptom of running sandboxed).

## One Unity instance, ever

Unity refuses to open a project that another editor has open, and two
batchmode runs against one `Library/` corrupt it. Three checks enforce the
rule, and the gate refuses rather than waits:

1. An atomic `mkdir build/.unity-gate.lock` (exit 3 if held). It lives inside
   the project, not under `TMPDIR`, because Claude Code gives sandboxed and
   unsandboxed shells different temp directories. The lock records the holder's
   pid; a lock whose pid is no longer running is removed as stale. The lock is
   removed on exit, including `Ctrl-C`.
2. `Temp/UnityLockfile` present (exit 4): an editor has the project open, or
   crashed and left the file behind. Delete it only after confirming no Unity
   process is running.
3. `pgrep -f 'projectPath.*unity/heaton-ca'` matches a running editor
   (exit 4). `UNITY_GATE_SKIP_PGREP=1` skips only this check.

Consequences for agents: never open `unity/heaton-ca` in the Unity Hub while
gates may run, never launch `Unity -batchmode` yourself, and never run two
gates at once (the lock will refuse the second one anyway).

## `.meta` discipline

Unity references assets by the GUID in their `.meta`. An asset committed
without its `.meta` gets a fresh GUID on import, which breaks every scene,
prefab, and ProjectSettings reference to it and produces churn in the next
sweep. Rules:

- Every file and folder under `Assets/` has a sibling `.meta`. Folders that
  already have one (`Assets/Resources`, `Assets/Resources/Icons`,
  `Assets/Scenes`, `Assets/Settings`) must not get a second one.
- Text assets you author (`.cs`, `.asmdef`, `csc.rsp`, `.md`, `.txt`, `.json`,
  `.jslib`, and the folders holding them) get a hand-written `.meta` from
  `tools/new-meta.sh <path> [more paths]` at the same time as the asset. It
  picks the importer block by extension (`MonoImporter` for `.cs`,
  `AssemblyDefinitionImporter` for `.asmdef`, `folderAsset: yes` for folders,
  `DefaultImporter` otherwise), mints a random 32-hex GUID with `uuidgen`,
  and refuses to overwrite an existing `.meta`. `--stdout` prints instead of
  writing; `--guid <32 hex>` pins a reserved GUID.
- Binary and Unity-native assets (`.png`, `.unity`, `.asset`,
  `.inputactions`, ...) carry importer settings only the editor can generate.
  Do not hand-write those; the next gate run creates them and
  `tools/meta-sweep.sh --stage` stages them.
- `tools/meta-sweep.sh` (dry run by default) lists `.meta` files under
  `Assets/` and `Packages/packages-lock.json` that git sees as untracked,
  modified, or deleted, plus warnings for assets without a `.meta` and `.meta`
  files without an asset. `--stage` runs `git add` on the listed files;
  `--strict` makes the warnings fatal. It never commits.
- Folders whose name ends in `~` (for example `Assets/Tests/Vectors~/`) are
  invisible to Unity and take no `.meta`; the sweep skips them.
- Reserved GUIDs that must not change: scene
  `5b8e2f0c4a6d41f7b9c3e1d5a7f0b2c4` (`Assets/Scenes/HeatonCAMainScene.unity`),
  input actions `052faaac586de48259a63d0c4782560b`
  (`Assets/HeatonCAInput.inputactions`, referenced by `preloadedAssets`),
  volume profile `10fc4df2da32a41aaa32d77bc913491c`
  (`Assets/Settings/HeatonCAVolumeProfile.asset`), and the script GUID
  `7d3a9c41e5f24b8a9c6d1e0f2a4b6c8d` reserved for
  `Assets/Scripts/AppController.cs.meta` so the copied scene's `App` object
  binds (written in WP1.7 with `tools/new-meta.sh --guid`).

Hand-written `.meta` template (Unity may normalize the importer block later;
the GUID is what matters):

```yaml
fileFormatVersion: 2
guid: <32 lowercase hex>
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
```

## Seeding: `tools/bootstrap.sh`

Documents, replays, and audits the one-time seeding of `unity/heaton-ca` from
the sibling `~/projects/heaton-life-unity` project (ProjectSettings, package
manifest with NativeGallery instead of NativeFilePicker, URP settings, the
renamed scene, input actions, and volume profile with their GUIDs kept, the
menu icon, and the HeatonCA identity edits). It refuses to run over an
existing `ProjectSettings/ProjectSettings.asset` unless `--force` is given,
never runs Unity, and never edits `/.gitignore`. `--verify` audits the current
tree; `--dry-run` prints the recipe; `--project <dir>` replays into a scratch
directory. The header comment of the script is the recipe of record.

## Conventions

- Scripts are `bash`, `set -euo pipefail`, `bash -n` clean, and run under the
  macOS default `/bin/bash` 3.2 (no associative arrays, no `mapfile`).
- American English everywhere: identifiers, comments, log strings, and this
  file.
- Report file paths absolutely in gate output so an orchestrator reading the
  transcript can open them.

## Engine provenance scripts

- `tools/import-engine.sh` copies the seven `HeatonLife.Core` files from the sibling
  `heaton-life` checkout (or `HEATONCA_UPSTREAM_ENGINE`) into `Assets/Engine/`, adding the
  provenance header, the namespace rename, and the single recorded modification (`ToPng`
  removed). Update the pinned commit in `Assets/Engine/PROVENANCE.md` when re-syncing.
- `tools/engine-sync-check.sh` reverses the header and namespace and diffs against upstream;
  exit 0 only when the sole difference is the `ToPng` removal. `--self` checks headers only
  when no upstream checkout exists (CI without the sibling repo).

## Device gates (run with the sandbox disabled, after the matching `build-*` gate)

| Script | Proves | Needs |
|---|---|---|
| `tools/macos-selfcheck.sh` | Mono macOS player prints `[HeatonCA] SELF-CHECK PASS` and exits 0 under `-selfcheck` | `build/macos/HeatonCA.app` |
| `tools/android-selfcheck.sh` | IL2CPP arm64 player passes on the `Play_Phone` AVD (boots it headless if no device is attached; `--probe` lists the environment) | `build/android/HeatonCA.apk`, Android SDK + emulator |
| `tools/ios-sim-selfcheck.sh` | IL2CPP simulator build passes on an iPhone simulator (`--probe` lists runtimes) | `build/ios-sim/Unity-iPhone.xcodeproj`, Xcode |
| `tools/webgl-smoke.sh` | wasm build loads in headless Chrome, honors `?rule=`, prints PASS | `build/webgl`, Google Chrome |
| `tools/windows-selfcheck.ps1` | Mono Windows player passes (run on the Windows VM) | `build\windows-x64\HeatonCA.exe` |

`tools/check-selfcheck.sh` is the shared verdict parser (exit 0 PASS, 1 FAIL, 2 timeout).

## Lint

- `tools/spelling-check.sh` — the repository's American-English rule plus two glyph rules:
  no British spellings anywhere; no non-ASCII in `Assets/Scripts` code (comments and test data
  are exempt, since neither renders); and `Assets/Scripts/AppStrings.cs`, the single home of every
  user-visible string, must be ASCII apart from the three Greek rule-decoder headers
  (U+03B1/03B2/03B3), each of which has an ASCII fallback constant beside it. Run it before a
  commit; CI runs it in the always-on `unity-lint` job.
