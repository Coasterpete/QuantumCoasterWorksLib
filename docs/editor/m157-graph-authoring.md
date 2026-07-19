# Milestone 157 Graph-Based Section Authoring Vertical Slice

## Purpose

M157 makes the backend `TrackAuthoringGraph` the Avalonia editor's authoritative
section-authoring model. Track Layout Package V2 remains the Open/Save compatibility
contract, while `TrackAuthoringDefinition` and `TrackAuthoringDocumentBuilder` remain
authoritative for producing evaluated track geometry.

The milestone deliberately supports one deterministic linear route. It does not add
branching, graph layout persistence, force-authored nodes, control-point editing,
timeline playback, trains, probes, or a new file format.

## User workflow

1. Launch the editor. The existing seven-section showcase document is imported into
   an authoring graph.
2. The left panel displays the connected route:
   `launch -> curve-in -> sweeper -> reverse-transition -> return-curve -> curve-out -> brake-run`.
3. Select the `sweeper` node.
4. The Inspector displays its backend section kind, ID, length, roll, and signed radius.
5. Change signed radius from `50` to `30` and select **Apply radius**.
6. The candidate graph is validated and compiled through the existing backend pipeline.
7. Only a successful candidate replaces the active graph. The viewport and diagnostics
   refresh from the new `TrackAuthoringCompilation` immediately after that commit.
8. Undo and Redo restore the complete immutable graph snapshot atomically.
9. Save writes Track Layout Package V2; Open reconstructs the same linear graph.

## Source of truth and ownership

```text
Open V2 JSON
  -> TrackLayoutPackageV2GraphAdapter
  -> TrackAuthoringGraph (authoritative editor state)
  -> TrackAuthoringGraphCompiler
  -> TrackAuthoringDefinition
  -> TrackAuthoringDocumentBuilder.Compile
  -> TrackAuthoringCompilation
  -> TrackSamplingService
  -> Avalonia viewport

Inspector edit
  -> candidate immutable TrackAuthoringGraph
  -> validate and compile candidate
  -> commit only on success
  -> TrackGraphSnapshotOperation

Save
  -> active TrackAuthoringGraph
  -> TrackLayoutPackageV2GraphAdapter
  -> V2 JSON
```

Avalonia code does not mutate `TrackLayoutPackageV2Dto.Sections`. The public `Package`
property remains as a detached compatibility snapshot generated from the graph. Mutating
that returned DTO does not mutate the document.

Metadata and heartline values are preserved as immutable ancillary V2 state. They are
read-only in M157 so there is no second UI-owned authoring representation.

## Candidate commit and history guarantees

`EditorWorkspace.ApplyGraphEdit` creates a candidate without changing the active document.
It calls the pure backend graph compiler first. Cycles, disconnection, duplicate IDs,
branching, merging, invalid endpoints, invalid section construction, or downstream backend
compilation errors are reported without changing:

- the active graph;
- the active `TrackAuthoringCompilation` or runtime;
- the viewport snapshot;
- dirty state; or
- Undo/Redo history.

Successful edits enter history as `TrackGraphSnapshotOperation`, which contains only the
before and after immutable graph snapshots. `UndoRedoService` executes an operation before
pushing it, providing a second commit gate if a snapshot unexpectedly fails recompilation.

## Dirty state and persistence

Dirty state is based on canonical Track Layout Package V2 content relative to the most
recent savepoint:

- Open and successful Save are clean.
- A graph edit that differs from the savepoint is dirty.
- Undo back to saved graph content is clean.
- Redo away from saved content is dirty.
- If the redone graph was the saved content, Redo is clean.

Save preserves the existing V2 contract, section IDs, deterministic route order, section
parameters, start pose, banking, metadata, and heartline values. Graph connections are
reconstructed from V2 section order on Open; graph layout, selection, viewport camera, and
history are not serialized.

## Running and smoke test

From the repository root:

```text
dotnet run --project Quantum.Editor.Avalonia/Quantum.Editor.Avalonia.csproj
```

Manual smoke test:

1. Confirm all seven connected graph nodes appear.
2. Save the initial document to establish a clean baseline.
3. Select `sweeper`, change radius to `30`, and apply.
4. Confirm the track bends more tightly, maximum absolute curvature exceeds `0.03 1/m`,
   total length remains `195 m`, selection remains on `sweeper`, and state is modified.
5. Undo and confirm radius `50`, the original viewport, and clean state.
6. Redo and confirm radius `30`, the tighter viewport, and modified state.
7. Enter radius `0` and confirm rejection without a new history entry or viewport change.
8. Save, reopen the file, and confirm the connected route, radius `30`, compiled viewport,
   banking, metadata, heartline, and clean state survive the round trip.

## Explicit limitations

- topology is displayed but cannot be edited in the UI;
- only constant-curvature signed radius is editable;
- straight length and transition, spatial, banking, and heartline editing are deferred;
- graph branching, switches, block topology, viewport control points, force nodes,
  playback, trains, probes, docking customization, and rendering polish remain out of scope.
