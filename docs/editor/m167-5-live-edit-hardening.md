# M167.5 Live-Edit Foundation Hardening

## Scope and outcome

M167.5 hardens the M167.4 live-edit path without expanding the editable-property
surface. `StraightSectionDefinition.Length` remains the reference interaction used
to prove scheduling, lifecycle, history, accessibility, configuration, metrics,
and exact commit behavior.

Straight Length is not intended to be the permanent limit of live editing. It is a
small, deterministic scalar whose existing pointer path exercises the same
transaction boundaries future properties will need, without mixing this foundation
work with new domain editors or viewport interaction models.

This milestone does not add interpolation dropdowns, arbitrary numeric live
editing, curvature or banking editing, viewport handles, control-point tools,
force authoring, timelines, adaptive tessellation, smooth rail meshes, Unity
integration, global cross-document scheduling, cooperative/incremental compilation,
or train playback changes. The faceted viewport remains a separate renderer concern.

## Preserved interaction contract

The M167.4 contract remains intact:

- provisional absolute values derive from the authored value captured at gesture
  start;
- the coordinator keeps at most one running and one latest pending evaluation;
- presentation is rebuilt only from the session's accepted `PresentedState`;
- release freezes and commits the exact newest revision without recompilation;
- invalid input keeps the last valid geometry and cannot enter history;
- camera framing, engineering station, and stable-node selection survive preview
  replacement;
- typed Length plus **Apply section** remains a one-shot edit; and
- source mutation commands remain disabled during a live transaction.

The background interval remains 30 Hz. The Release observations below do not show
evidence that changing it is necessary, so M167.5 does not tune it.

## Authoritative history ownership

`TrackAuthoringSession.History` is now the one authoritative history for both
existing one-shot graph commands and live transactions.

One-shot editor commands still validate, compile, and prepare a candidate exactly
once. They then call `TrackAuthoringSession.CommitPreparedEdit`, which records and
adopts that already-prepared state. Live commit continues to record the exact
prepared candidate through the session's transaction commit. The editor
`UndoRedoService` is retained as a UI/command façade, but when owned by
`EditorWorkspace` it delegates descriptions, counts, clear, undo, and redo to the
active session and owns no parallel entries.

This gives one ordering across interleaved command styles:

```text
one-shot A -> live transaction B -> one-shot C
undo order: C -> B -> A
redo order: A -> B -> C
```

A changed valid scrub creates one entry. No-op, invalid, cancelled, or abandoned
gestures create none. Undo and redo install retained prepared states in the document,
so neither operation recompiles. Saving updates both document and session clean
baselines; reopening reconstructs the same canonical committed content with an
appropriately empty new-session history.

## Lifecycle under non-cooperative compilation

Document replacement, document close, and workspace shutdown now retire the active
evaluation coordinator asynchronously:

1. the live transaction is cancelled and its session revision becomes ineligible;
2. the coordinator is detached from the workspace immediately;
3. pending work completes as cancelled;
4. running whole-route compilation is allowed to return if it cannot observe
   cancellation;
5. the returned product is discarded before publication or commit; and
6. the retirement task is observed so it cannot become an unobserved task exception.

The synchronous lifecycle call does not wait for the compiler. `EditorWorkspace`
implements `IAsyncDisposable` and exposes `WaitForLifecycleCompletionAsync` for
tests and hosts that need to await complete background retirement after initiating
non-blocking shutdown.

Coordinator snapshots expose quiet counters for:

- `RejectedStaleCompletions`;
- `DiscardedPostLifecycleCompletions`; and
- `AbandonedFinalCommits`.

These are diagnostics, not normal-interaction log messages. Stress tests hold a
non-cooperative evaluator inside final whole-route work while replacement, close,
and shutdown return; after release they verify no stale preview, document mutation,
deadlock, caller-thread block, unobserved exception, or late final commit.

## Keyboard interaction and accessibility

The selected straight Length scrubber is focusable and participates in normal tab
focus. Its keyboard semantics are:

| Key | Behavior |
|---|---|
| Left / Right | Begin the same authoring transaction if needed, then subtract/add one configured step |
| Shift + Left / Right | Fine step |
| Ctrl + Left / Right | Coarse step |
| Enter | Freeze and commit the exact newest revision |
| Escape | Cancel and restore committed presentation |
| focus loss while active | Cancel, preventing a stranded keyboard transaction |

Keyboard updates accumulate an offset from the gesture's captured start value; they
do not chain from accepted preview geometry. Commit and cancel restore focus to the
new straight Length scrubber after Inspector refresh. The control disables itself
while exact commit is pending and is disabled when another transaction already owns
the session.

