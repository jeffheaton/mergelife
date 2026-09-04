#!/usr/bin/env python3
"""Serve a HeatonCA WebGL build locally the way a bare static host would.

  python3 tools/webgl-serve.py                      build/webgl on http://127.0.0.1:8765/
  python3 tools/webgl-serve.py --port 9000
  python3 tools/webgl-serve.py --dir path/to/build  another build folder
  python3 tools/webgl-serve.py --content-encoding   behave like a tuned host instead
  python3 tools/webgl-serve.py --quiet              no per-request log lines

Why not `python3 -m http.server`: Python's server guesses `application/gzip`
for `.gz` files and knows nothing about `.wasm`, `.data`, or `.unityweb`.
This one sends the content types the Unity loader expects (`.wasm` as
`application/wasm`, `.js` as `application/javascript`, the data file and
`.unityweb` as `application/octet-stream`, a `.gz` file as the type of the
name inside it) and, by default, serves the gzip-compressed build files
WITHOUT a `Content-Encoding` header. That is the worst-case host the project
plans for: the loader sees gzip magic bytes in the response, notices the
browser did not decompress them, and falls back to its built-in JavaScript
gunzip (PlayerSettings "Decompression Fallback", on for this project). The
smoke test tools/webgl-smoke.sh runs against this server so that fallback is
exercised on every gate, not just on whichever host ships the build.

`--content-encoding` adds `Content-Encoding: gzip` (or `br`) to the compressed
files so the browser inflates them natively; that is how the published site
should be configured (Packaging/webgl/README.md) and is worth a second manual
run when the hosting headers change.

Threads are off in this project, so no COOP/COEP headers are needed. The
server binds 127.0.0.1 only and adds `Cache-Control: no-store` so a rebuilt
`build/webgl` is what the browser loads (the Unity data cache in IndexedDB is
keyed separately and unaffected). Exit 2 when the build folder or its
index.html is missing; exit 3 when the port is taken.
"""

import argparse
import errno
import os
import sys
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_DIR = os.path.join(PROJECT, "build", "webgl")
DEFAULT_PORT = 8765

CONTENT_TYPES = {
    ".wasm": "application/wasm",
    ".js": "application/javascript",
    ".mjs": "application/javascript",
    ".data": "application/octet-stream",
    ".unityweb": "application/octet-stream",
    ".symbols.json": "application/json",
    ".json": "application/json",
    ".html": "text/html; charset=utf-8",
    ".css": "text/css; charset=utf-8",
    ".png": "image/png",
    ".ico": "image/x-icon",
    ".txt": "text/plain; charset=utf-8",
    ".webmanifest": "application/manifest+json",
}
GZIP_MAGIC = b"\x1f\x8b"


def content_type_for(path):
    """The type of `path`, looking through a trailing .gz/.br at the inner name."""
    name = os.path.basename(path).lower()
    for compressed in (".gz", ".br"):
        if name.endswith(compressed):
            name = name[: -len(compressed)]
            break
    for suffix, mime in CONTENT_TYPES.items():
        if name.endswith(suffix):
            return mime
    return "application/octet-stream"


def content_encoding_for(path):
    """gzip / br / None for a compressed build file, by suffix and magic bytes."""
    name = os.path.basename(path).lower()
    if name.endswith(".gz"):
        return "gzip"
    if name.endswith(".br"):
        return "br"
    if name.endswith(".unityweb"):
        try:
            with open(path, "rb") as handle:
                return "gzip" if handle.read(2) == GZIP_MAGIC else "br"
        except OSError:
            return None
    return None


class BuildHandler(SimpleHTTPRequestHandler):
    """Static files with Unity-correct types and configurable compression headers."""

    send_content_encoding = False
    quiet = False
    protocol_version = "HTTP/1.1"

    def send_head(self):
        self._encoding = None
        if self.send_content_encoding:
            local = self.translate_path(self.path)
            if os.path.isfile(local):
                self._encoding = content_encoding_for(local)
        return super().send_head()

    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        encoding = getattr(self, "_encoding", None)
        if encoding:
            self.send_header("Content-Encoding", encoding)
            self._encoding = None
        super().end_headers()

    def guess_type(self, path):
        return content_type_for(path)

    def log_message(self, fmt, *args):
        if not self.quiet:
            super().log_message(fmt, *args)


def main(argv=None):
    parser = argparse.ArgumentParser(
        description="Serve a Unity WebGL build with the right content types "
        "and, by default, no Content-Encoding on compressed files."
    )
    parser.add_argument("--port", type=int, default=DEFAULT_PORT,
                        help="TCP port on 127.0.0.1 (default %(default)s)")
    parser.add_argument("--dir", default=DEFAULT_DIR,
                        help="build folder holding index.html (default build/webgl)")
    parser.add_argument("--content-encoding", action="store_true",
                        help="send Content-Encoding for .gz/.br/.unityweb files "
                        "(a tuned host); default is none (the fallback path)")
    parser.add_argument("--quiet", action="store_true",
                        help="suppress per-request log lines")
    args = parser.parse_args(argv)

    directory = os.path.abspath(args.dir)
    if not os.path.isfile(os.path.join(directory, "index.html")):
        sys.stderr.write(
            "webgl-serve: no index.html in %s; build first with "
            "tools/unity-gate.sh build-webgl\n" % directory)
        return 2

    handler = partial(BuildHandler, directory=directory)
    BuildHandler.send_content_encoding = args.content_encoding
    BuildHandler.quiet = args.quiet
    try:
        server = ThreadingHTTPServer(("127.0.0.1", args.port), handler)
    except OSError as error:
        if error.errno in (errno.EADDRINUSE, errno.EACCES):
            sys.stderr.write("webgl-serve: port %d is in use (%s)\n" % (args.port, error))
            return 3
        raise
    server.daemon_threads = True
    mode = "with Content-Encoding" if args.content_encoding else "no Content-Encoding (fallback path)"
    sys.stderr.write("webgl-serve: http://127.0.0.1:%d/ -> %s [%s]\n" % (args.port, directory, mode))
    sys.stderr.flush()
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
