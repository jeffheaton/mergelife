# Assets/Engine provenance

`Assets/Engine` is the `HeatonCA.Engine` assembly: the MergeLife cellular
automaton, its objective function, the genetic-algorithm evolver, the featured
rule catalog, and the PCG32 random number generator. It is a verbatim copy of
seven files from the HeatonLife.Core library, not a fork of them.

## Source

- Repository: <https://github.com/jeffheaton/heaton-life>
- Directory: `dotnet/src/HeatonLife.Core`
- Pinned commit: `46a117baf33a0f916d39e2c05991604ae8a1b424`
- License: Apache License, Version 2.0, copyright Jeff Heaton (full text in
  `unity/heaton-ca/THIRD_PARTY_NOTICES.md`)

## Files

Copied from upstream (each carries a five-line provenance header naming the
upstream path and commit):

| File | Contents |
| --- | --- |
| `MergeLife.cs` | The MergeLife lattice, rule parsing, stepping, soup seeding |
| `MergeLifeObjective.cs` | The 2018-trainer objective: run statistics and scoring |
| `Evolver.cs` | Genetic-algorithm search over rule strings |
| `MergeLifeGallery.cs` | The featured-rule catalog |
| `Pcg32.cs` | PCG32 (XSH-RR), the spec-pinned RNG |
| `Parallelism.cs` | The one indexed parallel loop the evolver uses |
| `ISimulation.cs` | The time-stepped simulation interfaces |

Not copied from upstream (the only local files in this folder):

- `HeatonCA.Engine.asmdef`: `noEngineReferences` is true, so the assembly is
  plain C# with no dependency on `UnityEngine`.
- `csc.rsp`: `-nullable:enable`, matching the upstream project's
  `<Nullable>enable</Nullable>` so the `?` annotations compile without
  CS8632 warnings.
- `AssemblyInfo.cs`: `InternalsVisibleTo("HeatonCA.EditorTests")` so the
  EditMode tests can drive the internal `RunEvaluator` for the convergence
  pins. This is the one non-upstream C# file in the folder.
- `PROVENANCE.md`: this file.

Upstream files deliberately left behind: `PngGrid.cs` (PNG encode/decode via
`DeflateStream`; the app writes PNGs with Unity's encoder), `StateCodec.cs`,
`RenderProgress.cs`, `Colormaps.cs`, and `Seeding.cs`. Two doc comments in the
copied files still mention types that were not carried (`Colormaps` in
`ISimulation.cs`, `<see cref="BuiltinPatterns"/>` in `MergeLifeGallery.cs`);
they are comments only and compile cleanly.

## The single modification

`tools/import-engine.sh` applies exactly one change to the upstream text,
mechanically and identically on every run:

1. Every file: the line `namespace HeatonLife` becomes
   `namespace HeatonCA.Engine`, and the provenance header is prepended.
2. `MergeLife.cs` only: the `ToPng` member is removed. That is its XML summary
   line, the line `public byte[] ToPng(int scale = 1) => PngGrid.EncodeRgb(...)`,
   and the blank line that would otherwise double. It was the only caller of
   `PngGrid`.

Nothing else differs from upstream, and `tools/engine-sync-check.sh` proves it
on every run by reversing the header and the rename and diffing against the
upstream checkout.

## Never edit numerics

Do not edit any of the seven copied files by hand, for any reason. In
particular, never "clean up" or "fix" the numerics: the stable sort by range
limit alone, the mode-padded neighbor sum, the 127/128 percent scaling, the
floor semantics, the strict `> 1000` cap that ends unconverged runs at 1001,
and the accepted Python-versus-C# nits (per-cycle evaluation bleed, the `mage`
off-by-one, `RandomRule` range byte 0..255) are all part of the contract. The
contract is pinned by `conformance/vectors.txt` at the repository root, the
vendored heaton-life vectors under `Assets/Tests/Vectors~`, and the on-device
determinism self-check. If something looks wrong, fix it upstream in
heaton-life first, then re-import.

## How to sync

1. Update the sibling `heaton-life` checkout (`../heaton-life` beside this
   repository) to the commit you want, with a clean working tree.
2. Run `tools/import-engine.sh` from `unity/heaton-ca`. It refuses a dirty
   upstream tree unless given `--allow-dirty`, records the commit in every
   header, and keeps the existing `.meta` GUIDs.
3. Update the pinned commit above; `tools/engine-sync-check.sh` fails until
   this file names the same commit as the headers.
4. Run `tools/engine-sync-check.sh` (exit 0 required) and then the EditMode
   tests, which replay the conformance vectors.

A different upstream location can be given with `SRC` or
`HEATONCA_UPSTREAM_ENGINE`; a checkout without git history needs `--sha`.
Continuous integration runs the sync check against a sparse checkout of
`jeffheaton/heaton-life`; on a machine without any upstream checkout,
`tools/engine-sync-check.sh --self` still verifies the headers, namespaces,
and file set.
