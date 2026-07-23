# M167.1 Measurement and Redundant Compilation Cleanup

## Scope

M167.1 measures and reduces redundant synchronous authoring work without adding
transactions, scheduling, background work, or interactive editing. Track Layout
Package V2 and the visible M166 editor workflow remain unchanged.

## Measurement hooks

The measurement hooks are internal and opt-in, so measurement does not add a public
backend or editor contract:

- `TrackAuthoringPipelineMeasurement` counts graph compiler invocations and records
  total graph-compilation and `EngineeringSnapshot` build time within one scoped
  execution context.
- `EditorViewportPipelineMeasurement` counts renderer-neutral viewport projection
  builds and records their total time within one scoped execution context.

The scopes use `AsyncLocal` ownership. Parallel tests can measure independent editor
operations without resetting global process counters.

## Confirmed pipeline before cleanup

The baseline counts below are asserted by
`M167CompilationPipelineMeasurementTests` in commit `776b8be` before the cleanup:

| Operation | Graph compiler invocations | Engineering snapshots | Viewport projections |
|---|---:|---:|---:|
| ApplyGraphEdit, non-empty changed route | 5 | 1 | 1 |
| ReplaceGraph, non-empty route | 2 | 1 when active | 1 when active |
| Save | 2 | 0 | 0 |
| Open | 3 | 1 | 1 |
| Undo to non-empty route | 2 | 1 | 1 |
| Redo to non-empty route | 2 | 1 | 1 |

For `ApplyGraphEdit`, the five full compilations are:

1. candidate preflight in `EditorWorkspace.ApplyGraphEdit`;
2. current-graph compilation hidden inside V2 export for `beforeJson`;
3. candidate compilation hidden inside V2 export for `afterJson`;
4. candidate compilation in `TrackEditorDocument.ReplaceGraph`;
5. candidate compilation hidden inside V2 export for dirty-state comparison.

Only the fourth candidate compilation becomes the active document compilation and
feeds the engineering and viewport projection. The other four results are discarded.

The other measured operations followed these paths:

- `ReplaceGraph`: compile the candidate, then compile it again while exporting JSON
  for dirty-state comparison.
- `Save`: export/compile once to write the file, then export/compile again while
  establishing the clean savepoint.
- `Open`: compile the imported graph, export/compile while constructing the document,
  then export/compile again when the file service marks it clean.
- `Undo` and `Redo`: call `ReplaceGraph`, producing the same two compilations as a
  direct replacement.

Serialization itself did not compile, but every graph-to-V2 export did. Engineering
snapshot and viewport projection construction occurred only from the one compilation
accepted by the active document; those stages were not duplicated.

## Implemented cleanup

M167.1 now:

- binds every `TrackAuthoringGraphCompileResult` to the exact immutable graph instance
  that produced it;
- provides a V2 export overload that accepts that exact result and rejects a result
  from another graph;
- creates one prepared editor graph state containing the graph, compilation, and
  canonical V2 JSON;
- retains the prepared before/after states in normal graph undo history; and
- caches the active canonical package JSON for save and dirty-state comparison.

The existing two-argument V2 export and public `ReplaceGraph` behavior remain
available. The overload is the only public API addition, and is required to reuse a
compile result safely across the `Quantum.Track` to `Quantum.IO` boundary.

## Before/after benchmark summary

The same scoped instrumentation tests produce these deterministic invocation counts:

| Operation | Before | After | Eliminated | Engineering snapshots after | Viewport projections after |
|---|---:|---:|---:|---:|---:|
| ApplyGraphEdit, non-empty changed route | 5 | 1 | 4 | 1 | 1 |
| ReplaceGraph, non-empty route | 2 | 1 | 1 | 1 when active | 1 when active |
| Save | 2 | 0 | 2 | 0 | 0 |
| Open | 3 | 1 | 2 | 1 | 1 |
| Undo to non-empty route | 2 | 0 | 2 | 1 | 1 |
| Redo to non-empty route | 2 | 0 | 2 | 1 | 1 |

Compilation, engineering snapshot, and viewport projection elapsed times are recorded
as `TimeSpan` totals for each measurement scope. The regression tests verify that a
measured invocation records positive elapsed time and that operations with zero work
record zero. Wall-clock budgets are intentionally deferred until representative small,
realistic, and long-route fixtures are established; M167.1 provides the hooks needed
for that benchmark work without introducing a benchmark dependency.

## Behavioral compatibility

The accepted compilation is still the source for `TrackDocument`,
`EngineeringSnapshot`, and viewport projection. Package JSON remains the canonical,
indented Track Layout V2 representation, and the reuse path is tested byte-for-byte
against the existing export path. Save/reopen, dirty-state transitions, graph editing,
and non-interactive undo/redo retain their M166 behavior.

Engineering snapshots and viewport projections remain one per changed active
compilation. No rendering, selection, dirty-state, persistence, or history behavior is
intended to change.
