# Section Pipeline (Planning Milestone)

## Goal
Define the future FVD-style section architecture for Quantum without changing runtime behavior yet.

## Current Baseline
- `Quantum.Track` has lightweight `TrackSection` types (`ForceSection`, `GeometricSection`) stored in `TrackDocument.Sections`.
- These section objects are currently data-only and are not yet mapped into `TrackEvaluator` geometry evaluation.
- `Quantum.FVD` already has a richer section model:
  - `FvdSectionDefinition` with `Kind`, `Domain`, `StartX`, `EndX`, and channel functions.
  - Non-overlap guarantees per `(Kind, Domain)` and deterministic boundary rules.
  - Channel evaluation for force targets (strict and permissive).
- `Quantum.Physics` consumes force targets through `IForceTargetProvider` and `ForceTargets`.

## Current Implementation Status

✔ `SectionResolver` provides deterministic interval resolution.  
✔ `SectionCurveAssembler` maps `GeometricSection` data into `CompositeSectionCurve`.  
✔ `ForceTargetResolver` resolves `ForceSection` intervals.  
✔ `ForceTargetSampler` converts force intervals into sampled force targets.  
✔ `SectionForceTargetProvider` adapts section-based targets to `IForceTargetProvider`.  
✔ `TrainStepLoop` can consume section force targets through explicit opt-in provider injection.

Default physics behavior remains unchanged.  
Section-driven physics is currently opt-in only.

## 1) How `ForceSection` and `GeometricSection` Should Evolve

### Design Direction
Evolve both section types from scalar payloads into timeline/channel sections that can be sampled deterministically in either distance or time domain.

### `ForceSection` Evolution
- Keep current constructor shape as a compatibility convenience layer.
- Add an FVD-style core representation behind it:
  - `Domain`: `Distance` or `Time`.
  - `StartX`, `EndX`: section coverage interval.
  - Channel functions for:
    - `NormalG`
    - `LateralG`
    - `RollRateDegPerSec`
- Existing scalar fields (`TargetNormalG`, `TargetLateralG`, `Length`, `Duration`) become shorthand for constant channel sections.
- Preserve current adapter semantics:
  - Missing `NormalG` is a hard failure for strict provider reads.
  - Missing lateral/roll channels can remain permissive defaults where required.

### `GeometricSection` Evolution
- Keep current `{Length, Curvature, Roll}` usage as compatibility shorthand.
- Add the same interval/domain shell used by force sections.
- Channel functions initially cover:
  - `Curvature`
  - `RollAngleDeg` (or roll equivalent in radians at the integration boundary)
- Future extension channels can be added later (for example yaw/pitch rates) without changing the section pipeline contract.

### Shared Section Contract
Long-term, `TrackSection` should normalize to a common definition shape:
- `Kind` (Force or Geometry)
- `Domain` (Distance or Time)
- `StartX`, `EndX`
- Channel function list

This aligns `Quantum.Track` with the proven structure already present in `Quantum.FVD`.

## 2) How Sections Map to Splines

### Canonical Coordinate
- Use track station distance `s` (meters along track) as the primary runtime coordinate.
- Keep time-domain sections supported as data, but gate runtime use until time-domain integration is intentionally introduced.

### Mapping Pipeline
1. Resolve active section by `(Kind, Domain, x)` where `x` is usually `s`.
2. Map `s` to segment-local position via `TrackEvaluator.EvaluateAtDistance`.
3. If the segment has a spline, evaluate frame/transform from spline-backed data.
4. If spline is absent, use existing deterministic fallback frame behavior.

### Coverage and Boundaries
- Use half-open intervals `[StartX, EndX)` for normal lookup.
- At touching boundaries, right-hand section wins.
- Final section end remains inclusive to avoid an uncovered terminal sample.
- Section intervals are independent of segment boundaries and may span multiple segments.

## 3) How Sections Map to Physics Targets

### Force Target Mapping
- Force section channels map directly to `ForceTargets`:
  - `NormalG -> ForceTargets.NormalG`
  - `LateralG -> ForceTargets.LateralG`
  - `RollRateDegPerSec -> ForceTargets.RollRateDegPerSec`

### Runtime Read Modes
- Strict read mode:
  - Fails when required channels are missing.
  - Keeps existing `NormalG` contract intact.
- Permissive read mode:
  - Missing lateral/roll channels default to zero.
  - Missing section coverage still returns false.

