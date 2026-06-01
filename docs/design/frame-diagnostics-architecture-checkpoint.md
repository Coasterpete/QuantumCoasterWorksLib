# Milestone 47: Frame Diagnostics Architecture Checkpoint

Status: docs-only architecture checkpoint. This document summarizes the frame
diagnostics foundation established across Milestones 39-46 before starting
`BankingProfile` runtime behavior.

This checkpoint does not implement runtime behavior, change production
contracts, add dependencies, or add Unity/browser/frontend code.

## Context

Milestone 39 established the external coaster simulation reference audit. The
main lesson from that audit was not to copy external implementation details, but
to keep Quantum centered on distance-based centerline evaluation, stable frame
generation, matrix/export boundaries, and mature spline/math adapters where they
make sense.

Milestone 40 captured the proposed `BankingProfile` design: banking should be a
coaster-domain roll-angle profile sampled by station distance, with
`TrackSegment.RollRadians` remaining the compatibility shorthand until a profile
exists.

Milestone 41 captured the transported frame sampler design: base-frame transport
should be explicit, ordered, deterministic, and separated from authored
banking. It also stated that scalar `TrackEvaluator` behavior should remain
context-free.

Milestones 42-46 turned that design context into diagnostic infrastructure:
self-authored fixtures, an explicit transported sampler, stateless-vs-transported
comparison diagnostics, a backend-only JSON export, and a self-contained local
browser viewer.

## What Now Exists

### Diagnostic fixtures

`Quantum.Debug/DiagnosticTrackFixtures.cs` now provides a small backend-only set
of self-authored `TrackDocument` fixtures:

- `straight-horizontal`
- `near-vertical-tangent-sequence`
- `crest-hill`
- `constant-radius-turn`
- `simple-banked-turn`
- `quarter-loop-like`

The fixtures include monotonic station-distance samples and curvature probe
distances. Tests use them to verify finite track evaluation, finite curvature
and radius diagnostics where applicable, and compatibility with
`TrackFrameSmoothnessDiagnostics` and `TrackFrameContinuityDiagnostics`.

These fixtures are diagnostic assets. They do not define new production frame
behavior or banking behavior.

### Transported frame sampler

`TransportedTrackFrameSampler` now exists as an explicit station-distance
sampler. It:

- requires finite distances in non-decreasing station order
- preserves duplicate sample distances and output order
- seeds the first frame from current evaluator behavior
- transports the unrolled base normal across the ordered sample sequence
- preserves evaluator position, tangent, and distance results
- applies existing segment `RollRadians` only after base-frame transport
- returns the existing `Quantum.Track.TrackFrame` contract

The sampler is not hidden inside `TrackEvaluator`. It is opt-in and sequence
based, which keeps transported behavior reviewable and prevents scalar frame
evaluation from becoming history-dependent.

### Stateless-vs-transported comparison diagnostics

`TransportedFrameComparisonDiagnostics` compares the current stateless evaluator
frame sequence with the transported sampler over the same distances. The report
captures:

- per-sample tangent, normal, binormal, frame, roll/twist, and matrix orientation
  deltas
- summary metric maxima and averages
- smoothness reports for both frame sets
- continuity reports for both frame sets

The comparison diagnostic is intentionally measurement-oriented. It does not
choose a production frame policy; it makes the behavioral difference visible so
future roll and banking work can be evaluated against a stable baseline.

### JSON export

`Quantum.IO.TransportedFrameComparison.V1` defines a backend-only versioned JSON
contract:

```text
quantum.transported_frame_comparison_diagnostics
```

The `transported-frame-comparison` debug command writes deterministic JSON for
all diagnostic fixtures:

```powershell
dotnet run --project Quantum.Debug -- transported-frame-comparison artifacts/frame-comparison/transported-frame-comparison.sample.json
```

The export includes fixture metadata, per-fixture samples, summary metrics,
smoothness metrics, continuity metrics, and continuity diagnostic text. It is a
separate diagnostics artifact, not a revision of `DebugViewportSnapshotV1` or
`TrainPoseExportV1`.

### Browser viewer

The `transported-frame-comparison-browser` debug command turns the JSON artifact
into a self-contained local HTML viewer:

```powershell
dotnet run --project Quantum.Debug -- transported-frame-comparison-browser artifacts/frame-comparison/transported-frame-comparison.sample.json artifacts/frame-comparison/transported-frame-comparison.browser.html
```

The viewer is a static diagnostic artifact that embeds the JSON payload and uses
plain HTML/SVG/JavaScript to inspect summary metrics, per-sample deltas, and
severity indicators. It does not add frontend dependencies, runtime browser
dependencies, Unity code, or a production visualization contract.

## What Remains Unchanged

### `TrackEvaluator` scalar behavior

`TrackEvaluator.EvaluateFrameAtDistance` remains deterministic and
context-free. A scalar call still evaluates one station distance independently,
without hidden frame history or cached traversal state.

`TrackEvaluator.EvaluateFramesAtDistances` also remains scalar-equivalent. The
current batch parity tests continue to verify that batch outputs match repeated
scalar evaluation for the same distances.

### `TrackFrame` contract

`TrackFrame` remains the public coaster-domain frame contract:

- station distance
- position
- tangent
- normal
- binormal
- right-handed basis convention
- canonical matrix conversion

No extra banking fields, transport metadata, diagnostic fields, or renderer
fields have been added to `TrackFrame`.

### `DebugViewportSnapshotV1`

`DebugViewportSnapshotV1` remains unchanged. The transported frame comparison
export is separate from the debug viewport snapshot contract, so existing
centerline, frame, debug line, box, and optional nested train-pose snapshots keep
their current shape.

