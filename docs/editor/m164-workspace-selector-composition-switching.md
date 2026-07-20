# Milestone 164 Workspace Selector and Composition Switching

M164 adds a workspace selector to the Avalonia editor shell and makes docking
composition selection registry-driven. Track remains the only functional
workspace. The milestone supplies the switching framework for later editor
vertical slices without implementing any Train, Supports, Terrain, or
Simulation editing behavior.

## Workspace selector

The main toolbar now contains a Workspace dropdown populated from
`WorkspaceProfileCatalog.VisibleProfiles`. It initially lists, in registration
order:

- Track;
- Train (Coming Soon);
- Supports (Coming Soon);
- Terrain (Coming Soon); and
- Simulation (Coming Soon).

Track is selected and enabled. The other entries remain visible but disabled,
and `WorkspaceProfileManager` continues to reject programmatic attempts to
activate an unavailable profile. `WorkspaceSelectorModel` is a small,
Avalonia-frontend projection that keeps selector initialization and activation
rules independently testable.

## Profile-owned docking composition

Each `WorkspaceProfile` now owns a `WorkspaceComposition`. A composition
contains its pane registry and a function that creates its default Dock.Avalonia
root graph through a minimal layout-builder boundary. `EditorDockFactory` no
longer constructs a Track-specific graph: it creates the panes registered by
the active composition and asks that composition for its layout.

The Track composition owns the unchanged M162/M163 default arrangement:

- Route on the left;
- the non-closeable Viewport in the center;
- Inspector on the right; and
- Math Plots active beside Diagnostics in the bottom tool group.

The four unavailable profiles own empty frontend placeholder compositions.
Those layouts are not activated by the current selector, but they allow a later
milestone to replace a profile's placeholder with its real pane registry and
default graph without adding workspace-specific branches to the shell or dock
factory. The `View > Panes` menu is also populated from the active pane registry.

## Switching and persistence

When an available registered profile changes, `MainWindow` obtains the next
layout from that profile's composition, updates the `DockControl`, and applies
the profile's pane, command-group, and overlay defaults. Docking adapters are
kept per workspace during the session, so switching does not discard an
in-memory layout. The active layout is saved before switching and on window
close.

Layout persistence is namespaced by workspace identifier. Track deliberately
retains the M163 filename:

```text
<LocalApplicationData>/QuantumCoasterWorks/Editor/track-docking-layout.json
```

This preserves existing Track layouts without migration. A future workspace
uses `<workspace-id>-docking-layout.json`, preventing one workspace's docking
graph from being restored into another composition. Missing or invalid Track
state still falls back to the unchanged default Track graph, and Reset Layout
still leaves the active editor document open.

## Registration path

A later workspace milestone can register a new vertical slice by supplying a
profile with:

- its stable `WorkspaceProfileId`, display metadata, and availability;
- a `WorkspaceComposition` containing its pane registrations and default layout;
- its pane content controls at the Avalonia composition root; and
- its command groups and overlay defaults.

The selector, activation path, pane menu, docking factory, layout cache, and
per-workspace persistence require no workspace-specific switch statement.

## Preserved frontend and backend boundaries

M164 changes only `Quantum.Editor.Avalonia` and its tests/documentation. The
existing `EditorWorkspace` instance and active Track document remain alive
across composition changes. Track compilation, engineering snapshots,
selection, authoring, undo/redo, document persistence, and viewport behavior
are unchanged.

No changes were made to `Quantum.Core`, `Quantum.Track`, `Quantum.Math`, the
authoring graph, `EngineeringSnapshot`, or the document model. No Train,
Supports, Terrain, or Simulation editor, tools, or document types were added.

## Tests

`WorkspaceProfileInfrastructureTests` covers selector initialization,
registration, Track activation, disabled placeholder profiles, and composition
lookup. `DockingInfrastructureTests` continues to cover the default Track
composition and M163 layout persistence, fallback, reset, and pane lifecycle
behavior.
