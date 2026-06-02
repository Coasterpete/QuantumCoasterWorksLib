# Unity DLL Handoff (Backend Visualizer)

This helper copies only the DLLs needed by the live backend `BackendTrainPipelineGizmoVisualizer` into a Unity project.

## Required DLLs

The live backend visualizer evaluates Quantum backend code inside Unity, so all four of these DLLs must sit together in the Unity plugin folder:

- `Quantum.Math.dll`
- `Quantum.Splines.dll`
- `Quantum.Track.dll`
- `GShark.dll`

`GShark.dll` is the external spline/NURBS dependency used by `Quantum.Splines`. The copy helper includes it automatically: it first looks beside the Release `Quantum.Track` output, then resolves the restored NuGet package path from `Quantum.Splines.deps.json`.

Not copied by default:
- `Quantum.IO.dll` (optional, only when explicitly requested)
- `Quantum.Debug.dll`
- `Quantum.Tests.dll`

## 1) Build Release outputs

From repo root:

```powershell
dotnet build .\Quantum.Track\Quantum.Track.csproj -c Release
```

This builds the live visualizer dependency chain, including `Quantum.Math`, `Quantum.Splines`, `Quantum.Track`, and the Release metadata the copy script uses to locate `GShark.dll`.

If you also want `Quantum.IO.dll` copied:

```powershell
dotnet build .\Quantum.IO\Quantum.IO.csproj -c Release
```

## 2) Copy DLLs to Unity

Script:
- `tools/copy-quantum-unity-dlls.ps1`

Default target (when run from Unity project root):
- `Assets/Plugins/Quantum`

Example (default target):

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\copy-quantum-unity-dlls.ps1
```

Example (custom target):

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\copy-quantum-unity-dlls.ps1 -Target "C:\MyUnityProject\Assets\Plugins\Quantum"
```

Example for the local manual validation project from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\copy-quantum-unity-dlls.ps1 -Target "C:\Dev4\TestingGrounds\Assets\Plugins\Quantum"
```

Example (include `Quantum.IO.dll`):

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\copy-quantum-unity-dlls.ps1 -IncludeQuantumIO
```

## 3) Use with `BackendTrainPipelineGizmoVisualizer`

1. In Unity, confirm `Quantum.Math.dll`, `Quantum.Splines.dll`, `Quantum.Track.dll`, and `GShark.dll` all exist in `Assets/Plugins/Quantum`.
2. Add `BackendTrainPipelineGizmoVisualizer` to a GameObject.
3. Enable Gizmos in Scene view.
4. Adjust `carCount`, `carSpacing`, and `playhead01` to inspect centerline/frame/car placement behavior.

This keeps the Unity side focused on the current backend pipeline visualizer without bringing in extra backend assemblies.

## DebugViewportSnapshotV1 Unity Handoff

`DebugViewportSnapshotV1` is the preferred renderer-neutral artifact for Unity snapshot inspection. The thin Unity runtime path lives in `Assets/Scripts/QuantumVisualizer`, with an optional Editor window under `Assets/Editor/QuantumVisualizer`:

- `DebugViewportSnapshotV1Dtos.cs`
- `DebugViewportSnapshotV1JsonLoader.cs`
- `DebugViewportSnapshotV1GizmoVisualizer.cs`
- `DebugViewportSnapshotV1TransformVisualizer.cs`
- `Assets/Editor/QuantumVisualizer/DebugViewportSnapshotBrowserWindow.cs`
- `Assets/Editor/QuantumVisualizer/DebugViewportSnapshotV1TransformVisualizerEditor.cs`

The loader accepts a Unity `TextAsset`, checks `contract == "quantum.debug_viewport_snapshot"` and `version == 1`, then maps the data into Unity-local DTOs. It does not require `GShark.dll`, `Quantum.Math.dll`, `Quantum.Splines.dll`, `Quantum.Track.dll`, `Quantum.IO.dll`, `Quantum.Debug.dll`, or schema validation inside Unity. The gizmo visualizer draws centerline points, frames, stable-kind lines, and stable-role oriented boxes. It only logs nested `TrainPoseExportV1` presence and car count; it does not render the nested train pose hierarchy.

