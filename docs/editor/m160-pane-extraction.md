# Milestone 160 Pane Extraction

M160 extracts the editor's five content panes without changing the editor
layout, authoring behavior, or backend pipeline. The fixed grid, splitters,
bottom tabs, toolbar, menu, and status bar remain owned by `MainWindow`; this
milestone does not introduce docking or layout persistence.

## Extracted controls

- `RoutePaneControl` owns the track header, connected authoring route, selected
  and highlighted node presentation, and route pointer/selection events.
- `ViewportPaneControl` owns the viewport header, statistics, selection overlay,
  and the existing `TrackViewportControl`. It preserves the fit, projection,
  transported-frame visibility, station cursor, section highlight, and sample
  selection surface.
- `InspectorPaneControl` owns the existing track, section, and canonical sample
  inspectors, including the established signed-radius edit path.
- `MathPlotsPaneControl` owns the plot header, channel visibility toggles,
  station readout, and the existing `EngineeringPlotWorkspaceControl`.
- `DiagnosticsPaneControl` owns the sampling and frame diagnostic summary and
  list.

Each pane is a public Avalonia `UserControl` with a parameterless constructor
and explicit state/event properties for composition. The lower-level viewport
and Math Plot renderers remain unchanged and are nested by their pane controls.

## Coordination boundary

`MainWindow` remains the composition root. It listens to `EditorWorkspace`
notifications, supplies the current immutable snapshots and selection state to
the panes, relays pane interactions to the existing workspace commands, and
synchronizes station and section highlighting across panes.

No new document, graph, geometry, sampling, or projection model was added.
`EditorWorkspace`, `TrackSamplingService`, `EngineeringSnapshot`, the authoring
graph compiler, undo/redo, and Track Layout Package V2 persistence remain the
same sources of truth used before extraction. The Inspector's radius edit still
uses `EditorWorkspace.ApplyGraphEdit`, so validation and atomic history behavior
are unchanged.

## Deliberate exclusions

- docking, floating panes, tab rearrangement, or saved layouts;
- new pane commands or editor features;
- backend or file-format changes;
- renderer replacement or engine dependencies.

The extraction contract is covered by
`Quantum.Tests/Editor/PaneExtractionContractTests.cs`; the complete solution
build and test suite continue to validate the existing backend and editor
behavior.
