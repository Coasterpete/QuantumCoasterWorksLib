# Milestone 156 Avalonia Editor Vertical Slice

## Purpose

Milestone 156 turns the preserved Avalonia scaffold into a visibly useful technical editor while keeping every coaster-domain calculation in the existing backend. The application is an integrated vertical slice, not a production-complete coaster editor or final renderer choice.

The editor owns desktop concerns:

- application layout, menus, toolbar, panels, and status;
- open/save dialogs and active-document lifecycle;
- outliner, selection, inspector, commands, and undo/redo history;
- viewport projection, drawing, pan/zoom, hit testing, and presentation colors.

The backend continues to own:

- Track Layout Package V2 DTOs, JSON, validation, and mapping;
- authoring definitions and document compilation;
- spline-backed centerline evaluation and measured station distance;
- transported track frames, banking profiles, curvature/smoothness, and continuity diagnostics.

No Avalonia reference was added to a backend project.

## Running the editor

From the repository root:

```text
dotnet run --project Quantum.Editor.Avalonia/Quantum.Editor.Avalonia.csproj
```

The application starts with an unsaved, self-authored showcase layout containing seven straight, curvature-transition, and constant-curvature sections. It also includes an interpolated banking profile and heartline metadata. This makes the first window useful without requiring a fixture or external asset.

## Workspace layout

- **File menu and toolbar:** New, Open, Save, Save As, Undo, Redo, fit, and transported-frame visibility.
- **Outliner:** active layout, ordered sections, spatial control points when present, banking keys, and heartline summary.
- **Viewport:** sampled centerline colored by section, transported normal/binormal axes, selected section highlighting, and station-sample selection.
- **Inspector:** editable track metadata, section parameters, banking keys, heartline offsets, and spatial control points; sampled frames are read-only diagnostic selections.
- **Diagnostics:** compilation size, sample count, maximum curvature, banking range, and transported-frame continuity issues.
- **Status bar:** current operation, selection, and saved/modified state.

## File and document behavior

Open and Save use the existing `quantum.track_layout_package` contract version 2. JSON and `.qcwtrack`/`.qcwtrack.json` filenames are accepted by the picker. Files are written as indented UTF-8 without a byte-order mark.

Every opened or edited package is validated through `TrackLayoutPackageV2Mapper` and compiled through `TrackAuthoringDocumentBuilder`. Invalid edits are rejected before the active package or compiled runtime is replaced. New documents and successful inspector edits are dirty; successful saves clear the dirty flag. New/Open warn before replacing a dirty active document.

## Editing and undo/redo

Inspector edits operate on a cloned package snapshot. A successful edit recompiles the complete backend authoring snapshot and records the before/after V2 JSON in the undo stack. Undo and redo therefore restore coherent package, document, runtime, banking, outliner, viewport, and diagnostics state together.

Supported inspector edits include:

- source name, layout ID, and heartline normal/lateral offsets;
- section ID, length, roll, signed radius, and transition endpoint curvatures;
- banking-key station, roll, and interpolation vocabulary;
- spatial control-point coordinates.

Changing a section length adjusts the terminal banking-key distance by the same delta. The backend validator still rejects invalid ordering, domains, radii, spatial start contracts, measured-length mismatches, or unsupported vocabulary.

## Viewport interaction

- Mouse wheel: zoom around the cursor.
- Middle- or right-button drag: pan.
- Left click near the centerline: select the nearest sampled transported frame.
- **Fit** or `F`: fit the complete sampled layout.
- Projection selector: isometric, top X/Z, or side X/Y.
- **Frames**: toggle transported normal/binormal axes.

Viewport station selection identifies the owning section and synchronizes the outliner highlight. Outliner selection highlights the same section in the viewport and switches the inspector to the selected editor object.

## Keyboard shortcuts

| Shortcut | Command |
|---|---|
| `Ctrl+N` | New track document |
| `Ctrl+O` | Open Track Layout Package V2 |
| `Ctrl+S` | Save |
| `Ctrl+Shift+S` | Save As |
| `Ctrl+Z` | Undo |
| `Ctrl+Y` | Redo |
| `F` | Fit track in viewport |

## Internal architecture

`EditorWorkspace` is the editor-independent coordinator. It composes the preserved services and projects the active `TrackEditorDocument` into outliner nodes and `TrackViewportSnapshot` samples. The Avalonia window consumes those projections and forwards user intent back through editor commands and package edit operations.

`TrackEditorDocument` retains the existing public track-document wrapper API and adds an optional V2 package, compiled authoring snapshot, path, change notification, and package snapshot replacement. Legacy callers can still construct it from a `TrackDocument` and display name.

`TrackSamplingService` consumes `TrackAuthoringCompilation.Runtime`, `BankingProfileSampler`, `TrackFrameSmoothnessDiagnostics`, and `TrackFrameContinuityDiagnostics`. The custom viewport only projects and draws the resulting backend vectors; it does not evaluate curves or construct transported frames.

Editor-independent behavior is covered in `Quantum.Tests/Editor`, including file round trips, compilation, visible sampling state, commands, document activation, selection notifications, invalid-edit rollback, dirty state, and undo/redo.

## Current limitations

- One active document is presented at a time; the service can retain multiple open documents, but tabs and close-document UI are not implemented.
- The viewport is a 2D technical projection, not a GPU 3D/PBR renderer, ride-through view, or mesh editor.
- Section insertion, deletion, and drag reordering are not part of this slice.
- Spatial control-point edits must continue to satisfy existing backend start-frame and measured-length contracts; the editor does not auto-fit declared length.
- Save writes V2 authored-layout state only. Selection, viewport camera, and undo history are intentionally not persisted in the backend contract.
- OS/window-manager close does not currently show the dirty-document confirmation used by New and Open.
