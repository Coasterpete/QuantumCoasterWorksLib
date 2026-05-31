# Diagnostic Track Fixtures

Milestone 42 adds a small backend-only fixture library in
`Quantum.Tests/Diagnostics/DiagnosticTrackFixtures.cs`.

The fixtures are self-authored deterministic `TrackDocument` cases for frame,
distance, and curvature diagnostics. They are intentionally test assets only:
they do not define new production frame behavior, transported-frame sampling, or
runtime banking-profile behavior.

## Fixture Set

- `straight-horizontal`: zero-curvature horizontal line using `LineCurve`.
- `near-vertical-tangent-sequence`: near-vertical straight line using
  `LineCurve`, intended to exercise reference-up fallback regions.
- `crest-hill`: simple cubic Bezier hill with a climb, crest, and descent.
- `constant-radius-turn`: horizontal constant-radius turn with exact test
  curvature.
- `simple-banked-turn`: horizontal constant-radius turn with constant segment
  roll applied through existing `TrackSegment.RollRadians` behavior.
- `quarter-loop-like`: vertical quarter-arc built through
  `GeometricSectionTrackDocumentBuilder`.

## What The Tests Validate

`DiagnosticTrackFixtureTests` verifies that each fixture:

- exposes finite, monotonic station-distance samples
- evaluates finite, orthonormal `TrackFrame` values through `TrackEvaluator`
- produces finite curvature diagnostics through `TrackPhysicsAdapter`
- produces finite radius diagnostics for non-zero-curvature probes
- can be passed through `TrackFrameSmoothnessDiagnostics` and
  `TrackFrameContinuityDiagnostics` without changing those production contracts

`TransportedFrameComparisonDiagnosticsTests` uses the same fixture distances to
compare existing stateless `TrackEvaluator` frame sampling against
`TransportedTrackFrameSampler`. The comparison diagnostic is backend-only and
records per-station tangent, normal, binormal, frame, roll/twist, and matrix
orientation deltas. It also carries the existing smoothness and continuity
reports for both frame sets so tests can check whether transported sampling
reduces unintended frame jumps without changing scalar evaluator behavior.

Straight fixtures intentionally have zero curvature, so their mathematical radius
is not finite. Curved fixtures provide non-zero curvature probes with finite
radius diagnostics.

## Non-Goals

These fixtures do not:

- implement transported frame sampling
- implement `BankingProfile` runtime behavior
- change `TrackFrame`
- change `DebugViewportSnapshotV1`
- change `TrainPoseExportV1`
- add dependencies
- add Unity, browser, frontend, or renderer code
