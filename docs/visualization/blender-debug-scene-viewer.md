# Blender Debug Scene Viewer

The Blender debug scene viewer is an optional diagnostic adapter that combines
`DebugViewportSnapshotV1` and `TrainPoseExportV1` JSON in one generated Blender
scene. It is useful when the track centerline, frame ticks, debug lines,
snapshot boxes, and detailed train body/bogie/wheel placeholders need to be
inspected together.

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
  - `centerline`: renderable curve through `centerlinePoints`.
  - `frames`: tangent, normal, and binormal tick curves sampled from `frames`.
  - `debug_lines`: renderer-neutral `lines`, grouped by stable kind.
  - `boxes`: oriented placeholder cube boxes from snapshot `boxes`.
- `train_pose`: present when a `TrainPoseExportV1` input is imported.
  - `bodies`: train body placeholder cubes.
  - `bogies`: train bogie placeholder cubes.
  - `wheels`: train wheel placeholder cubes.
  - `transforms`: body, bogie, and wheel transform empties.
  - `axes`: tangent, normal, and binormal pose tick curves.
- `scene`: `Quantum.debug_scene_camera` and `Quantum.debug_scene_key_light`.

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

The Blender scene owns only adapter-side artifacts: generated collections,
curves, placeholder meshes, empties, materials, camera, and light.
