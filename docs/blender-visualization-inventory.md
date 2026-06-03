# Blender Visualization Inventory

Last updated: 2026-06-03

Scope audited:

- Optional Blender tooling under `tools/blender/`
- Blender-related visualization docs under `docs/visualization/`
- Renderer-neutral artifacts currently consumed by the Blender path

This inventory is separate from `docs/unity-visualizer-inventory.md`. Unity
scripts, Unity editor windows, prefabs, DLL copy steps, and Unity `TextAsset`
workflows are intentionally not inventoried here.

Status labels used:

- `Current`: aligned with the current artifact-first debug visualization path.
- `Candidate`: plausible future Blender-facing handoff, but not implemented or
  not locked.
- `Deferred`: explicitly later work.
- `Out of scope`: should not be added to the backend or current handoff.

## Current Blender-Facing Tooling

| Item | Purpose | Status | Dependencies | Backend Contract Impact | Recommended |
|---|---|---|---|---|---|
| `tools/blender/import_debug_viewport_snapshot_v1.py` | Imports `DebugViewportSnapshotV1` JSON into Blender as generated collections, centerline curves, frame tick curves, debug line curves, placeholder boxes, camera, and light. | Current | Blender Python only: `bpy`, `mathutils`, Python standard library. Must run inside Blender. | None. Consumes `quantum.debug_viewport_snapshot` v1 as an adapter. | Keep |
| `tools/blender/import_train_pose_export_v1.py` | Imports `TrainPoseExportV1` JSON into Blender as a generated train-pose collection with body, bogie, wheel, transform-empty, and axis placeholder diagnostics. | Current | Blender Python only: `bpy`, `mathutils`, Python standard library. Must run inside Blender. | None. Consumes `quantum.train_pose` v1 as an adapter. | Keep |
| `tools/blender/import_debug_scene.py` | Imports `DebugViewportSnapshotV1`, `TrainPoseExportV1`, or both into one generated `Quantum Debug Scene` diagnostic collection by reusing the existing Blender importers. | Current | Blender Python only: `bpy`, `mathutils`, Python standard library, and sibling scripts in `tools/blender/`. Must run inside Blender. | None. Coordinates existing v1 contracts as a Blender-side adapter only. | Keep |
| `docs/visualization/blender-debug-viewer.md` | Usage notes for generating snapshot artifacts and importing them into Blender from command line or Blender Scripting workspace. | Current | Documentation only. | None. | Keep |
| `docs/visualization/blender-train-pose-viewer.md` | Usage notes for importing `TrainPoseExportV1` JSON into Blender for train hierarchy inspection. | Current | Documentation only. | None. | Keep |
| `docs/visualization/blender-debug-scene-viewer.md` | Usage notes for the combined debug scene importer, including snapshot-only, train-only, and combined import modes. | Current | Documentation only. | None. | Keep |
| `docs/blender-handoff.md` | Milestone 65 handoff boundary for Blender as an optional visualization/import layer. | Current | Documentation only. | None. | Keep |
| `docs/blender-visualization-inventory.md` | Milestone 65 inventory of current and future Blender-facing artifacts and boundaries. | Current | Documentation only. | None. | Keep |

## Renderer-Neutral Inputs

| Artifact | Producer | Blender Use | Status | Notes |
|---|---|---|---|---|
| `DebugViewportSnapshotV1` JSON | `Quantum.Debug` commands and `Quantum.IO` mapping/serialization. | Primary current Blender input for centerline, frames, lines, placeholder boxes, and optional nested train-pose presence; can be paired with `TrainPoseExportV1` in the combined debug scene importer. | Current | Preferred handoff source. Keep camera, material, mesh, and scene concerns outside the contract. |
| `TrainPoseExportV1` JSON | `Quantum.IO` train pose export path and debug commands. | Current input for full body/bogie/wheel hierarchy diagnostics when the flatter snapshot box layer is not enough; can be imported alone or alongside a snapshot in the combined debug scene importer. | Current | Remains a train pose snapshot contract, not a Blender scene format. |
| Self-authored sampled-frame CSV fixtures | `Quantum.Tests/IO/Fixtures` plus `Quantum.Debug` CSV-to-snapshot command. | Fixture input only after backend conversion to `DebugViewportSnapshotV1` JSON. | Current | Blender should not parse NoLimits/project CSV directly for the current path. |
| Generated SVG previews | `Quantum.Debug` SVG command and demo script. | Human reference only; Blender import should use JSON, not SVG as authoritative geometry. | Current | SVG smoothing/preview behavior is not backend geometry. |
| Generated HTML gallery/browser | `Quantum.Debug` gallery/browser commands and demo script. | Human reference only. | Current | Local static debug aid, not a Blender dependency. |
| Mesh export artifacts | Future export adapter. | Possible future source for Blender mesh inspection. | Deferred | Define a neutral export boundary first. Do not add mesh handles to current JSON contracts. |
| GLTF/GLB | Future export/import path. | Possible future interchange format for richer visualization. | Deferred | Keep scale, pivot, material, and import policy adapter-owned. |

## Current Importer Behavior

`tools/blender/import_debug_viewport_snapshot_v1.py` currently:

- accepts a JSON path after `--` or through `--snapshot`
- opens a Blender file picker when run interactively without a path
- verifies `contract == "quantum.debug_viewport_snapshot"` and `version == 1`
- creates or reuses a generated collection named `Quantum DebugViewportSnapshotV1`
- clears the generated collection before rebuilding it
- maps Quantum Y-up coordinates into Blender Z-up coordinates
- creates a centerline curve from `centerlinePoints`
- creates sampled tangent, normal, and binormal tick curves from `frames`
- groups snapshot `lines` by stable line kind
- creates oriented placeholder cube objects from snapshot `boxes`
- creates a simple debug camera and area light
- prints import counts to Blender's console

