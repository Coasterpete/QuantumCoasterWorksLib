# Milestone 5 Fixture Import Path

## Current Status

Milestone 5 uses a deliberately narrow sampled-frame CSV fixture import path. This is a debug/test bridge only, not a general NoLimits importer.

Current pieces:

- `docs/testing/nolimits-csv-fixtures.md` defines allowed fixture sources and scope boundaries.
- `Quantum.IO/Fixtures/Csv/CenterlineFrameCsvFixtureParser.cs` reads the tiny sampled-frame fixture schema.
- `Quantum.Debug` can write a deterministic `DebugViewportSnapshotV1` sample from the smoke scenario.
- `Quantum.Debug` can also bridge a sampled-frame CSV fixture directly to `DebugViewportSnapshotV1` JSON.
- `Quantum.Debug` can validate and summarize a generated `DebugViewportSnapshotV1` JSON file before any viewer consumes it.
- Milestone 7 adds a small self-authored sampled-frame fixture pack that exercises the CSV-to-snapshot path without committing generated JSON snapshots.

## Smallest CSV Schema

Use a tiny self-authored or synthetic CSV that represents already-sampled centerline frames in meters. This keeps the first import path deterministic and avoids rebuilding spline, interpolation, or NoLimits project import behavior.

Required header:

```csv
distanceMeters,xMeters,yMeters,zMeters,tangentX,tangentY,tangentZ,normalX,normalY,normalZ,binormalX,binormalY,binormalZ
```

Example fixture:

```csv
distanceMeters,xMeters,yMeters,zMeters,tangentX,tangentY,tangentZ,normalX,normalY,normalZ,binormalX,binormalY,binormalZ
0,0,0,0,1,0,0,0,1,0,0,0,1
5,5,0,0,1,0,0,0,1,0,0,0,1
10,10,0,0,1,0,0,0,1,0,0,0,1
```

Rules for the first parser:

- Parse with invariant culture.
- Require the exact header names above.
- Require finite numeric values.
- Require non-negative, monotonically increasing `distanceMeters`.
- Preserve row order exactly.
- Do not infer missing tangent, normal, or binormal values in the first version.
- Treat the CSV as a Quantum test/debug fixture, not as full NoLimits compatibility.

Fixture metadata should stay outside the rows at first: the command or test can pass `sourceFixtureName`, and units should default to `meters`.

## Milestone 7 Synthetic Fixture Pack

The fixture pack lives under `Quantum.Tests/IO/Fixtures` and uses the same sampled-frame CSV schema. These files are intentionally tiny, synthetic, and self-authored:

- `Milestone7.synthetic.straight_line.centerline_frames.csv`: flat X-axis control case with fixed tangent/normal/binormal axes.
- `Milestone7.synthetic.simple_hill.centerline_frames.csv`: vertical grade changes with changing tangent and normal vectors.
- `Milestone7.synthetic.banked_turn.centerline_frames.csv`: horizontal quarter-turn samples with rolled normal/binormal axes.
- `Milestone7.synthetic.descending_ascending_curve.centerline_frames.csv`: lateral curve with descending and ascending grade changes.

The regression tests parse each CSV, preserve row count and monotonically increasing station distances, map the rows to `DebugViewportSnapshotV1`, validate the DTO, and verify JSON serialize/deserialize determinism. Command-path tests generate JSON into temporary directories and run `debug-viewport-snapshot-v1-validate` against those temp snapshots. Generated snapshot JSON is not committed.

## Code Placement

Parser/import code should live in `Quantum.IO`, for example:

- `Quantum.IO/Fixtures/Csv/CenterlineFrameCsvFixture.cs`
- `Quantum.IO/Fixtures/Csv/CenterlineFrameCsvFixtureParser.cs`

That keeps file parsing near the existing export contracts without adding renderer or frontend dependencies. `Quantum.IO` already references `Quantum.Math` and `Quantum.Track`, so the parser can return `TrackFrame` samples directly.

The debug command bridge should live in `Quantum.Debug` only after the parser exists, for example:

```powershell
dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-from-csv Quantum.Tests/IO/Fixtures/Milestone5.synthetic.centerline_frames.csv artifacts/debug-viewport/Milestone5.synthetic.json
```

That command should be a thin adapter:

1. Read the CSV fixture through `Quantum.IO`.
2. Convert rows to `TrackFrame[]`.
3. Build a `DebugViewportSnapshotV1Source` with `Units = "meters"`, `SourceFixtureName`, and `SampledFrames`.
4. Call `DebugViewportSnapshotV1Mapper.Export(source)`.
5. Serialize with `DebugViewportSnapshotV1Json.Serialize`.

If `outputJsonPath` is omitted, the command writes beside the input CSV using the suffix `.debug-viewport-snapshot-v1.json`.

Validate the generated JSON before handing it to Unity, Unreal, Avalonia/Silk.NET, OpenTK, or another viewer:

```powershell
dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-validate artifacts/debug-viewport/Milestone5.synthetic.json
```

The validator stays in `Quantum.IO/DebugViewport/V1` and checks the renderer-neutral contract only: contract/version, metadata, finite numeric values, centerline distance ordering, sample/frame count consistency, non-zero frame vectors, positive box dimensions, finite line endpoints, and nested train pose diagnostics when present.

No Unity, Unreal, Avalonia, Silk.NET, OpenTK, Veldrid, renderer, or engine package should be referenced by any `Quantum.*` project for this path.

## Test Plan For The First Implementation

The tiny synthetic fixture lives under `Quantum.Tests/IO/Fixtures` and is copied to test output like the existing JSON fixtures.

Coverage includes:

- Parser returns the same frame count and values on repeated parses.
- Parser preserves sample count, row order, station distances, and source fixture metadata when mapped.
- Command parser accepts `debug-viewport-snapshot-v1-from-csv`.
- Command parser accepts `debug-viewport-snapshot-v1-validate`.
- Command writes an output JSON file.
- Serialized output deserializes through `DebugViewportSnapshotV1Json.Deserialize`.
- The deserialized contract is `quantum.debug_viewport_snapshot` with version `1`.
- Metadata preserves units, source fixture name, and sample count.
- Centerline and frame counts match the CSV fixture row count.
- Validator accepts valid generated snapshot JSON.
- Validator rejects invalid contract/version, sample count mismatches, decreasing distances, and malformed numeric/frame/box data.
- Validate command prints contract/version, units, source fixture name, counts, train pose presence, and pass/fail status.
- Running the command twice with the same input produces identical JSON.
- A backend dependency guard confirms `Quantum.IO` and `Quantum.Debug` do not reference Unity, Unreal, Avalonia, Silk.NET, OpenTK, Veldrid, or other renderer/frontend assemblies.

This should be enough to connect a tiny fixture to the existing snapshot pipeline while keeping the change small and reversible.
