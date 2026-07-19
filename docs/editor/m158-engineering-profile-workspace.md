# Milestone 158 Math Plot Workspace Direction

## Status and purpose

This is a documentation-only direction for M158 and later editor milestones. It
does not implement UI or change backend behavior.

Quantum's primary editor will become a precision track-engineering workspace
inspired by openFVeiD/FVD workflows while retaining Quantum's own code,
architecture, terminology, branding, and engineering capabilities. The primary
working surface will combine a 3D technical viewport with synchronized
Math Plots, diagnostics, section boundaries, control points, and
numeric inspectors. A node or bubble graph must not define the editor's visual
identity.

M157's `TrackAuthoringGraph` remains the authoritative editor authoring model.
M158 changes how that model is presented and operated; it does not replace it
with plot-owned or viewport-owned state.

## Product and workspace direction

The default workspace should make numerical track behavior visible and
inspectable:

- the 3D viewport shows the evaluated centerline, frames, selected section,
  selected control point, cursor station, and optional train pose;
- a stack of aligned plots shows authored and realized engineering quantities
  against station distance or time;
- one shared cursor and selection model coordinates the viewport, plots,
  section list, boundaries, control points, and inspectors;
- numeric inspectors provide precise, unit-aware authoring and read-only
  diagnostic values;
- graph edits continue to use candidate validation, compilation, and atomic
  commit before any visible canonical snapshot changes.

The editor should expose engineering detail without presenting derived samples
as authored data. Authored targets and realized results must use distinct names
and visual treatment when both are shown.

## Planned synchronized plots

The engineering workspace is planned to support the following aligned plots:

- pitch;
- yaw;
- roll/banking;
- curvature and radius;
- elevation;
- speed;
- vertical, lateral, and longitudinal G-force;
- pitch rate, yaw rate, and roll rate;
- jerk and continuity diagnostics.

Every channel needs an explicit backend definition, units, sign convention,
reference frame, domain, sampling policy, and unavailable-value policy before
the editor enables it. In particular:

- pitch, yaw, and roll must follow documented Quantum frame and angle
  conventions, including backend-owned angle unwrapping for continuous plots;
- angular-rate labels must state whether the rate is per station distance or
  per time, for example `deg/m` or `deg/s`;
- curvature and radius must state signed-versus-magnitude semantics, and the
  backend must define the zero-curvature radius representation;
- vertical, lateral, and longitudinal G-force must state their mapping to the
  canonical train/track frame and whether the values represent proper force,
  kinematic acceleration, or another documented quantity;
- jerk must state the acceleration component, reference frame, derivative
  domain, and units;
- continuity channels must report backend diagnostic metrics and threshold
  events rather than a UI-generated smoothness score.

Plots may contain authored targets, realized values, or diagnostics. The legend
and inspector must identify which category each series belongs to. A realized
curve is never directly editable as though it were an authoring control.

## Canonical-data rule

Geometry, plots, the viewport, numeric inspectors, train poses, and dynamics
must consume the same versioned backend evaluation pipeline. They must not
introduce UI-only curve evaluation, frame construction, derivative estimates,
force calculations, angle unwrapping, or other approximations.

The intended ownership flow is:

```text
TrackAuthoringGraph (authoritative authoring state)
  -> TrackAuthoringGraphCompiler
  -> TrackAuthoringDefinition
  -> TrackAuthoringDocumentBuilder.Compile
  -> TrackAuthoringCompilation
  -> canonical station/time evaluation and diagnostics
       -> TrackEvaluator and BankingProfileSampler
       -> train-pose and dynamics calculations when requested
       -> one revisioned engineering sample snapshot
  -> viewport, plots, inspectors, and selection projections
```

One engineering snapshot must identify the graph/compilation revision, sampling
options, station grid, optional dynamics run, train/reference-point definition,
and applicable tolerances. All visible consumers use that snapshot or exact
queries against the same immutable compilation revision. A successful authoring
edit replaces the graph, compilation, engineering snapshot, viewport, plots,
and inspectors as one coherent workspace update. A rejected edit replaces none
of them.

