# Milestone 39: External Coaster Simulation Reference Audit

Date: 2026-05-31

This document is a reference audit only. It summarizes public roller coaster
simulation articles by Ercan Akyurek and compares their concepts to the current
QuantumCoasterWorks backend. It does not copy source code, port implementation
details, or add runtime behavior.

## Sources Audited

Primary public articles from `https://ercanakyuerek.de/posts/`:

- [Gravity](https://ercanakyuerek.de/posts/writing-a-roller-coaster-simulation/gravity/)
- [Evaluating Motion](https://ercanakyuerek.de/posts/writing-a-roller-coaster-simulation/evaluating-motion/)
- [Linear Track](https://ercanakyuerek.de/posts/writing-a-roller-coaster-simulation/linear-track/)
- [Friction and Air Resistance](https://ercanakyuerek.de/posts/writing-a-roller-coaster-simulation/friction-and-air-resistance/)
- [Transformation Matrix](https://ercanakyuerek.de/posts/writing-a-roller-coaster-simulation/transformation-matrix/)
- [Curve Nodes](https://ercanakyuerek.de/posts/writing-a-roller-coaster-simulation/curve-nodes/)
- [Bezier curve](https://ercanakyuerek.de/posts/writing-a-roller-coaster-simulation/bezier-curve/)
- [Curve orientation](https://ercanakyuerek.de/posts/writing-a-roller-coaster-simulation/curve-orientation/)
- [NURBS, Roll, Physics in Action](https://ercanakyuerek.de/posts/roller-coaster-simulation/nurbs-roll-physics-in-action/)
- [Uniform Rational B-Spline curve](https://ercanakyuerek.de/posts/writing-a-roller-coaster-simulation/b-spline-curve/)

The articles are educational TypeScript/Three.js references. Quantum should use
them for vocabulary, comparison, and milestone planning, not as an implementation
source.

## Current Quantum Baseline

Quantum already has a coaster-domain backend lane for the same general problem:
sample a centerline by station distance, produce a stable frame, place train
boxes/bogies/wheels by distance offsets, and export renderer-neutral debug data.

Relevant current concepts:

- `Quantum.Track.TrackFrame`: public coaster-domain frame with distance,
  position, tangent, normal, binormal, and a canonical matrix conversion.
- `Quantum.Splines.TrackFrame` and `TrackFrameSampler`: support-layer frame and
  arc-length sampling path.
- `TrackEvaluator`: distance-to-frame evaluation from a `TrackDocument`, with
  spline-backed and fallback segment paths.
- `TrainCarTransformProvider`: lead-distance based train pose sampling for
  bodies, bogies, wheels, and articulated car frames.
- `TrainPoseExportV1`: versioned train pose JSON with frames and matrices.
- `DebugViewportSnapshotV1`: renderer-neutral centerline, frame, debug line,
  box, and optional train-pose export boundary.
- `TrackFrameSmoothnessDiagnostics`, `TrackFrameContinuityDiagnostics`, and
  `TrackPhysicsAdapter.TryGetCurvatureAtDistance`: current curvature/radius and
  frame quality diagnostics.
- `DebugTrackContinuousSampler` and the browser debug viewer: optional
  diagnostics/debug visualization, not backend architecture.
- `TrainFollowerState` and `TrainStepLoop`: simple distance, speed,
  gravity/resistance, force-target, and curvature diagnostic motion loop.
- `GSharkNurbsCurveAdapter`: mature-library-backed NURBS evaluation path, with
  legacy custom B-spline/NURBS code kept as compatibility/parity baseline.

## Topic Comparison

| Topic | Reference idea | Quantum alignment | Gap or caution |
| --- | --- | --- | --- |
| NURBS / B-splines | Smooth coaster centerlines benefit from B-spline/NURBS-style continuity, weighted control points, clamped/open/closed boundary behavior, and arc-length sampling. | Quantum already has `IParamCurve`/`IArcLengthCurve`, `GSharkNurbsCurveAdapter`, FVD NURBS build results, and parity tests against legacy custom curves. This aligns with the project rule to prefer mature spline libraries. | Boundary policy is not yet a first-class coaster authoring contract. Future work should clarify clamped versus closed track behavior, derivative-backed curvature, and arc-length fidelity without expanding custom generic spline code. |
| Curve orientation | Fixed global-up `lookAt` style orientation can become unstable near vertical tangents. The reference moves toward incremental orientation / parallel transport. | Quantum validates finite orthonormal frames and already has a debug continuous sampler that transports the normal between samples and aligns frame signs. | The main `TrackEvaluator` still builds spline frames from a selected reference up per sample, then applies segment roll. A future production path may need an explicit frame-transport policy for batch distance sampling, tested by existing smoothness/continuity diagnostics. |
| Roll / banking | Roll is treated as rotation around the forward/tangent direction. The reference mentions cubic roll interpolation in a live simulation and leaves exact roll interpolation policy open in the B-spline article. | Quantum has `TrackSegment.RollRadians`, frame-axis roll rotation, debug roll blending across segment boundaries, roll continuity diagnostics, and `ForceSection` roll-rate channels. | Banking is not yet a standalone profile/domain concept. Constant segment roll plus debug-only blending is useful for prototypes, but future milestones should design a distance-domain `BankingProfile` or roll curve with explicit interpolation, units, and force-section interaction. |
| Transformation matrices | The reference collapses position and forward direction into one transformation-at-distance concept so simulation and rendering consume a single pose. | Quantum uses `TrackFrame` as the source of truth and exports matrices through `TrackFrame.ToMatrix4x4`, `Transform3d`, `Matrix4x4d`, `TrainPoseExportV1`, and `DebugViewportSnapshotV1`. | Precision is mixed today: some paths export `System.Numerics.Matrix4x4` float widened to double, while others use `Matrix4x4d`. This is documented in `TrainPoseExportV1`, but future work should keep matrix convention tests tight. |
| Gravity | Gravity can be projected onto the track direction; the later linear-track article expresses this as a dot product between forward direction and a downward gravity vector. | Quantum does the same conceptually in `TrainFollowerState.GravityAccelerationAlongTrack` and in `TrainStepLoop` when a track-frame provider is available. | Good alignment. Future work is mostly documentation/diagnostics: units, sign convention, and how gravity projection combines with force targets. |
| Friction and air resistance | The reference uses a simple directional energy-loss model: speed-squared air resistance plus friction scaled by gravity, opposing current motion. | Quantum has configurable linear drag, quadratic drag, and rolling resistance in `TrainFollowerState.UpdateWithResolvedGravityAcceleration`. Resistance opposes speed direction and can stop near-zero reversals. | Coefficient meaning is still prototype-level. Future milestones should document coefficient units and expected ranges before tuning against real layouts or external tools. |
| Motion evaluation | The reference starts with a compact per-frame integration: update velocity from acceleration and distance from velocity. | Quantum has a deterministic fixed-step `TrainStepLoop`, loop/clamp distance behavior, force-target projection, tangential projected mode, and sampled analytics. | Quantum is already more structured than the reference. Future work should compare integration choices with deterministic regression fixtures and avoid adding advanced dynamics before centerline/frame/train placement is stable. |
| Distance sampling | The reference repeatedly converts requested distance into curve samples or bounding nodes and keeps simulation independent from the curve generator. | Quantum's `TrackEvaluator`, `CompiledTrackSamplingContext`, `ArcLengthCurveAdapter`, train car spacing, bogie spacing, and debug snapshot samples are all distance-based. | Strong alignment. Worth future profiling/accuracy checks around adaptive arc-length LUTs and high-curvature track sections. |
| Browser debug viewer | The reference articles use browser demos to make coaster math visible and interactive. | Quantum has a backend-generated browser debug viewer for `DebugViewportSnapshotV1`, including centerline, frames, train boxes, bogies, wheels, and curvature/radius inspection. | Quantum should keep this viewer as a thin debug adapter. Future improvements can add time-series playback or force vectors without making browser concerns part of backend architecture. |

## Alignment Summary

The biggest conceptual match is the distance-based abstraction. The reference
series separates motion from the curve generator by asking for transform data at
an arc length. Quantum already does this with `TrackEvaluator`, `TrackFrame`,
train pose sampling, and debug exports.

The second strong match is matrix/frame-centered simulation. The reference
evolves from separate point and forward-vector queries toward a transform at
distance. Quantum's backend is already centered on a frame contract and versioned
matrix export, which is the right shape for Unity, Unreal, browser, Blender, or
future standalone viewers.

The third match is using mature spline/geometry help. The reference introduces
B-spline/NURBS concepts as practical tools for smooth coaster tracks. Quantum
already has the adapter-first G-Shark path and should continue moving consumer
code toward `IParamCurve`/`IArcLengthCurve` boundaries rather than growing custom
generic spline machinery.

## Gaps Worth Future Milestones

1. Production frame transport policy

   Define whether `TrackEvaluator.EvaluateFramesAtDistances` should preserve
   orientation incrementally across a batch sample, expose a dedicated transported
   frame sampler, or keep production sampling stateless while diagnostics use
   transported frames. Use existing smoothness/continuity reports to compare.

2. Banking/roll profile design

   Add a coaster-domain roll/banking concept before adding more roll math:
   likely a distance-domain profile that can represent constant roll, keyed roll,
   cubic interpolation, and maybe roll-rate-driven sections. Keep it separate
   from visualization and from generic spline code.

3. Derivative-backed curvature and radius diagnostics

   Prefer exact or library-backed curve derivatives where available, then fall
   back to finite differences. Consider exporting explicit curvature/radius
   samples in a future debug contract version instead of deriving them only in
   the browser viewer.

4. Arc-length fidelity checks

   Add focused tests or diagnostics for high-curvature sections where uniform
   parameter sampling can hide distance error. Explore adaptive sampling or
   higher-resolution LUT policy behind existing interfaces.

5. Motion model coefficient semantics

   Document coefficient units for linear drag, quadratic drag, and rolling
   resistance. Add simple benchmark scenarios that make sign, stopping, and
   downhill/uphill behavior obvious.

6. Time-series train pose/debug exports

   `TrainPoseExportV1` and `DebugViewportSnapshotV1` are snapshot contracts.
   Animation currently belongs to consumers or debug tooling. A future milestone
   could define a backend-neutral sampled time-series export if needed.

7. Closed-loop and station wrapping semantics

   The reference distinguishes open/clamped/closed curves. Quantum has loop
   behavior in `TrainFollowerState`, but track-document closed-loop semantics and
   train placement across the wrap boundary deserve an explicit contract.

## Safe C# Equivalents To Explore Later

- Keep using `GShark` for NURBS evaluation where it fits, and prefer adapters
  over expanding `Quantum.Splines.NurbsCurve` or `BSplineCurve`.
- Evaluate `MathNet.Numerics` for scalar interpolation, numerical integration,
  root finding, and linear algebra if future roll-profile or arc-length work
  needs mature numerical routines.
- Use `System.Numerics.Matrix4x4` and `System.Numerics.Quaternion` only where
  single precision is acceptable, such as visualization or export adapter edges.
  Keep core backend precision expectations explicit before adopting float-heavy
  APIs internally.
- If quaternion interpolation becomes a backend requirement, evaluate a
  double-precision mature math package before adding a custom general-purpose
  quaternion library. A tiny coaster-specific helper is acceptable only if a
  suitable dependency is not justified.
- Consider `UnitsNet` only if unit ambiguity starts blocking physics or import
  work. Do not add it just for documentation.

Any future dependency should preserve the current backend rules: no UnityEngine
or UnityEditor dependencies in core `Quantum.*` projects, no renderer-driven
architecture, and no broad project rewrite.

## Non-Goals For This Audit

- No code copied from the referenced articles.
- No TypeScript, Three.js, React, or browser implementation port.
- No change to Quantum runtime behavior.
- No new package dependency.
- No redesign of the architecture.

## Recommended Next Milestone Candidates

1. Create a small design note for `BankingProfile` / roll interpolation policy,
   including how it relates to `TrackSegment.RollRadians` and `ForceSection`
   roll-rate channels.
2. Prototype a backend-only transported-frame sampler behind existing interfaces,
   then compare against current `TrackEvaluator` output using
   `TrackFrameSmoothnessDiagnostics`.
3. Add curvature/radius diagnostic fixtures for straight, constant-radius,
   vertical, and banked sections before exposing richer debug payload fields.

