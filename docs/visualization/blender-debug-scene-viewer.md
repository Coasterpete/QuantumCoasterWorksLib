# Blender Debug Scene Viewer

The Blender debug scene viewer is an optional diagnostic adapter that combines
`DebugViewportSnapshotV1` and `TrainPoseExportV1` JSON in one generated Blender
scene. It is useful when the track centerline, frame ticks, debug lines,
snapshot boxes, and detailed train body/bogie/wheel placeholders need to be
inspected together. The combined importer applies a brighter diagnostic style
than the single-artifact importers so the generated scene is easier to read in
the viewport and in quick screenshots.

This workflow does not define a new backend contract. It reuses the existing
renderer-neutral JSON artifacts and the existing Blender importers under
`tools/blender/`.

## Generate Input Artifacts

From the repository root, create or refresh a debug viewport snapshot:

```powershell
dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1 artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json
```

Create or refresh a train pose export:

```powershell
dotnet run --project Quantum.Debug -- train-pose-export-v1 artifacts/train-pose/TrainPoseExportV1.sample.json
```

Generated JSON and Blender outputs are local debug artifacts by default. Do not
commit generated `.blend`, render, or screenshot files unless there is a clear
release reason.

## Run From Blender Command Line

Snapshot-only import:

```powershell
blender --python tools/blender/import_debug_scene.py -- --snapshot artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json
```

Train-only import:

```powershell
blender --python tools/blender/import_debug_scene.py -- --pose artifacts/train-pose/TrainPoseExportV1.sample.json
```

Combined import:

```powershell
blender --python tools/blender/import_debug_scene.py -- --snapshot artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json --pose artifacts/train-pose/TrainPoseExportV1.sample.json
```

The script also accepts one or two positional JSON files and classifies them by
their `contract` and `version` fields:

```powershell
blender --python tools/blender/import_debug_scene.py -- artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json artifacts/train-pose/TrainPoseExportV1.sample.json
```

## Render A Diagnostic PNG

For repeatable screenshot smoke checks, run the combined importer from a clean
factory-startup Blender session and pass `--render-output`. The importer still
uses the same snapshot-only, train-only, and combined import modes; the render
step is optional and runs after the generated camera frames the imported bounds.

```powershell
$render = Join-Path $env:TEMP "quantum-debug-scene-smoke.png"
blender --background --factory-startup --python tools/blender/import_debug_scene.py -- --snapshot artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json --pose Assets/DebugData/TrainPoseExportV1.sample.json --render-output $render --resolution-width 1280 --resolution-height 720 --camera-mode diagnostic
```

`--render-output <path>` writes a PNG still image and creates the parent
directory when needed. `--resolution-width` and `--resolution-height` are
optional; when a render output is requested and no resolution is provided, the
script uses `1600x900`. `--camera-mode default` preserves the normal generated
camera angle, while `--camera-mode diagnostic` uses a slightly higher oblique
angle for smoke screenshots.

Use temporary render paths for smoke checks. Generated PNG, `.blend`, and other
render outputs are local diagnostics and should not be committed.

## Combined Validation Summary

When both a `DebugViewportSnapshotV1` snapshot and a `TrainPoseExportV1` train
pose are imported together, the script prints a small train-on-track validation
summary to Blender's console. Snapshot-only and train-only imports still work as
focused single-artifact views and do not run the combined spatial check.

The combined validation reports:

- snapshot/track bounds from usable `centerlinePoints`
- broader snapshot import bounds, including frame, line, and placeholder box
  extents
- train bounds from the generated train pose placeholders
- train body position count and centerline point count
- min/average/max distance from imported train body positions to the nearest
  centerline point
- warnings when the snapshot has no usable centerline geometry, the train pose
  has no usable body geometry, bounds are empty, or train bodies appear far from
  the imported centerline samples

Warnings are also stored as custom properties on the generated
`Quantum Debug Scene` collection. If warnings are present, the importer creates a
small `validation` child collection with a text object named
`Quantum.debug_scene.validation_warnings` so the issue is visible in the scene.

