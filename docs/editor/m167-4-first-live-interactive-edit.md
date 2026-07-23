# M167.4 First Live Interactive Edit

## Scope and outcome

M167.4 proves one complete Avalonia authoring loop for
`StraightSectionDefinition.Length`:

1. left-press the horizontal scrubber beside Length;
2. capture the pointer and begin one authoring transaction;
3. submit absolute provisional lengths while dragging;
4. display only session-accepted preview state;
5. show an invalid raw value without changing committed content;
6. release to await and commit the exact newest revision; or
7. press Escape, lose capture, replace/close the document, or close the workspace
   to cancel and restore committed presentation.

This milestone does not add general numeric live editing, viewport handles,
timelines, banking keys, control points, force authoring, incremental compilation,
parallel compilation, or a global scheduler.

## UI interaction

Only a selected straight section has the new `↔` affordance beside Length. The
existing `NumericUpDown` field and **Apply section** button keep their M166 one-shot
behavior.

The scrubber uses the authored value captured at pointer press:

```text
absoluteLength = startLength + totalHorizontalPointerDelta * sensitivity
```

It never derives the next value from a previous provisional result.

| Modifier | Sensitivity |
|---|---:|
| none | 0.1 m/pixel |
| Shift | 0.01 m/pixel |
| Ctrl | 1.0 m/pixel |

Press submits the unchanged base value, so a press/release with no movement follows
the exact same revision/commit path but creates no history entry. Movement updates
the raw field immediately. Accepted and committed values are identified separately
in the status below the field.

## Ownership and dispatch flow

`EditorWorkspace` owns exactly one `TrackAuthoringSession` and one
`TrackAuthoringEvaluationCoordinator` for its active `TrackEditorDocument`. The
context is created on activation and retained across ordinary document refreshes.
It is cancelled and disposed before/while handling New, Open, active-document
replacement, active-document close, and workspace shutdown.

`ApplyGraphEdit` remains the existing one-shot path. A normal one-shot document
change rehydrates the same session instance through `ReplaceSessionState`; M167.4
does not migrate every editor command.

The frontend flow is:

```mermaid
sequenceDiagram
    participant Pointer as "Avalonia scrubber"
    participant Inspector as "InspectorPaneControl"
    participant Workspace as "EditorWorkspace"
    participant Coordinator as "Evaluation coordinator"
    participant Session as "TrackAuthoringSession"
    participant Projection as "Engineering/viewport projection"

    Pointer->>Inspector: "press / total horizontal delta"
    Inspector->>Workspace: "begin + submit absolute length"
    Workspace->>Coordinator: "reserve/schedule revision"
    Coordinator->>Session: "publish valid or invalid candidate"
    Coordinator-->>Inspector: "immutable outcome task"
    Inspector->>Inspector: "marshal to Avalonia dispatcher"
    Inspector->>Workspace: "publish completion"
    Workspace->>Session: "read current PresentedState"
    Workspace->>Projection: "project only session-accepted state"
    Pointer->>Inspector: "release"
    Inspector->>Workspace: "await CommitLatestAsync"
    Workspace->>Session: "commit exact newest revision"
    Workspace->>Projection: "adopt exact prepared state; no compile"
```

The awaited outcome cannot nominate UI geometry. On the dispatcher,
`EditorWorkspace.PublishStraightLengthOutcome` verifies the active transaction and
newest provisional revision, then re-reads `TrackAuthoringSession.PresentedState`.
Only that state reaches `EngineeringSnapshotBuilder`, the renderer-neutral
`TrackViewportSnapshot`, Math Plots, Route, Diagnostics, and Inspector status.

## Gesture lifecycle and exact commit

The session captures the committed graph at press. Every operation applies to that
same captured graph. The serialized coordinator permits one running and one latest
pending evaluation; an intermediate pending revision is coalesced.

Release is asynchronous. `CommitLatestAsync` freezes the newest reserved revision,
bypasses remaining throttle delay for that revision, and awaits it without blocking
the UI thread. A successful changed commit installs the exact candidate graph,
compile result, and canonical package JSON into the document. No evaluation or graph
compilation is repeated.

The existing generic editor undo service receives one logical prepared-state entry
for the gesture so it remains ordered with unchanged one-shot commands. The session
also retains its M167.2 transaction history until a later generic document-history
transition rehydrates the session. This narrow adapter avoids a broad command-stack
migration in M167.4.

| End condition | Compilation at end | Editor undo entries |
|---|---:|---:|
| changed valid release | 0 | 1 |
| no-op release | 0 | 0 |
| invalid newest release | 0 | 0 |
| Escape/capture loss/lifecycle cancel | 0 | 0 |

Undo and redo restore the retained prepared states without compilation. Save reads
only `TrackEditorDocument` committed content; Save and Save As are disabled during a
gesture, and the workspace rejects a direct save attempt while one is active.

## Invalid provisional values

A non-positive or non-finite raw length is deliberately held by a candidate
operation until evaluation. This allows the coordinator/session to reserve a newer
provisional revision before immutable section construction rejects the value. That
new revision structurally invalidates older work.

The field and scrubber receive red invalid styling. The Inspector and Diagnostics
pane show structured graph/application diagnostics and the status:

> Last valid preview — current value is invalid

The viewport may retain the session's last valid presented state. Committed graph,
canonical persistence content, dirty state, and history do not change. Releasing an
invalid newest revision fails exact commit, cancels the transaction, restores the
committed value in the field, and republishes committed presentation.

## Inspector refresh and pointer capture

