# Milestone 165 First Functional Train Workspace

M165 enables the first functional Train workspace in the Avalonia editor. It
is a focused editor vertical slice over the existing immutable
`Quantum.Track.TrainConsistDefinition`, `TrainCarGeometry`, and
`TrainBogieLayout` backend types. Track remains the default workspace and its
Track Layout Package V2, graph authoring, engineering plots, viewport,
undo/redo, docking, and persistence behavior are unchanged.

## Train composition

The Train entry in the M164 workspace selector is now enabled. Its registered
frontend composition contains three real panes:

- **Train Configuration** edits car count, car-center spacing, body length,
  body width, body height, and bogie spacing;
- **Train Preview** draws a deterministic top-view box-and-bogie schematic;
  and
- **Train Summary** reports approximate consist length, inter-car gap,
  car-center spacing, body dimensions, bogie spacing, validation state, and
  definition revision.

The default Train dock graph places Configuration on the left, the
non-closeable Preview in the primary center area, and Summary along the bottom.
The composition is supplied by the registered Train profile through the same
`WorkspaceComposition` path as Track. No Train branch was added to
`EditorDockFactory` or the docking lifecycle infrastructure.

## Atomic backend validation

`TrainConsistEditorSession` is a small frontend editing boundary. Text fields
are parsed using invariant culture and then used to construct real backend
`TrainCarGeometry`, `TrainBogieLayout`, and `TrainConsistDefinition` instances.
The candidate becomes current only after every backend constructor succeeds.

If parsing fails, a scalar is non-positive or non-finite, or bogie spacing
exceeds body length, the edit is rejected with a concise message. The previous
immutable `TrainConsistDefinition` instance and its revision remain unchanged,
so Preview and Summary continue to display the last valid geometry.

The session is intentionally not a second train domain model. It only holds
frontend input state and the authoritative immutable backend definition.

## Deterministic preview and calculations

`TrainConsistPresentation` converts the backend definition into a read-only
schematic projection. Car centers are spaced by `CarSpacing` and centered
around station zero. Bogie centers are placed at plus/minus half
`BogieSpacing` from each car center.

The calculated readouts use:

```text
approximate consist length = car length + (car count - 1) * car-center spacing
inter-car gap = car-center spacing - car length
```

A negative gap is reported as body overlap rather than rejected because the
existing backend validation deliberately permits that geometry.

The preview knows only immutable consist geometry. A later train-style system
can associate that geometry with imported `.glb`/`.gltf` visual assets at a
separate frontend/adapter boundary without changing this edit session. M165
does not import assets or define a train-style file format.

## Switching, commands, and layout persistence

Track and Train docking adapters remain cached independently during the
session. Each adapter is paired with a cached frontend `DockControl`; switching
saves the outgoing layout and swaps the selected host control into the shell.
This lets Dock rematerialize the registered pane controls while preserving both
workspace visual trees in memory. `EditorDockableViewLocator` supplies the
small frontend template that presents a registered pane control only in Dock's
actual content surface, not in tab header or close-button template probes.

Track keeps `track-docking-layout.json`; Train uses
`train-docking-layout.json` through the M164 per-workspace persistence rule.

The generic `View > Panes` and `View > Reset Layout` commands remain available
in Train. Track document commands, Track undo/redo, Fit Track, transported
frames, Math Plots, projection controls, and their keyboard shortcuts are
hidden or inactive while Train is selected. The in-memory Track document stays
alive and is restored untouched on return to Track.

## Tests

Automated editor tests cover:

- Train profile availability and selector activation;
- Train pane registration and default docking layout;
- construction of backend geometry/layout/consist values;
- parse and backend-validation failures;
- preservation of the last valid immutable definition after rejection;
- deterministic schematic positions and calculated readouts;
- Track-to-Train and Train-to-Track activation;
- independent in-memory layout state; and
- separate Track and Train persisted layouts.

## Deferred

M165 does not add a train document or file format, 3D train assets, seats,
restraints, wheel assembly editing, articulation editing, train-on-track
placement, physics playback, simulation controls, or permanent train-style
asset metadata. Supports, Terrain, and Simulation remain disabled placeholders.
