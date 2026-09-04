# HeatonCA on the web

The WebGL build is the fifth HeatonCA target and the only one with no store
review: it is a folder of static files that replaces the old JavaScript viewer
in `js/web-full-screen/`. This file is the release-side record — where it goes,
how to publish it, how to prove it works afterward, and the caveats worth
telling anyone who links to it. The header mechanics (why `.unityweb`, what each
`Content-Type` is, what a tuned host gains) live in
`Packaging/webgl/README.md`; this is the operational half.

## Where it is hosted

> **PLACEHOLDER — not yet decided.** No bucket, distribution, or path has been
> registered for this build. Fill in the three rows below before the first
> publish and delete this note.

| Thing | Value |
| ----- | ----- |
| Public URL | `https://www.heatonresearch.com/mergelife/app/` *(PLACEHOLDER)* |
| Upload destination | `s3://<bucket>/<prefix>/` *(PLACEHOLDER)* |
| CDN distribution | CloudFront `<distribution-id>` *(PLACEHOLDER, or none)* |

Two constraints on whatever gets chosen:

- **HTTPS.** The Copy buttons use the async clipboard API, which browsers only
  expose in a secure context; on plain HTTP the app falls back to the legacy
  `execCommand` copy and Safari may refuse outright.
- **A stable path**, because the URL is what the tutorial page and every
  `?rule=` link point at. Moving the site to a new origin also strands every
  visitor's saved settings and finds (see "Everything lives in the browser").

The predecessor viewer sat under `/mergelife/` on heatonresearch.com
(`ml-fullscreen.html`, a small form that submitted to `ml-fullscreen2.html`);
keeping the Unity build in the same neighborhood keeps those old inbound links
one redirect away.

## Publishing

```sh
Packaging/web/publish-webgl.sh                                   # dry run: the manifest
Packaging/web/publish-webgl.sh --dest s3://<bucket>/<prefix> --confirm
```

The script uploads `build/webgl/` (the output of `tools/unity-gate.sh
build-webgl`) file by file with the right `Content-Type`, `Content-Encoding`,
and `Cache-Control` on each object. Points that matter at release time:

- **A run without `--confirm` uploads nothing.** It prints the manifest —
  every file, its size, and the three headers it would get — and exits. That
  dry run is part of Gate 4, so it is always current.
- **`HeatonCA_BurstDebugInformation_DoNotShip/` is excluded by wildcard.** Burst
  writes that folder on every build; it is internal symbol text and must never
  reach the public site. The manifest lists what it dropped.
- **The build must be HeatonCA.** The script refuses when
  `build/webgl/index.html` is missing or its `<title>` is not `HeatonCA`, which
  is the cheap way to catch a stale or foreign folder left in `build/webgl`.
- **`index.html` is uploaded last** and is the only `no-cache` file, so it is
  the switch that flips the site to the new build. Everything under `Build/` and
  `TemplateData/` is cached for a year and marked `immutable`.
- **Payload names do not carry a hash** (`webGLNameFilesAsHashes` is 0), so an
  immutable redeploy over the *same* prefix would leave returning visitors on
  the old payloads. Either publish each version under its own prefix
  (`.../heatonca/2.0.0/`) and repoint the page, or redeploy flat with
  `--no-immutable` and invalidate the CDN.
- Uploading needs network access, so it must run with the Claude Code Bash
  sandbox **disabled**. The dry run is offline.
- The script also accepts a local directory as the destination, which is a plain
  mirror with no headers — useful when the host has its own upload tool. The
  build still works that way; see "Content-Encoding" below.

At 2.0.0 the site is 10 files and about 17 MB on disk: `webgl.wasm.unityweb`
11.1 MB and `webgl.data.unityweb` 6.7 MB dominate, both already gzip-compressed.

## Verifying after a publish

1. **Load the page.** The dark loading bar appears, then the Home screen. A
   cold visit downloads ~17 MB, so give it a moment on a slow link.
2. **Open the browser console** (F12, Console tab) and look for the determinism
   self-check the app logs on every start:

   ```
   [HeatonCA] SELF-CHECK PASS
   ```

   That single line is the proof that the engine produced byte-identical
   results after Unity compiled it to WebAssembly — the same check
   `tools/webgl-smoke.sh` gates on locally. `SELF-CHECK FAIL` means do not
   announce the build. The About screen shows the same verdict without the
   console.
3. **Try a deep link**, which exercises the URL plumbing and the simulator in
   one step:

   ```
   https://<host>/?rule=e542-5f79-9341-f31e-6c6b-7f08-8773-7068
   ```

   The page should open straight into the Simulator running the red world, and
   the console should carry `[HeatonCA] url ?rule=e542-...`.