The old refresh path cleared and recreated every Inspector child after each
`WorkspaceChanged`. While the same straight-length transaction and selection remain
active, `InspectorPaneControl.Refresh` now updates the existing numeric field,
scrubber styling, and status text in place. It does not replace the captured control,
change focus, or submit values recursively. Normal rebuild behavior resumes after
commit/cancel or a selection/document change.

## Camera, station, and selection preservation

Ordinary `TrackViewportControl.Snapshot` replacement no longer sets `fitPending`.
It preserves:

- projection mode;
- projected world center;
- pan;
- scale/zoom.

Fit remains available for the first non-empty presentation of a document, the first
non-empty track after an empty presentation, explicit **Fit**, and the pre-existing
projection-mode reset.

Before a new engineering projection is built, the workspace retains the prior
station. It clamps that station to the new total length, finds the nearest canonical
sample, and updates the sample/section association. It no longer resets the cursor
to sample zero.

Section selection remains keyed by stable node ID. After projection, the workspace
finds that node in the new ordered route and replaces only its derived route index.
The M167.4 length operation cannot delete its target.

## Command-conflict policy

The first implementation disables source-mutating UI commands during the active
transaction:

- Save and Save As;
- Undo and Redo;
- Route Add, Insert Before/After, Delete, Move Up/Down;
- Route node selection;
- New and Open.

The workspace also rejects direct one-shot edits, selection changes, Save, Undo, and
Redo while the transaction is active. New/Open/document replacement/close/shutdown
retain defensive cancellation even though their normal UI entry points are disabled.
Viewport Fit, camera navigation, overlays, and non-source presentation remain usable.

## Execution mode and scheduler correction

The document coordinator opts into
`AuthoringEvaluationExecutionMode.SerializedBackground` with the M167.3 30 Hz
preset. Synchronous mode remains the coordinator's global default.

Real headless pointer testing exposed one M167.3 throttle edge case: after a delay
won `Task.WhenAny`, an abandoned semaphore wait could consume a later work signal
and strand the newest pending revision. The coordinator now cancels the losing
delay/signal wait. A production regression test verifies that a throttled pending
revision starts without another submission.

## Compile counts and representative metrics

One valid, non-empty started evaluation invokes the graph compiler once. A raw value
rejected while constructing its immutable straight definition invokes it zero times.
Successful commit, no-op/invalid end handling, cancel, undo, redo, and save invoke it
zero times.

The following Debug/.NET 8 results were recorded on 2026-07-22 using self-authored
all-straight routes. Each interaction submitted 60 raw absolute updates in six
10-update bursts through the 30 Hz serialized coordinator. They are observations,
not product gates.

| Route | Sections | Raw / submitted | Coalesced | Started / compiler | Accepted previews | Stale | Mean submit-to-present | Final commit wait | Total interaction |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| small | 1 | 60 / 60 | 54 | 6 / 6 | 5 | 0 | 34.674 ms | 0.557 ms | 176.673 ms |
| realistic | 40 | 60 / 60 | 54 | 6 / 6 | 5 | 0 | 32.968 ms | 3.821 ms | 205.563 ms |
| long | 160 | 60 / 60 | 53 | 7 / 7 | 5 | 1 | 31.662 ms | 16.863 ms | 373.311 ms |

Interaction metrics combine the existing coordinator/pipeline instrumentation with
frontend raw-pointer and accepted-presentation counters. They include raw pointer
updates, submitted/coalesced/started work, accepted previews, stale completions,
submit-to-present timing, exact final wait, and compiler invocations.

## Verification

Focused tests cover:

- active-document session/coordinator lifecycle and replacement cancellation;
- committed/presented separation, exact adoption, zero-compile commit, no-op,
  invalid, cancel, Save exclusion, undo/redo, and save/reopen;
- real headless press/move/release, absolute base derivation, Shift fine mode,
  Escape, pointer-capture loss, invalid styling/status, latest-wins repaint,
  Inspector capture survival, typed Apply, and mutation disabling;
- camera snapshot preservation, explicit/initial Fit behavior, station clamping and
  nearest-sample remapping, and stable-node route-index remapping; and
- small/realistic/long interaction metrics without hard latency gates.

## Limitations

- Only straight-section Length has a scrub affordance.
- Sensitivity is fixed rather than user-configurable.
- The compiler is still whole-route and non-cooperatively cancellable once entered.
- A document has one serialized coordinator, but there is no global scheduler across
  documents/workspaces.
- The narrow generic undo adapter is transitional until more editor commands adopt
  session-owned history.
- Metrics are local Debug measurements, not cross-platform budgets.
- No station timeline, viewport handles, banking-key editing, spatial control-point
  editing, force authoring, or train playback is included.

## Recommended M167.5 entry point

M167.5 should harden this one proven interaction before adding more live fields:

1. run repeatable Release-build metrics on representative spatial and mixed-geometry
   documents across Windows, macOS, and Linux;
2. add lifecycle stress coverage for document replacement and shutdown while the
   final compile is non-cooperatively running;
3. consolidate the transitional generic editor/session history adapter so one
   session-owned stack can remain authoritative across mixed one-shot/live commands;
4. expose percentile submit-to-present and final-wait measurements instead of only
   cumulative/mean timings;
5. verify accessible keyboard alternatives and configurable scrub sensitivity; and
6. decide, from measured data, whether the 30 Hz interval needs per-document tuning.

Do not begin timeline, viewport-handle, arbitrary numeric, banking, force, or spatial
editing work until this lifecycle/history/measurement hardening is complete.
