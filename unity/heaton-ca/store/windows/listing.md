# Microsoft Store listing — HeatonCA for Windows (2.0.0)

Partner Center text and declarations for the MSIX built by
`Packaging/windows/package-msix.ps1`. The Windows channel ships **twice**: this
Store package, and the unsigned zip on a GitHub release that
`docs/windows-build.md` covers. Both come from the same
`CIBuild.Windows` Mono player and the same passing determinism self-check —
neither is a rebuild of the other.

- **Product**: HeatonCA — name reserved in Partner Center, 2026-09-07
- **Package identity**: `JeffHeaton.HeatonCA`, publisher
  `CN=CA868514-4C45-44CC-A8F7-4302E1A9FD41` (the account's, shared with Heaton
  Life and Dynaface)
- **This is a NEW product.** There is no Windows app on the record to update.
- **Minimum Windows**: 10.0.19043 (21H1), from `AppxManifest.xml` — Unity 6
  players do not support older Windows 10 builds.
- **Localization**: English (U.S.) only.
- **Packaging**: Desktop Bridge (`Windows.FullTrustApplication` +
  `runFullTrust`), Mono, x64. Not UWP — see
  [the manifest](../../Packaging/windows/msix/AppxManifest.xml) for why.

> **Open items — nothing below can be uploaded until these are settled.**
>
> 1. ~~**Reserve the name** and confirm the product identity.~~ **Done
>    2026-09-07.** The name HeatonCA is reserved, and all three values on
>    Product management > Product identity match `AppxManifest.xml` exactly:
>    `Package/Identity/Name` = `JeffHeaton.HeatonCA`,
>    `Package/Identity/Publisher` = `CN=CA868514-4C45-44CC-A8F7-4302E1A9FD41`,
>    `Package/Properties/PublisherDisplayName` = `Jeff Heaton`. The expected
>    `<Publisher>.<ReservedName>` form held; no manifest edit was needed.
> 2. **Shoot the screenshot set** (see §5). None exist yet.
> 3. Decide whether the Store listing links the GitHub zip at all. Two download
>    routes for one app is a support question, not a technical one.

---

## 1. Product identity and category

- **Name** — limit **50**, using **8**:

  ```
  HeatonCA
  ```

- **Category**: `Education > Science` — the sibling listings use
  Entertainment on Apple's taxonomy, but the app is a research tool with a
  cited paper behind it, and Microsoft's Education tree fits it better than
  Games (which would pull in age-rating questions about gameplay this app has
  none of).

- **Pricing**: Free, all markets. No in-app purchases, no trial.

- **Age rating**: complete the IARC questionnaire. Every answer is "no" — no
  violence, no user interaction, no data sharing, no unrestricted web access.
  Expect 3+/Everyone.

## 2. Description

- **Short description** — limit **200**, using **78**:

  ```
  Evolve and explore MergeLife cellular automata: gallery, decoder, and trainer.
  ```

- **Description** — limit **10000**. Reuse the Google Play full description
  verbatim from `store/android/listing.md` §"Full description"; it is written
  to the same 4000-character budget and needs no Windows-specific edits. Do
  **not** paste the Apple description, which references "your iPhone".

- **What's new in this version**: first release on Windows. Say that plainly
  rather than reusing the 2.0.0 "rewritten from the PyQt app" note, which is
  about a Windows app that never existed.

## 3. Properties and declarations

- **Privacy policy URL**: the same URL the other listings use (see
  `store/macos/listing.md` §1). Required, because Partner Center demands one
  from every product regardless of what it collects.
- **Data collected**: none. The app never connects on its own — the manifest
  declares no `internetClient`, which makes that claim structural rather than
  a promise.
- **Support contact**: Jeff Heaton, `jeff@jeffheaton.com`.
- **System requirements**: nothing beyond the manifest's minimum. The app
  needs no GPU feature above what the Unity 6 URP player already requires.

## 4. Packages

- Upload `build/msix/HeatonCA_<version>_x64.msix` **unsigned** — the Store
  re-signs on publication. A locally signed package is not more correct and
  can be refused.
- **Version** is `bundleVersion + ".0"` (2.0.0 → `2.0.0.0`); the Store
  requires the fourth field to be 0. Name + version + architecture must be
  unique across all uploads, and a fix reaches installed users only as a
  **higher** version — there is no re-upload at the same number.
- **arm64**: `package-msix.ps1 -Arch arm64` is wired and both `.msix` files
  can go in one submission, but there is no `CIBuild` entry for an ARM64
  player yet, so this ships x64 only. On ARM64 Windows the x64 package runs
  under emulation, which is exactly how it was verified.

## 5. Screenshots

**None committed yet.** Partner Center wants at least one; ship the same eight
scenes as every other channel so the sets stay comparable — the scene list and
the rules live in `store/README.md`, and they all apply here.

| Set | Directory | Pixels | Orientation | Count |
|---|---|---|---|---|
| Windows desktop | `store/windows/` | **2560x1600** | landscape | 1-10 (ship 8) |

- Partner Center's floor is 1366x768; 2560x1600 matches the Mac set, so the
  two desktop listings show the same pictures at the same size.
- Shoot from the **registered MSIX build**
  (`package-msix.ps1 -Register`), not the loose player — the window chrome and
  the Start-menu identity are what a Store customer will see.
- Capture the content area only, no title bar and no desktop:

  ```powershell
  # window content rect, no shadow, PNG
  # (any capture tool is fine; verify the size afterward)
  Add-Type -AssemblyName System.Drawing
  [System.Drawing.Image]::FromFile('store\windows\01-simulator-red-world.png').Size
  ```

- Strip alpha and verify sizes with the same Python snippet in
  `store/README.md` ("Stripping alpha and verifying"), adding
  `"store/windows": (2560, 1600, 10)` to its `want` map.

## 6. Submission checklist

1. ~~Reserve the name; paste the real identity into `AppxManifest.xml`.~~
   Done 2026-09-07 — the manifest already matched the assigned identity.
2. `tools/unity-gate.sh` is a Mac gate — on Windows, build with
   `CIBuild.Windows` per `docs/windows-build.md`.
3. `powershell -File Packaging\windows\package-msix.ps1 -Register` and drive
   the app: menus, the Explorer reveal, the gallery, a short Evolve run.
4. Shoot the eight screenshots from that registered build.
5. `powershell -File Packaging\windows\package-msix.ps1` for the upload
   package. It re-runs the determinism self-check; a red check stops the
   release.
6. Upload, fill in this listing, submit.
