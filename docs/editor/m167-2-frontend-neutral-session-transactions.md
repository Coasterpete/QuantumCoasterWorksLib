# M167.2 Frontend-Neutral Session, Transaction, and Revision Model

## Scope

M167.2 adds a synchronous, headless authoring-session boundary. It does not add
scheduling, background work, throttling, pointer gestures, live viewport behavior,
incremental compilation, or a broad `EditorWorkspace` migration.

`Quantum.Application` targets `netstandard2.1` and directly references only:

- `Quantum.Track` for immutable graph operations, validation, and compilation;
- `Quantum.IO` for canonical Track Layout Package V2 preparation.

Dependency-contract tests reject Avalonia, Unity, Unreal, renderer, browser, and
windowing dependencies. Geometry algorithms and section semantics remain in
`Quantum.Track`; persistence mapping remains in `Quantum.IO`.

## Ownership before and after

Before M167.2, M167.1's prepared source/compilation/JSON state and prepared-state
undo retention lived inside `Quantum.Editor.Avalonia`. That removed redundant work
from the current editor, but another frontend could not reuse the ownership model.

After M167.2:

- `PreparedTrackGraphState` is the shared immutable source graph, exact compile
  result, ancillary persistence state, and canonical package JSON tuple;
- `TrackAuthoringSession` owns the committed state, presented state, active
  transaction, structural revisions, dirty baseline, and authoring history;
- `InteractiveAuthoringTransaction` captures the exact before-state, base committed
  revision, stable target section ID, and parameter/operation identity;
- `EvaluatedTrackCandidate` retains the exact candidate graph, evaluation,
  compilation, canonical JSON, and structured diagnostics;
- `AuthoringHistory` retains exact prepared before/after states and exposes retained
  canonical JSON byte size without introducing eviction policy; and
- Avalonia reuses `PreparedTrackGraphState`, but its existing one-shot workspace and
  generic undo integration otherwise remain unchanged.

The session's ownership rules are:

| Operation | Committed state | Presented state | Normal history | Dirty state | Persistable content |
|---|---|---|---|---|---|
| Begin | unchanged | committed | unchanged | unchanged | committed |
| Valid update | unchanged | newest valid candidate | unchanged | unchanged | committed |
| Invalid update | unchanged | last valid candidate, if any | unchanged | unchanged | committed |
| Cancel | unchanged | committed | unchanged, including redo | unchanged | committed |
| Changed commit | exact evaluated candidate | committed candidate | one undo entry; redo cleared | recomputed | committed candidate |
| No-op commit | unchanged | committed | unchanged, including redo | unchanged | committed |
| Undo | exact retained before-state | committed before-state | entry moves to redo | recomputed | committed before-state |
| Redo | exact retained after-state | committed after-state | entry moves to undo | recomputed | committed after-state |

## Structural revisions

The application stale/current decisions use immutable structural value identities:

- `AuthoringSessionId`;
- `CommittedSourceRevision`;
- `TransactionRevision`;
- `ProvisionalEditRevision`;
- `EvaluatedCandidateRevision`.

Committed, transaction, and provisional sequences are monotonic within their
scopes. An evaluated candidate revision combines the captured committed base
and provisional revision. Update and commit compare these values structurally;
object-reference equality is used only by the M167.1 exact compile-result ownership
guard, not for transaction freshness.

Every matching valid or invalid update advances the provisional revision. A
mismatched transaction is rejected before evaluation. A commit accepts only the
exact newest structurally matching valid revision. Consequently, an older valid
preview cannot commit after a newer invalid update.

## Synchronous transaction path

The reusable interactive path is:

1. `BeginTransaction` captures the committed revision and exact prepared before-state.
2. `SubmitCandidate` applies an immutable absolute candidate operation to that
   captured base graph, never to a preceding provisional graph.
3. `TrackAuthoringCandidateEvaluator` validates and compiles synchronously.
4. A successful result is prepared through the compile-result-aware V2 export path.
5. `Commit` adopts that exact prepared candidate without reevaluation.
6. `Cancel`, `Undo`, and `Redo` only switch retained prepared states.

`ApplyOneShot` provides the same begin, one evaluation, and commit sequence for a
future non-interactive caller migration. M166 `EditorWorkspace.ApplyGraphEdit` is not
migrated in this milestone.

An empty candidate remains a valid editor/session state. It has no compilation and
no persistable package JSON, matching the existing empty-route editor behavior.

## Persistence and dirty state

`PersistableCanonicalPackageJson` always comes from `CommittedState`. A provisional
preview therefore cannot leak into Save. `MarkClean` captures the current committed
canonical JSON as the savepoint; `ReplaceSessionState` establishes open/new content,
invalidates previous session revisions, clears history, and chooses a clean or dirty
baseline.

Dirty state changes only after changed commit, undo, redo, mark-clean, or session
replacement. Returning by undo to the byte-identical clean canonical content reports
clean.

## Compile counts

Scoped pipeline instrumentation and focused tests establish:

| Session operation | Graph compiler invocations |
|---|---:|
| Non-empty candidate update | 1 |
| Empty candidate update | 0 |
| Commit of evaluated candidate | 0 |
| Undo | 0 |
| Redo | 0 |

Commit also reuses the exact candidate compile-result instance and canonical JSON
instance. Undo and redo restore the exact retained prepared states.

## Verification

The M167.2 tests cover committed/presented separation, base-derived updates,
monotonic valid and invalid revisions, structured rejection, stale structural
revision rejection, cancel and redo retention, no-op and changed commits, exact
candidate adoption, savepoint cleanliness, one-entry history, zero-compilation
commit/undo/redo, one-shot editing, empty candidates, retained-size exposure, and
frontend dependency contracts.

The full solution test run passes 1,887 tests with zero failures and zero skips.

## M167.3 entry point

M167.3 should begin with a thin scheduling coordinator above
`TrackAuthoringSession`. It should retain the session's structural transaction and
provisional revisions as the acceptance authority while adding latest-pending work
policy. The session remains the owner of committed, presented, dirty, persistence,
and history state. No M167.3 scheduling behavior is implemented here.
