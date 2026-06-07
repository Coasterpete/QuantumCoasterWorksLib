# Distance Inspection Native Adapter Strategy

This document describes the recommended path for native Unreal, C++, BGFX, Unity, and other frontend consumers of the distance inspection contract.

The near-term goal is not to convert the tested C# backend into C++. The first step is a native consumer adapter that reads `DistanceInspectionSnapshotV1` JSON and builds engine-specific debug UI or visualization from that stable handoff.

## Recommended Near-Term Architecture

Keep the C# backend as the source of truth for centerline, section, channel, and distance inspection behavior. Native frontends should consume backend output rather than reimplement backend rules.

Use `DistanceInspectionSnapshotV1` JSON as the stable frontend handoff:

- Schema: `docs/contracts/distance-inspection-snapshot-v1.schema.json`
- Sample: `docs/contracts/distance-inspection-snapshot-v1.sample.json`
- Manifest: `docs/contracts/distance-inspection-snapshot-v1.manifest.json`
- Handoff guide: `docs/contracts/distance-inspection-json-handoff.md`

Native frontends should parse the schema, sample, and manifest first. This gives Unreal, C++, BGFX, Unity, or another host a deterministic contract to target before any native backend exists.

Keep frontend-specific work in the native adapter:

- Engine-specific structs and view models.
- Widgets, panels, inspectors, and editor tooling.
- Materials, meshes, draw calls, and renderer resources.
- Coordinate conversion and host-specific transform conventions.
- Selection, highlighting, filtering, and debug presentation policy.

Do not push those concerns into the backend contract. The backend contract should remain a renderer-neutral snapshot of debug state.

## Why Not Mass-Convert The Backend Yet

The C# backend has growing test coverage around coaster-domain behavior. A bulk C++ conversion would risk behavior drift in centerline evaluation, section coverage, channel evaluation, and distance-based placement before native consumers have a stable contract to compare against.

AI-assisted conversion tools can still be useful, but only for small isolated modules where parity can be proven. Do not use automated conversion as a reason to replace broad backend areas without tests.

A native C++ backend can happen later. The safer path is:

1. Build native consumers against the JSON contract.
2. Establish sample and golden-output comparisons.
3. Translate only isolated modules when parity tests exist.
4. Replace backend behavior only after native output matches the tested C# behavior.

## Native Consumer Responsibilities

Native adapters should follow the v1 contract exactly:

- Validate `contract` before reading the payload.
- Validate `version` before reading the payload.
- Parse enum-like values as strings, not numeric enum ordinals.
- Preserve `sections` order.
- Preserve `channels` order.
- Preserve `channelValues` order.
- Treat missing section kinds as normal.
- Treat the payload as read-only UI/debug state.

The payload is not an editable document model. It is a snapshot for inspection, comparison, debug rendering, and frontend UI.

## Future C++ Options

Native work can evolve in stages:

1. JSON-only native adapter.
   Parse `DistanceInspectionSnapshotV1` JSON directly in the native frontend and build UI/debug visualization from it.

2. Native C++ DTO structs.
   Generate or handwrite simple DTO structs from the JSON schema. Keep them contract-shaped and separate from engine UI, renderer, and editor types.

3. Bindings or interop layer.
   Expose selected backend calls to native hosts only when the call boundary is clear and testable.

4. Full C++ port of isolated math modules.
   Port only small math or geometry modules after C# golden outputs and parity tests exist. Avoid replacing broad coaster-domain behavior until equivalence is proven.

## Unreal Guidance

Start by parsing the JSON into Unreal-friendly structs inside an Unreal adapter or plugin. Those structs may use Unreal naming, reflection, memory, and editor conventions, but they should stay outside the backend contract.

Build editor and debug UI around the parsed snapshot:

- Section lists.
- Channel/value tables.
- Distance readouts.
- Diagnostic badges.
- Optional debug drawing based on adapter-owned transform conversion.

Do not mix Unreal-specific types into `DistanceInspectionSnapshotV1`, the schema, the sample payload, or backend DTOs. `UObject`, `Actor`, `Component`, Blueprint, material, viewport, and editor concepts belong in the Unreal adapter.

## Testing Expectations

Any future C++ consumer or equivalent DTO layer should read the same checked-in sample JSON:

- `docs/contracts/distance-inspection-snapshot-v1.sample.json`

Any translated math or geometry module should be tested against C# golden outputs before it replaces existing backend behavior. Parity tests should compare meaningful coaster-domain results, not only successful parsing.

For now, passing the existing .NET tests and building the solution should remain enough for documentation-only changes.
