# Quantum CoasterWorks

Advanced roller coaster editor and simulation platform.

## Core Rules

- Keep code modular, deterministic, and test-first.
- Favor reusable libraries.
- Unity HDRP is host/render layer only.
- Core coaster logic belongs in Quantum.* libraries.
- Keep files small and single responsibility.
- Reuse before rewriting.
- Avoid overengineering.
- Treat all code as current final state.
- Do not change existing behavior contracts unless explicitly requested.
- Run `dotnet test QuantumCoasterWorks.sln --nologo` before reporting done.

## Architecture

Read `structure.md` before making changes.

## Priority Systems

1. Quantum.Math
2. Quantum.Splines
3. Quantum.FVD
4. Quantum.Physics
5. Quantum.IO

## Current Backend State

- Current validation: 223/223 tests passing.
- `Quantum.Track` supports document, segments, traversal, frame, transform, and spline-backed spatial evaluation.
- `TrackDocument`, `TrackSegment`, `StraightSegment`, and `CurvedSegment` exist.
- `TrackEvaluator` supports:
  - `Evaluate`
  - `EvaluateAt`
  - `EvaluateAtDistance`
  - `EvaluateTransform`
  - `EvaluateFrame`
- `Quantum.Physics` has `TrackPhysicsAdapter` for read-only frame/transform sampling from `TrackDocument`.
- `TrackPhysicsAdapter` prefers spline-derived curvature when available.
- Finite-difference curvature remains the fallback when spline-derived curvature is unavailable.
- `TrainStepLoop` is still not integrated with `TrackDocument`.
- `Matrix3x3` exists in `Quantum.Math` as the minimal 3x3 basis transform primitive.
- `Transform3d` exists in `Quantum.Math` with:
  - `Matrix3x3 Rotation`
  - `Vector3d Position`
  - point/direction transform helpers and simple rigid inverse
- `ITrackFrameBasis` exists in `Quantum.Math` to avoid `Quantum.Math -> Quantum.Splines` dependency cycles.
- `TrackFrame` implements `ITrackFrameBasis`.
- `Transform3d.FromTrackFrame(...)` provides the spatial bridge from track frame basis to world transform.
- FVD force target pipeline exists.
- `FvdGraph` supports strict and permissive force target queries.
- `FvdForceTargetProviderAdapter` uses permissive query behavior but requires `NormalG`.
- `ForceTargetProjection` maps `ForceTargets` to projected acceleration using `TrackFrame`.
- `AccelerationDecomposer` decomposes projected acceleration into:
  - tangential
  - normal
  - binormal
- `TrainStepLoop` now supports integration modes, including `TangentialProjected`.
- `TangentialProjected` integration mode now supports:
  - track-frame gravity projection
  - spline-derived curvature
  - curvature normal diagnostics (scalar + vector)
  - combined world acceleration diagnostics
  - controlled curvature speed influence (default OFF via multiplier)
- `LegacyNormalComponent` remains unchanged and behavior-preserved.
- Sampled `TrainFollowerState` includes:
  - `ProjectedAcceleration`
  - `TangentialAcceleration`
  - `NormalAcceleration`
  - `BinormalAcceleration`
- Normal forces remain diagnostics-only and are not applied to motion integration.
- `LateralG` and `RollRate` are currently diagnostics/data-path only and do not affect 1D motion.
- All new physics behavior is opt-in and guarded.
- `TrainStepLoop` remains deterministic and stable.

## Locked Contracts

- Existing tests must remain green.
- Missing `NormalG` returns false through the adapter.
- Missing permissive channels such as `LateralG` or `RollRate` default to zero.
- No FVD coverage returns false.
- Provider sampling is deterministic and occurs once per step using pre-step distance.
- Sampling analytics may throw if required force targets are missing.
- `Step()` integration behavior must not change unless the task explicitly requests an integration change.

## Intentionally Not Done Yet

- Full 3D train motion.
- Lateral/roll force coupling.
- Time-domain FVD solver.
- Unity integration.
- Audio system.
- Production editor UI.
- Routing/switch/tilt/drop/bounce systems.

## Next Recommended Lane

- Continue incremental spatial tooling on top of current track evaluation architecture.
- Next safe milestone: introduce lateral acceleration diagnostics (binormal direction) derived from `TrackFrame` and curvature/roll context, diagnostics-only.
- Keep `TrainStepLoop` behavior untouched while expanding evaluation helpers.

Preserve deterministic behavior and keep special-track expansion gated behind architecture readiness.

## AI Instructions

Before editing:
1. Inspect existing code.
2. Make a minimal plan.
3. Edit only necessary files.
4. Preserve architecture boundaries.
5. Add or update tests for behavior changes.
6. Keep refactors separate from behavior changes when possible.
