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
