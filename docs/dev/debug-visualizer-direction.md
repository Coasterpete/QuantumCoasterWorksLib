# Milestone 31 - Debug Visualizer Direction Spike

Date: 2026-05-29

Scope: direction report only. No editor, renderer, frontend scaffold, or backend rewrite is part of this milestone.

## Recommendation

The best first visualizer path is an artifact-first browser/static HTML/SVG viewer that consumes the existing renderer-neutral `DebugViewportSnapshotV1` JSON and generated SVG previews.

Keep the current Unity visualizer as an optional richer debug adapter for live/prototype inspection, but do not make Unity the first required visual shell and do not let Unity concepts define backend contracts.

Defer Avalonia, WPF/WinForms, MonoGame, OpenTK, and Silk.NET until Quantum has a clearer interactive authoring workflow. Avalonia plus a technical viewport is still a credible future editor direction, and Silk.NET/OpenTK may still be evaluated later for a standalone viewport. They are too much infrastructure for the current need: quickly inspecting centerline, frame, train, bogie, wheel, and artifact correctness.

## Why This Fits Quantum Right Now

Quantum is backend-first and already has the important visual boundary in place:

- `Quantum.Debug` can generate `DebugViewportSnapshotV1` JSON.
- `Quantum.Debug` can validate snapshot JSON.
- `Quantum.Debug` can generate multi-panel SVG previews from snapshot JSON.
- The demo script already creates a local static gallery under `artifacts/debug-viewport/`.
- `Quantum.IO` owns versioned JSON contracts and validation.
- The backend dependency tests protect `Quantum.*` projects from frontend and renderer dependencies.
- Unity scripts under `Assets/Scripts/QuantumVisualizer` already prove that richer external adapters can consume backend data without owning backend architecture.

The artifact-first browser/SVG path is the smallest useful visual loop because it uses outputs Quantum already produces. It requires no game engine install, no custom rendering loop, no desktop UI shell, no GPU abstraction choice, and no new backend reference.

It also fits the current milestone priorities:

1. Stable centerline evaluation.
2. Stable orientation frames.
3. Distance-based train car placement.
4. Thin debug visualization adapters.
5. Tests and fixtures that protect frame, distance, and train placement behavior.

## Direction Comparison

| Option | Fit now | Why |
| --- | --- | --- |
| Browser/static HTML/SVG artifact viewer | Best first path | Smallest loop, uses existing JSON/SVG artifacts, easy to diff, easy to open locally, no backend dependency risk. |
| Unity debug visualizer | Keep optional | Already useful for live gizmos and playback, but it requires Unity and should remain a prototype/debug adapter rather than the required visual shell. |
| Avalonia desktop shell | Future editor candidate | Good long-term C# desktop shell, but scaffolding it now would pull attention into editor UI before workflows are ready. |
| WPF/WinForms desktop shell | Not first | Windows-only and less aligned with the current cross-platform/editor-shell direction than Avalonia. Useful only for a very temporary local tool. |
| MonoGame | Not first | Good for game-style rendering, but Quantum needs debug inspection and future editor workflows more than a game loop. |
| OpenTK | Later viewport candidate | Viable technical viewport layer, but it creates renderer/input/windowing work before the backend needs it. |
| Silk.NET | Later viewport candidate | Still a strong future technical viewport candidate, but too low-level for the first visual feedback loop. |

## What The First Visualizer Should Display

The first visual shell should display generated backend artifacts, not own live coaster state. It should make correctness problems visible fast:

- Centerline samples as raw points and polylines.
- Elevation/profile view so hills and drops are visible even when top-down shape is simple.
- Frame axes for tangent, normal, and binormal at sampled station distances.
- Frame continuity diagnostics and obvious flips/twists when available.
- Placeholder train body boxes.
- Bogie and wheel boxes or markers.
- Debug line primitives from `DebugViewportSnapshotV1`.
- Snapshot metadata: contract, version, units, sample count, fixture/source name, box count, line count, train-pose presence.
- Links to the source JSON, generated SVG, validation command, and related preview index.

The first shell can start as static output:

- `snapshot-preview-index.md` for a textual artifact index.
- `index.html` for a local visual gallery.
- SVG panels generated from snapshot JSON.

A tiny next step can add local browser conveniences such as layer toggles, per-snapshot selection, and simple pan/zoom, but those should remain viewer behavior over exported data.

## What It Should Not Become Yet

The first visualizer should not become:

