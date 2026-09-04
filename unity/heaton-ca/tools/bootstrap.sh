#!/usr/bin/env bash
# bootstrap.sh -- seed unity/heaton-ca from the sibling heaton-life-unity
# project, or verify that the seeded tree still matches the recipe.
#
# Records exactly what the hand-seeding of 2026-09-02 did, so it can be
# replayed (into a scratch directory with --project, or over the real tree
# with --force) and audited (--verify). It never launches Unity: the first
# tools/unity-gate.sh run is what creates Library/, packages-lock.json, and
# the rest of the generated state. The editor opens a project from Assets/ +
# Packages/manifest.json + ProjectSettings/ alone, so no -createProject step
# is needed.
#
# Usage:
#   tools/bootstrap.sh [--force] [--dry-run] [--source <heaton-life-unity>] [--project <dir>]
#   tools/bootstrap.sh --verify [--project <dir>]
#
# Refuses to run when <project>/ProjectSettings/ProjectSettings.asset already
# exists unless --force is given. The recipe is idempotent: --force re-copies
# every seeded file and re-applies the identity edits, so the result equals a
# fresh seed (given an unchanged source). Files outside the seed list
# (Assets/Scripts, Assets/Engine, Assets/SelfCheck, tools/, ...) are never
# touched, and the repository .gitignore is never edited (its unity/heaton-ca
# block was appended by hand in WP0.1; this script only warns if it is gone).
#
# Source: --source, else HEATON_LIFE_UNITY, else <repo>/../heaton-life-unity.
#
# The recipe:
#   1. ProjectSettings/*.asset, ProjectVersion.txt (6000.5.0f1), and
#      SceneTemplateSettings.json copied verbatim; EditorBuildSettings.asset
#      then points at Assets/Scenes/HeatonCAMainScene.unity.
#   2. Packages/manifest.json = heaton-life's dependencies minus
#      com.yasirkula.nativefilepicker plus com.yasirkula.nativegallery (git
#      URL); keys sorted, two-space indent, no trailing newline (json.dumps).
#   3. Assets/Settings/** (+ Settings.meta) copied; HeatonLifeVolumeProfile
#      .asset/.asset.meta renamed to HeatonCAVolumeProfile keeping the GUID
#      10fc4df2da32a41aaa32d77bc913491c that PC_RPAsset and Mobile_RPAsset
#      reference, and its m_Name updated; Build Profiles/*.asset point at the
#      renamed scene.
#   4. Assets/Scenes/HeatonLifeMainScene.unity(.meta) copied as
#      HeatonCAMainScene.unity(.meta), GUID 5b8e2f0c4a6d41f7b9c3e1d5a7f0b2c4
#      kept (EditorBuildSettings and the Build Profiles reference it);
#      Scenes.meta copied.
#   5. Assets/HeatonLifeInput.inputactions(.meta) copied as
#      HeatonCAInput.inputactions(.meta), GUID 052faaac586de48259a63d0c4782560b
#      kept (ProjectSettings preloadedAssets and EditorBuildSettings reference
#      it). The JSON "name" field inside stays "HeatonLifeInput": Unity names
#      the asset after the file, and no wrapper code is generated from it.
#   6. Assets/Resources.meta, Resources/Icons.meta, Resources/Icons/menu.png
#      and its .meta copied verbatim.
#   7. Empty Packaging/, docs/, store/, tools/ directories created.
#   8. Identity edits (each must match exactly once, keyed on the field name
#      so a second run is a no-op):
#        ProjectSettings/ProjectSettings.asset
#          companyName Jeff Heaton; productName HeatonCA; bundleVersion 2.0.0;
#          cloudProjectId cleared (heaton-life's 3852cc27-... otherwise leaks);
#          AndroidIsGame 0; macAppStoreCategory public.app-category.utilities;
#          AndroidKeystoreName / AndroidKeyaliasName cleared (signing is
#          env-driven in CIBuild); webGLCompressionFormat 1 (gzip);
#          webGLDecompressionFallback 1; webGLThreadsSupport 0;
#          webGLTemplate PROJECT:HeatonCA; applicationIdentifier Android
#          com.heatonresearch.heatonca, Standalone + iPhone
#          com.heatonresearch.heaton-ca; m_SplashScreenLogos: [] (the logo
#          GUID f317940e... does not exist in this project).
#        ProjectSettings/QualitySettings.asset
#          m_PerPlatformDefaultQuality WebGL 0 -> 1 (level 0 "Mobile"
#          excludes Standalone-class targets).
#      Assets/Editor/ProjectIdentity.cs (WP0.2) applies the same values from
#      inside the editor and must stay idempotent over this result.
#
# The scene's App object binds to script GUID 7d3a9c41e5f24b8a9c6d1e0f2a4b6c8d,
# reserved for Assets/Scripts/AppController.cs.meta (WP1.7 writes it with
# tools/new-meta.sh --guid).
set -euo pipefail