### Physics Scope Guardrails
- Keep `TrainStepLoop` integration behavior unchanged in this milestone.
- Continue treating lateral/roll as diagnostics/data-path unless explicitly upgraded in a later milestone.

## 4) Conceptual Comparison to KexEngine-Style Nodes/Sections

### Similarities
- Section-first authoring where Force and Geometric sections are the main design tools.
- Channels are timeline-driven values sampled over a section interval.
- Duration can be distance- or time-based.
- Deterministic section evaluation at any query position.

### Differences (Current Quantum)
- Quantum is currently library-first and document-based, not yet graph-node-first.
- `TrackDocument` and `FvdGraph` are separate data roots today.
- Node graph concepts such as anchor/path routing and branch priority are not yet part of `Quantum.Track` runtime contracts.

### Practical Alignment Strategy
- Match Kex-style behavior at the section evaluation layer first.
- Delay graph/node orchestration until section-to-spline and section-to-physics mappings are stable and test-covered.

## 5) Completed Milestone: Deterministic Section Pipeline

### Objective
Add the minimum new contracts needed to make section evaluation deterministic and testable, without changing simulation behavior.

### Scope
1. Introduce a read-only section resolution layer in `Quantum.Track` (domain + interval + boundary semantics).
2. Add adapters that translate existing `ForceSection`/`GeometricSection` shorthand into normalized channel sections.
3. Add tests for:
   - overlap rejection
   - boundary handoff behavior
   - strict vs permissive force channel reads
   - no-coverage diagnostics
4. Keep `TrainStepLoop` and motion integration untouched.

### Why This Is Safest
- It reuses existing FVD section semantics already validated by tests.
- It avoids coupling geometry solving and motion integration in the same milestone.
- It creates a stable base for later milestones (time-domain targets, geometric channel expansion, node-graph orchestration).

## 6) Force Channel Interpolation

### Status
Phase 1 complete:
- Constant
- Linear
- SmoothStep

All interpolation modes are opt-in and preserve existing behavior by default.

### Completed Phase 2 Presets
- Quadratic
- Cubic
- Quartic
- Quintic
- Sinusoidal

### Deferred
- Plateau

### Future Direction
- Custom easings
- Keyframed channel curves
- FVD-style channel functions

Plateau interpolation is reserved for future force-profile shapes where a channel rises to a target, holds, then optionally falls. It should not be added to the basic `start → end` interpolation enum until the API supports multi-stage channel profiles.

### Guardrails
- No default physics behavior changes.
- No automatic section-to-physics coupling.
- Existing constant-section tests must remain unchanged.

### Custom Easing Design Direction

Status:
A minimal easing abstraction (`IForceEasingFunction`) has been introduced. Built-in interpolation modes are currently wrapped through this interface, enabling future extension without modifying the enum-based system.

### Multi-Point Channel Direction

Status:
A minimal `KeyframedForceEasingFunction` has been introduced. It supports sorted keypoints, clamped evaluation, duplicate-t validation, piecewise-linear interpolation, and optional per-segment easing through `IForceEasingFunction`.

Future force channels should support multiple control points across a section interval, allowing complex force shaping beyond single start→end interpolation.

Initial implementations may support:
- A list of (t, value) keypoints
- Interpolation between keypoints using existing easing functions

This system should build on top of `IForceEasingFunction` rather than replacing it.

### ForceSection Channel Support (v1)

Status:
`ForceSection` now supports optional channel-based definitions:
- `NormalGChannel : IForceEasingFunction?`
- `LateralGChannel : IForceEasingFunction?`
- `RollRateChannel : IForceEasingFunction?`

Channels override interpolation when present. When null, existing start/end + interpolation behavior is preserved.

### ForceSection Channel Container (v2)

Status:
A minimal channel container abstraction has been introduced via `ForceChannelSet`.

- `ForceSection` now optionally exposes:
  - `Channels : ForceChannelSet?`

- `ForceChannelSet` groups channel definitions:
  - `NormalG : IForceChannel?`
  - `LateralG : IForceChannel?`
  - `RollRate : IForceChannel?`

- `ForceChannel` is a lightweight adapter over `IForceEasingFunction`.


### Resolution Priority

When sampling a section:

1. `ForceSection.Channels` (v2 container) is used when a matching channel is present
2. Individual channel properties (`NormalGChannel`, etc.) are used if present
3. Fallback to existing start/end + interpolation behavior (v1)

