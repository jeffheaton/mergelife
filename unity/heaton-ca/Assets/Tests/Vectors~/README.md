# Vendored conformance vectors

Golden test data replayed by the EditMode tests in `Assets/Tests/EditMode`
(`TestSupport.VendoredRoot()` points here).

- Source repo: https://github.com/jeffheaton/heaton-life (`vectors/`)
- Commit: `46a117baf33a0f916d39e2c05991604ae8a1b424`
- spec_version: 0.2.0 (from `params.json`; the `mergelife-decode` cases declare none)
- Files: 17 across 8 cases (excluding this README)

These files are copied verbatim, never edited here; re-run `tools/vendor-vectors.sh`
to update. The trailing tilde on `Vectors~` keeps Unity from importing the folder,
so nothing inside has a `.meta`.

| Case | Files | Spec |
|---|---|---|
| `evolve/mini-run-24` | best.f64, params.json | spec/evolve.md |
| `evolve/objective-a07f-48` | params.json, runs.f64, score.f64 | spec/evolve.md |
| `evolve/objective-redworld-48` | params.json, runs.f64, score.f64 | spec/evolve.md |
| `evolve/operators-seeded` | params.json | spec/evolve.md |
| `mergelife-decode/red-world` | params.json | spec/mergelife.md (decode) |
| `mergelife-decode/promoted-and-negative` | params.json | spec/mergelife.md (decode) |
| `mergelife-decode/tied-limits` | params.json | spec/mergelife.md (decode) |
| `mergelife/redworld-48` | params.json, state_00000.png, state_00001.png, state_00010.png, state_00050.png | spec/mergelife.md |

Specs live in the source repo under `spec/`; `spec/rng.md` defines the PCG32 stream
every seeded case depends on.
