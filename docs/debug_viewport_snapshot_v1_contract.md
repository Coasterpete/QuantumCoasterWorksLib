# DebugViewportSnapshotV1 JSON Contract

- Contract name: `quantum.debug_viewport_snapshot`
- Version: `1`
- Serialization naming: camelCase JSON properties
- Machine-readable schema: `Quantum.Tests/IO/Fixtures/DebugViewportSnapshotV1.schema.json`
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

## Stable Visualization Vocabulary

Snapshot consumers should treat role/kind strings as stable semantic tokens, not renderer object names.

Box roles:

- `train.body`: normal placeholder train body box.
- `train.body.banking-profile`: train body box from the opt-in BankingProfile sample path.
- `train.bogie`: bogie placeholder box when a snapshot includes bogie boxes.
- `train.wheel`: wheel placeholder box when a snapshot includes wheel boxes.

Line kinds:

- `frame.axis.tangent`: frame tangent/forward debug line.
- `frame.axis.normal`: frame normal/up debug line.
- `frame.axis.binormal`: frame binormal/right debug line.
- `diagnostic.line`: generic diagnostic line when the line is not one frame axis.

The JSON schema and backend validator reject unknown role/kind values so importer code can map them deliberately.

## Coordinate Convention

Frame convention matches `Quantum.Track.TrackFrame`:

- `position` = centerline or placeholder center in backend track-space coordinates.
- `tangent` = forward axis.
- `normal` = up axis.
- `binormal` = right/lateral axis.
- basis handedness = right-handed, with `binormal ~= tangent x normal`.
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

When `Quantum.Debug` writes debug viewport JSON or SVG output under `artifacts/debug-viewport`, it also refreshes `artifacts/debug-viewport/snapshot-preview-index.md`. The generated Markdown index lists snapshot JSON files and matching SVG previews with repository-relative paths, last-written timestamps, nested train-pose presence, and train-pose car counts so local demo output is easier to find and inspect.

Validate or inspect a generated snapshot with:

```powershell
dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-validate artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json
```

The validation command is backend-only and prints a concise summary of contract/version, units, source fixture name, centerline/frame/line/box counts, train pose presence, and pass/fail status. It checks identity metadata, finite numeric values, centerline distance ordering, sample/frame count consistency, frame orthonormality and handedness, positive box dimensions, known role/kind vocabulary, finite and non-degenerate line endpoints, and nested train pose validation when `trainPose` is present. Nested train pose validation uses the existing `TrainPoseExportV1` validator and enables matrix bottom-row and matrix/frame consistency checks for visualization handoff readiness.

The sample is intentionally small and frontend-neutral. It is backed by the
built-in `AuthoringPipelineProofScenario`, so the normal sample command proves
the `Quantum.Track.Authoring` path through compilation, frame sampling,
distance-based train placement, train-pose export, and debug snapshot export.
The authored centerline is a zero-roll 12 m straight, 24 m constant-curvature
arc, and 12 m straight. The output includes nine frames at 6 m intervals and
five train body boxes centered at station distances 36, 30, 24, 18, and 12 m.
It includes:

- contract/version metadata and `metadata.units`
- sampled `centerlinePoints`
- matching orientation `frames`
- three frame-axis debug `lines`
- simple train body `boxes`
- nested `trainPose` data produced by the existing `TrainPoseExportV1` mapper

Generated SVG previews are still renderer-neutral debug artifacts. Their top-down panel now draws `lines` keyed by debug kind where practical and draws `boxes` as oriented train/body rectangles using each box frame's tangent and binormal. This does not change the JSON contract or introduce renderer/frontend dependencies.

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

Unity prefab/model placement is adapter-owned and must stay outside this contract.
For the current Unity `DebugViewportSnapshotV1TransformVisualizer`, generated box
wrappers own pose and scale, wrapper local +X/+Y/+Z map to backend
tangent/normal/binormal, and any assigned prefab is only a local-identity child.
Do not add prefab, mesh, material, import-scale, or GLB metadata fields to
`DebugViewportSnapshotV1`.
