# Milestone 41: Transported Frame Sampler Design Note

Date: 2026-05-31

Status: historical design note, implemented for canonical runtime sampling by
Milestone 138 PR 3. `TrackEvaluator` scalar and batch frame APIs now sample from
the same deterministic transported-frame history. Requested finite station
distances are resolved against measured geometric arc length, clamped to track
extents, and stored in `Quantum.Track.TrackFrame.Distance`. Roll is applied after
base-frame transport. `TransportedTrackFrameSampler` remains only as an obsolete
compatibility facade.

Milestone 151 refresh: `TrackSamplingOptions` is the source of truth for
compiled arc-length and transported-frame sample density. The default
`CompiledTrackRuntime` constructor uses `TrackSamplingOptions.Default`; custom
runtime options may change transported-frame node density while preserving
centerline position, tangent, and station-distance semantics. Query-list density
and ordering do not define frame history for `TrackEvaluator`; the compiled
runtime transport history does.

The rest of this note is retained as historical rationale. Statements below
about "future" transported behavior or "current" stateless evaluator behavior
describe the pre-Milestone 138 state, not the current runtime contract. See
`docs/public-coaster-api-boundary.md` for the current public behavior summary.

## Historical Context

Quantum's public coaster-domain sampling lane was, and remains:

```text
TrackDocument -> TrackEvaluator -> TrackFrame
```

Before canonical runtime transport, `TrackEvaluator.EvaluateFrameAtDistance`
and `TrackEvaluator.EvaluateFramesAtDistances` returned finite,
orthonormalized `TrackFrame` values by evaluating the centerline position and
tangent, choosing a reference-up axis, building normal/binormal from cross
products, then applying segment roll around the tangent.

That older behavior was deterministic but stateless: each frame was built
independently from the sampled tangent. The implemented runtime now compiles a
transported unrolled base-frame history and samples scalar and batch frames from
that same history before applying segment roll or an explicit `BankingProfile`.

## Problem With Stateless Reference-Up Frames

A stateless global-up frame usually works on shallow track because projecting a
stable world-up direction onto the plane perpendicular to the tangent gives a
reasonable track normal. It becomes fragile when the centerline tangent points
near that up direction.

Near steep, vertical, or inverted tangents:

- the projected global-up vector can become very small
- small tangent changes can cause large normal/binormal changes
- fallback axis selection can switch between neighboring samples
- two nearby samples can choose different base-frame orientations even though
  the centerline itself is smooth
- segment roll then rotates an already discontinuous base frame, making twist
  spikes harder to separate from intentional banking

The current `TrackEvaluator` avoids invalid zero-length axes by selecting a
fallback reference axis and orthonormalizing the result. That protects frame
validity, but it does not guarantee visual or diagnostic continuity through
vertical tangent regions.

## Implemented Transported Sampling Shape

The implemented canonical sampler operates over a compiled station-distance
node sequence, not over the caller's requested query sequence. The basic shape
is:

1. Validate finite input distances and resolve the document length policy.
2. Compile transport nodes from the measured segment lengths using
   `TrackSamplingOptions.TransportSamplesPerSegment`.
3. Seed the first unrolled normal from authored start-pose data when present,
   otherwise from a deterministic fallback.
4. Move across compiled nodes, carrying the previous unrolled normal forward.
5. Transport that previous normal onto the current tangent's perpendicular
   plane with the shared rotation-minimizing transport helper.
6. For a requested station distance, use the preceding compiled node and
   transport from that node to the exact sampled tangent.
7. Rebuild `Binormal = Tangent x Normal` and re-orthonormalize.
8. Apply segment roll or profile banking around the current tangent after
   base-frame transport.
9. Return the existing `TrackFrame` contract.

This is parallel-transport-style sampling: the frame changes as little as
possible while the tangent follows the centerline. It separates centerline
orientation continuity from authored banking.

The first production design does not need to become a general-purpose geometry
library. A small coaster-specific sampler can use existing vector operations and
clear fallback rules. If later work needs quaternion interpolation, exact
rotation minimization, or higher-order numerical behavior, evaluate mature math
or geometry libraries before growing broad custom math code.

## Seeding Policy

The first frame needs a deterministic unrolled normal. Candidate policies:

- Historical candidate: seed from the then-current stateless evaluator normal
  at the first distance, then project it onto the first tangent plane.
- Use an explicitly supplied seed normal from a caller or document-level
  authoring setting.
- Use a deterministic fallback axis least aligned with the first tangent.

Historical candidate policies included seeding from the then-current stateless
evaluator. The implemented runtime seeds from authored start-pose normal data
when present and otherwise uses deterministic fallback-axis projection rules.
The sampler still has fallback behavior for degenerate or non-finite seed data.

## Relationship To `TrackEvaluator.EvaluateFrameAtDistance`

Superseded guidance: the original note recommended keeping scalar evaluation
stateless until a deliberate migration. That migration has happened. Scalar
evaluation is still deterministic and context-free from the caller's point of
view, but it samples the compiled canonical transported-frame history rather
than rebuilding an independent reference-up frame.

## Relationship To `TrackEvaluator.EvaluateFramesAtDistances`