Coordinate mapping expectations:

- Backend `position.x/y/z` are Quantum track-space coordinates in meters.
- Backend `tangent` is forward, `normal` is up, and `binormal` is right/lateral.
- Backend frames are right-handed: `binormal ~= tangent x normal`.
- Unity uses `Vector3` plus `Quaternion`; perform any handedness or axis remapping at the importer boundary and keep that policy out of `Quantum.*`.
- Box `length` follows tangent, `height` follows normal, and `width` follows binormal.
- Transform visualizer wrappers map local +X to backend tangent/length, local +Y to backend normal/height, and local +Z to backend binormal/width.

Prefab placement expectations for `DebugViewportSnapshotV1TransformVisualizer`:

- Author the prefab pivot at the visual box center.
- Use the Body Prefab slot for `train.body` boxes.
- Use the Banking Profile Prefab slot for `train.body.banking-profile` boxes.
- The prefab is instantiated as the only visual child under each generated box wrapper.
- The prefab child local position, rotation, and scale stay at identity (`0,0,0`, identity rotation, `1,1,1`).
- The generated wrapper owns snapshot pose and dimensions; wrapper local scale is `length,height,width`.
- If the role prefab slot is empty, the visualizer creates a unit `FallbackCube` child at local identity instead.
- The visualizer inspector and Snapshot Browser report Body, Banking Profile, Bogie, and Wheel prefab slots as `Assigned` or `Missing`.

Simple train-body prototype prefab:

1. Create an empty GameObject named `PrototypeTrainBody`.
2. Reset the root transform to local position `0,0,0`, rotation `0,0,0`, and scale `1,1,1`.
3. Add one or more self-authored child cubes centered around the root pivot and roughly inside normalized `-0.5..0.5` local X/Y/Z bounds.
4. Save the root as a prefab.
5. Assign that prefab to Body Prefab for the built-in sample or Banking Profile Prefab for the BankingProfile sample.
6. Rebuild generated boxes and confirm each generated wrapper has one `Prefab` child at local identity.

Generated transform hierarchy:

```text
GeneratedSnapshot
  train.body
  train.body.banking-profile
  train.bogie
  train.wheel
  unknown
```

Suggested imported hierarchy:

```text
QuantumSnapshot_<name>
  Centerline
  Frames
    frame_axis_tangent
    frame_axis_normal
    frame_axis_binormal
  DebugLines
    frame_axis_tangent
    frame_axis_normal
    frame_axis_binormal
    diagnostic_line
  Train
    Bodies
    Bogies
    Wheels
  Metadata
```

Material ownership belongs entirely to Unity. Map the stable vocabulary to Unity-side colors/materials, for example `train.body`, `train.body.banking-profile`, `train.bogie`, `train.wheel`, `frame.axis.tangent`, `frame.axis.normal`, `frame.axis.binormal`, and `diagnostic.line`. Do not add material, shader, prefab, camera, light, render-pipeline, or scene-object fields to the backend snapshot contract.

Placeholder sizing stays literal: snapshot box length follows tangent, height follows normal, and width follows binormal. If a snapshot only has nested `TrainPoseExportV1` and no bogie/wheel boxes, this milestone intentionally does not draw the nested pose; it reports only nested pose presence and car count.

## Snapshot Browser Editor Window

