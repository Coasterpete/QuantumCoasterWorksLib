# Distance Inspection JSON Handoff

The distance inspection JSON handoff is a small, renderer-neutral snapshot for frontend/debug tools that need to inspect section coverage and evaluated channel values at one centerline distance. It lets Unreal, C++, BGFX, Unity, or other consumers build UI/debug views from stable JSON without depending on C# evaluator internals.

This contract is `DistanceInspectionSnapshotV1`. Treat v1 as stable: future breaking changes should use a new version rather than mutating this shape.

## Contract Files

- Schema: `docs/contracts/distance-inspection-snapshot-v1.schema.json`
- Sample: `docs/contracts/distance-inspection-snapshot-v1.sample.json`
- Backend DTOs/JSON mapper: `Quantum.IO/DistanceInspection/V1`

## Generate A Fresh Sample

Generate the default debug sample:

```powershell
dotnet run --project Quantum.Debug -- distance-inspection-json
```

The default output path is `artifacts/track/distance-inspection.sample.json`.

Pass an explicit output path when a frontend fixture should live somewhere else:

```powershell
dotnet run --project Quantum.Debug -- distance-inspection-json artifacts/track/distance-inspection.sample.json
```

The command writes a deterministic backend debug artifact. It does not change evaluator behavior or the v1 contract shape.

## Top-Level Fields

- `contract`: must be `quantum.distance_inspection_snapshot`.
- `version`: must be `1`.
- `distance`: inspected centerline distance.
- `sections`: ordered list of sections active or relevant at `distance`.

## Section Fields

- `kind`: section kind string, such as `Force` or `Geometry`.
- `domain`: section domain string, such as `Distance`.
- `startX`: section start coordinate in its domain.
- `endX`: section end coordinate in its domain.
- `diagnostic`: section evaluation diagnostic string.
- `channels`: ordered channel names declared by the section.
- `channelValues`: ordered evaluated channel/value pairs at the inspected distance.

## Channel Value Fields

- `channel`: evaluated channel name.
- `value`: evaluated numeric value.

## Consumer Guidance

- Validate `contract` and `version` before reading the rest of the payload.
- Treat enum-like values as strings owned by the contract, not as numeric enum ordinals.
- Preserve `sections` order when presenting or comparing output.
- Preserve `channels` and `channelValues` order.
- Do not assume every section kind is present.
- Treat the payload as a read-only UI/debug snapshot, not an editable source of truth.

Unreal, C++, BGFX, Unity, and future debug frontends should parse this v1 contract as a stable data handoff before any native engine port exists. Engine-specific structs, materials, widgets, draw calls, and coordinate conversion belong in the consumer adapter, not in the backend contract.