It does not:

- evaluate Quantum backend code inside Blender
- import Unity assets or prefabs
- parse CSV fixtures directly
- render nested `TrainPoseExportV1` hierarchy
- export GLTF/GLB
- create production materials or train meshes
- change backend contracts

`tools/blender/import_train_pose_export_v1.py` currently:

- accepts a JSON path after `--` or through `--pose`
- opens a Blender file picker when run interactively without a path
- verifies `contract == "quantum.train_pose"` and `version == 1`
- creates or reuses a generated collection named `Quantum TrainPoseExportV1`
- clears only that generated collection before rebuilding it
- maps Quantum Y-up coordinates into Blender Z-up coordinates
- creates placeholder cubes named from the neutral hierarchy prefixes
  `train.body`, `train.bogie`, and `train.wheel`
- creates simple transform empties and grouped axis tick curves for available
  body, bogie, and wheel transforms
- applies wheel local offsets on top of the exported wheel/bogie pose so the
  current v1 wheel layout is visible
- creates a simple debug camera and area light
- prints import counts to Blender's console

It does not:

- evaluate Quantum backend code inside Blender
- import Unity assets, prefabs, scripts, or DLLs
- parse CSV fixtures directly
- create production train art, bogie models, wheel models, or materials
- change `TrainPoseExportV1`, `DebugViewportSnapshotV1`, `TrackFrame`, or
  backend train placement contracts

`tools/blender/import_debug_scene.py` currently:

- accepts `--snapshot`, `--pose`/`--train`, or one/two positional JSON paths
  after `--`
- auto-classifies positional JSON inputs by `contract` and `version`
- allows snapshot-only, train-only, and combined imports
- verifies the existing `quantum.debug_viewport_snapshot` v1 and
  `quantum.train_pose` v1 contract identities through the existing importers
- creates or reuses a generated collection named `Quantum Debug Scene`
- clears only that generated collection before rebuilding it
- creates a `snapshot` child collection by reusing the snapshot importer for
  centerline, frame tick, debug line, and placeholder box visualization
- creates a `train_pose` child collection by reusing the train pose importer for
  body, bogie, wheel, transform-empty, and axis diagnostics
- creates one combined debug camera and area light from the combined imported
  bounds
- prints combined import counts to Blender's console

It does not:

- define a new Blender scene JSON contract
- modify `DebugViewportSnapshotV1`, `TrainPoseExportV1`, `TrackFrame`, or
  backend train placement contracts
- modify `Quantum.Core`, `Quantum.Track`, `Quantum.IO`, or other backend
  projects
- import Unity assets, prefabs, scripts, or DLLs
- parse CSV fixtures directly
- create production train art, bogie models, wheel models, or materials
- unpack nested `TrainPoseExportV1` data from a snapshot as a substitute for a
  train-pose JSON import

## Future Blender Candidates

| Candidate | Purpose | Status | Contract Boundary |
|---|---|---|---|
| Mesh export smoke path | Export simple backend-owned diagnostic meshes for Blender inspection. | Deferred | Add a separate neutral mesh/export artifact, not fields inside `DebugViewportSnapshotV1`. |
| GLTF/GLB handoff | Use standard interchange for richer viewer or presentation assets. | Deferred | Keep GLTF/GLB as export/import adapter output. Do not add Blender or GLTF dependencies to core backend projects. |
| Render preset helper | Optional Blender-side script for repeatable camera/render settings. | Candidate | Blender-only settings file or script under `tools/blender/`; no backend fields. |
| Screenshot smoke check | Optional manual or scripted validation that an imported snapshot is visible. | Candidate | Runs outside backend tests unless a future CI environment explicitly supports Blender. |

## Out-of-Scope Items

| Item | Reason |
|---|---|
| Blender package references in `Quantum.Core` | Core backend must stay engine-agnostic and build without Blender/Python. |
| `bpy` or Python dependency in backend projects | Blender adapter concern only. |
| Blender material/camera/light fields in `DebugViewportSnapshotV1` | Renderer-owned concerns should stay outside renderer-neutral snapshot data. |
| Unity prefab metadata in Blender docs | Unity handoff stays separate. |
| Third-party or production train art committed as fixture assets | Current fixtures and visuals should be self-authored and minimal unless permission is explicit. |
| Full NoLimits project import through Blender | Current CSV path is a narrow backend debug/test bridge only. |

## Consolidation Notes

- `DebugViewportSnapshotV1` remains the best current Blender input because it
  already carries stable point, tangent, frame, line, box, and optional train
  pose data without renderer ownership.
- `TrainPoseExportV1` is now a current Blender input for focused train hierarchy
  inspection. It should stay diagnostic-only and should not grow renderer-owned
  fields.
- `tools/blender/import_debug_scene.py` is the current combined inspection path
  when a centerline snapshot and detailed train pose need to be viewed together.
  It coordinates the existing Blender importers instead of creating a new
  renderer-owned backend artifact.
- The current Blender importers are intentionally thinner than the Unity debug
  viewer path. That remains acceptable because Blender is being used as an
  optional visualization/import surface, not a live backend host.
- Future GLTF/GLB work should start from a neutral Quantum export artifact and a
  documented adapter convention for units, pivots, and axes. It should not
  retrofit Blender or GLTF fields into existing snapshot contracts.
