# Blender Train Pose Viewer

The Blender train pose viewer is an optional diagnostic adapter for
`TrainPoseExportV1` JSON. It exists to inspect the exported train hierarchy with
simple body, bogie, wheel, transform-empty, and axis placeholders. It is not a
production art importer and does not change the backend contract.

Blender-specific code lives under `tools/blender/` and consumes generated JSON.
The Quantum backend remains engine-agnostic.

## Input Contract

The importer consumes the existing `TrainPoseExportV1` JSON contract only:

```text
contract: quantum.train_pose
version: 1
```

The script verifies this identity before importing. Mismatched or missing
identity fields are rejected.

## Run From Blender Command Line

With Blender on your `PATH`, run from the repository root:

```powershell
blender --python tools/blender/import_train_pose_export_v1.py -- Assets/DebugData/TrainPoseExportV1.sample.json
```

You can also pass the path with `--pose`:

```powershell
blender --python tools/blender/import_train_pose_export_v1.py -- --pose Assets/DebugData/TrainPoseExportV1.sample.json
```

The script prints a concise import summary in Blender's console, including the
loaded JSON path and counts for cars, body placeholders, bogie placeholders,
wheel placeholders, transform empties, axis curve objects, camera, and light.

## Run From Blender Script Runner

1. Open Blender.
2. Switch to the `Scripting` workspace.
3. Open `tools/blender/import_train_pose_export_v1.py`.
4. Press `Run Script`.
5. If no `TRAIN_POSE_PATH` is set in the script, Blender opens a JSON file picker.
6. Load a `TrainPoseExportV1` JSON file, such as
   `Assets/DebugData/TrainPoseExportV1.sample.json`.

For repeated manual runs, you may set the script's top-level `TRAIN_POSE_PATH`
value before pressing `Run Script`:

```python
TRAIN_POSE_PATH = r"C:\Dev4\QuantumCoasterWorksLib\Assets\DebugData\TrainPoseExportV1.sample.json"
```

## Expected Blender Objects

The importer creates one marked collection named `Quantum TrainPoseExportV1`.
Re-running the import clears only that generated collection before rebuilding
it.

Inside the collection, you should see:

- `bodies`: placeholder cubes named with `train.body...`.
- `bogies`: placeholder cubes named with `train.bogie...`.
- `wheels`: placeholder cubes named with `train.wheel...`.
- `transforms`: simple empty axes for available body, bogie, and wheel transform
  points.
- `axes`: grouped tangent, normal, and binormal tick curves.
- `scene`: `Quantum.train_pose_camera` and `Quantum.train_pose_key_light`.

Quantum track space is Y-up. The Blender adapter maps positions and frame axes
into Blender Z-up space as:

```text
Quantum (x, y-up, z-lateral) -> Blender (x, y-lateral, z-up)
```

The body cube uses exported car geometry when available. Bogie and wheel
placeholder sizes use the v1 train definition and fall back to small diagnostic
dimensions when values are missing or invalid.

## Notes

This viewer does not import Unity prefabs, read Unity assets, evaluate backend
code in Blender, parse CSV fixtures, or create production train meshes. It is a
diagnostic placeholder scene for checking exported train pose hierarchy,
orientation, and wheel offsets.