`Window > Quantum > Snapshot Browser` opens a Unity Editor-only control surface for
the snapshot visualizers. It lets a user assign a `DebugViewportSnapshotV1` JSON
`TextAsset`, browse discovered snapshots under `Assets`, parse the selected
snapshot, inspect metadata and role counts, create or update a scene viewer
GameObject named `Quantum Snapshot Viewer`, select that viewer, and call the
transform visualizer `Rebuild` and `Clear` actions. It also reports transform
visualizer prefab slot status and exposes selection actions for
`GeneratedSnapshot`, generated `train.body` wrappers, and generated
`train.body.banking-profile` wrappers. It also has a Generated Artifacts workflow
that copies backend output from `artifacts/debug-viewport` into
`Assets/DebugData` without adding CSV parsing or backend dependencies to Unity.

The created viewer GameObject receives:

- `DebugViewportSnapshotV1GizmoVisualizer`
- `DebugViewportSnapshotV1TransformVisualizer`

The selected snapshot is applied to both components. The window searches Unity
`Assets` for snapshots and groups discovered rows by source:

- Built-in
- BankingProfile
- CSV fixtures
- Other valid snapshots
- Invalid/unknown DebugData JSON warnings

Known generated files include:

- `DebugViewportSnapshotV1.sample.json`
- `DebugViewportSnapshotV1.banking-profile.sample.json`
- `Milestone7.synthetic.straight_line.snapshot.json`
- `Milestone7.synthetic.simple_hill.snapshot.json`
- `Milestone7.synthetic.banked_turn.snapshot.json`
- `Milestone7.synthetic.descending_ascending_curve.snapshot.json`

Each row shows the snapshot name and asset path, with `Load`, `Apply`, and
`Ping` actions. Valid rows also show row-level metadata:

- `metadata.sourceFixtureName`
- `metadata.sampleCount`
- centerline count
- frame count
- box count
- nested train pose present/absent
- nested train pose car count

Known generated files are shown even if their current content is invalid, so
corrupted or stale synced files surface readable contract/version/JSON warnings
instead of quietly disappearing. Unknown invalid JSON is shown when it lives
under `Assets/DebugData`, where generated artifact import problems are most
useful to see.

The browser refreshes its discovered list when Unity project assets change. The
Generated Artifacts panel has a source folder field, defaults to
`artifacts/debug-viewport`, optionally cleans existing JSON/SVG/HTML files under
`Assets/DebugData`, copies current `*.json`, `*.svg`, and `*.html` files, calls
`AssetDatabase.Refresh()`, rescans the snapshot list, and reports copied file
counts. If the selected `TextAsset` content changes while the window is open,
the browser reloads that snapshot and updates the parsed panels.

The parsed panels include:

- basic counts for centerline points, frames, lines, boxes, and nested train pose
- `metadata.units`
- `metadata.sourceFixtureName`
- `metadata.sampleCount`
- role counts for `train.body`, `train.body.banking-profile`, `train.bogie`,
  `train.wheel`, and `unknown`
- prefab status for Body, Banking Profile, Bogie, and Wheel slots

The status panel calls out no snapshot selected, invalid JSON, wrong
contract/version, no generated artifacts under `Assets/DebugData`, no viewer
assigned/found, and Play Mode scene-editing lockout. The DebugData artifact
buttons open `Assets/DebugData/index.html` and `Assets/DebugData/browser.html`
when those generated files are present.

The editor window is a Unity adapter only. It does not add backend dependencies,
does not change the `DebugViewportSnapshotV1` contract, and does not introduce
runtime animation or render-pipeline requirements.

## Unity Debug Artifact Sync

Use the Snapshot Browser Generated Artifacts panel, or
`tools/sync-unity-debug-data.ps1`, to copy generated debug viewport artifacts
into a local Unity project without manual copy commands.

Default source:
- `artifacts/debug-viewport`

Default target Unity project:
- `C:\Dev4\TestingGrounds`

Default target asset folder:
- `C:\Dev4\TestingGrounds\Assets\DebugData`

Examples from the repository root:

```powershell
.\tools\sync-unity-debug-data.ps1
.\tools\sync-unity-debug-data.ps1 -Target "C:\Dev4\TestingGrounds"
.\tools\sync-unity-debug-data.ps1 -Source "artifacts\debug-viewport" -Target "C:\Dev4\TestingGrounds" -Clean
```

If local PowerShell execution policy blocks direct script execution, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\sync-unity-debug-data.ps1 -Target "C:\Dev4\TestingGrounds"
```

The script creates `Assets\DebugData` when needed, copies generated `*.json`,
`*.svg`, and `*.html` files, and overwrites existing files with matching names.
Use `-Clean` to remove existing copied JSON, SVG, and HTML files from
`Assets\DebugData` before copying the current artifacts.

Unity imports copied JSON files under `Assets\DebugData` as `TextAsset`s. After
sync or in-window import, the Snapshot Browser refreshes its discovered list when
Unity reports the asset changes. Built-in, BankingProfile, CSV fixture, and other
valid snapshot JSON files appear in their source groups, DebugData JSON problems
surface as warnings, and the local gallery/browser HTML buttons enable when
`index.html` and `browser.html` are present.

## Manual Built-In Unity Validation

Use `C:\Dev4\TestingGrounds` only as a local manual validation project. Do not commit that project into this repository.

1. Generate current artifacts from the repository root:

```powershell
.\tools\demo-technical-preview-0.1.cmd
```

2. In `TestingGrounds`, copy the snapshot-only runtime scripts into the Unity project's `Assets/Scripts/QuantumVisualizer`: `DebugViewportSnapshotV1Dtos.cs`, `DebugViewportSnapshotV1JsonLoader.cs`, `DebugViewportSnapshotV1GizmoVisualizer.cs`, `DebugViewportSnapshotV1TransformVisualizer.cs`, and `TrainPoseExportV1Dtos.cs`. Copy `Assets/Editor/QuantumVisualizer/DebugViewportSnapshotBrowserWindow.cs` and `Assets/Editor/QuantumVisualizer/DebugViewportSnapshotV1TransformVisualizerEditor.cs` into the Unity project's `Assets/Editor/QuantumVisualizer`. Do not copy `BackendTrainPipelineGizmoVisualizer.cs` unless the backend DLLs from the earlier handoff section are also installed.
3. Import the generated debug viewport artifacts into Unity. In the Snapshot
   Browser, use the Generated Artifacts source folder `artifacts/debug-viewport`
   when the folder is under the Unity project root, or browse to the repository
   artifact folder. Click `Import / Refresh Generated Artifacts`; enable clean
   before import when you want to replace stale local files. The older script
   path is still available:

```powershell
.\tools\sync-unity-debug-data.ps1 -Target "C:\Dev4\TestingGrounds"
```

Unity imports the copied JSON files from `Assets\DebugData` as `TextAsset`s.
The Snapshot Browser discovered list should update after Unity refreshes those
assets, without closing and reopening the window.

4. Open `Window > Quantum > Snapshot Browser`.
5. Confirm `DebugViewportSnapshotV1.sample.json`,
   `DebugViewportSnapshotV1.banking-profile.sample.json`, and synced fixture
   snapshots appear in Built-in, BankingProfile, and CSV fixtures groups. CSV
   rows should show `metadata.sourceFixtureName` values ending in `.csv`, plus
   sample, centerline, frame, box, train pose, and train pose car counts.
6. Click the `Open Gallery index.html` and `Open Browser browser.html` buttons
   when present and confirm the local pages open.
7. Load `DebugViewportSnapshotV1.sample.json` from its row and confirm the stats
   show `centerline points = 9`, `frames = 9`, `lines = 3`, `boxes = 2`,
   `trainPose = present`, and `trainPose car count = 2`. Confirm metadata fields
   are populated and role counts show `train.body = 2`.
8. Click `Create / Update Viewer` and confirm the scene has `Quantum Snapshot
   Viewer` with both `DebugViewportSnapshotV1GizmoVisualizer` and
   `DebugViewportSnapshotV1TransformVisualizer`.
9. Click `Select Viewer` and confirm `Quantum Snapshot Viewer` is selected.
10. Click `Rebuild Generated Boxes` and confirm two wrappers appear under
    `GeneratedSnapshot/train.body`.
    Click `Select Generated Hierarchy`, then `Select Body Instances`, and confirm
    the generated root and two body wrappers are selected as expected.
11. Load `DebugViewportSnapshotV1.banking-profile.sample.json` and confirm the
    stats show `centerline points = 10`, `frames = 10`, `lines = 3`, `boxes = 3`,
    `trainPose = present`, and `trainPose car count = 3`. Confirm role counts show
    `train.body.banking-profile = 3`.
12. Click `Rebuild Generated Boxes` and confirm three wrappers appear under
    `GeneratedSnapshot/train.body.banking-profile`.
    Click `Select Banking Profile Instances` and confirm the three generated
    banking-profile wrappers are selected.
13. Select invalid, empty, or wrong-contract JSON `TextAsset`s through the object
    field and confirm the status panel shows readable warnings.
14. While the browser is open, run a clean import from the Generated Artifacts
    panel and confirm the selected asset reloads or clears without a stale
    selection exception.
15. Click `Clear Generated Boxes` and confirm the `GeneratedSnapshot` child root
    is removed.
16. Verify the visual layers with the component toggles:

- Built-in sample: console summary should report `centerlinePoints=9`, `frames=9`, `lines=3`, `boxes=2`, `trainPose=present`, `trainPoseCars=2`.
- BankingProfile sample: console summary should report `centerlinePoints=10`, `frames=10`, `lines=3`, `boxes=3`, `trainPose=present`, `trainPoseCars=3`.
- CSV fixture snapshots: centerline and frame axes should render, with `boxes=0`, `trainPose=absent`, and `trainPoseCars=0`.

17. Validate prefab placement through the transform visualizer:

- With the `train.body` prefab slot empty, rebuild the built-in sample and confirm two wrappers under `GeneratedSnapshot/train.body`, each with one `FallbackCube` child at local identity.
- Confirm the Prefab Status section reports Body Prefab as `Missing`, then assign a simple self-authored cube-based train-body prefab with center pivot to the Body Prefab slot.
- Rebuild the built-in sample and confirm two `Prefab` children under the same wrappers. The console summary should report `prefabInstances=2` and `fallbackCubes=0`, and Body Prefab should report `Assigned`.
- With the `train.body.banking-profile` prefab slot empty, rebuild the BankingProfile sample and confirm three wrappers under `GeneratedSnapshot/train.body.banking-profile`, each with one `FallbackCube` child at local identity.
- Confirm Banking Profile Prefab reports `Missing`, then assign the same self-authored cube-based train-body prefab to the Banking Profile Prefab slot.
- Rebuild the BankingProfile sample and confirm three `Prefab` children. The console summary should report `prefabInstances=3` and `fallbackCubes=0`, and Banking Profile Prefab should report `Assigned`.
- In both samples, confirm each wrapper local scale is `x = snapshot length`, `y = snapshot height`, `z = snapshot width`, while each prefab or fallback child remains local position `0,0,0`, local rotation identity, and local scale `1,1,1`.

Optional GLB readiness check:

- Import only a self-authored cube-like GLB.
- Do not introduce third-party or production model assets.
- Check only pivot, orientation, and import scale against the same center-pivot/local-identity convention.
- Record whether the imported prefab can stay at root scale `1,1,1`; do not add GLB, material, mesh, or prefab fields to `DebugViewportSnapshotV1`.

No HDRP/URP setup, materials, runtime animation, live backend evaluation, `GShark.dll`, or backend DLL copy is required for this snapshot-only gizmo, transform, and browser-window path. Prefabs are optional Unity-side visuals only.
