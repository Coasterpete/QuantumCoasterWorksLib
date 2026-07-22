# Milestone 166 First Functional Track Authoring

## Scope

M166 establishes the first end-to-end track creation workflow:

1. New creates an empty authoring graph.
2. Add creates a typed Straight, Constant Curvature, or Curvature Transition section.
3. Before and After insert relative to the selected stable node ID.
4. Delete and Move Up/Down use immutable backend route operations.
5. Applying an edit validates and compiles the complete candidate route.
6. Only a successful candidate replaces the active graph and compilation.
7. Undo/Redo restore graph snapshots, including the empty route.
8. A non-empty compiled route saves and reopens through Track Layout Package V2.

Spatial NURBS sections remain supported by Open/Save and compilation, but M166 does
not add spatial control-point creation or editing.

## Backend authoring contract

TrackAuthoringSectionDefinition is the common immutable node payload. It owns:

- stable section ID;
- TrackAuthoringSectionFamily; and
- stable backend TypeId.

The geometric definitions derive from this contract and remain their own typed
parameter and validation models. The backend discriminator is independent from
Avalonia labels and Track Layout Package vocabulary.

TrackAuthoringGraphRouteValidator validates and orders topology without compiling
payloads. An empty route is structurally valid. TrackAuthoringGraphCompiler adds
the non-empty and compiler-support requirements.

TrackAuthoringGraphOperations owns append, insert, delete, move, and replace
behavior. Avalonia does not construct graph edges.

## Atomic editor transaction

    Avalonia field draft
      -> typed Quantum.Track definition
      -> immutable candidate graph
      -> topology validation
      -> complete backend compilation
      -> TrackGraphSnapshotOperation
      -> viewport and Math Plot refresh

Invalid definitions, invalid topology, unsupported families, and downstream compile
failures do not replace the active graph, compilation, viewport, dirty state, or
Undo history.

The empty graph has no compilation, viewport samples, or V2 package. Save is enabled
after the first section compiles. Deleting the final section returns to the empty
state and Undo restores the prior compiled graph.

## Banking policy

M166 does not silently remap explicit station-domain banking keys. A section insert,
delete, or length edit that invalidates explicit banking coverage is rejected by the
existing backend compilation gate. Automatic banking rebasing requires a separately
designed authoring policy.

## Persistence

Track Layout Package V2 continues to preserve the four existing geometric section
types. M166 does not add force data to V2 and does not save empty packages.

Future force-authored sections require:

- an immutable force authoring definition;
- explicit force-to-centerline compiler semantics;
- structured diagnostics; and
- a lossless source-authoring project or companion package.

The common graph identity, topology, selection, and Undo infrastructure does not
depend on those future compiler or persistence decisions.

## Explicit exclusions

- force-authored nodes and FVD solver integration;
- spatial control-point editing;
- imported-curve and predefined-element implementations;
- automatic banking-key remapping;
- branching routes and graph-layout persistence;
- force profiles added to Track Layout Package V2; and
- Unity, Unreal, or renderer dependencies in backend projects.
