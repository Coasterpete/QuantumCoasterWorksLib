# Milestone 161 Workspace Profile Infrastructure

M161 introduces editor workspace profiles without changing the M160 workbench
layout or any track-authoring behavior. The Track workspace is the default and
only available profile. It composes the same Route, Viewport, Inspector, Math
Plots, and Diagnostics panes, the same command surfaces, and the same default
transported-frame overlay as M160.

## Profile model

The infrastructure lives in `Quantum.Editor.Avalonia.Services.Workspaces`:

- `WorkspaceProfileId` provides stable identifiers for Track and the planned
  Train, Support, Terrain, and Simulation workspaces.
- `WorkspaceProfile` is immutable composition metadata: display name, icon key,
  available panes, default pane visibility, command groups, overlay defaults,
  and availability/switcher visibility.
- `WorkspaceProfileCatalog` registers profiles, rejects duplicate identifiers,
  performs lookup, and identifies the default profile.
- `WorkspaceProfileManager` selects the catalog default, exposes the current
  profile, and validates and notifies profile changes.

`WorkspaceProfileCatalog.CreateDefault()` registers all five named definitions.
Track is available and visible. Train, Support, Terrain, and Simulation are
deliberately unavailable and hidden until each has a meaningful vertical slice.

## Workbench integration

`MainWindow` remains the composition root and now receives a
`WorkspaceProfileManager` alongside the existing `EditorWorkspace`. It maps
known pane, command-group, and overlay identifiers onto the already extracted
controls. The Track metadata exactly matches the M160 composition, so no
workspace selector or other visible UI is added in M161.

Additional profiles can be registered in the catalog and selected through the
manager without adding profile-specific branching to `MainWindow`. New pane
types will still require a future composition extension when their vertical
slice exists; M161 does not add placeholder panes.

## Preserved boundaries

The workspace profile layer contains composition metadata only. It does not own
or modify:

- `EditorWorkspace`, `TrackAuthoringGraph`, or the authoring model;
- compilation, `EngineeringSnapshot`, sampling, or engineering calculations;
- selection, synchronized Math Plot interaction, or viewport rendering;
- undo/redo, Track Layout Package V2 persistence, or document state; or
- docking, floating panes, layout persistence, or pane extraction.

Registration, lookup, default selection, unavailable future definitions, and
switch notifications are covered by
`Quantum.Tests/Editor/WorkspaceProfileInfrastructureTests.cs`.
