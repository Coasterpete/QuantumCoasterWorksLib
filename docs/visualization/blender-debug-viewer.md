# Blender Debug Snapshot Viewer

The Blender debug snapshot viewer is an optional visualization adapter for `DebugViewportSnapshotV1` JSON. It is meant for screenshots, quick render checks, and public-facing debug visuals. It is not the final Quantum editor, renderer, frontend, or backend architecture direction.

The C# backend stays engine-agnostic. Blender-specific code lives under `tools/blender/` and consumes generated JSON from `artifacts/debug-viewport/`.

## Generate Snapshot Artifacts

From the repository root, run the technical preview demo:

```powershell
.\tools\demo-technical-preview-0.1.cmd
```

This creates or refreshes ignored local artifacts such as:

```text
artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json
artifacts/debug-viewport/Milestone7.synthetic.straight_line.snapshot.json
artifacts/debug-viewport/Milestone7.synthetic.simple_hill.snapshot.json
artifacts/debug-viewport/Milestone7.synthetic.banked_turn.snapshot.json
artifacts/debug-viewport/Milestone7.synthetic.descending_ascending_curve.snapshot.json
```

Generated `.json`, `.svg`, `.html`, `.blend`, and render image files are local debug output by default and should not be committed unless there is a clear release reason.

## Run From Blender Command Line

With Blender on your `PATH`, run:

```powershell
blender --python tools/blender/import_debug_viewport_snapshot_v1.py -- artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json
```

The script prints a concise import summary in Blender's console, including the loaded JSON path and counts for centerline points, frame tick objects, debug line objects, boxes, camera, and light.

## Run From Blender Script Runner

1. Open Blender.
2. Switch to the `Scripting` workspace.
3. Open `tools/blender/import_debug_viewport_snapshot_v1.py`.
4. Press `Run Script`.
5. If no `SNAPSHOT_PATH` is set in the script, Blender opens a JSON file picker.
6. Load `artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json`.

For repeated manual runs, you may set the script's top-level `SNAPSHOT_PATH` value before pressing `Run Script`:

```python
SNAPSHOT_PATH = r"C:\Dev2\QuantumCoasterWorksLib\artifacts\debug-viewport\DebugViewportSnapshotV1.sample.json"
```

## Expected Blender Objects

The importer creates one marked collection named `Quantum DebugViewportSnapshotV1`. Re-running the import clears that generated collection before rebuilding it.

Inside the collection, you should see:

- `centerline`: a renderable curve through `centerlinePoints`.
- `frames`: tangent, normal, and binormal tick curves sampled from `frames`.
- `debug_lines`: renderer-neutral `lines` from the snapshot, grouped by line kind when present.
- `boxes`: placeholder cube boxes for snapshot `boxes`, oriented by frame tangent, binormal, and normal.
- `scene`: `Quantum.debug_camera` and `Quantum.debug_key_light`.

Quantum track space is Y-up. The Blender adapter maps positions and frame axes into Blender Z-up space as:

```text
Quantum (x, y-up, z-lateral) -> Blender (x, y-lateral, z-up)
```

## Screenshots And Renders

After import, use Blender's normal manual capture tools:

- For a quick viewport image, frame the `Quantum DebugViewportSnapshotV1` collection and use Blender's viewport screenshot or viewport render tools.
- For a camera render, select or use `Quantum.debug_camera`, then render with Blender's normal render command.
- Save screenshots or renders outside the repo, or under ignored local artifact folders, unless there is an explicit reason to commit them.

This viewer is intentionally simple: it uses curves, placeholder boxes, basic materials, a camera, and a light. It does not smooth SVG previews, change centerline interpolation, or introduce any Blender dependency into `Quantum.*` backend projects.