4. **Check the headers actually landed** (skip on a host that cannot set them):

   ```sh
   curl -sI https://<host>/Build/webgl.wasm.unityweb | grep -i 'content-type\|content-encoding\|cache-control'
   ```

   Expect `application/wasm`, `gzip`, and the year-long cache line. If
   `Content-Encoding` is absent the site still works — it just starts slower.
5. **Look at one more browser.** Chrome, Safari, and Firefox on the desktop are
   the supported set. `[UnityCache] Could not connect to cache: Database
   timeout` in a private window is normal and harmless.

## Deep links: the contract

The build honors the same three query parameters as the JavaScript viewer it
replaces (`js/web-full-screen/ml-fullscreen2.js`), so every page that already
embeds a MergeLife rule by URL keeps working against the Unity build.

| Parameter  | Meaning | Accepted | When absent or invalid |
| ---------- | ------- | -------- | ---------------------- |
| `rule`     | MergeLife rule to run | 8 groups of 4 hex digits, dashed or not, either case | Home screen |
| `size`     | cell size in logical pixels | a whole number, 1 to 25 | the saved setting |
| `controls` | simulator toolbar | `on` / `1` / `true`, `off` / `0` / `false` | `on` |

```
https://<host>/?rule=e542-5f79-9341-f31e-6c6b-7f08-8773-7068&size=5&controls=off
```

Differences from the old viewer, all deliberate:

- The old page **defaulted `controls` to off** (its embedding pages always
  passed the parameter explicitly); the app defaults to **on**, because a
  visitor who arrives with a bare `?rule=` should be able to steer. Both values
  are still honored, so `controls=off` reproduces the old kiosk look exactly.
- A malformed rule used to render nothing. Here it falls back to the Home
  screen rather than showing an empty canvas.
- A `size` outside the app's 1-25 range is **refused**, not clamped: the
  visitor's saved cell size is a better answer than an arbitrary 25.
- Unknown parameters are ignored, a repeated parameter takes its last value, and
  a `#fragment` is not treated as a query.

Parsing is pure C# (`Assets/Scripts/UrlParams.cs`) and pinned by the EditMode
suite, so these rules hold on every platform, not just in a browser.

## Everything lives in the browser

The web build has no account, no server, and no cloud copy. Settings and evolve
finds go to `PlayerPrefs`, which Unity backs with **IndexedDB in the visitor's
browser profile**. Say this plainly to anyone who might invest time in a search:

- Data is per browser, per profile, per origin. Another browser, another
  machine, or a private window starts empty. **Nothing syncs**, not even
  between two tabs on two of the user's own devices.
- **Clearing site data erases it**, as does the browser evicting storage under
  pressure. So does moving the site to a different host name.
- The browser-side budget is roughly 1 MB, so the finds gallery is capped at 60
  finds with small derived thumbnails.
- `OnApplicationQuit` never fires in a browser. The page persists on
  `beforeunload` and when the tab is hidden; IndexedDB writes are asynchronous,
  so a tab killed by the OS at that instant can still lose the last change.
- **Save PNG is the export.** A find that matters should be downloaded, or
  reproduced natively — the same rule hex pasted into the iOS, Android, macOS,
  or Windows app renders the identical world.

## Mobile browsers are not supported

Unity does not officially support mobile browsers for WebGL, and this project
does not try to. The page loads on a phone anyway, fills the viewport, and shows
a dismissible banner:

> HeatonCA runs best in a desktop browser.

Memory limits, missing WebGL features, and the wasm download over cellular are
the reasons; none of them are fixable from inside the build. Phones and tablets
get the native iOS and Android apps, which are faster, persist properly, and can
save to the photo library. Treat a mobile bug report against the web build as a
pointer to the native app, not as a defect.

## Content-Encoding: works everywhere, faster when tuned

The build ships with **Compression Format: Gzip and Decompression Fallback ON**.
Unity gzips the framework, wasm, and data files, names them `.unityweb`, and
puts a JavaScript gunzip in the loader. On load the loader checks whether the
browser already inflated the response:

- Host **sent `Content-Encoding: gzip`** -> the browser decompressed it natively
  and the loader uses the bytes directly. Fastest start.
- Host **sent no encoding header** -> the loader sees the gzip magic bytes and
  inflates them in JavaScript. Slower to start, and it logs one console line
  suggesting the header, but it works.

That is the deliberate trade: the site cannot be broken by a host that ignores
compression (GitHub Pages, a plain bucket, a shared web host), and it gets
faster on one that honors it. `publish-webgl.sh` sets the header for S3, so a
publish through it lands on the fast path.

Two things that *do* break it: a host that gzip-compresses the already-gzipped
files a second time (double compression defeats the magic-byte check), and
turning Decompression Fallback off — the files then become `.gz`, the header
stops being optional, and a host that omits it fails with "WebAssembly
streaming compilation failed". Neither is worth doing.

Threads are off, so no `Cross-Origin-Opener-Policy` or
`Cross-Origin-Embedder-Policy` headers are needed and the site works from any
origin.
