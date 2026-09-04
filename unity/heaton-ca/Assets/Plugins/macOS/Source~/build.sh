#!/usr/bin/env bash
# build.sh -- compile HeatonCAMac.bundle from HeatonCAMac.mm.
#
# Unity loads the COMPILED bundle, not this source: Assets/Plugins/macOS/
# HeatonCAMac.bundle is a committed binary, and a "Source~" folder is invisible
# to Unity (the trailing tilde), which is why the source can sit right beside
# it with no .meta of its own. So after editing HeatonCAMac.mm you must run
# this script, or the player keeps the old menu bar and nothing tells you.
# (The heaton-life / dynaface-unity layout.)
#
# Universal binary on purpose: the Mac App Store build must run on Apple
# Silicon and on the Intel Macs still receiving macOS 12, so both slices are
# compiled and -mmacosx-version-min matches the project's LSMinimumSystemVersion.
#
# Requires the Xcode command line tools (clang++). Run it from anywhere; every
# path is resolved relative to this script. It writes outside the Claude Code
# Bash sandbox's default allow-list only if the checkout is outside it -- inside
# the repository it needs no special permission, but note that it does WRITE a
# file into Assets/, so run it with the sandbox disabled if a policy blocks
# writes there.
set -euo pipefail

cd "$(dirname "$0")"
OUT="../HeatonCAMac.bundle"

clang++ -bundle -fobjc-arc -arch arm64 -arch x86_64 -mmacosx-version-min=12.0 \
    -Wall -framework Cocoa \
    -o "$OUT" HeatonCAMac.mm
chmod +x "$OUT"

echo "Built $OUT"
lipo -info "$OUT"