Avalonia automation metadata supplies a stable automation ID, the accessible name
"Straight section length live editor", and help text describing arrows,
Shift/Ctrl, Enter, and Escape. Real Avalonia headless key injection covers begin,
normal/fine/coarse update, commit, cancel, focus restoration, automation properties,
and disabled state.

## Sensitivity configuration

`StraightLengthScrubSensitivity` is a small injected configuration object shared by
pointer and keyboard input. A pointer pixel and a keyboard arrow step use the same
configured magnitude.

Defaults remain:

| Mode | Metres per pixel/step |
|---|---:|
| normal | 0.1 |
| Shift fine | 0.01 |
| Ctrl coarse | 1.0 |

Shift wins when both modifiers are present, preserving prior behavior. Every value
must be finite and greater than zero. `EditorWorkspace` accepts an optional instance;
there is intentionally no preferences UI in this milestone.

## Release measurement methodology

Repeat the measurement with:

```powershell
dotnet test Quantum.Tests/Quantum.Tests.csproj -c Release --filter "FullyQualifiedName~M167ReleaseLiveEditMeasurementTests" --logger "console;verbosity=detailed"
```

The self-authored matrix contains small (1 straight), realistic (40 straight), long
(160 straight), mixed-geometry (40 straight/constant-curvature/transition), and
spatial (1 straight plus 23 weighted degree-3 spatial) routes. Straight
`section-000` is the edited reference in every route.

Each case performs four fresh interactions. An interaction submits 24 absolute
updates in three eight-update bursts through the production serialized 30 Hz
coordinator. The first two bursts await and publish session-accepted results; the
last burst is frozen for exact commit. Counts aggregate all four interactions.
Submit-to-present samples come from accepted evaluator outcomes. Final-wait samples
come from the four exact commits. P50/P95/P99 use the nearest-rank method; with four
final-wait samples, P95 and P99 are the observed maximum.

The following Windows x64, .NET 8.0.29, Release results were recorded on
2026-07-22. They are local observations, not product gates.

| Route | Sections | Submitted | Coalesced | Started / compiler | Accepted previews | Stale | Present P50 / P95 / P99 (ms) | Final wait P50 / P95 / P99 (ms) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| small | 1 | 96 | 80 | 16 / 16 | 8 | 4 | 45.670 / 47.149 / 47.149 | 0.175 / 0.182 / 0.182 |
| realistic | 40 | 96 | 82 | 14 / 14 | 8 | 2 | 2.229 / 48.462 / 48.462 | 2.094 / 2.251 / 2.251 |
| long | 160 | 96 | 83 | 13 / 13 | 8 | 1 | 9.361 / 44.086 / 44.086 | 8.030 / 14.881 / 14.881 |
| mixed geometry | 40 | 96 | 82 | 14 / 14 | 8 | 2 | 4.779 / 42.624 / 42.624 | 4.758 / 4.812 / 4.812 |
| spatial | 24 | 96 | 83 | 13 / 13 | 8 | 1 | 18.564 / 72.415 / 72.415 | 17.760 / 23.014 / 23.014 |

Every started valid evaluation invoked the compiler exactly once. Coalescing removed
most submitted work, and commit performed no additional compilation. The spatial
case had the highest observed tail and final wait, but remained well below the scale
that would justify changing the current interval from this single-machine sample.

## Verification coverage

Focused tests cover Release metric collection and percentile math; mixed and spatial
fixtures; queued/running/frozen-final lifecycle replacement, close, and shutdown;
stale revision rejection; unified one-shot/live ordering; no-op, invalid, cancel,
undo, redo, save, and reopen behavior; real headless keyboard commit/cancel and
Shift/Ctrl precision; configured pointer sensitivity; and the existing camera,
station, selection, command policy, typed Apply, and exact-commit regressions.

## Remaining risks

- Release observations are from one Windows machine. macOS/Linux and slower machine
  distributions remain unmeasured.
- Four final waits per route make P95/P99 useful as recorded maxima, not stable
  population estimates.
- Whole-route compilation remains non-cooperative once inside the backend compiler;
  lifecycle safety discards its result but cannot reclaim its CPU time early.
- The UI façade is intentionally bound to graph authoring history. Future non-graph
  editor operations will need an explicit ownership decision rather than silently
  reintroducing a second stack.
- Only Straight Length exercises this foundation; additional property types still
  need their own validation and interaction design.

## Recommended next milestone

Before adding a broad set of live fields, run the same Release measurement command
on representative macOS and Linux machines and increase repetition count in a
dedicated performance environment. If those observations remain healthy, the next
milestone can extract the proven transaction/input binding into a reusable scalar
live-edit adapter and add one carefully selected second property type. Viewport
handles, banking/control-point editors, force authoring, timelines, renderer
tessellation, Unity integration, and incremental compilation should remain separate
milestones.
