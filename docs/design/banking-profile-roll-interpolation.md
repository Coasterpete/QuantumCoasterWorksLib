# Milestone 40: BankingProfile and Roll Interpolation Design Note

Date: 2026-05-31

Status: design note plus Milestone 48 backend prototype. This document proposes
coaster-domain behavior for banking and roll interpolation. Milestone 48 adds
an opt-in backend-only `BankingProfile` model and sampler; it does not change
default `TrackEvaluator` behavior, `TrackFrame`, `DebugViewportSnapshotV1`,
`TrainPoseExportV1`, or dependencies.

## Context

`docs/research/external-coaster-sim-reference-audit.md` identifies roll/banking
as a gap in the current backend. Quantum already treats roll as rotation around
the sampled tangent, and current debug tooling can blend segment roll for
visual diagnostics. What is missing is a first-class coaster-domain policy for
how roll angle changes over station distance.

The future `BankingProfile` should be a track-domain roll-angle profile. It
should not be a generic math spline system and should not become a renderer
contract. Its job is to answer one stable question:

> At station distance `s`, what roll angle should be applied around the
> centerline tangent?

## Milestone 48 Prototype Update

Milestone 48 introduces the first backend-only runtime prototype:

- `BankingProfile`: validated ordered distance keys with roll radians and
  interval interpolation modes.
- `BankingProfileSampler`: scalar roll sampling for constant, linear, and
  smoothstep intervals.
- An explicit profile-aware frame sampling path that reuses transported base
  frames and applies profile roll only when callers opt in.

This keeps authored banking separate from the default evaluator path. Existing
segment `RollRadians` behavior remains the compatibility baseline unless a
caller explicitly chooses the BankingProfile sampler.

## Milestone 49 Diagnostics Update

Milestone 49 adds backend-only BankingProfile sampling diagnostics and a
versioned JSON export:

```text
quantum.banking_profile_diagnostics
```

The `banking-profile-diagnostics` debug command writes a deterministic sample
artifact under `artifacts/banking-profile/`. Each sample reports station
distance, roll radians, roll degrees, interpolation mode, source key interval,
and approximate roll slope in radians per meter when the neighboring station
distances make that practical. The summary reports sample count, min/max roll,
and maximum absolute roll slope.

This is inspection infrastructure only. It does not change default
`TrackEvaluator` behavior, `TrackFrame`, `DebugViewportSnapshotV1`, or
`TrainPoseExportV1`.

## Current Baseline

Today `TrackSegment.RollRadians` is the only authored roll angle on
`TrackSegment`. `TrackEvaluator` samples a segment, builds a frame from the
centerline tangent and a reference-up axis, then applies the segment roll as a
constant rotation around that tangent.

`DebugTrackContinuousSampler` has a private debug-only roll profile that reads
segment `RollRadians` values and smooths them near segment boundaries when a
roll blend distance is requested. That is useful for diagnostics, but it is not
a public authoring model and should not define production banking semantics.

## Relationship To `TrackSegment.RollRadians`

`TrackSegment.RollRadians` should remain the compatibility shorthand for now:

- A segment with `RollRadians = 0.0` is unbanked unless a future explicit
  banking profile says otherwise.
- A segment with non-zero `RollRadians` means constant roll across that segment
  in the current evaluator.
- A future `BankingProfile` can be initialized from segment rolls by creating
  distance-domain keys at segment boundaries.
- When a future profile exists on a document, it should be the authoritative
  roll source for frame generation in opt-in or migrated paths.
- If no profile exists, current segment roll behavior should continue unchanged.

This keeps existing documents and tests stable while giving future authoring a
better place to express ramps, holds, and smooth transitions.

## Proposed Domain Model

A minimal future profile could be shaped like this conceptually:

- `BankingProfile`: ordered roll keys over station distance.
- `BankingKey.Distance`: station distance `s` in meters.
- `BankingKey.RollRadians`: unwrapped roll angle in radians.
- `BankingKey.InterpolationToNext`: interpolation policy for the interval after
  this key.

The keys should be sorted by distance and sampled in the track station domain.
For a key interval `[s0, s1]`, normalized interval position is:

```text
t = (s - s0) / (s1 - s0)
```

The first implementation should prefer explicit validation over clever repair:
finite distances, finite roll values, strictly increasing key distances, and
at least one key for a non-empty profile. Open-ended samples can clamp to the
nearest key if that matches the surrounding `TrackEvaluator` distance behavior,
but authoring tools should still warn when the profile does not cover the track
length intentionally.

Roll angles should be treated as continuous unwrapped radians. Avoid automatic
wrapping around `pi` or `2*pi` while interpolating, because a designer may
intentionally author a full roll or inversion sequence.

## Interpolation Modes

### Constant Roll

Constant roll holds the left key value until the next key. This mode exactly
represents today's per-segment `TrackSegment.RollRadians` behavior when keys are
placed at segment boundaries.

Use cases:

- flat sections
- constant banked turns
- compatibility import from existing segment roll

Tradeoff: the roll angle can step at boundaries, so frame twist diagnostics may
show sharp discontinuities.

### Linear Roll

Linear roll interpolates directly between the two key angles:

```text
roll = lerp(roll0, roll1, t)
```

Use cases:

- simple roll ramps
- deterministic fixtures where expected values are easy to inspect
- early editor/debug tooling

Tradeoff: roll angle is continuous, but roll slope changes abruptly at keys.
That means derived roll rate can jump even though the roll angle itself does
not.

