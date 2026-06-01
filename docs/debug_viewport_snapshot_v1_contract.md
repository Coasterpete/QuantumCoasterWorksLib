# DebugViewportSnapshotV1 JSON Contract

- Contract name: `quantum.debug_viewport_snapshot`
- Version: `1`
- Serialization naming: camelCase JSON properties
- Intended consumers: Unity, Unreal, standalone editors, test tools, and other thin visualization adapters.

This contract is a backend export boundary for debug viewport data. It describes sampled coaster-domain data and simple debug primitives only; it does not describe cameras, meshes, materials, draw calls, editor widgets, or renderer resources.

## Top-Level Fields

- `contract` (string, required): must equal `quantum.debug_viewport_snapshot`.
- `version` (integer, required): must equal `1`.
- `metadata` (object, required): units, optional source fixture name, and sample count.
- `centerlinePoints` (array, required): sampled station-distance points on the centerline.
- `frames` (array, required): optional orientation frame samples when available from backend sampling.
- `lines` (array, required): renderer-neutral debug line segments.
- `boxes` (array, required): renderer-neutral oriented debug boxes for placeholders such as bodies, bogies, or wheels.
- `trainPose` (object or null, optional): nested `TrainPoseExportV1` payload when a complete train pose snapshot is available.

## Coordinate Convention

Frame convention matches `Quantum.Track.TrackFrame`:

- `position` = centerline or placeholder center in backend track-space coordinates.
- `tangent` = forward axis.
- `normal` = up axis.
- `binormal` = right/lateral axis.
- `distance` = backend station distance `s`.

Box dimensions are coaster-domain dimensions:

- `length` follows the frame tangent.
- `height` follows the frame normal.
- `width` follows the frame binormal.

## Units

`metadata.units` defaults to `meters`, matching the current backend station-distance convention. Viewers may convert units at adapter boundaries, but this contract does not embed renderer or engine coordinate conversion policy.

## Sample Artifact

Generate a deterministic backend sample with:

```powershell
dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1
```

The default output path is `artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json`. An explicit path can be passed as the first command argument.

Generate a BankingProfile train-pose inspection sample with:

```powershell
dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-banking-profile
```

The default output path is `artifacts/debug-viewport/DebugViewportSnapshotV1.banking-profile.sample.json`. This sample nests a `TrainPoseExportV1` payload produced by the opt-in `EvaluateTrainPose(..., BankingProfile)` runtime path. It does not add `BankingProfile` state to `TrackDocument`, change default `TrackEvaluator` sampling, or change the default train-pose overload.

When `Quantum.Debug` writes debug viewport JSON or SVG output under `artifacts/debug-viewport`, it also refreshes `artifacts/debug-viewport/snapshot-preview-index.md`. The generated Markdown index lists snapshot JSON files and matching SVG previews with repository-relative paths and last-written timestamps so local demo output is easier to find.

Validate or inspect a generated snapshot with:

```powershell
dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-validate artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json
```

The validation command is backend-only and prints a concise summary of contract/version, units, source fixture name, centerline/frame/line/box counts, train pose presence, and pass/fail status. It checks identity metadata, finite numeric values, centerline distance ordering, sample/frame count consistency, non-zero frame vectors, positive box dimensions, finite line endpoints, and nested train pose validation when `trainPose` is present.

The sample is intentionally small and frontend-neutral. It is built from the existing deterministic debug smoke scenario and includes:

- contract/version metadata and `metadata.units`
- sampled `centerlinePoints`
- matching orientation `frames`
- three frame-axis debug `lines`
- simple train body `boxes`
- nested `trainPose` data produced by the existing `TrainPoseExportV1` mapper

The BankingProfile sample follows the same `DebugViewportSnapshotV1` contract and uses the existing nested `TrainPoseExportV1` shape. Its fixture is self-authored and deterministic so current browser/debug tooling can inspect profile-backed body, bogie, wheel, and articulated frames without a renderer or UI change.

## Fixture Regression Path

`Quantum.Tests/IO/Fixtures` includes a tiny self-authored Milestone 7 sampled-frame CSV pack for the CSV-to-snapshot bridge:

- straight line
- simple hill
- banked turn
- descending/ascending curve

Tests generate `DebugViewportSnapshotV1` JSON from these CSVs in temporary directories, validate the generated payloads with the backend validator command, and assert deterministic JSON round trips. No generated fixture snapshot JSON is committed.

## Adapter Usage

A Unity, Unreal, Avalonia + Silk.NET/OpenTK, or other viewer can consume this snapshot by:

1. Drawing `centerlinePoints` as a polyline.
2. Drawing `frames` as optional axes/gizmos.
3. Drawing `lines` as debug line batches.
4. Drawing `boxes` as oriented placeholder bodies, bogies, wheels, or diagnostics.
5. Reading `trainPose` when it needs the fuller body/bogie/wheel transform hierarchy already covered by `TrainPoseExportV1`.

The adapter owns all renderer-specific concerns such as materials, colors, meshes, scene objects, camera controls, and coordinate handedness conversion.

Future viewers should keep this as a thin adapter boundary:

- Unity: load the JSON as a text asset or file, verify `contract` and `version`, convert backend vectors at the Unity adapter edge, then draw gizmos or placeholder cubes.
- Unreal: parse the same JSON into Unreal-side structs, verify identity/version, convert coordinates in the plugin/module boundary, then draw debug lines or transient actors.
- Standalone viewer: parse the JSON in the app shell, keep camera/input/rendering state outside the payload, and render the arrays with whichever viewport technology is selected later.
