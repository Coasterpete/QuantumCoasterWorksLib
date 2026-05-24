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

## Adapter Usage

A Unity, Unreal, Avalonia + Silk.NET/OpenTK, or other viewer can consume this snapshot by:

1. Drawing `centerlinePoints` as a polyline.
2. Drawing `frames` as optional axes/gizmos.
3. Drawing `lines` as debug line batches.
4. Drawing `boxes` as oriented placeholder bodies, bogies, wheels, or diagnostics.
5. Reading `trainPose` when it needs the fuller body/bogie/wheel transform hierarchy already covered by `TrainPoseExportV1`.

The adapter owns all renderer-specific concerns such as materials, colors, meshes, scene objects, camera controls, and coordinate handedness conversion.
