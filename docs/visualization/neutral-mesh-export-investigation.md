# Neutral Mesh Export Investigation

Last updated: 2026-06-03

Milestone 74 adds a small `MeshExportV1` contract sketch in `Quantum.IO` with
DTOs, JSON serialization, and validation. It does not add a real mesh generator,
export command, Blender importer, adapter, renderer dependency, or generated
mesh fixture.

## Current Decision

Mesh export is now represented by a separate neutral `MeshExportV1` sketch. It
is topology-only and intentionally small: contract identity, version, named
meshes, vertices, flat triangle indices, optional normals, and neutral material
slot labels.

Do not add mesh fields to `DebugViewportSnapshotV1` or `TrainPoseExportV1`.
Those contracts already have clear purposes:

- `DebugViewportSnapshotV1` is a sampled debug viewport snapshot for centerline
  points, frames, debug lines, placeholder boxes, and optional train-pose
  pairing.
- `TrainPoseExportV1` is a train pose hierarchy snapshot for bodies, bogies,
  wheels, frames, matrices, distances, and consist geometry.
- Mesh data is renderable topology, not sampled track state or train placement
  state.

Keeping mesh export separate protects the current contracts from renderer-owned
concerns such as topology layout, material binding, import scale, smoothing,
asset pivots, UV channels, and file-format policy.

## Why Not Extend Existing Contracts

Adding mesh fields to `DebugViewportSnapshotV1` would blur a debug primitive
contract into a scene or asset contract. Snapshot boxes currently communicate
semantic placeholder geometry, not production topology. A viewer can draw those
boxes as gizmos, curves, cubes, or inspector overlays without owning mesh import
policy.

Adding mesh fields to `TrainPoseExportV1` would couple train placement to train
art. The train pose contract should continue to answer where bodies, bogies, and
wheels are in coaster-domain space. It should not decide which mesh, material
slot, pivot, UV layout, or renderer asset is attached to those transforms.

Separate mesh export also allows different validation and lifecycle rules. A
pose snapshot can stay small and deterministic while a future mesh artifact can
grow richer topology, optional normals, UVs, labels, and adapter notes without
forcing every pose/debug consumer to parse unused geometry arrays.

## Current Contract Sketch

`MeshExportV1` is renderer-neutral and Quantum-owned, but still only a sketch.
The current DTO shape is:

- `contract`: `quantum.mesh_export`.
- `version`: `1`.
- `meshes[]`: one or more named mesh records.
- `meshes[].name`: stable mesh name.
- `meshes[].vertices`: positions in Quantum/backend space.
- `meshes[].triangleIndices`: flat indexed triangles.
- `meshes[].normals`: optional per-vertex normals.
- `meshes[].materialSlotLabels`: neutral labels only, such as `track.rail`,
  `track.spine`, `train.body`, or `support.column`.

The validator rejects empty mesh collections, empty mesh topology, malformed
triangle index counts, out-of-range triangle indices, non-finite vertex/normal
values, mismatched normal counts, and empty material slot labels. Serialization
uses the same camelCase `System.Text.Json` pattern as the existing v1 contracts.

The sketch deliberately does not include metadata, UVs, submeshes, material
objects, renderer handles, file paths, pivots, cameras, lights, or adapter
policy. Those remain deferred until a real exporter and consumer need them.

Material slots should be stable semantic labels, not Blender materials, Unity
materials, Unreal materials, shader names, texture paths, or GLTF material
objects. Adapters can map neutral labels to local materials.

## Units, Axes, And Pivots

The future mesh artifact should preserve Quantum geometry in backend track
space. Renderer-specific unit conversion, axis conversion, handedness conversion,
and pivot interpretation should remain adapter-owned conventions.

For example:

- Blender adapters can convert Quantum's current Y-up convention into Blender
  Z-up space.
- Unity adapters can map the same neutral positions into Unity scene space and
  attach Unity materials or prefabs outside the backend contract.
- GLTF/GLB exporters can apply their own coordinate and scale policy while
  writing a standards-compliant file.
- Future standalone viewers can choose their viewport coordinate conventions at
  their own boundary.

If `MeshExportV1` includes metadata about units or source coordinate space, that
metadata should describe Quantum assumptions only. It should not prescribe a
Blender import scale, Unity transform hierarchy, GLTF node pivot, camera, light,
shader, or material asset.

Pivots should be treated carefully. A mesh record may eventually need a neutral
origin or local transform for grouping, but engine-specific pivot behavior should
not leak into the backend. Adapters should own any final object-origin, node, or
scene-graph decisions.

## Adapter Consumption

Blender should consume a future mesh artifact through `tools/blender/` or an
equivalent optional adapter. No Blender `MeshExportV1` importer exists yet. A
future adapter would parse `MeshExportV1`, verify contract/version, convert
coordinates into Blender space, create Blender mesh objects, assign local
materials from neutral labels, and keep `.blend`, render, and converted outputs
out of committed source by default.

Unity should consume the same artifact through Unity-side tooling only. A Unity
adapter would parse the neutral payload, create transient or generated meshes,
map material-slot labels to Unity materials, and keep `UnityEngine` and
`UnityEditor` dependencies outside `Quantum.*` backend projects.

GLTF/GLB should be treated as adapter output or a separate interchange adapter,
not as the backend contract itself. A GLTF exporter can consume `MeshExportV1`
and write `.gltf` or `.glb` using documented axis, unit, material-label, and node
policies. Quantum should not need a GLTF dependency just to describe neutral
mesh topology.

Future viewers, including Avalonia plus Silk.NET/OpenTK or other technical
viewports, should consume the neutral artifact the same way: validate identity,
read topology, map labels locally, and own renderer resources at the viewer
boundary.

## Deferred Work

The mesh export path remains a contract sketch until a later milestone defines a
real generator/exporter and importer. Likely next steps:

- Decide whether the first real exporter writes JSON-only, binary, or a JSON
  manifest plus binary buffers.
- Define triangle winding, normal requirements, optional UV channels, and any
  additional validation diagnostics.
- Decide how mesh artifacts relate to track sections, generated supports, train
  placeholders, and production train art.
- Add a backend mesh generator or export adapter.
- Add a Blender importer only in optional tooling.
- Add adapter smoke tests only in optional tooling, not core backend projects.

Until then, current Blender, Unity, and browser diagnostics should continue to
use `DebugViewportSnapshotV1`, `TrainPoseExportV1`, SVG previews, and local
adapter-owned placeholder geometry.
