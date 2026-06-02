# Unity Visualizer Inventory

Last updated: 2026-06-01

Scope audited:
- Current repo: `Assets/Scripts/QuantumVisualizer`
- Legacy external Unity workspace, if present: `Assets/Scripts/QuantumVisualizer`

Status labels used:
- `Current`: aligned with current train-on-centerline milestone.
- `Legacy`: older/superseded implementation or duplicate.
- `Experimental`: useful probe tool, but not part of current core path.

## Current Wireframe Backend Viewer Features

Reference implementation: `Assets/Scripts/QuantumVisualizer/BackendTrainPipelineGizmoVisualizer.cs`

- Rails (`drawRails`)
- Cross ties (`drawCrossTies`)
- Banking ribbon (`drawBankingRibbon`)
- Heartline (`drawHeartline`)
- Train hierarchy debug markers (`drawBogieMarkers`, `drawWheelMarkers`, `drawArticulationCenterPoints`, `drawCouplerConnections`)
- Playback/HUD (`playhead01`, `autoPlay`, `playbackSpeed`, `loopPlayback`, `drawDebugHud`)

## Visualizer Scripts

| Script | Backend System Visualized | Status | Required DLLs | Overlap with `BackendTrainPipelineGizmoVisualizer` | Recommended |
|---|---|---|---|---|---|
| `Assets/Scripts/QuantumVisualizer/BackendTrainPipelineGizmoVisualizer.cs` | Live backend wireframe viewer on deterministic `TrackDocument` -> `TrackEvaluator` -> `TrainCarTransformProvider`; renders rails, cross ties, banking ribbon, heartline, distance-based train placement, train hierarchy debug markers, and playback/HUD diagnostics. | Current | `UnityEngine.CoreModule.dll`; `Quantum.Math.dll`; `Quantum.Splines.dll`; `Quantum.Track.dll`; `UnityEditor.dll` (editor-only code path). | Baseline script (self). | Keep |
| `Assets/Scripts/QuantumVisualizer/DebugViewportSnapshotV1GizmoVisualizer.cs` | File-based Scene-view gizmo visualizer for `quantum.debug_viewport_snapshot` JSON; renders centerline polyline, frame axes, stable-kind debug lines, stable-role oriented boxes, and logs nested train-pose presence/car count only. | Current | `UnityEngine.CoreModule.dll` (plus local support scripts in same folder). | Complements the live backend viewer: consumes renderer-neutral artifacts instead of evaluating backend code in Unity. | Keep |
| `Assets/Scripts/QuantumVisualizer/DebugViewportSnapshotV1TransformVisualizer.cs` | File-based transform hierarchy visualizer for `quantum.debug_viewport_snapshot` boxes; creates generated role groups and wrapper GameObjects whose local +X/+Y/+Z axes map to backend tangent/normal/binormal, with optional Unity prefabs or fallback cubes as local-identity children. | Current | `UnityEngine.CoreModule.dll` (plus local support scripts in same folder). | Complements the gizmo visualizer by creating inspectable scene objects for snapshot boxes; does not evaluate backend code or own backend dimensions beyond wrapper scale. | Keep |
| `Assets/Scripts/QuantumVisualizer/TrainPoseGizmoVisualizer.cs` | Replay visualizer for exported `quantum.train_pose` JSON (`TrainPoseExportV1Dto`), including body/bogie/wheel frames and sampled centerline channels. | Current | `UnityEngine.CoreModule.dll` (plus local support scripts in same folder). | Partial overlap: same train/body/bogie/wheel debug surfaces, but input is exported JSON instead of live backend evaluation. | Keep |
| Legacy external `Assets/Scripts/QuantumVisualizer/TrainPoseGizmoVisualizer.cs` | Older replay visualizer for `TrainPoseExportV1` JSON, includes legacy gizmo layers plus primitive car/bogie/wheel overlay. | Legacy | `UnityEngine.CoreModule.dll` (plus local DTO/loader scripts in same folder). | High overlap: draws many of the same train debug primitives as pipeline visualizer, but from file-based replay. | Deprecate (after confirming no active scene dependency) |
| Legacy external `Assets/Scripts/QuantumVisualizer/BackendDebugTrackGizmo.cs` | Simple sampled track from local control points (`LineCurve` segments), optional force overlay from longitudinal-force preview JSON, sample frame axes. | Experimental | `UnityEngine.CoreModule.dll`; `Quantum.Math.dll`; `Quantum.Splines.dll`. | Partial overlap: centerline and frame debugging overlap; force-overlay coloring is unique. | Merge (retain useful force-overlay behavior, then retire duplicate track drawing path) |
| Legacy external `Assets/Scripts/QuantumVisualizer/BackendLineCurveGizmo.cs` | Minimal `LineCurve` sampling sanity check between two endpoints. | Legacy | `UnityEngine.CoreModule.dll`; `Quantum.Math.dll`; `Quantum.Splines.dll`. | Partial overlap: narrow subset of centerline sampling only. | Deprecate |
| Legacy external `Assets/Scripts/QuantumVisualizer/LongitudinalForcePreviewGizmo.cs` | Graph preview of longitudinal force samples (`kind = longitudinal-force-preview`) with optional multi-profile legend and section-t coloring. | Experimental | `UnityEngine.CoreModule.dll`; `UnityEditor.dll` (default-asset loading and legend labels in editor). | No direct overlap: plots longitudinal force graph, not train placement along track. | Keep |
| Legacy external `Assets/Scripts/QuantumVisualizer/LongitudinalSpeedPreviewGizmo.cs` | Graph preview of longitudinal speed samples (`kind = longitudinal-speed-preview`) with optional target-G curve. | Experimental | `UnityEngine.CoreModule.dll`; `UnityEditor.dll` (default-asset loading and legend labels in editor). | No direct overlap: plots speed/G curves, not train transform pipeline. | Keep |

## Non-Visualizer Support Scripts In These Folders

These are not gizmo visualizers themselves, but are dependencies for visualizer workflows:

- `Assets/Scripts/QuantumVisualizer/TrainPoseExportV1Dtos.cs`
- `Assets/Scripts/QuantumVisualizer/TrainPoseJsonLoader.cs`
- `Assets/Scripts/QuantumVisualizer/DebugViewportSnapshotV1Dtos.cs`
- `Assets/Scripts/QuantumVisualizer/DebugViewportSnapshotV1JsonLoader.cs`
- `Assets/Scripts/QuantumVisualizer/DebugViewportSnapshotV1TransformVisualizer.md`
- Legacy external `Assets/Scripts/QuantumVisualizer/TrainPoseExportV1Dto.cs`
- Legacy external `Assets/Scripts/QuantumVisualizer/TrainPoseExportV1Loader.cs`

## Consolidation Notes

- There are two `TrainPoseGizmoVisualizer` implementations across two projects. The one under `QuantumCoasterWorks` is the better candidate for the active path; the `QuantumCoasterWorksUnity` copy is a legacy duplicate.
- `BackendTrainPipelineGizmoVisualizer` should remain the reference visualizer for the current milestone (stable centerline/frame/distance-based car placement plus rails/cross ties/banking ribbon/heartline/train hierarchy markers/playback HUD).