Superseded guidance: the original note described batch sampling as a possible
future home for transported behavior. Current batch evaluation samples the same
compiled transported history as scalar evaluation, preserves caller order,
allows duplicate and unordered finite distances, and remains scalar-equivalent
at each station. `TransportedTrackFrameSampler` now forwards to canonical
evaluation and remains only for obsolete compatibility coverage.

## Relationship To Frame Diagnostics

`TrackFrameSmoothnessDiagnostics` and `TrackFrameContinuityDiagnostics` should
remain measurement tools, not frame-generation policies.

They are well suited for validating future sampler work:

- compare stateless and transported frame sets over the same distances
- measure tangent, normal, binormal, frame-angle, and twist deltas
- detect roll discontinuities separately from tangent changes
- flag matrix-orientation jumps that would show up in exported transforms
- support regression fixtures for vertical, banked, and boundary-heavy track
  shapes

The transported sampler should aim to reduce unintended normal/binormal and
twist spikes while preserving the tangent path. Diagnostics should make that
visible without requiring a new debug export contract.

## BankingProfile Ordering

`BankingProfile` should apply after base-frame transport.

Future frame generation should be ordered like this:

1. Resolve station distance.
2. Evaluate centerline position and tangent.
3. Build the unrolled base frame by transport.
4. Sample `BankingProfile` or compatibility `TrackSegment.RollRadians`.
5. Rotate base `Normal` and `Binormal` around `Tangent`.
6. Re-orthonormalize and return `TrackFrame`.

This keeps two concerns independent:

- transported base frames define how the track's unbanked orientation follows
  the centerline
- banking defines intentional roll around that tangent

Roll should not be used to repair base-frame discontinuity. If the unrolled
transported frame is smooth, then roll diagnostics can focus on authored banking
changes instead of reference-up artifacts.

## Open Track Behavior

For open tracks, transport is one-directional:

- clamp or reject distances according to the same station-distance policy chosen
  for the API
- seed the first requested sample deterministically
- transport forward through the requested distance sequence
- do not try to make the final frame match any implicit start frame

Open-track behavior should be stable for debug sampling, train placement, and
export snapshots where there is no loop closure requirement.

## Closed-Loop Behavior

Closed loops need an explicit future contract. A transported frame carried all
the way around a loop can return to the starting tangent with a residual twist
relative to the starting normal. That residual is a geometric effect of the
path, not necessarily a bug.

Possible loop policies:

- Preserve raw transport and allow a seam twist at the closure.
- Distribute the residual twist gradually around the loop so the first and last
  frames meet.
- Let an authored seed or banking profile define where the residual is absorbed.

Closed-loop behavior should not be inferred from current distance clamping.
Before production loop support, Quantum needs explicit document semantics for
closed centerlines, wrapping station distances, and train placement across the
wrap boundary.

## Deterministic Tests Needed Later

The runtime behavior now exists. Tests should continue to cover:

- scalar and batch evaluation remain deterministic and station-equivalent
- batch sampling clearly handles unsorted and duplicate distances
- all transported frames are finite and orthonormal
- tangent output matches the underlying centerline tangent samples
- straight horizontal track preserves the expected starting frame
- vertical or near-vertical tangent sequences avoid reference-axis flip spikes
- repeated sampling of the same document and distances returns identical frames
- custom transport sample densities preserve centerline samples and converge
  orientation toward denser transported-frame histories
- open-track endpoint behavior is stable
- closed-loop residual twist policy is deterministic once loop semantics exist
- zero banking leaves transported base frames unrolled
- constant banking rotates normal/binormal without changing tangent
- smooth `BankingProfile` roll changes produce expected twist diagnostics
- `TrackFrameSmoothnessDiagnostics` and
  `TrackFrameContinuityDiagnostics` report lower unintended frame jumps against
  selected stateless baseline fixtures

Use self-authored fixtures only. Good fixtures include a vertical line, a
quarter-loop-like curve, a crest with tangent near global up, a constant-radius
banked turn, and a simple closed loop once closed-track semantics are defined.

## Browser Debug Viewer Diagnostics

The browser debug viewer can compare stateless and transported frames as a thin
diagnostic layer over backend samples. Useful future views include:

- side-by-side frame axes for stateless versus transported samples
- color-coded twist delta along station distance
- normal/binormal flip markers near steep tangents
- plots of tangent, normal, binormal, roll, and matrix-orientation deltas
- threshold markers from `TrackFrameContinuityDiagnostics`
- a toggle that shows the unrolled transported base frame separately from the
  final banked frame
- a table of worst intervals with distance, angle delta, and issue kind
- optional train boxes sampled from both frame policies to reveal visible car
  orientation jitter

These diagnostics should not change `DebugViewportSnapshotV1`. The viewer can
derive comparisons from two sampled frame sets, or a future debug contract
version can carry explicit diagnostic samples after the backend sampler exists.

## Non-Goals

- No runtime behavior in this milestone.
- No changes to `TrackFrame`.
- No changes to `DebugViewportSnapshotV1`.
- No changes to `TrainPoseExportV1`.
- No Unity, Unreal, browser, renderer, or editor dependency.
- No new math, spline, interpolation, or geometry dependency.
- No rewrite of `TrackEvaluator` or the train placement pipeline.
