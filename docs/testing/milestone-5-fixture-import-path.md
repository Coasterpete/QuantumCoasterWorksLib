# Milestone 5 Fixture Import Path

## Current Finding

There is no clean CSV fixture import path in the repository yet. The current CSV-related material is policy and release-scope documentation only:

- `docs/testing/nolimits-csv-fixtures.md` defines allowed fixture sources and scope boundaries.
- `docs/release/technical-preview-0.1-scope.md` calls for a self-authored fixture path.
- `Quantum.Debug` can already write a `DebugViewportSnapshotV1` sample, but it builds that sample from `SamplingPerfSmokeScenario`, not from CSV.

Because no parser or fixture CSV exists yet, Milestone 5 should start with a deliberately narrow sampled-frame fixture import instead of a general NoLimits importer.

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

## Code Placement

Parser/import code should live in `Quantum.IO`, for example:

- `Quantum.IO/Fixtures/Csv/CenterlineFrameCsvFixture.cs`
- `Quantum.IO/Fixtures/Csv/CenterlineFrameCsvFixtureParser.cs`

That keeps file parsing near the existing export contracts without adding renderer or frontend dependencies. `Quantum.IO` already references `Quantum.Math` and `Quantum.Track`, so the parser can return `TrackFrame` samples directly.

The debug command bridge should live in `Quantum.Debug` only after the parser exists, for example:

```powershell
dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-from-csv fixtures/milestone5.synthetic.csv artifacts/debug-viewport/Milestone5.synthetic.json
```

That command should be a thin adapter:

1. Read the CSV fixture through `Quantum.IO`.
2. Convert rows to `TrackFrame[]`.
3. Build a `DebugViewportSnapshotV1Source` with `Units = "meters"`, `SourceFixtureName`, and `SampledFrames`.
4. Call `DebugViewportSnapshotV1Mapper.Export(source)`.
5. Serialize with `DebugViewportSnapshotV1Json.Serialize`.

No Unity, Unreal, Avalonia, Silk.NET, OpenTK, Veldrid, renderer, or engine package should be referenced by any `Quantum.*` project for this path.

## Test Plan For The First Implementation

Add a tiny synthetic fixture under `Quantum.Tests/IO/Fixtures`, copied to test output like the existing JSON fixtures.

Cover these behaviors:

- Parser returns the same frame count and values on repeated parses.
- Parser preserves sample count, row order, station distances, and source fixture metadata when mapped.
- Serialized output deserializes through `DebugViewportSnapshotV1Json.Deserialize`.
- The deserialized contract is `quantum.debug_viewport_snapshot` with version `1`.
- A backend dependency guard confirms `Quantum.IO` and `Quantum.Debug` do not reference Unity, Unreal, Avalonia, Silk.NET, OpenTK, Veldrid, or other renderer/frontend assemblies.

This should be enough to connect a tiny fixture to the existing snapshot pipeline while keeping the change small and reversible.