The frontend may project backend results into pixels and may omit already
evaluated display points for rendering performance. It may not invent new
sample values. Cursor and inspector values must come from an exact backend query
or a backend-defined interpolation over the same snapshot, not from screen-space
interpolation. Missing canonical data is shown as unavailable; it is not filled
with a plausible-looking UI curve.

## Station-distance and time-domain synchronization

Station distance is the primary geometry domain and uses Quantum's canonical
measured centerline arc-length semantics. Time-domain views are tied to a
specific canonical dynamics run and its train/reference-point definition.

The shared navigation state should carry at least:

- compilation and dynamics-run revision;
- active domain (`station distance` or `time`);
- station distance;
- time when a dynamics mapping exists;
- train/reference point used for the mapping;
- selected section/control identity where applicable.

Switching domains preserves the same physical sample through a backend-provided
station-to-time or time-to-station mapping. The mapping must use dynamics
samples from the same compilation and must define interpolation, clamping,
stopped-train, reverse-motion, and repeated-station behavior. When more than one
time maps to a station, the active time occurrence remains part of the cursor
state; the editor must not pretend the mapping is one-to-one. If no canonical
dynamics run exists, the time domain and time-derived channels remain visibly
unavailable rather than being inferred from geometry.

All plots in the current domain share one horizontal scale, pan/zoom range,
cursor, and section overlays. A domain change updates the complete plot stack,
viewport train pose, and inspector together.

## Shared cursor and selection behavior

Cursor and selection are related but distinct:

- pointer hover moves a transient shared cursor across every visible plot and
  the corresponding backend-evaluated position in the viewport;
- clicking a plot or the centerline pins the shared station/time selection;
- clicking a section, boundary, or control point selects that authored object
  and moves the shared cursor to its canonical location when one is defined;
- moving the viewport station or train playback position moves every plot
  crosshair and refreshes the numeric inspector;
- plot pan/zoom is shared horizontally but does not silently change authoring
  selection;
- keyboard stepping, if provided, advances through canonical samples or an
  explicit engineering increment rather than screen pixels.

Selection identity should survive recompilation when the same authored object
still exists. If an edit removes the selected object, the workspace must apply
a documented fallback such as its nearest surviving section or a cleared
selection. The cursor must clamp or remap through backend distance semantics;
stale screen coordinates must not determine the fallback.

## Section-boundary and control-point synchronization

Resolved section boundaries appear as aligned vertical markers across the plot
stack and as markers or highlighted transitions in the viewport. Selecting a
boundary in either surface selects the same boundary everywhere and exposes its
upstream/downstream section identities and exact canonical station in the
inspector.

Control points and profile keys must have stable authoring identities. Their
viewport handles, plot handles, section/outliner entries, and inspector rows are
different projections of the same graph-backed object. Editing any projection
creates a candidate graph edit, validates and compiles it through the backend,
and commits it through the existing undo/redo transaction only on success.

Boundary positions shown over realized data come from resolved compiled section
intervals. The UI must not reconstruct them by accumulating displayed lengths.
Coincident boundaries or keys must remain individually selectable without
altering their canonical station values.

## Numeric inspector requirements

The inspector must support precision work rather than rounded display-only
editing:

- show the selected object's stable ID, owning section, active station/time,
  source revision, and availability state;
- show explicit units and enough precision to diagnose backend behavior;
- distinguish editable authored values, authored targets, and read-only
  realized/diagnostic values;
- show angle, rate, curvature/radius, position/elevation, speed, acceleration/G,
  and jerk conventions relevant to the current sample;
- expose section-local and global station values when both are useful;
- expose train/reference-point identity for dynamics values;
- validate finite values, ranges, ordering, and units before constructing a
  candidate graph edit;
- preserve exact entered values in authoring state rather than replacing them
  with formatted display text;
- report backend validation or compilation errors without partially updating
  the graph, plots, viewport, selection, dirty state, or history;
- mark stale or unavailable diagnostics explicitly and never substitute zero
  unless zero is the canonical backend result.

Display-unit conversion belongs to the editor, but stored values and backend
queries continue to use the canonical backend units. Conversion must be
reversible within documented numeric tolerance.

## Role of the node canvas

The node canvas becomes secondary/internal tooling. It may remain available for
developer diagnostics, topology inspection, advanced route operations, or
future workflows where graph structure is genuinely the clearest view. It must
not occupy the primary workspace by default or become a second authoring model.