### Notes

- This change is fully backward compatible.
- Existing `ForceSection` scalar and interpolation behavior remains unchanged.
- The container enables future expansion toward multi-channel, domain-aware section definitions.

### ForceSection Multi-Channel Support (v3)

Status:
`ForceChannelSet` now supports multiple channels per force type.

- Additional optional collections:
  - `NormalGChannels : IReadOnlyList<IForceChannel>?`
  - `LateralGChannels : IReadOnlyList<IForceChannel>?`
  - `RollRateChannels : IReadOnlyList<IForceChannel>?`

### Resolution Priority (Multi-Channel)

When sampling a section (per force type):

1. Multi-channel list (`*Channels`) is used when non-null and non-empty
2. Single channel from `ForceChannelSet` (v2)
3. Individual `ForceSection` channel properties (v1)
4. Fallback to start/end + interpolation behavior (v1)

### Combination Rule

- When multiple channels are present, each channel is evaluated at `t`
- Results are combined via deterministic summation

### Notes

- This change is fully backward compatible
- Existing single-channel behavior remains unchanged
- Multi-channel support enables layered force definitions and is a step toward full FVD-style channel composition

### ForceSection Blend Modes (v4)

Status:
`ForceChannelSet` now supports deterministic blend modes for multi-channel lists.

- `ForceChannelBlendMode` supports:
  - `Sum`
  - `Max`
  - `Override`

- Blend modes are configurable per force type:
  - `NormalGBlendMode`
  - `LateralGBlendMode`
  - `RollRateBlendMode`

### Blend Behavior

When a multi-channel list is used:

1. `Sum` combines all sampled channel values through deterministic summation
2. `Max` uses the largest sampled channel value
3. `Override` uses the last channel in the list

### Notes

- Default blend mode is `Sum`, preserving v3 behavior.
- Blend modes only affect multi-channel lists.
- Single-channel paths and legacy fallback behavior remain unchanged.

### ForceSection Domain Support (v5)

Status:
Force channel domain plumbing has been introduced.

- `ForceChannelDomain` supports:
  - `Distance`
  - `Time`

- `ForceSection` now exposes:
  - `Domain : ForceChannelDomain`

- `ForceChannelSet` may optionally override the section domain:
  - `Domain : ForceChannelDomain?`

### Domain Resolution

When sampling a section:

1. `ForceSection.Channels.Domain` is used when present
2. Otherwise `ForceSection.Domain` is used
3. Default domain is `Distance`

### Notes

- `Distance` preserves existing behavior.
- `Time` is currently a stub and uses the same normalized sampling value as `Distance`.
- No time-based integration behavior is active yet.
- This prepares the system for future time-domain force sections.

### ForceSection Time-Domain Sampling (v6)

Status:
`ForceTargetSampler` now supports opt-in time-domain sampling through explicit elapsed-time overloads.

- Existing distance-only sampling overloads remain unchanged.
- New sampling overloads accept:
  - `distance`
  - `elapsedTime`

### Domain Behavior

When using the elapsed-time overloads:

1. `Distance` domain uses distance-derived normalized `t`
2. `Time` domain uses `elapsedTime / ForceSection.Duration`
3. `ForceChannelSet.Domain` still overrides `ForceSection.Domain`

### Validation

Time-domain sampling requires:
- finite `elapsedTime`
- non-null `ForceSection.Duration`
- finite, positive duration
- `elapsedTime` inside `[0, Duration]`

### Notes

- No `TrainStepLoop` behavior changes were made.
- Time-domain sampling is opt-in through the new sampler overloads.
- Existing legacy sampling behavior is preserved.

### ForceSection Provider Time-Domain Sampling (v7)

Status:
`SectionForceTargetProvider` now exposes explicit elapsed-time APIs for opt-in time-domain sampling.

- New provider APIs:
  - `Sample(double distance, double elapsedTime)`
  - `TryGetForceTargets(double distance, double elapsedTime, out ForceTargets targets)`

### Behavior

- Existing distance-only provider APIs remain unchanged.
- New elapsed-time APIs delegate to `ForceTargetSampler` time-domain sampling.
- Time-domain validation behavior matches the sampler.

### Notes

- No `TrainStepLoop` behavior changes were made.
- Provider-level time-domain sampling is opt-in.
- This prepares the system for future optional runtime integration.