## Run From Blender Script Runner

1. Open Blender.
2. Switch to the `Scripting` workspace.
3. Open `tools/blender/import_debug_scene.py`.
4. Press `Run Script`.
5. Select one or two JSON files in the file picker.

For repeated manual runs, you may set one or both top-level path constants
before pressing `Run Script`:

```python
SNAPSHOT_PATH = r"C:\Dev4\QuantumCoasterWorksLib\artifacts\debug-viewport\DebugViewportSnapshotV1.sample.json"
TRAIN_POSE_PATH = r"C:\Dev4\QuantumCoasterWorksLib\artifacts\train-pose\TrainPoseExportV1.sample.json"
```

## Expected Blender Objects

The importer creates or reuses one marked generated collection named
`Quantum Debug Scene`. Re-running the import clears only that generated
collection before rebuilding it. If a non-generated collection with that name
already contains user-authored objects, the importer stops instead of clearing
it.

Inside `Quantum Debug Scene`, the script creates:

- `snapshot`: present when a `DebugViewportSnapshotV1` input is imported.
  - `track_geometry`: main snapshot geometry that can be toggled as one group.
    - `centerline`: renderable curve through `centerlinePoints`.
    - `debug_lines`: renderer-neutral `lines`, grouped by stable kind.
    - `boxes`: oriented placeholder cube boxes from snapshot `boxes`.
  - `snapshot_inspection_overlays`: diagnostic overlays that can be hidden
    without hiding the centerline or boxes.
    - `frames`: tangent, normal, and binormal tick curves sampled from `frames`.
- `train_pose`: present when a `TrainPoseExportV1` input is imported.
  - `train_geometry`: train placeholder geometry that can be toggled as one
    group.
    - `bodies`: train body placeholder cubes.
    - `bogies`: train bogie placeholder cubes.
    - `wheels`: train wheel placeholder cubes.
  - `train_inspection_overlays`: transform and frame aids that can be hidden
    without hiding the train placeholders.
    - `transforms`: body, bogie, and wheel transform empties.
    - `axes`: tangent, normal, and binormal pose tick curves.
- `scene`: `Quantum.debug_scene_camera` and `Quantum.debug_scene_key_light`.
- `validation`: present only when combined train-on-track validation produces
  warnings.
  - `Quantum.debug_scene.validation_warnings`: small text note summarizing the
    warnings.

## Styling

The generated materials are Blender-side diagnostics only. They are not part of
`DebugViewportSnapshotV1` or `TrainPoseExportV1`.

- Track centerline: bright teal, thicker than secondary debug curves.
- Frame and train pose axes: tangent blue, normal green, binormal violet.
- Debug lines: warm orange/yellow so they stand apart from the centerline.
- Snapshot placeholder boxes: translucent amber.
- Train bodies: translucent blue.
- Bogies: gold.
- Wheels: opaque dark graphite.
- Unknown placeholders: translucent red.

The default camera is an orthographic generated camera named
`Quantum.debug_scene_camera`. It frames the combined imported bounds, including
snapshot placeholder box extents, and is intended as a convenient starting view
rather than a production render camera. When `--render-output` is used, this
camera is also the still-render camera.

Quantum track space is Y-up. The reused Blender adapters map positions and
frame axes into Blender Z-up space as:

```text
Quantum (x, y-up, z-lateral) -> Blender (x, y-lateral, z-up)
```

## Boundary Notes

The combined importer does not modify `DebugViewportSnapshotV1`,
`TrainPoseExportV1`, `Quantum.Core`, `Quantum.Track`, `Quantum.IO`, or backend
train placement code. It does not import Unity prefabs, evaluate backend code
inside Blender, parse CSV fixtures directly, or define production train art.
The train-on-track validation is also Blender-side only; it compares imported
artifact positions and does not change either JSON contract.

The Blender scene owns only adapter-side artifacts: generated collections,
curves, placeholder meshes, empties, materials, camera, and light.
