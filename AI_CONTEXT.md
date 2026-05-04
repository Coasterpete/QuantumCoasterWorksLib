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

- Current validation: 152/152 tests passing.
- FVD force target pipeline exists.
- `FvdGraph` supports strict and permissive force target queries.
- `FvdForceTargetProviderAdapter` uses permissive query behavior but requires `NormalG`.
- `ForceTargetProjection` maps `ForceTargets` to projected acceleration using `TrackFrame`.
- `AccelerationDecomposer` decomposes projected acceleration into:
  - tangential
  - normal
  - binormal
- `TrainStepLoop` now supports integration modes, including `TangentialProjected`.
- `LegacyNormalComponent` remains the default mode and behavior-preserved.
- `TangentialProjected` integration mode is implemented and validated with:
  - inclined-track gravity projection
  - energy sanity/no-drag
  - constant-force kinematics
  - divergence from legacy mode when `NormalG` exists
- Sampled `TrainFollowerState` includes:
  - `ProjectedAcceleration`
  - `TangentialAcceleration`
  - `NormalAcceleration`
  - `BinormalAcceleration`
- `LateralG` and `RollRate` are currently diagnostics/data-path only and do not affect 1D motion.

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
- Circuit/shuttle/switch/tilt/drop/bounce systems.

## Next Recommended Lane

- Short cleanup/context pass.
- Then establish math transform foundation such as `Matrix3x3`/`Matrix4x4` or frame-to-transform helpers.
- Do not expand special track systems until track evaluation architecture exists.

Preserve deterministic behavior and keep special-track expansion gated behind architecture readiness.

## AI Instructions

Before editing:
1. Inspect existing code.
2. Make a minimal plan.
3. Edit only necessary files.
4. Preserve architecture boundaries.
5. Add or update tests for behavior changes.
6. Keep refactors separate from behavior changes when possible.
