# Milestone 41: Transported Frame Sampler Design Note

Date: 2026-05-31

Status: design note only. This document proposes future coaster-domain behavior
for transported frame sampling before `BankingProfile` runtime work begins. It
does not change runtime evaluation, `TrackFrame`, `DebugViewportSnapshotV1`,
`TrainPoseExportV1`, or dependencies.

## Context

Quantum's current public coaster-domain sampling lane is:

```text
TrackDocument -> TrackEvaluator -> TrackFrame
```

`TrackEvaluator.EvaluateFrameAtDistance` and
`TrackEvaluator.EvaluateFramesAtDistances` already return finite,
orthonormalized `TrackFrame` values for station-distance sampling. The current
frame construction is intentionally simple and deterministic: evaluate the
centerline position and tangent, choose a reference-up axis, build normal and
binormal from cross products, then apply the segment roll around the tangent.

That behavior is good enough for the current prototype, but it is stateless.
Each frame is built independently from the sampled tangent. A future transported
frame sampler should make the unrolled base frame continuous along the
centerline before any banking or roll profile is applied.

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

## Proposed Transported Sampling Concept

A future transported sampler should operate over an ordered station-distance
sequence. The basic shape is:

1. Validate finite input distances and resolve the document length policy.
2. Evaluate centerline positions and tangents at each requested distance.
3. Seed the first unrolled normal from a deterministic source.
4. Move from sample to sample, carrying the previous unrolled normal forward.
5. Project or minimally rotate that previous normal onto the current tangent's
   perpendicular plane.
6. Rebuild `Binormal = Tangent x Normal` and re-orthonormalize.
7. Align signs against the previous frame to avoid accidental 180 degree flips.
8. Apply roll/banking around the current tangent after base-frame transport.
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

- Use the current stateless `TrackEvaluator` normal at the first distance, then
  project it onto the first tangent plane.
- Use an explicitly supplied seed normal from a caller or document-level
  authoring setting.
- Use a deterministic fallback axis least aligned with the first tangent.

For early runtime work, the safest default is probably to seed from the current
evaluator so existing unbanked starting orientation remains familiar. The
sampler should still have a fallback for degenerate or non-finite seed data.

## Relationship To `TrackEvaluator.EvaluateFrameAtDistance`

`EvaluateFrameAtDistance(double)` is scalar. It has no previous sample, no next
sample, and no caller-provided traversal direction. That makes true transported
frame behavior ambiguous.

Recommended policy:

- Keep scalar evaluation unchanged until a deliberate behavior-changing
  milestone.
- Treat scalar evaluation as the stable compatibility path for consumers that
  need one independent frame.
- If a transported result is needed for a single visual marker, sample it
  through an explicit transported sampler using a deterministic sequence and
  then select the desired frame.
- Do not make scalar evaluation secretly depend on hidden cached history,
  because that would make results order-dependent and difficult to test.

Longer term, `TrackEvaluator` may expose an explicit transported-frame entry
point, but the scalar method should remain deterministic and context-free unless
its contract is intentionally revised.

## Relationship To `TrackEvaluator.EvaluateFramesAtDistances`

Batch frame sampling is the natural home for transported behavior because the
ordered distance list provides traversal context. However, the current batch API
is tested as scalar-equivalent: each returned frame should match evaluating that
same distance independently.

Recommended migration path:

- Add a future explicit API or helper for transported batch sampling rather than
  changing `EvaluateFramesAtDistances` silently.
- Require distances to be non-decreasing, or define an internal sort/restore
  policy, before transport is enabled.
- Preserve current `EvaluateFramesAtDistances` semantics until tests and
  downstream consumers are intentionally migrated.
- If transported behavior eventually becomes the default batch policy, update
  scalar parity tests and document the ordering requirement clearly.

An explicit name such as `EvaluateTransportedFramesAtDistances` or a
`TransportedTrackFrameSampler` would make the behavior visible and reviewable.

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

When runtime behavior is added, tests should cover:

- stateless scalar evaluation remains deterministic and context-free
- transported batch sampling rejects or clearly handles unsorted distances
- all transported frames are finite and orthonormal
- tangent output matches the underlying centerline tangent samples
- straight horizontal track preserves the expected starting frame
- vertical or near-vertical tangent sequences avoid reference-axis flip spikes
- repeated sampling of the same document and distances returns identical frames
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
