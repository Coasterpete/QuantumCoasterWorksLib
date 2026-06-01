# Unity DLL Handoff (Backend Visualizer)

This helper copies only the DLLs needed by `BackendTrainPipelineGizmoVisualizer` into a Unity project.

## Required DLLs

- `Quantum.Math.dll`
- `Quantum.Splines.dll`
- `Quantum.Track.dll`
- `GShark.dll`

Not copied by default:
- `Quantum.IO.dll` (optional, only when explicitly requested)
- `Quantum.Debug.dll`
- `Quantum.Tests.dll`

## 1) Build Release outputs

From repo root:

```powershell
dotnet build .\Quantum.Track\Quantum.Track.csproj -c Release
```

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

Example (include `Quantum.IO.dll`):

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\copy-quantum-unity-dlls.ps1 -IncludeQuantumIO
```

## 3) Use with `BackendTrainPipelineGizmoVisualizer`

1. In Unity, confirm copied DLLs exist in `Assets/Plugins/Quantum`.
2. Add `BackendTrainPipelineGizmoVisualizer` to a GameObject.
3. Enable Gizmos in Scene view.
4. Adjust `carCount`, `carSpacing`, and `playhead01` to inspect centerline/frame/car placement behavior.

This keeps the Unity side focused on the current backend pipeline visualizer without bringing in extra backend assemblies.

## DebugViewportSnapshotV1 Unity Handoff

`DebugViewportSnapshotV1` is the preferred renderer-neutral artifact for the next Unity import step. A Unity importer should load JSON from a file or `TextAsset`, check `contract == "quantum.debug_viewport_snapshot"` and `version == 1`, then map the data into transient Unity-side objects. The importer should not require `Quantum.Track`, `Quantum.Debug`, or other backend assemblies at runtime unless it is explicitly a live backend debug visualizer. A file-based snapshot importer can use Unity-local DTOs or generated schema-bound DTOs that mirror the JSON contract.

Coordinate mapping expectations:

- Backend `position.x/y/z` are Quantum track-space coordinates in meters.
- Backend `tangent` is forward, `normal` is up, and `binormal` is right/lateral.
- Backend frames are right-handed: `binormal ~= tangent x normal`.
- Unity uses `Vector3` plus `Quaternion`; perform any handedness or axis remapping at the importer boundary and keep that policy out of `Quantum.*`.
- Box `length` follows tangent, `height` follows normal, and `width` follows binormal.

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

Placeholder sizing should stay literal: use snapshot box sizes in meters and build simple cubes or gizmos around each box frame. If a snapshot only has nested `TrainPoseExportV1` and no bogie/wheel boxes, Unity may draw bogie and wheel markers from the nested pose, but marker dimensions remain Unity adapter policy.

The next Unity step should be a thin `DebugViewportSnapshotV1` importer: parse JSON, validate identity/version, optionally validate against the schema during editor import, create transient debug objects, and leave all camera, light, material, prefab, play-mode animation, and cleanup behavior in Unity code.