The graph itself remains authoritative. The Math Plot Workspace sends
edits to `TrackAuthoringGraph`; it does not mutate a parallel list of plotted
sections. Open/Save compatibility continues through the established graph and
Track Layout Package adapters until a later contract is deliberately approved.

## M158 implementation scope

M158 should deliver a focused vertical slice of the new editing identity:

1. Make the synchronized viewport and Math Plot stack the default
   workspace, with the node canvas moved behind a secondary/internal surface.
2. Introduce one revisioned, editor-facing engineering snapshot sourced from
   the active `TrackAuthoringCompilation`. Backend calculations remain in
   engine-agnostic projects; Avalonia owns only interaction and rendering.
3. Implement the station-distance domain first with canonical pitch, yaw,
   roll/banking, curvature/radius, elevation, and per-distance pitch/yaw/roll
   rate channels, plus existing backend continuity events and threshold markers.
   Define and test the backend angle, unwrapping, rate, radius, and continuity
   contracts before enabling their UI series.
4. Implement shared horizontal pan/zoom, hover cursor, pinned station selection,
   viewport marker, section-boundary overlays, control-point/profile-key
   selection, and synchronized numeric inspection.
5. Preserve M157's graph-backed signed-radius edit as the first end-to-end
   editable profile operation. Route the edit through candidate graph
   compilation and atomic undo/redo; do not edit realized plot samples.
6. Add backend and editor-independent tests for channel conventions, snapshot
   revision coherence, cursor mapping, boundary/control selection, rejected-edit
   rollback, and viewport/plot/inspector agreement.

M158 may present the complete planned plot catalog with unavailable states, but
it must not fabricate values for channels outside the implemented canonical
backend slice.

## Deferred after M158

The following remain future M158+ work unless a prerequisite is already proven
and deliberately pulled into the milestone:

- canonical dynamics-run integration for time-domain navigation;
- speed and vertical/lateral/longitudinal G-force plots driven by a selected
  train and canonical dynamics configuration;
- per-time pitch/yaw/roll rates, jerk channels, and richer continuity overlays;
- direct manipulation of banking, force, elevation, and arbitrary curvature
  profiles beyond the initial graph-backed radius edit;
- multi-train or multi-reference-point comparison;
- branching-route, switch, block-zone, and topology editing workflows;
- production GPU/PBR rendering, ride-through presentation, terrain, supports,
  final train meshes, and renderer selection;
- graph-layout persistence or replacement of Track Layout Package persistence;
- copying openFVeiD implementation details, assets, branding, or file formats.

Deferred channels must enter the workspace only after their backend ownership,
units, sign/reference conventions, deterministic sampling, and regression tests
are established.

## Proposed implementation breakdown

### M158.1: Canonical engineering snapshot

Define the revisioned station-sampling request and result contracts. Produce
geometry/frame/profile channels and resolved boundary/key metadata from one
active compilation. Add deterministic backend tests before UI consumption.

### M158.2: Profile-first workspace shell

Recompose the Avalonia workspace around the viewport and aligned plot stack.
Keep the graph accessible as secondary tooling and render only values supplied
by the canonical snapshot.

### M158.3: Shared navigation and selection

Add plot/viewport cursor synchronization, pinned station selection, aligned
pan/zoom, section-boundary and control/key selection, and stable selection
remapping after successful compilation.

### M158.4: Precision inspector and editable radius slice

Add unit-aware authored-versus-realized inspection and connect the selected
curvature/radius operation to the existing graph candidate/commit/undo path.
Keep other realized channels read-only.

### M158.5: Coherence validation

Prove that graph revision, compilation, viewport, plots, selection, and
inspector advance atomically. Cover unavailable channels and failed edits, and
perform a manual smoke test over the self-authored showcase layout.

## M158 acceptance boundary

M158 is complete when the default editor reads as a synchronized numerical
track workspace, a radius edit flows through the authoritative graph and updates
every implemented canonical view coherently, and unavailable dynamics channels
are honest about missing backend data. It is not required to complete the full
FVD solver, time-domain dynamics suite, direct manipulation for every profile,
or a production renderer.
