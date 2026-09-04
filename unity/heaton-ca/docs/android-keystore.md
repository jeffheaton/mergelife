# The HeatonCA Android upload keystore

Google Play refuses a debug-signed bundle, so `CIBuild.AndroidPlayStore` (the
entry point behind `tools/unity-gate.sh build-android-aab`) will not build
`build/android/HeatonCA.aab` unless it can sign it. This page is how to create
the key it signs with, how to hand it to the build, and what never to do with
it. The listing text, console declarations, and upload checklist are next door
in `store/android/listing.md`.

> **Never commit the keystore or any password.** Not to this repository, not to
> a gist, not into `store/`, `Packaging/`, `CLAUDE.md`, a CI YAML file, or a
> commit message. The repository's `.gitignore` has no `*.keystore` rule, so the
> protection is entirely "the file lives outside the repo" — keep it that way.

## Upload key vs app signing key

On the first upload, accept **Play App Signing**. Google then generates and
holds the *app signing key* that actually signs what users install, and the key
made here becomes only the **upload key**: it proves to Play that an upload came
from you, and Play re-signs the artifact before shipping it.

That split is the safety net. If the upload key is lost or compromised, Google
can register a replacement (see "If the upload key is lost" below) and nothing
about the published app changes. If you had opted out of Play App Signing and
lost the app signing key, the app could never be updated again — a new package
name would be the only way forward. So: accept Play App Signing, and treat this
keystore as important-but-recoverable.

## Create the keystore

`keytool` ships with any JDK. Unity installs one with the Android build support
module, which is the copy this project can rely on:

```bash
KEYTOOL=/Applications/Unity/Hub/Editor/6000.5.0f1/PlaybackEngines/AndroidPlayer/OpenJDK/bin/keytool
"$KEYTOOL" -version        # or just use `keytool` if a JDK is already on PATH
```

Make a home for it outside the repository and generate the key:

```bash
mkdir -p ~/secrets && chmod 700 ~/secrets

"$KEYTOOL" -genkeypair -v \
  -keystore ~/secrets/heatonca-upload.keystore \
  -storetype PKCS12 \
  -alias upload \
  -keyalg RSA -keysize 4096 \
  -validity 10000 \
  -dname "CN=Jeff Heaton, O=Jeff Heaton, C=US"

chmod 600 ~/secrets/heatonca-upload.keystore
```

Notes on those choices:

- **`-validity 10000`** (about 27 years). Play requires an upload certificate
  valid past 2033; a short validity is a slow-motion outage.
- **RSA 4096** is Play's recommendation for upload keys.
- **`-storetype PKCS12`** is the modern standard format; `keytool` nags about
  JKS being proprietary. If a Gradle or Unity version ever rejects it, recreate
  with `-storetype JKS` — nothing else changes.
- **`-alias upload`** is a name, not a secret. Whatever you choose here is the
  value of `HEATONCA_ANDROID_KEYALIAS` forever after.
- keytool prompts for the store password and (for PKCS12) reuses it as the key
  password. Use a long random passphrase from a password manager; you will paste
  it into the environment, never type it from memory.
- `-dname` is cosmetic: Play identifies the key by fingerprint, not by name.
  Drop the flag to answer keytool's questions (organizational unit, city, state)
  interactively, or leave the short form above as is.

Back it up the moment it exists: the keystore file **and** its passwords, in
whatever encrypted store holds your Apple credentials. A Time Machine backup of
`~/secrets` counts; a copy on the same disk does not.

## Hand it to the build

`CIBuild` reads four variables and refuses half-configured input (all four set:
release signing; none set: a debug-signed `.apk`, fine for the emulator and
fatal for Play; some set: the build fails with the list of what is missing).

The passwords should not sit in a file or in shell history. The macOS keychain
is the least awkward place for them:

