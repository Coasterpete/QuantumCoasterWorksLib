# Blender Visualization Index

Last updated: 2026-06-03

This is the on-ramp for Quantum's optional Blender diagnostics. The Blender
tools consume renderer-neutral JSON artifacts and stay outside the backend
contracts; they do not add Blender dependencies to `Quantum.*` projects.

## Workflow Map

Use these pages for the detailed workflow notes:

- Snapshot import: [blender-debug-viewer.md](blender-debug-viewer.md)
- Train pose import: [blender-train-pose-viewer.md](blender-train-pose-viewer.md)
- Combined debug scene import, validation, and render smoke:
  [blender-debug-scene-viewer.md](blender-debug-scene-viewer.md)
- Blender boundary and handoff rules: [blender-handoff.md](../blender-handoff.md)
- Neutral mesh export investigation:
  [neutral-mesh-export-investigation.md](neutral-mesh-export-investigation.md)
- Current tooling and future candidates:
  [blender-visualization-inventory.md](../blender-visualization-inventory.md)

## Generate Artifacts

From the repository root, generate the current snapshot and train pose JSON:

```powershell
dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1 artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json
dotnet run --project Quantum.Debug -- train-pose-export-v1 artifacts/train-pose/TrainPoseExportV1.sample.json
```

## Smoke Script

Use the wrapper for the common Blender visualization smoke path:

```powershell
.\tools\smoke-blender-visualization.ps1
```

The script refreshes the `DebugViewportSnapshotV1` and `TrainPoseExportV1`
sample JSON files under ignored `artifacts/`, validates the snapshot, and then
runs the snapshot import, train pose import, combined debug scene import, and
temporary PNG render smoke when `blender` is available on `PATH`. If Blender is
not available, the Blender-specific checks are skipped with a clear message.

For the broader technical preview artifact refresh:

```powershell
.\tools\demo-technical-preview-0.1.cmd
```

## Validate Before Import

Validate a debug viewport snapshot before handing it to Blender:

```powershell
dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-validate artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json
```

The Blender train pose and combined scene importers also verify the existing
JSON `contract` and `version` identities before importing. When a snapshot and
train pose are imported together, `import_debug_scene.py` prints a Blender-side
train-on-track validation summary with bounds and nearest-centerline distance
statistics.

## Common Import Commands

Import a `DebugViewportSnapshotV1` snapshot:

```powershell
blender --python tools/blender/import_debug_viewport_snapshot_v1.py -- artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json
```

Import a `TrainPoseExportV1` train pose:

```powershell
blender --python tools/blender/import_train_pose_export_v1.py -- --pose artifacts/train-pose/TrainPoseExportV1.sample.json
```

Import both artifacts into one combined debug scene:

```powershell
blender --python tools/blender/import_debug_scene.py -- --snapshot artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json --pose artifacts/train-pose/TrainPoseExportV1.sample.json
```

The combined importer also supports snapshot-only and train-only modes:

```powershell
blender --python tools/blender/import_debug_scene.py -- --snapshot artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json
blender --python tools/blender/import_debug_scene.py -- --pose artifacts/train-pose/TrainPoseExportV1.sample.json
```

## Render Smoke

Use a temporary PNG path for repeatable Blender render smoke checks:

```powershell
$render = Join-Path $env:TEMP "quantum-debug-scene-smoke.png"
blender --background --factory-startup --python tools/blender/import_debug_scene.py -- --snapshot artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json --pose artifacts/train-pose/TrainPoseExportV1.sample.json --render-output $render --resolution-width 1280 --resolution-height 720 --camera-mode diagnostic
```

`--render-output` writes a PNG after import. If width and height are omitted,
the importer uses its documented default render resolution.

## What Not To Commit

Keep generated Blender and render outputs local by default:

- Do not commit generated `.blend`, `.blend1`, PNG render, screenshot, or
  viewport capture files unless a release milestone explicitly asks for them.
- Do not commit generated JSON, SVG, HTML, or render smoke artifacts from
  `artifacts/`; that folder is ignored for local debug output.
- Do not commit Blender `__pycache__` or `.pyc` files.
- Do not add third-party art, production train meshes, converted GLTF/GLB, or
  other imported assets without explicit permission and a documented release
  reason.

