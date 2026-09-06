# MergeLife

Guidance for Codex and other AI assistants working in this repository.

## Language and spelling

- Use American English spellings everywhere: identifiers, comments, log/UI
  strings, commit messages, and documentation. Prefer `color`, `gray`,
  `center`, `behavior`, `neighbor`, `traveled`, `favorite`, `canceled`,
  `analyze`, `normalize`, `initialize`, `license`, `defense`, `modeling`,
  `labeled` over their British forms.
- The only exception is an external API or vendored dependency that dictates
  the spelling — mirror those exactly and do not "fix" them. In this
  repository that covers the vendored Bootstrap assets under
  `js/viewer-web/css/` and `js/viewer-web/js/bootstrap.min.js`, which contain
  British forms and must be left byte-for-byte as shipped upstream.

## Unity app (unity/heaton-ca)

HeatonCA, the MergeLife app for iPhone, iPad, macOS, Android, Windows, and
the web. `unity/heaton-ca/README.md` and `unity/heaton-ca/tools/README.md`
carry the full protocol; these are the rules that bite hardest.

- **Spelling.** The American English rule above applies here too: C#
  identifiers, comments, UI text, and the documents under
  `unity/heaton-ca/docs/` and `unity/heaton-ca/store/`.
- **One Unity editor per project path.** Never open the project in the Unity
  Hub while gates may run and never launch `Unity -batchmode` by hand; two
  editors against one `Library/` corrupt it. Every gate goes through
  `unity/heaton-ca/tools/unity-gate.sh <compile|editmode|playmode|build-*>`,
  which takes a lock and refuses when an editor already has the project open.
  In Codex that script must run with the Bash sandbox **disabled**:
  Unity needs its licensing IPC and its caches under `~/Library`, and
  sandboxed runs fail with misleading license errors. Agents write code and
  `.meta` files; the gates are run serially on the main checkout.
- **`.meta` discipline.** Every file and folder under `Assets/` has a sibling
  `.meta` — an asset committed without one gets a fresh GUID on import, which
  breaks every reference to it. Mint the `.meta` for text assets you author
  (`.cs`, `.asmdef`, `.md`, `.json`, `.jslib`, folders) in the same change
  with `tools/new-meta.sh`; let Unity generate the ones for binary and
  native assets (`.png`, `.unity`, `.asset`, `.inputactions`) and stage them
  after the next gate with `tools/meta-sweep.sh --stage`. Folders whose name
  ends in `~` are invisible to Unity and take none.
- **`Assets/Engine/` is not ours to edit.** It is a verbatim copy of
  heaton-life's `HeatonLife.Core`, written by `tools/import-engine.sh`
  (provenance header, namespace renamed to `HeatonCA.Engine`, `ToPng`
  removed) and enforced by `tools/engine-sync-check.sh`, which allows that one
  difference and nothing else. Its numerics are pinned by
  `conformance/vectors.txt`, the same vectors the Python, JavaScript, Java,
  and C engines replay, so "cleaning up" the engine is a conformance break.
  Fix it upstream and re-import.
- **Platform seams.** Every `#if UNITY_*` in runtime code lives in
  `EvolveScheduler.cs` (threaded versus single-threaded search),
  `AppStore.cs` (file storage versus PlayerPrefs), `PngExporter.cs` (photo
  gallery, browser download, or file), `WebGlBridge.cs` (the jslib externs),
  and `NativeMenus*.cs` (desktop menu bars). No other file gets a platform
  define. Editor scripts that touch `UnityEditor.iOS`, `.Android`, or macOS
  carry the matching guard, including the `using`, so the project still
  builds on a machine without that module installed.
- **Strings.** Every user-visible string lives in
  `Assets/Scripts/AppStrings.cs`; views never spell text inline. Numeric
  readouts format with `CultureInfo.InvariantCulture`.
- **The determinism self-check ships.** Every player build runs
  `DeterminismSelfCheck` at startup and logs
  `[HeatonCA] SELF-CHECK PASS`; `-selfcheck` makes the player exit with the
  verdict, which is what the `tools/*-selfcheck.sh` device gates and
  `tools/webgl-smoke.sh` read. A red check means the backend broke the
  math — usually a new IL2CPP target. Diagnose it; do not ship it.
- **Build churn not to chase.** `Assets/Scripts/BuildInfo.cs` is rewritten
  before every player build and restored to its `dev` placeholder afterward,
  and `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` renumbers
  its `rid:` entries whenever the editor rewrites it. Revert those instead of
  committing them. `Library/`, `Temp/`, `Logs/`, `build/`, and the generated
  `.csproj` / `.slnx` files are git-ignored.