```bash
# once: store the two passwords (the command prompts, nothing is echoed)
security add-generic-password -a "$USER" -s heatonca-android-keystore -w
security add-generic-password -a "$USER" -s heatonca-android-keyalias  -w

# per shell that will build a release .aab -- or just `source release-env.sh`,
# which runs exactly these lookups and exports nothing unless all four resolve
export HEATONCA_ANDROID_KEYSTORE="$HOME/secrets/heatonca-upload.keystore"
export HEATONCA_ANDROID_KEYALIAS=upload
export HEATONCA_ANDROID_KEYSTORE_PASS="$(security find-generic-password -a "$USER" -s heatonca-android-keystore -w)"
export HEATONCA_ANDROID_KEYALIAS_PASS="$(security find-generic-password -a "$USER" -s heatonca-android-keyalias  -w)"

# Play also needs a version code higher than every previous upload
export HEATONCA_ANDROID_VERSION_CODE=1
```

Then build and check the result (**with the Claude Code Bash sandbox disabled** —
Unity needs the licensing IPC and process lookups the sandbox blocks):

```bash
tools/unity-gate.sh build-android-aab
Packaging/android/check-16kb.sh              # 16 KB page compliance, a Play requirement
```

If you would rather keep a sourced env file than use the keychain, put it at
`~/secrets/heatonca-android-env.sh`, `chmod 600` it, and source it in the shell
that builds. It must live outside the repository, and it must never be quoted
into an issue, a chat, or a commit.

## Verify what you made

```bash
"$KEYTOOL" -list -v -keystore ~/secrets/heatonca-upload.keystore
```

Expect exactly one entry, `PrivateKeyEntry`, with your alias, a 4096-bit RSA
key, and a validity ending roughly 27 years out. Record the **SHA-256
fingerprint** next to the passwords: after the first upload, Play Console shows
the upload certificate's fingerprint under *Test and release > Setup > App
signing*, and the two must match. A mismatch means the wrong keystore signed the
bundle.

To confirm an artifact really carries that signature before uploading:

```bash
"$(dirname "$KEYTOOL")/jarsigner" -verify -verbose:summary -certs \
  build/android/HeatonCA.aab | head -20
```

(An `.aab` is jar-signed, so `jarsigner` is the right tool; `apksigner` is for
APKs. Play re-signs the app with the app signing key regardless — this only
proves the *upload* signature.)

## If the upload key is lost

Play can register a new one. Generate a fresh keystore exactly as above, export
the public certificate, and send it through Play Console's key-reset request:

```bash
"$KEYTOOL" -export -rfc \
  -keystore ~/secrets/heatonca-upload-new.keystore \
  -alias upload \
  -file ~/secrets/heatonca-upload-certificate.pem
```

Google swaps the registered upload certificate (it takes a couple of days), and
uploads continue. Users see nothing; the app signing key never changed.

## What must not end up in the repository

- `*.keystore`, `*.jks`, `*.p12`, `*.pem` private keys, and any file holding
  their passwords. Keep them in `~/secrets`, not in the working tree.
- The four `HEATONCA_ANDROID_*` values, in any file, including this one.
- `ProjectSettings/ProjectSettings.asset` **must not carry a keystore path or
  alias**. `ProjectIdentity.Apply` clears `AndroidKeystoreName` and
  `AndroidKeyaliasName` for exactly this reason, and `CIBuild` sets them from
  the environment for the duration of a build — but a store build leaves the
  values behind in the working tree, so check before committing:

  ```bash
  grep -nE 'AndroidKeystoreName|AndroidKeyaliasName|androidUseCustomKeystore' \
    unity/heaton-ca/ProjectSettings/ProjectSettings.asset
  ```

  `ProjectIdentity.CheckYaml` asserts that the first two are **empty**, so a
  non-empty value is build churn, not a setting: re-run `ProjectIdentity.Apply`
  (or revert the file) before the commit. `androidUseCustomKeystore` is not
  asserted — `CIBuild` flips it per build — but a clean tree carries the `0` a
  debug-signed build leaves behind.
- If CI is ever given the job of signing (today `.github/workflows/ci.yml` only
  runs the test suites), the keystore goes in as a base64 GitHub **secret** that
  the workflow writes to a temp file and deletes, and the passwords go in as
  secrets too. Never as workflow inputs, never in the repository.