SCENE_GUID="5b8e2f0c4a6d41f7b9c3e1d5a7f0b2c4"
INPUT_GUID="052faaac586de48259a63d0c4782560b"
VOLUME_GUID="10fc4df2da32a41aaa32d77bc913491c"
SPLASH_LOGO_GUID="f317940ecbb245368cbe61a34ef3c8dc"
EDITOR_VERSION="6000.5.0f1"
GALLERY_URL="https://github.com/yasirkula/UnityNativeGallery.git"

usage()
{
    cat <<__USAGE__
usage: tools/bootstrap.sh [--force] [--dry-run] [--source <heaton-life-unity>] [--project <dir>]
       tools/bootstrap.sh --verify [--project <dir>]
       replays (or audits) the seeding of unity/heaton-ca from heaton-life-unity; never runs Unity
__USAGE__
}

say()
{
    echo "bootstrap: $*"
}

FORCE=0
DRY=0
VERIFY=0
SRC_OPT=""
PROJECT_OPT=""
while [ $# -gt 0 ]; do
    case "$1" in
        --force) FORCE=1 ;;
        --dry-run) DRY=1 ;;
        --verify) VERIFY=1 ;;
        --source|--project)
            if [ $# -lt 2 ]; then
                say "$1 needs a value" >&2
                usage >&2
                exit 2
            fi
            if [ "$1" = "--source" ]; then SRC_OPT="$2"; else PROJECT_OPT="$2"; fi
            shift ;;
        -h|--help) usage; exit 0 ;;
        *)
            say "unknown argument '$1'" >&2
            usage >&2
            exit 2 ;;
    esac
    shift
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
DEFAULT_PROJECT="$(cd "$SCRIPT_DIR/.." && pwd -P)"
if ! REPO="$(git -C "$DEFAULT_PROJECT" rev-parse --show-toplevel 2>/dev/null)"; then
    REPO="$(cd "$DEFAULT_PROJECT/../.." && pwd -P)"
fi

PROJECT="${PROJECT_OPT:-$DEFAULT_PROJECT}"
if [ -d "$PROJECT" ]; then
    PROJECT="$(cd "$PROJECT" && pwd -P)"
