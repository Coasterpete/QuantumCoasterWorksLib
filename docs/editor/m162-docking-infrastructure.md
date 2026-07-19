# Milestone 162 Docking Infrastructure

M162 replaces the fixed M160/M161 workbench grid with a Dock.Avalonia layout.
The Track workspace remains the default and only visible workspace, with the
same Route, Viewport, Inspector, Math Plots, and Diagnostics pane controls and
the same editor coordination owned by `MainWindow`.

## Docking integration

The Avalonia frontend references Dock.Avalonia `12.0.0.2`, its Fluent theme,
and the matching MVVM model package. All library-specific code is contained in
`Quantum.Editor.Avalonia.Services.Docking`:

- `DockPaneRegistry` registers the five stable M161 pane identifiers, titles,
  and close behavior.
- `EditorDockFactory` creates and initializes the Dock.Avalonia model graph,
  supplies the existing pane controls as frontend contexts, enables floating
  host windows, and hides closed tools so they can be restored.
- `EditorDockingAdapter` is the shell-facing boundary for pane lookup,
  visibility, close, and reopen operations.
- `DockingLayoutIds` identifies only the frontend docking hosts; these IDs do
  not enter workspace or backend contracts.

The default layout mirrors the previous workbench: Route on the left,
Viewport in the center, Inspector on the right, and a bottom tab group with
Math Plots active beside Diagnostics. Proportional splitters provide resizing.
Dock.Avalonia supplies drag docking, tabbing, and floating windows. The
Viewport can move, dock, tab, and float, but its close action is disabled by
default.

## Pane lifecycle

Route, Inspector, Math Plots, and Diagnostics are closeable. Closing one moves
it into Dock.Avalonia's hidden-tool collection while retaining its previous
dock. The `View > Panes` menu restores and focuses any registered pane; the
existing `View > Math Plots` command now uses the same path.

This milestone does not serialize the docking graph. Every editor launch
creates the documented default Track arrangement. Layout persistence remains
reserved for a later milestone.

## Preserved editor behavior

M162 reuses the exact M160 pane control instances. `MainWindow` still:

- projects the active document, selection, viewport snapshot, and engineering
  snapshot into those controls;
- routes authoring, viewport, and Math Plot interactions into the existing
  `EditorWorkspace` APIs;
- synchronizes station cursor and section highlighting across panes; and
- owns document commands, compilation updates, undo/redo, and persistence UI.

No changes were made to `EditorWorkspace`, `EngineeringSnapshot`, the
authoring graph, document model, compilation, engineering calculations, or any
backend project. Docking packages are referenced only by
`Quantum.Editor.Avalonia`.

## Deliberate exclusions

- saved or restored user layouts;
- workspace selector UI;
- Train, Support, Terrain, or Simulation workspaces;
- placeholder panes; and
- backend or Track Workspace docking abstractions.

`Quantum.Tests/Editor/DockingInfrastructureTests.cs` covers pane registration,
adapter/factory initialization, the default Track composition, closed-pane
restoration, and the non-closeable primary Viewport contract. Existing editor
and backend tests continue to cover the preserved behavior.
