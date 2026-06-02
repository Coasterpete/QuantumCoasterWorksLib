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
powershell -ExecutionPolicy Bypass -File .\tools\copy-quantum-unity-dlls.ps1 -Target "C:\Dev4\TestingGrounds1\Assets\Plugins\Quantum"
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

`DebugViewportSnapshotV1` is the preferred renderer-neutral artifact for Unity snapshot inspection. The thin Unity path lives entirely in `Assets/Scripts/QuantumVisualizer`:

- `DebugViewportSnapshotV1Dtos.cs`
- `DebugViewportSnapshotV1JsonLoader.cs`
- `DebugViewportSnapshotV1GizmoVisualizer.cs`
- `DebugViewportSnapshotV1TransformVisualizer.cs`

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
- The prefab is instantiated as the only visual child under each generated box wrapper.
- The prefab child local position, rotation, and scale stay at identity (`0,0,0`, identity rotation, `1,1,1`).
- The generated wrapper owns snapshot pose and dimensions; wrapper local scale is `length,height,width`.
- If the role prefab slot is empty, the visualizer creates a unit `FallbackCube` child at local identity instead.

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

## Manual Built-In Unity Validation

Use `C:\Dev4\TestingGrounds1` only as a local manual validation project. Do not commit that project into this repository.

1. Generate current artifacts from the repository root:

```powershell
.\tools\demo-technical-preview-0.1.cmd
```

2. In `TestingGrounds1`, copy the snapshot-only scripts into the Unity project's `Assets/Scripts/QuantumVisualizer`: `DebugViewportSnapshotV1Dtos.cs`, `DebugViewportSnapshotV1JsonLoader.cs`, `DebugViewportSnapshotV1GizmoVisualizer.cs`, `DebugViewportSnapshotV1TransformVisualizer.cs`, and `TrainPoseExportV1Dtos.cs`. Do not copy `BackendTrainPipelineGizmoVisualizer.cs` unless the backend DLLs from the earlier handoff section are also installed.
3. Copy the generated JSON snapshots you want to inspect into a Unity `Assets` folder so Unity imports them as `TextAsset`s. Typical sources:

```text
C:\Dev4\QuantumCoasterWorksLib\artifacts\debug-viewport\DebugViewportSnapshotV1.sample.json
C:\Dev4\QuantumCoasterWorksLib\artifacts\debug-viewport\DebugViewportSnapshotV1.banking-profile.sample.json
C:\Dev4\QuantumCoasterWorksLib\artifacts\debug-viewport\Milestone7.synthetic.straight_line.snapshot.json
C:\Dev4\QuantumCoasterWorksLib\artifacts\debug-viewport\Milestone7.synthetic.simple_hill.snapshot.json
C:\Dev4\QuantumCoasterWorksLib\artifacts\debug-viewport\Milestone7.synthetic.banked_turn.snapshot.json
C:\Dev4\QuantumCoasterWorksLib\artifacts\debug-viewport\Milestone7.synthetic.descending_ascending_curve.snapshot.json
```

4. Create an empty GameObject, add `DebugViewportSnapshotV1GizmoVisualizer` and `DebugViewportSnapshotV1TransformVisualizer`, assign the same snapshot `TextAsset` to both components, and enable Gizmos in Scene view.
5. Verify the visual layers with the component toggles:

- Built-in sample: console summary should report `centerlinePoints=9`, `frames=9`, `lines=3`, `boxes=2`, `trainPose=present`, `trainPoseCars=2`.
- BankingProfile sample: console summary should report `centerlinePoints=10`, `frames=10`, `lines=3`, `boxes=3`, `trainPose=present`, `trainPoseCars=3`.
- CSV fixture snapshots: centerline and frame axes should render, with `boxes=0`, `trainPose=absent`, and `trainPoseCars=0`.

6. Validate prefab placement through the transform visualizer:

- With the `train.body` prefab slot empty, rebuild the built-in sample and confirm two wrappers under `GeneratedSnapshot/train.body`, each with one `FallbackCube` child at local identity.
- Assign a simple self-authored cube prefab with center pivot to `train.body`, rebuild the built-in sample, and confirm two `Prefab` children under the same wrappers. The console summary should report `prefabInstances=2` and `fallbackCubes=0`.
- With the `train.body.banking-profile` prefab slot empty, rebuild the BankingProfile sample and confirm three wrappers under `GeneratedSnapshot/train.body.banking-profile`, each with one `FallbackCube` child at local identity.
- Assign the same self-authored cube prefab to `train.body.banking-profile`, rebuild the BankingProfile sample, and confirm three `Prefab` children. The console summary should report `prefabInstances=3` and `fallbackCubes=0`.
- In both samples, confirm each wrapper local scale is `x = snapshot length`, `y = snapshot height`, `z = snapshot width`, while each prefab or fallback child remains local position `0,0,0`, local rotation identity, and local scale `1,1,1`.

Optional GLB readiness check:

- Import only a self-authored cube-like GLB.
- Do not introduce third-party or production model assets.
- Check only pivot, orientation, and import scale against the same center-pivot/local-identity convention.
- Record whether the imported prefab can stay at root scale `1,1,1`; do not add GLB, material, mesh, or prefab fields to `DebugViewportSnapshotV1`.

No HDRP/URP setup, materials, editor windows, runtime animation, live backend evaluation, `GShark.dll`, or backend DLL copy is required for this snapshot-only gizmo and transform path. Prefabs are optional Unity-side visuals only.