### TrainStepLoop Elapsed-Time Force Sampling (v8)

Status:
`TrainStepLoop` now supports opt-in elapsed-time force sampling.

- New provider capability interface:
  - `IElapsedTimeForceTargetProvider`

- `SectionForceTargetProvider` implements elapsed-time force sampling.

- `TrainStepLoop` now exposes:
  - `UseElapsedTimeForceSampling`

### Behavior

When `UseElapsedTimeForceSampling` is `false`:
1. `TrainStepLoop` uses the legacy distance-only provider path.
2. Existing behavior is preserved.

When `UseElapsedTimeForceSampling` is `true`:
1. If the provider supports `IElapsedTimeForceTargetProvider`, elapsed-time sampling is used.
2. If not, the loop falls back to the legacy provider path.

### Notes

- Default behavior remains distance-based.
- Elapsed-time force sampling is explicitly opt-in.
- No existing TrainStepLoop behavior changes unless the opt-in flag is enabled.

### TrackFrame Matrix Conversion (v9)

Status:
`TrackFrame` keeps T/N/B + position as the source of truth, while `Matrix4x4` is used as an output/interop representation only.

### TrackEvaluator Frame Output

Status:
`TrackEvaluator` can now produce document-bound `TrackFrame` output through `EvaluateFrameAtDistance(double distance)`.

- Position matches existing distance evaluation.
- T/N/B basis vectors are explicitly orthonormalized.
- Existing `EvaluateAtDistance` behavior remains unchanged.

This connects track evaluation to the `TrackFrame -> Matrix4x4` interop layer.

### Train Car Transform Output

Status:
A minimal train-car transform layer has been introduced.

- `TrainCarTransformProvider` computes per-car transforms using `TrackEvaluator`.
- Each car produces:
  - `CarIndex`
  - `Distance`
  - `TrackFrame`
  - `Matrix4x4`

### Behavior

- Car 0 is placed at `leadDistance`
- Car i is placed at:
  - `leadDistance - i * carSpacing`
- Out-of-range distances are rejected explicitly
- Matrices are derived from `TrackFrame.ToMatrix4x4()`

### Notes

- No `TrainStepLoop` behavior changes were made
- This is a geometry/representation layer only
- This connects force evaluation → spatial frames → renderable transforms

### Train Car Debug Visualization

Status:
Renderer-agnostic debug geometry for train cars has been added.

- `TrainCarDebugGizmoBuilder` produces wireframe boxes per car
- Boxes are:
  - centered on `TrackFrame.Position`
  - oriented using T/N/B
  - length → Tangent
  - width → Binormal
  - height → Normal

### Notes

- Each car produces 12 line segments (wire box)
- No renderer/engine dependency
- No TrainStepLoop or physics changes
- This extends the debug visualization layer for spatial verification

### Camera Transform System

Status:
A renderer-agnostic camera transform system has been introduced based on `TrackFrame`.

Supported camera modes:
- Ride camera
- Target camera
- Fly-by camera
- B-roll camera

### Behavior

- Ride cameras use local offsets in T/N/B space.
- Target cameras look from a camera position toward a target position.
- Fly-by cameras use a fixed camera position and target a moving track/train frame.
- B-roll cameras support local offsets and optional look-ahead along the track.

### Notes

- All camera modes output `CameraTransform`.
- Camera transforms are interop-ready through `Matrix4x4`.
- No renderer, Unity, TrainStepLoop, or physics behavior changes were made.

### Fly-View and Walk-View Cameras

Status:
Free-camera style view modes have been added.

- `FlyViewCameraState` supports:
  - `Position`
  - `YawRadians`
  - `PitchRadians`
  - `RollRadians`

- `WalkViewCameraState` supports:
  - `Position`
  - `YawRadians`
  - `PitchRadians`
  - `EyeHeight`
  - `RollRadians`

### Behavior

- Fly-view computes a free camera transform from yaw/pitch/roll.
- Walk-view is a thin layer over fly-view with an eye-height offset.
- Walk-view does not implement collision, gravity, stairs, crouch, sprint, or ground snapping yet.

### Notes

- No renderer or Unity dependency.
- No physics or `TrainStepLoop` behavior changes.
- These camera modes prepare the system for editor navigation and viewer integration.

## References
- KexEdit node graph and section concepts: <https://individualkex.github.io/KexEdit/reference/node-graph.html>