### Cubic / Smooth Roll

The first smooth mode should be simple and deterministic, such as smoothstep:

```text
u = t * t * (3 - 2 * t)
roll = lerp(roll0, roll1, u)
```

This gives a smooth ease-in/ease-out with zero slope at both interval ends. It
is appropriate for early coaster-domain banking ramps without introducing a
general-purpose spline stack.

A richer later mode could use cubic Hermite interpolation with explicit key
slopes in radians per meter. If that becomes necessary, prefer a mature
interpolation/numerical library or a very small coaster-specific adapter rather
than growing a broad custom math system.

## Roll Rate And Force Sections

`BankingProfile` defines geometry: roll angle as a function of station distance,
`roll(s)`.

`ForceSection` currently carries force target channels, including roll rate in
degrees per second. That channel is a target or diagnostic force-domain value,
not currently the authority for frame geometry.

The relationship is:

```text
rollSlopeRadPerMeter = d(rollRadians) / ds
rollRateRadPerSecond = rollSlopeRadPerMeter * speedMetersPerSecond
rollRateDegPerSecond = rollRateRadPerSecond * 180 / pi
```

Future systems can use this relationship in two safe ways:

- Diagnostics: compare a profile-derived roll rate against
  `ForceSection.RollRateDegPerSec` at the current train speed.
- Authoring/solving: integrate a desired roll-rate target into proposed roll
  keys only when a speed profile or solver context is explicitly available.

Do not silently make `ForceSection` roll-rate channels drive `TrackFrame`
generation. That would couple motion targets, train speed, and geometry in a
way that is too implicit for the current backend.

## Sign Conventions

Quantum's public frame convention is:

- `Tangent`: forward along the centerline.
- `Normal`: up from the track frame.
- `Binormal`: right/lateral axis.
- `Binormal = Tangent x Normal` for the right-handed frame basis.

Positive roll should follow the current evaluator behavior: right-hand positive
rotation around `Tangent`. With a frame whose tangent is +X, normal is +Y, and
binormal is +Z, positive roll rotates `Normal` toward `+Binormal` and rotates
`Binormal` toward `-Normal`.

Positive roll rate means roll angle is increasing under the same convention.
Positive lateral G currently projects along `+Binormal` in force projection.

Renderer adapters must handle engine coordinate conversion at their own
boundaries. The backend sign convention should remain renderer-neutral and
documented in terms of `TrackFrame`.

## Units

Recommended core units:

- Station distance `s`: meters.
- Roll angle: radians.
- Roll slope: radians per meter.
- Roll rate diagnostics: radians per second internally, degrees per second when
  comparing with existing `ForceTargets.RollRateDegPerSec`.
- Force target normal/lateral/longitudinal values: G units as already modeled.

UI, browser, or import adapters may display banking in degrees, but the backend
profile should store radians to match `TrackSegment.RollRadians` and frame
rotation math.

## Future TrackFrame Generation

The future frame-generation path should stay conceptually small:

1. Resolve station distance `s`.
2. Evaluate centerline position and tangent.
3. Build or transport an unrolled base frame.
4. Sample `BankingProfile` for `rollRadians` at `s`.
5. Rotate base `Normal` and `Binormal` around `Tangent` by `rollRadians`.
6. Orthonormalize and return the existing `TrackFrame` contract.

This would make banking a scalar input to frame generation, not a new frame
type. `TrackFrame` should remain the public pose basis, and matrix/export
contracts can continue consuming `TrackFrame` without knowing whether the roll
came from segment shorthand or a profile.

Batch sampling should prefer a stable transported base frame before applying
profile roll. That keeps centerline orientation continuity and banking
interpolation as separate concerns that can be tested independently.

## Browser Debug Viewer Diagnostics

The browser debug viewer can eventually display banking diagnostics as a thin
adapter over backend data. Useful views include:

- Roll angle versus station distance.
- Banking keys and interpolation mode markers.
- Roll slope in radians per meter.
- Estimated roll rate in degrees per second when a sampled train speed is
  available.
- Warnings for non-finite values, uncovered distance ranges, discontinuous
  steps, or unusually high roll slopes.
- Visual frame axes and banking ribbon colored by roll angle or roll-rate
  magnitude.
- Optional comparison between current `TrackSegment.RollRadians` output and a
  future explicit `BankingProfile`.

This should not change `DebugViewportSnapshotV1`. A future viewer can derive
some diagnostics from sampled frames, or a later debug contract version can add
explicit banking samples after the backend profile API exists.

## Future Test Shape

When runtime behavior is intentionally added, tests should cover:

- constant profile parity with existing `TrackSegment.RollRadians`
- finite validation and sorted-key rejection
- clamped or boundary sampling behavior
- linear interpolation expected values
- smoothstep endpoint and midpoint expected values
- unwrapped roll interpolation across values greater than `2*pi`
- derived roll-rate diagnostics from distance slope and speed
- frame orthonormality after applying sampled roll

## Non-Goals

- No default `TrackEvaluator` runtime behavior change in this milestone.
- No changes to `TrackFrame`.
- No changes to `DebugViewportSnapshotV1`.
- No changes to `TrainPoseExportV1`.
- No Unity, Unreal, browser, or renderer dependency.
- No new math, interpolation, or spline dependency.
- No attempt to solve lateral force, roll-rate, and banking coupling yet.