else
    case "$PROJECT" in
        /*) ;;
        *) PROJECT="$PWD/$PROJECT" ;;
    esac
fi

SRC="${SRC_OPT:-${HEATON_LIFE_UNITY:-$REPO/../heaton-life-unity}}"
if [ -d "$SRC" ]; then
    SRC="$(cd "$SRC" && pwd -P)"
fi

# --- helpers ----------------------------------------------------------------------
run()
{
    if [ "$DRY" -eq 1 ]; then
        printf 'bootstrap:   [dry-run]'
        printf ' %q' "$@"
        echo
    else
        "$@"
    fi
}

# sed_in_place <sed expression> <file>: BSD and GNU sed differ on -i, so go via a temp file.
sed_in_place()
{
    if [ "$DRY" -eq 1 ]; then
        say "  [dry-run] sed -e $1 $2"
        return
    fi
    local tmp="$2.bootstrap.tmp"
    sed -e "$1" "$2" > "$tmp"
    mv -f "$tmp" "$2"
}

# copy_tree <src dir> <dst dir>: regular files only, .DS_Store skipped, directories created.
copy_tree()
{
    local src="$1" dst="$2" f rel
    while IFS= read -r -d '' f; do
        rel="${f#$src/}"
        run mkdir -p "$dst/$(dirname "$rel")"
        run cp "$f" "$dst/$rel"
    done < <(find "$src" -type f ! -name '.DS_Store' -print0 | sort -z)
}

write_manifest()
{
    python3 - "$SRC/Packages/manifest.json" "$PROJECT/Packages/manifest.json" "$GALLERY_URL" <<'__PY__'
import json
import sys

src, dst, gallery_url = sys.argv[1], sys.argv[2], sys.argv[3]
with open(src) as fh:
    deps = dict(json.load(fh)["dependencies"])
deps.pop("com.yasirkula.nativefilepicker", None)
deps["com.yasirkula.nativegallery"] = gallery_url
with open(dst, "w") as fh:
    fh.write(json.dumps({"dependencies": deps}, indent=2, sort_keys=True))
__PY__
}

# apply_identity: the step-8 edits. Every pattern is anchored on its field name
# and must match exactly once, so the edits are idempotent and a layout change
# in a future Unity version fails loudly instead of silently skipping a value.
# (Python rather than sed: the splash-logo edit spans several lines, and
# BSD/GNU sed disagree on multi-line addressing.)
apply_identity()
{
    python3 - "$PROJECT" <<'__PY__'
import re
import sys

project = sys.argv[1]


def edit(path, edits):
    with open(path, encoding="utf-8") as fh:
        text = fh.read()
    for label, pattern, replacement in edits:
        repl = replacement if callable(replacement) else (lambda m, r=replacement: r)
        text, n = re.subn(pattern, repl, text, flags=re.M)
        if n != 1:
            sys.exit("bootstrap: identity edit '%s' matched %d time(s) in %s (expected exactly 1)" % (label, n, path))
    with open(path, "w", encoding="utf-8") as fh:
        fh.write(text)
    print("bootstrap:   %d identity edit(s) applied to %s" % (len(edits), path))


edit(project + "/ProjectSettings/ProjectSettings.asset", [
    ("companyName", r"^  companyName:.*$", "  companyName: Jeff Heaton"),
    ("productName", r"^  productName:.*$", "  productName: HeatonCA"),
    ("bundleVersion", r"^  bundleVersion:.*$", "  bundleVersion: 2.0.0"),
    ("cloudProjectId", r"^  cloudProjectId:.*$", "  cloudProjectId: "),
    ("AndroidIsGame", r"^  AndroidIsGame:.*$", "  AndroidIsGame: 0"),
    ("macAppStoreCategory", r"^  macAppStoreCategory:.*$", "  macAppStoreCategory: public.app-category.utilities"),
    ("AndroidKeystoreName", r"^  AndroidKeystoreName:.*$", "  AndroidKeystoreName: "),
    ("AndroidKeyaliasName", r"^  AndroidKeyaliasName:.*$", "  AndroidKeyaliasName: "),
    ("webGLCompressionFormat", r"^  webGLCompressionFormat:.*$", "  webGLCompressionFormat: 1"),
    ("webGLDecompressionFallback", r"^  webGLDecompressionFallback:.*$", "  webGLDecompressionFallback: 1"),
    ("webGLThreadsSupport", r"^  webGLThreadsSupport:.*$", "  webGLThreadsSupport: 0"),
    ("webGLTemplate", r"^  webGLTemplate:.*$", "  webGLTemplate: PROJECT:HeatonCA"),
    ("applicationIdentifier",
     r"^  applicationIdentifier:\n    Android:.*\n    Standalone:.*\n    iPhone:.*$",
     "  applicationIdentifier:\n"
     "    Android: com.heatonresearch.heatonca\n"
     "    Standalone: com.heatonresearch.heaton-ca\n"
     "    iPhone: com.heatonresearch.heaton-ca"),
    ("m_SplashScreenLogos",
     r"^  m_SplashScreenLogos:.*(?:\n  - logo:.*\n    duration:.*)*$",
     "  m_SplashScreenLogos: []"),
])

edit(project + "/ProjectSettings/QualitySettings.asset", [
    ("m_PerPlatformDefaultQuality.WebGL",
     r"^(  m_PerPlatformDefaultQuality:\n(?:    .*\n)*?    WebGL:).*$",
     lambda m: m.group(1) + " 1"),
])
__PY__
}

# --- seeding ----------------------------------------------------------------------
seed()
{
    if [ ! -f "$SRC/ProjectSettings/ProjectVersion.txt" ]; then
        say "source project not found at $SRC (pass --source or set HEATON_LIFE_UNITY)" >&2
        exit 1
    fi
    if [ -e "$PROJECT/ProjectSettings/ProjectSettings.asset" ] && [ "$FORCE" -ne 1 ]; then
        say "refusing: $PROJECT is already seeded (ProjectSettings/ProjectSettings.asset exists). Use --verify to audit it, or --force to re-copy the seed files and re-apply the identity edits." >&2
        exit 1
    fi
    say "source  $SRC"
    say "project $PROJECT"

    say "1. ProjectSettings"
    run mkdir -p "$PROJECT/ProjectSettings"
    for f in "$SRC"/ProjectSettings/*.asset "$SRC/ProjectSettings/ProjectVersion.txt" "$SRC/ProjectSettings/SceneTemplateSettings.json"; do
        run cp "$f" "$PROJECT/ProjectSettings/${f##*/}"
    done
    sed_in_place 's#Assets/Scenes/HeatonLifeMainScene\.unity#Assets/Scenes/HeatonCAMainScene.unity#' "$PROJECT/ProjectSettings/EditorBuildSettings.asset"
    if [ "$DRY" -ne 1 ] && ! grep -q "^m_EditorVersion: $EDITOR_VERSION\$" "$PROJECT/ProjectSettings/ProjectVersion.txt"; then
        say "warning: ProjectVersion.txt is not $EDITOR_VERSION; the gate runner defaults to that editor"
    fi

    say "2. Packages/manifest.json"
    run mkdir -p "$PROJECT/Packages"
    if [ "$DRY" -eq 1 ]; then
        say "  [dry-run] write manifest: heaton-life dependencies - nativefilepicker + nativegallery, sorted keys"
    else
        write_manifest
    fi

    say "3. Assets/Settings"
    run mkdir -p "$PROJECT/Assets/Settings"
    run cp "$SRC/Assets/Settings.meta" "$PROJECT/Assets/Settings.meta"
    copy_tree "$SRC/Assets/Settings" "$PROJECT/Assets/Settings"
    for ext in asset asset.meta; do
        if [ -e "$PROJECT/Assets/Settings/HeatonLifeVolumeProfile.$ext" ] || [ "$DRY" -eq 1 ]; then
            run mv -f "$PROJECT/Assets/Settings/HeatonLifeVolumeProfile.$ext" "$PROJECT/Assets/Settings/HeatonCAVolumeProfile.$ext"
        fi
    done
    sed_in_place 's/^  m_Name: HeatonLifeVolumeProfile$/  m_Name: HeatonCAVolumeProfile/' "$PROJECT/Assets/Settings/HeatonCAVolumeProfile.asset"
    for f in "$PROJECT/Assets/Settings/Build Profiles"/*.asset; do
        if [ -e "$f" ]; then
            sed_in_place 's#Assets/Scenes/HeatonLifeMainScene\.unity#Assets/Scenes/HeatonCAMainScene.unity#' "$f"
        fi
    done

    say "4. Assets/Scenes/HeatonCAMainScene.unity"
    run mkdir -p "$PROJECT/Assets/Scenes"
    run cp "$SRC/Assets/Scenes.meta" "$PROJECT/Assets/Scenes.meta"
    run cp "$SRC/Assets/Scenes/HeatonLifeMainScene.unity" "$PROJECT/Assets/Scenes/HeatonCAMainScene.unity"
    run cp "$SRC/Assets/Scenes/HeatonLifeMainScene.unity.meta" "$PROJECT/Assets/Scenes/HeatonCAMainScene.unity.meta"

    say "5. Assets/HeatonCAInput.inputactions"
    run cp "$SRC/Assets/HeatonLifeInput.inputactions" "$PROJECT/Assets/HeatonCAInput.inputactions"
    run cp "$SRC/Assets/HeatonLifeInput.inputactions.meta" "$PROJECT/Assets/HeatonCAInput.inputactions.meta"

    say "6. Assets/Resources/Icons/menu.png"
    run mkdir -p "$PROJECT/Assets/Resources/Icons"
    run cp "$SRC/Assets/Resources.meta" "$PROJECT/Assets/Resources.meta"
    run cp "$SRC/Assets/Resources/Icons.meta" "$PROJECT/Assets/Resources/Icons.meta"
    run cp "$SRC/Assets/Resources/Icons/menu.png" "$PROJECT/Assets/Resources/Icons/menu.png"
    run cp "$SRC/Assets/Resources/Icons/menu.png.meta" "$PROJECT/Assets/Resources/Icons/menu.png.meta"

    say "7. empty top-level directories"
    run mkdir -p "$PROJECT/Packaging" "$PROJECT/docs" "$PROJECT/store" "$PROJECT/tools"

    say "8. identity edits (ProjectSettings.asset, QualitySettings.asset)"
    if [ "$DRY" -eq 1 ]; then
        say "  [dry-run] apply the step-8 identity edits listed in the header"
    else
        apply_identity
    fi

    case "$PROJECT" in
        "$REPO"/*)
            if ! git -C "$REPO" check-ignore -q "$PROJECT/Library/x" 2>/dev/null; then
                say "warning: $PROJECT/Library is not git-ignored; append the unity/heaton-ca block to $REPO/.gitignore by hand (this script never edits it)"
            fi ;;
    esac
}

# --- verification -----------------------------------------------------------------
verify()
{
    local failures=0

    ok()
    {
        say "  ok    $*"
    }
    bad()
    {
        say "  FAIL  $*"
        failures=$((failures + 1))
    }
    has_file()   # <relative path>
    {
        if [ -f "$PROJECT/$1" ]; then ok "$1"; else bad "$1 missing"; fi
    }
    has_line()   # <relative path> <grep pattern> <description>
    {
        if [ -f "$PROJECT/$1" ] && grep -q -- "$2" "$PROJECT/$1"; then ok "$3"; else bad "$3"; fi
    }
    lacks_line() # <relative path> <grep pattern> <description>
    {
        if [ -f "$PROJECT/$1" ] && ! grep -q -- "$2" "$PROJECT/$1"; then ok "$3"; else bad "$3"; fi
    }

    say "verifying $PROJECT"
    has_file "ProjectSettings/ProjectSettings.asset"
    has_file "ProjectSettings/SceneTemplateSettings.json"
    has_line "ProjectSettings/ProjectVersion.txt" "^m_EditorVersion: $EDITOR_VERSION\$" "ProjectVersion.txt is $EDITOR_VERSION"
    has_line "ProjectSettings/EditorBuildSettings.asset" "path: Assets/Scenes/HeatonCAMainScene.unity" "EditorBuildSettings lists HeatonCAMainScene"
    has_line "ProjectSettings/EditorBuildSettings.asset" "guid: $SCENE_GUID" "EditorBuildSettings references the scene GUID"
    has_line "ProjectSettings/EditorBuildSettings.asset" "guid: $INPUT_GUID" "EditorBuildSettings references the input actions GUID"
    has_line "ProjectSettings/ProjectSettings.asset" "guid: $INPUT_GUID" "preloadedAssets references the input actions GUID"

    say "identity"
    local ps="ProjectSettings/ProjectSettings.asset"
    has_line "$ps" '^  companyName: Jeff Heaton$' "companyName is Jeff Heaton"
    has_line "$ps" '^  productName: HeatonCA$' "productName is HeatonCA"
    has_line "$ps" '^  bundleVersion: 2\.0\.0$' "bundleVersion is 2.0.0"
    has_line "$ps" '^  cloudProjectId: $' "cloudProjectId is cleared"
    has_line "$ps" '^  AndroidIsGame: 0$' "AndroidIsGame is 0"
    has_line "$ps" '^  macAppStoreCategory: public\.app-category\.utilities$' "macAppStoreCategory is public.app-category.utilities"
    has_line "$ps" '^  AndroidKeystoreName: $' "AndroidKeystoreName is cleared"
    has_line "$ps" '^  AndroidKeyaliasName: $' "AndroidKeyaliasName is cleared"
    has_line "$ps" '^  webGLCompressionFormat: 1$' "webGLCompressionFormat is 1 (gzip)"
    has_line "$ps" '^  webGLDecompressionFallback: 1$' "webGLDecompressionFallback is 1"
    has_line "$ps" '^  webGLThreadsSupport: 0$' "webGLThreadsSupport is 0"
    has_line "$ps" '^  webGLTemplate: PROJECT:HeatonCA$' "webGLTemplate is PROJECT:HeatonCA"
    has_line "$ps" '^    Android: com\.heatonresearch\.heatonca$' "Android identifier is com.heatonresearch.heatonca"
    has_line "$ps" '^    Standalone: com\.heatonresearch\.heaton-ca$' "Standalone identifier is com.heatonresearch.heaton-ca"
    has_line "$ps" '^    iPhone: com\.heatonresearch\.heaton-ca$' "iPhone identifier is com.heatonresearch.heaton-ca"
    has_line "$ps" '^  m_SplashScreenLogos: \[\]$' "m_SplashScreenLogos is empty"
    lacks_line "$ps" "$SPLASH_LOGO_GUID" "no reference to the heaton-life splash logo GUID"
    lacks_line "$ps" "heatonlife" "no heatonlife identifiers remain"
    has_line "ProjectSettings/QualitySettings.asset" '^    WebGL: 1$' "QualitySettings default WebGL quality level is 1"

    say "packages"
    has_line "Packages/manifest.json" "\"com.yasirkula.nativegallery\": \"$GALLERY_URL\"" "manifest has com.yasirkula.nativegallery"
    lacks_line "Packages/manifest.json" "com.yasirkula.nativefilepicker" "manifest lacks com.yasirkula.nativefilepicker"
    if python3 -c 'import json,sys; json.load(open(sys.argv[1]))' "$PROJECT/Packages/manifest.json" 2>/dev/null; then
        ok "manifest is valid JSON"
    else
        bad "manifest is valid JSON"
    fi

    say "assets"
    has_file "Assets/Settings.meta"
    has_file "Assets/Settings/HeatonCAVolumeProfile.asset"
    has_line "Assets/Settings/HeatonCAVolumeProfile.asset" "^  m_Name: HeatonCAVolumeProfile\$" "volume profile m_Name is HeatonCAVolumeProfile"
    has_line "Assets/Settings/HeatonCAVolumeProfile.asset.meta" "^guid: $VOLUME_GUID\$" "volume profile keeps GUID $VOLUME_GUID"
    has_line "Assets/Settings/PC_RPAsset.asset" "$VOLUME_GUID" "PC_RPAsset references the volume profile GUID"
    has_line "Assets/Settings/Mobile_RPAsset.asset" "$VOLUME_GUID" "Mobile_RPAsset references the volume profile GUID"
    if [ -e "$PROJECT/Assets/Settings/HeatonLifeVolumeProfile.asset" ] || [ -e "$PROJECT/Assets/Settings/HeatonLifeVolumeProfile.asset.meta" ]; then
        bad "no HeatonLifeVolumeProfile leftovers in Assets/Settings"
    else
        ok "no HeatonLifeVolumeProfile leftovers in Assets/Settings"
    fi
    for p in Android iOS macOS; do
        has_file "Assets/Settings/Build Profiles/$p.asset.meta"
        has_line "Assets/Settings/Build Profiles/$p.asset" "m_path: Assets/Scenes/HeatonCAMainScene.unity" "Build Profiles/$p.asset lists HeatonCAMainScene"
    done

    has_file "Assets/Scenes.meta"
    has_file "Assets/Scenes/HeatonCAMainScene.unity"
    has_line "Assets/Scenes/HeatonCAMainScene.unity.meta" "^guid: $SCENE_GUID\$" "scene keeps GUID $SCENE_GUID"
    has_file "Assets/HeatonCAInput.inputactions"
    has_line "Assets/HeatonCAInput.inputactions.meta" "^guid: $INPUT_GUID\$" "input actions keep GUID $INPUT_GUID"
    has_file "Assets/Resources.meta"
    has_file "Assets/Resources/Icons.meta"
    has_file "Assets/Resources/Icons/menu.png"
    has_file "Assets/Resources/Icons/menu.png.meta"

    local stale
    stale="$(grep -rIl 'HeatonLifeMainScene\|HeatonLifeVolumeProfile' "$PROJECT/ProjectSettings" "$PROJECT/Assets/Settings" "$PROJECT/Assets/Scenes" 2>/dev/null || true)"
    if [ -z "$stale" ]; then
        ok "no HeatonLifeMainScene/HeatonLifeVolumeProfile references remain"
    else
        bad "stale HeatonLife references in: $(echo "$stale" | tr '\n' ' ')"
    fi

    local unpaired=""
    local d f
    for d in Settings Scenes Resources; do
        [ -d "$PROJECT/Assets/$d" ] || continue
        while IFS= read -r -d '' f; do
            if [ ! -e "$f.meta" ]; then
                unpaired="$unpaired ${f#$PROJECT/}"
            fi
        done < <(find "$PROJECT/Assets/$d" -mindepth 1 \( -name '*~' -o -name '.*' \) -prune -o ! -name '*.meta' -print0)
    done
    if [ -z "$unpaired" ]; then
        ok "every seeded asset under Assets/Settings, Scenes, Resources has a .meta"
    else
        bad "seeded assets without .meta:$unpaired"
    fi

    if [ "$failures" -eq 0 ]; then
        say "verify: PASS"
        return 0
    fi
    say "verify: FAIL ($failures problem(s))"
    return 1
}

if [ "$VERIFY" -eq 1 ]; then
    verify
    exit $?
fi

seed
if [ "$DRY" -eq 1 ]; then
    say "dry run complete; nothing written"
    exit 0
fi
verify
