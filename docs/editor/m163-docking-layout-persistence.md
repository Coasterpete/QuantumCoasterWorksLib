# Milestone 163 Docking Layout Persistence

M163 persists the M162 Dock.Avalonia Track layout between editor sessions. The
feature remains entirely in `Quantum.Editor.Avalonia`; it does not add docking
or persistence contracts to any Quantum backend, workspace, authoring, or
document model.

## Saved frontend state

`DockLayoutPersistenceService` serializes the Dock.Avalonia root graph through
the matching `Dock.Serializer.Newtonsoft` package. The saved graph contains:

- pane docking positions and proportional splitter sizes;
- floating-window layout, bounds, and window state;
- tab groups and their active tabs; and
- hidden or closed panes.

The frontend file also records the stable ID of each hidden pane's prior dock.
Dock.Avalonia deliberately keeps that runtime link out of its serialized model,
so this small piece of docking-only metadata allows `View > Panes` to reopen a
restored hidden pane in its previous group. No pane control or editor data is
serialized. During restore, `EditorDockFactory` validates the five registered
pane IDs and reattaches the existing Avalonia controls as their contexts.

The default location is:

```text
<LocalApplicationData>/QuantumCoasterWorks/Editor/track-docking-layout.json
```

The service creates the directory on first save and replaces the file through
a temporary file so an interrupted write does not replace the last complete
layout.

## Startup, shutdown, and fallback

`App` supplies the persistence service to `MainWindow`. The docking adapter
loads and initializes the saved layout before the shell assigns it to
`DockControl`. A successfully restored layout is not overwritten by workspace
profile defaults, which preserves panes that the user closed.

The main window saves the current docking graph while it is closing. A missing
file starts with the documented M162 default. A malformed, incompatible, or
incomplete graph is ignored and also falls back to that default:

- Route at left;
- non-closeable Viewport in the center;
- Inspector at right; and
- Math Plots active beside Diagnostics in the bottom tab group.

Loading and saving failures do not prevent the editor from starting or
closing.

## Reset Layout

`View > Reset Layout` deletes the saved layout and replaces the active docking
graph with a newly created M162 default Track layout. The same pane controls
remain connected, and the active editor document is not opened, closed,
reloaded, or otherwise changed. If the user later closes the editor normally,
the default arrangement becomes the next saved layout.

## Boundary and exclusions

This file stores docking presentation state only. It does not contain or alter:

- viewport camera state;
- editor selection;
- document or project data;
- engineering snapshots;
- workspace profile state; or
- undo/redo history.

No Dock.Avalonia or docking-serialization package is referenced by
`Quantum.Core`, `Quantum.Track`, `Quantum.Math`, `EditorWorkspace`,
`EngineeringSnapshot`, the authoring graph, or the document model.

## Tests

`Quantum.Tests/Editor/DockingInfrastructureTests.cs` covers save/restore of
split proportions, active tabs, hidden panes, prior hidden-pane hosts, frontend
context rebinding, floating-window geometry, missing files, corrupted files,
fallback to the M162 default, and Reset Layout deletion/default recreation.
