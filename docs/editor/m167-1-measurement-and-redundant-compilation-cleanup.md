# M167.1 Measurement and Redundant Compilation Cleanup

## Scope

M167.1 measures and reduces redundant synchronous authoring work without adding
transactions, scheduling, background work, or interactive editing. Track Layout
Package V2 and the visible M166 editor workflow remain unchanged.

## Measurement hooks

The measurement hooks are internal and opt-in so the public backend and editor
contracts do not change:

- `TrackAuthoringPipelineMeasurement` counts graph compiler invocations and records
  total graph-compilation and `EngineeringSnapshot` build time within one scoped
  execution context.
- `EditorViewportPipelineMeasurement` counts renderer-neutral viewport projection
  builds and records their total time within one scoped execution context.

The scopes use `AsyncLocal` ownership. Parallel tests can measure independent editor
operations without resetting global process counters.

## Confirmed baseline

The baseline counts below are asserted by
`M167CompilationPipelineMeasurementTests` before the cleanup:

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

## Cleanup target

M167.1 will retain the already validated graph compilation through document
replacement and history, provide a V2 export path for an existing successful compile
result, and cache canonical committed package JSON. The target counts are:

| Operation | Target graph compiler invocations |
|---|---:|
| ApplyGraphEdit, non-empty changed route | 1 |
| ReplaceGraph, non-empty route | 1 |
| Save | 0 |
| Open | 1 |
| Undo | 0 |
| Redo | 0 |

Engineering snapshots and viewport projections remain one per changed active
compilation. No rendering, selection, dirty-state, persistence, or history behavior is
intended to change.
