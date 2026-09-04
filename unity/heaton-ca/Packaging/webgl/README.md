# HeatonCA WebGL: hosting notes

`tools/unity-gate.sh build-webgl` (`CIBuild.WebGL`) writes a static site to
`unity/heaton-ca/build/webgl/`:

```
build/webgl/
  index.html                 from Assets/WebGLTemplates/HeatonCA (title "HeatonCA",
                             full-viewport canvas, dark ground, mobile banner)
  TemplateData/              style.css, favicon.png, apple-touch-icon.png,
                             the two progress-bar images
  Build/
    webgl.loader.js       plain JavaScript, never compressed
    webgl.framework.js.unityweb
    webgl.wasm.unityweb
    webgl.data.unityweb
  StreamingAssets/           only if the project ever adds streaming assets
```

Upload the folder as is. There is no server-side code.

## Compressed artifacts and the `.unityweb` suffix

The project builds with **Compression Format: Gzip** and **Decompression
Fallback: on** (`ProjectIdentity.Apply` pins both; `CIBuild.WebGL` re-applies
them). Unity therefore gzips the framework, wasm, and data files and names
them `.unityweb` rather than `.gz`, and the loader carries a JavaScript gunzip.
On load it looks at the first bytes of each response:

- If the server sent `Content-Encoding: gzip`, the browser has already
  inflated the bytes and the loader uses them directly. Fastest.
- If the bytes still start with the gzip magic, the loader inflates them
  itself (the fallback) and prints one console line suggesting the header.
  Slower to start, but it works on any static host with no configuration:
  GitHub Pages, a plain S3 bucket, a shared web host.

So the build cannot be broken by a host that ignores compression, and it can
be made faster by one that honors it. If Decompression Fallback is ever
turned off, the files become `.gz` and the `Content-Encoding` header stops
being optional; the loader then fails with a "WebAssembly streaming
compilation failed" banner on a host that omits it.

Local check of both paths (Chrome, headless, prints the app's console lines):

```
tools/webgl-smoke.sh                                   # fallback path, no header
python3 tools/webgl-serve.py --content-encoding        # tuned-host path, then open
                                                       # http://127.0.0.1:8765/ by hand
```

## Optional headers for a tuned host

For each file under `Build/`:

| File                             | `Content-Type`             | `Content-Encoding` (optional) |
| -------------------------------- | -------------------------- | ----------------------------- |
| `*.loader.js`                    | `application/javascript`   | none                          |
| `*.framework.js.unityweb`        | `application/javascript`   | `gzip`                        |
| `*.wasm.unityweb`                | `application/wasm`         | `gzip`                        |
| `*.data.unityweb`                | `application/octet-stream` | `gzip`                        |
| `*.symbols.json.unityweb`        | `application/json`         | `gzip` (debug builds only)    |

Rules of thumb:

- Never let the host gzip-compress on the fly a file that is already gzipped;
  double compression breaks the fallback's magic-byte check.
- `Content-Encoding` without a matching `Content-Length` of the *compressed*
  size confuses some CDNs; upload the compressed bytes and set the header,
  do not recompress.
- Threads are off (`webGLThreadsSupport 0`), so no `Cross-Origin-Opener-Policy`
  or `Cross-Origin-Embedder-Policy` headers are required, and the site works
  from any origin, including `file://`-free local servers such as
  `tools/webgl-serve.py`.
- Cache `Build/*` aggressively only if the file names carry a hash
  (`webGLNameFilesAsHashes` is 0 here, so they do not): give `Build/*` and
  `index.html` a short max-age or `no-cache`, and rely on Unity's own
  IndexedDB data cache (`webGLDataCaching 1`) for repeat visits.
- Serve over HTTPS. The clipboard API the Copy buttons use requires a secure
  context; on plain HTTP the app falls back to the legacy `execCommand` copy.

`Packaging/web/publish-webgl.sh` (Phase 4) uploads with these headers to S3;
until then any static host that serves the folder works.

## Persistence lives in the browser

The WebGL player stores settings and evolve finds in `PlayerPrefs`, which
Unity backs with IndexedDB in the browser profile (`AppStore.CreateDefault()`
picks `PrefsStore` on WebGL). Consequences to tell users and testers:

- Data is per browser, per profile, per origin. Moving the site to a new host
  name, or opening it in another browser or a private window, starts empty.
- Clearing site data, or the browser evicting storage under pressure, erases
  it. There is no account and no cloud copy; the Save PNG button is the export.
- The browser-side budget is roughly 1 MB, so the finds store is capped at 60
  finds with derived thumbnails (the EditMode size test keeps the serialized
  store under 512 KB).
- `OnApplicationQuit` never fires in a browser. The page calls
  `SendMessage("App", "OnBeforeUnload")` on `beforeunload` and when the tab is
  hidden (`Assets/Plugins/WebGL/HeatonCA.jslib`), and the app persists and
  calls `PlayerPrefs.Save()` there. IndexedDB writes are asynchronous, so a
  tab killed by the OS in the same instant can still lose the last change;
  closing the tab normally does not.

## Query parameters (deep links)

The page honors the same parameters as the original full-screen web viewer
(`js/web-full-screen/ml-fullscreen2.js`):

| Parameter  | Meaning                                                     | Default             |
| ---------- | ----------------------------------------------------------- | ------------------- |
| `rule`     | MergeLife rule as 8 groups of 4 hex digits, dashed or not    | the app's home page |
| `size`     | cell size in logical pixels (1 to 25)                        | the saved setting   |
| `controls` | `on` shows the simulator toolbar, `off` hides it            | `on`                |

Example: `https://<host>/heatonca/?rule=e542-5f79-9341-f31e-6c6b-7f08-8773-7068&size=5&controls=off`

`?rule=` opens the simulator running that rule; a malformed rule falls back to
the home page. Both `controls` values are honored; the app default is `on`
(the old viewer defaulted to `off`, but its embedding pages always passed the
parameter). The build logs the query string it saw as
`[HeatonCA] url ?rule=...`, which `tools/webgl-smoke.sh` echoes.

## What the WebGL build does not do

- **No threads.** The evolve search runs on the main thread in 8 ms slices
  per frame (`MainThreadChunkRunner`), at least one evaluation per frame, so
  a long-lived rule can hold a frame for a while; the Evolve screen says so
  ("Browser mode"). Enabling threads would require the COOP/COEP headers
  above and SharedArrayBuffer support, and Unity's threaded WebGL is still
  experimental; the project does not use it.
- **Mobile browsers are unsupported.** Unity does not support them
  officially. The page loads anyway, shows a dismissible banner ("HeatonCA
  runs best in a desktop browser."), and fills the viewport, but memory limits
  and missing WebGL features on phones are not the project's problem to fix.
  Phones and tablets get the native iOS and Android apps.
- **No file system.** Save PNG hands the browser a download (`Blob` plus an
  anchor click); there is no folder mirror and no reveal-in-Finder.
- **Popups.** Links open with `window.open` inside the click handler so popup
  blockers allow them; when one is blocked anyway the console shows
  `[HeatonCA] popup blocked: <url>` and the smoke test fails.
- **Exceptions.** Built with `ExplicitlyThrownExceptionsOnly`: a managed
  exception the app throws is reported; a null reference in engine code
  aborts the page with the Unity error banner. The determinism self-check
  runs on every start and logs `[HeatonCA] SELF-CHECK PASS|FAIL`; the About
  page shows the same verdict.