- A full coaster editor.
- A final frontend decision.
- A production renderer.
- A game engine replacement.
- A high-fidelity ride-through renderer.
- A Unity scene/prefab/material workflow that backend code depends on.
- An Avalonia application scaffold before editor workflows are defined.
- A custom OpenGL/Vulkan/DirectX renderer.
- An authoring model for track sections, handles, constraints, or undo/redo.
- A source of truth for centerline, frame, train, bogie, wheel, or force semantics.

It should answer one narrow question: "Does the backend output look correct enough to trust the current centerline/frame/train-placement slice?"

## Backend Data And Artifact Consumption

The first viewer should consume backend outputs through versioned artifacts:

1. Generate snapshot JSON with `Quantum.Debug`.
2. Validate snapshot JSON before display.
3. Render SVG/HTML from the JSON, or have a browser viewer parse the same JSON directly.
4. Treat `DebugViewportSnapshotV1` as the primary debug viewport contract.
5. Treat nested `TrainPoseExportV1` as the detailed train body/bogie/wheel transform hierarchy when present.
6. Keep renderer colors, camera defaults, layer visibility, pan/zoom, and coordinate conversion in the viewer layer.

Recommended data flow:

```text
TrackDocument
  -> TrackEvaluator
  -> TrackFrame / train placement
  -> TrainPoseExportV1 and DebugViewportSnapshotV1
  -> validation
  -> SVG / static HTML / optional thin adapters
```

The viewer should not call directly into mutable backend models as its primary path. File/artifact consumption keeps the contract testable, replayable, diffable, and independent of any UI host.

## Coupling Risks

The main risk is letting the first convenient viewer become the architecture.

Specific coupling risks:

- Unity objects, scene lifecycle, prefabs, materials, gizmos, or coordinate policies leaking into `Quantum.*` projects.
- Browser viewer layer names, colors, or SVG layout becoming backend DTO fields.
- Future Avalonia/editor state being mixed into `Quantum.Track` or `Quantum.IO`.
- Renderer camera/input assumptions being stored in backend snapshots instead of viewer preferences.
- Direct references from backend projects to Unity, Avalonia, WPF, MonoGame, OpenTK, Silk.NET, or browser-specific libraries.
- Copy-pasted DTOs drifting from `Quantum.IO` versioned contracts.
- Treating preview smoothing or SVG presentation paths as authoritative geometry.

Mitigations:

- Keep `DebugViewportSnapshotV1` and `TrainPoseExportV1` as renderer-neutral contracts.
- Keep generated artifacts under ignored `artifacts/` by default.
- Keep frontend adapters outside backend projects.
- Validate contract and version before drawing.
- Add viewer features only when they reveal backend correctness issues.
- Keep dependency-boundary tests green.

## Near-Term Tiny Visual Shell Plan

1. Preserve the existing artifact workflow.
   - Keep `dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1`.
   - Keep `debug-viewport-snapshot-v1-validate`.
   - Keep `debug-viewport-snapshot-v1-svg`.
   - Keep `tools/demo-technical-preview-0.1.*` producing `artifacts/debug-viewport/index.html`.

2. Improve the static artifact gallery only where it shortens inspection.
   - Show source JSON and SVG links clearly.
   - Show contract/version/sample counts.
   - Keep the gallery local and generated.
   - Avoid committing generated artifacts unless release work explicitly needs them.

3. Add a tiny browser JSON viewer only if static SVG panels become limiting.
   - Load a local `DebugViewportSnapshotV1` file.
   - Draw centerline, frames, lines, boxes, bogies, and wheels in a simple canvas or SVG layer.
   - Add layer toggles for centerline, frames, train boxes, bogies, wheels, and diagnostics.
   - Keep it static-file friendly with no build system at first.

4. Keep Unity as the optional richer debug path.
   - Use Unity when live playback, Scene view gizmos, or quick 3D inspection are valuable.
   - Keep Unity-specific scripts in `Assets/` or a future adapter project.
   - Do not require Unity for backend test or artifact workflows.

5. Revisit Avalonia plus Silk.NET/OpenTK after the backend has a minimal authoring workflow.
   - Define what an editor needs to edit before building the editor shell.
   - Keep Avalonia responsible for UI/workbench concerns.
   - Keep the technical viewport behind an adapter.

## Decision

For Milestone 31, Quantum should choose the browser/static HTML/SVG artifact viewer as the first visual shell direction.

That choice is intentionally modest. It uses the current engine-agnostic debug contracts, helps inspect the exact backend behavior under active development, and leaves the future editor and renderer decisions open.
