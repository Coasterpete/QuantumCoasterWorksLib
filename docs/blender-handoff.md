# Milestone 65 Blender Handoff

Last updated: 2026-06-02

Blender is an optional visualization and import layer for Quantum debug output.
It is useful for screenshots, quick render checks, artifact review, and later
artist-facing inspection, but it is not part of the backend contract surface.

This handoff is separate from the Unity handoff. Unity-specific scripts,
prefabs, editor windows, DLL copy steps, and `Assets/DebugData` sync workflows
belong in the Unity docs. Blender-specific scripts and manual import notes
belong here or under `docs/visualization/`.

## Boundary Rules

- Do not modify `DebugViewportSnapshotV1`, `TrainPoseExportV1`, `TrackFrame`, or
  backend train placement contracts for Blender.
- Do not add Blender, Python, `bpy`, `mathutils`, mesh, material, camera, light,
  shader, scene object, or render dependency references to `Quantum.Core` or
  other core backend projects.
- Keep Blender code under optional tooling locations such as `tools/blender/`.
- Keep generated `.blend`, render images, mesh exports, and converted GLTF/GLB
  files out of committed source unless there is an explicit release reason.
- Use renderer-neutral Quantum artifacts as the handoff source whenever
  possible: JSON snapshots first, self-authored CSV samples through backend
  conversion when needed, mesh exports later, and GLTF/GLB later.

## Preferred Artifact Flow

The current Milestone 65 flow should stay artifact-first:

```text
Quantum backend/debug commands
  -> DebugViewportSnapshotV1 JSON
  -> optional SVG/HTML previews
  -> optional Blender import script
  -> Blender collections, curves, placeholder boxes, camera, and light
```

For fixture-driven debugging:

```text
self-authored or synthetic sampled-frame CSV
  -> Quantum.Debug CSV-to-snapshot command
  -> DebugViewportSnapshotV1 JSON
  -> optional Blender import
```

For future richer visualization:

```text
Quantum-owned export adapter
  -> neutral mesh or scene artifact
  -> GLTF/GLB candidate
  -> optional Blender import
```

The backend should not know whether Blender, Unity, Unreal, a browser preview,
or a future standalone viewport consumes the artifact.

## Current Snapshot Handoff

Generate current debug viewport artifacts from the repository root:

```powershell
.\tools\demo-technical-preview-0.1.cmd
```

Or generate only the built-in snapshot:

```powershell
dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1 artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json
```

Validate before Blender import:

```powershell
dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-validate artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json
```

Import into Blender with Blender on `PATH`:

```powershell
blender --python tools/blender/import_debug_viewport_snapshot_v1.py -- artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json
```

The importer consumes `quantum.debug_viewport_snapshot` version `1` JSON and
creates a generated Blender collection named `Quantum DebugViewportSnapshotV1`.
It maps Quantum Y-up track space to Blender Z-up space at the adapter boundary:

```text
Quantum (x, y-up, z-lateral) -> Blender (x, y-lateral, z-up)
```

See `docs/visualization/blender-debug-viewer.md` for the current operator and
manual Scripting workspace usage.

## Blender Adapter Responsibilities

The Blender side owns:

- coordinate-system conversion into Blender space
- collection and object naming
- curve, mesh, camera, light, material, and render setup
- object cleanup for regenerated imports
- viewport/camera framing
- screenshot and render output
- any eventual GLTF/GLB import settings

The Blender side must not require:

- backend contract changes
- `Quantum.Core` changes
- live backend evaluation inside Blender
- Unity scripts, prefabs, editor windows, or copied DLLs
- committed third-party art assets

## Current Expected Blender Scene

The current importer creates one generated collection with these child groups:

- `centerline`: renderable curve from snapshot `centerlinePoints`.
- `frames`: tangent, normal, and binormal tick curves sampled from snapshot
  `frames`.
- `debug_lines`: renderer-neutral snapshot `lines` grouped by stable line kind.
- `boxes`: placeholder cubes for snapshot `boxes`, oriented by frame tangent,
  binormal, and normal.
- `scene`: generated debug camera and key light.

This is deliberately a simple diagnostic scene. Materials and camera/light setup
are Blender adapter choices, not backend data.

## Neutral Artifact Preference

Use artifacts in this order unless a future milestone says otherwise:

1. `DebugViewportSnapshotV1` JSON for sampled centerline, frames, debug lines,
   placeholder boxes, and optional nested train pose.
2. Self-authored or synthetic sampled-frame CSV only as backend-side fixture
   input that converts to `DebugViewportSnapshotV1`.
3. `TrainPoseExportV1` JSON when a viewer needs the fuller train pose hierarchy
   instead of the flatter debug snapshot box view.
4. Mesh export artifacts only after a Quantum-owned export boundary exists.
5. GLTF/GLB only after mesh/material/scale/pivot expectations are documented as
   adapter-owned and do not leak into backend contracts.

Do not add Blender material names, GLTF import settings, mesh handles, cameras,
lights, or prefab-like references to `DebugViewportSnapshotV1`.

## Manual Validation Checklist

Use this checklist when refreshing the Blender handoff:

- Build and test the backend without Blender installed.
- Generate `DebugViewportSnapshotV1.sample.json`.
- Validate the JSON with the backend validator command.
- Import the JSON into Blender through `tools/blender/import_debug_viewport_snapshot_v1.py`.
- Confirm the generated collection is rebuilt cleanly on repeated imports.
- Confirm centerline, frame ticks, debug lines, and oriented boxes are visible.
- Confirm the coordinate conversion keeps vertical motion in Blender Z.
- Save screenshots or `.blend` files outside committed source by default.

## Non-Goals

- No Blender dependency in `Quantum.Core`, `Quantum.Track`, `Quantum.IO`, or
  `Quantum.Debug`.
- No Blender-driven backend API changes.
- No Unity handoff replacement.
- No production train meshes, bogies, wheels, scenery, terrain, or material
  pipeline.
- No commitment to Blender as the final editor, renderer, or presentation tool.
- No full NoLimits project import or third-party asset ingestion.