### `TrainPoseExportV1`

`TrainPoseExportV1` remains unchanged. Train pose export still serializes the
current train placement results and matrices. The transported diagnostics work
does not change train pose DTOs, validation, or regression snapshots.

### `BankingProfile` runtime behavior

No `BankingProfile` runtime behavior exists yet. The current runtime roll source
is still `TrackSegment.RollRadians`, and that value still represents constant
segment roll in the existing evaluator path.

## Why This Is A Safe Foundation For `BankingProfile`

The foundation is safe because it separates the two concerns that banking will
otherwise blur:

1. Base-frame stability along the centerline.
2. Authored roll angle around the sampled tangent.

The transported sampler proves that Quantum can evaluate a smoother unrolled
base frame over an ordered distance sequence without changing scalar
`TrackEvaluator` behavior. The comparison diagnostics prove that stateless and
transported frames can be measured side by side over the same fixtures.

That gives the next milestone a clear place to start: implement
`BankingProfile` as a roll-angle scalar sampled by distance, then apply that
roll after a base frame is available. The existing fixtures and comparison
reports can tell whether a BankingProfile change affected tangent, base-frame
continuity, roll/twist, matrix orientation, or export-facing behavior.

The architecture also keeps debug tooling out of the production contract path.
JSON and browser diagnostics can grow as needed while `TrackFrame`,
`DebugViewportSnapshotV1`, and `TrainPoseExportV1` remain stable until a
deliberate contract revision is justified.

## Risks Before Implementing `BankingProfile`

### Sign conventions

The backend convention must stay explicit:

- tangent points forward along station distance
- normal is the track up axis
- binormal is the lateral/right axis
- `Binormal = Tangent x Normal`
- positive roll is right-hand positive rotation around tangent

Milestone 48 should include tests that prove positive roll rotates the normal
toward positive binormal for a simple +X tangent fixture. Without that anchor,
profile interpolation can look correct numerically while being inverted for
train and force consumers.

### Roll source precedence

The design intent is:

- if no profile exists, preserve current `TrackSegment.RollRadians` behavior
- if an explicit profile exists in a new opt-in path, it should be the
  authoritative geometric roll source
- `ForceSection` roll-rate channels should remain force/diagnostic targets, not
  implicit frame geometry drivers

Milestone 48 should make precedence visible in API names and tests. Silent
fallback or mixed roll sources would make banking hard to debug.

### Segment `RollRadians` compatibility

Existing documents and tests depend on segment roll. A constant
`BankingProfile` should be able to match `TrackSegment.RollRadians` behavior for
compatibility fixtures before any richer interpolation is trusted.

Compatibility should be tested before adding smooth interpolation, because it
protects current train pose and debug export behavior from accidental roll
changes.

### Interpolation mode validation

The design note proposes constant, linear, and smooth/smoothstep interpolation.
The runtime prototype should validate:

- finite key distances
- finite roll values
- strictly increasing key distances
- valid interpolation modes
- deterministic boundary behavior
- unwrapped radians rather than automatic angle wrapping

Validation should reject ambiguous authoring input early rather than repairing
it silently.

### Fixture coverage gaps

The current fixture set is good enough for frame diagnostics, but
`BankingProfile` will need additional self-authored coverage:

- multi-key roll ramps on a straight track
- roll ramps through a constant-radius turn
- unwrapped roll values greater than `2*pi`
- profile endpoints that clamp or intentionally do not cover the full track
- comparisons between segment-roll compatibility and explicit profile output
- eventually, closed-loop or station-wrapping fixtures once those semantics are
  defined

Closed-loop residual twist is still a separate future contract. It should not be
accidentally solved as part of the first banking runtime prototype.

## Recommended Milestone 48 Plan

1. Add a minimal backend `BankingProfile` domain model.

   Keep it coaster-specific: ordered distance keys, roll radians, and an
   interpolation mode for the interval after each key. Do not add renderer,
   Unity, browser, or general-purpose spline dependencies.

2. Add profile validation and scalar sampling tests.

   Cover null/empty policy, finite values, strictly increasing distances,
   boundary sampling, constant interpolation, linear interpolation, smoothstep
   interpolation, and unwrapped radians.

3. Add segment-roll compatibility helpers or tests.

   Prove that a profile generated from current segment roll values can match the
   existing constant-roll behavior for simple fixtures.

4. Keep runtime integration opt-in for the first prototype.

   Do not change `TrackEvaluator.EvaluateFrameAtDistance` or
   `EvaluateFramesAtDistances` by default. Prefer an explicit profile-aware
   helper or sampler so behavior changes are reviewable.

5. Apply banking after base-frame construction.

   Preserve the order established by the design notes:

   ```text
   distance -> centerline position/tangent -> unrolled base frame -> roll sample -> TrackFrame
   ```

6. Add focused frame tests before touching exports.

   Verify tangent preservation, orthonormality, roll sign, constant-roll parity,
   and expected interpolated normal/binormal values. Export contract changes
   should remain out of scope unless a later milestone explicitly asks for them.

7. Re-run transported comparison diagnostics after the prototype.

   Use the existing fixtures and add banking-specific fixtures only as needed.
   The goal is to see whether profile roll changes are intentional and isolated,
   not to make the browser viewer or JSON contract define runtime behavior.

## Non-Goals

- No production `BankingProfile` runtime behavior in Milestone 47.
- No changes to `TrackEvaluator`, `TrackFrame`, `DebugViewportSnapshotV1`, or
  `TrainPoseExportV1`.
- No new package dependencies.
- No Unity, Unreal, browser, or frontend code.
- No project architecture redesign.
- No imported external coaster assets or copied external implementation code.
