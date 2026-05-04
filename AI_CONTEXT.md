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

- Current validation: 141/141 tests passing.
- FVD force target pipeline exists.
- `FvdGraph` supports strict and permissive force target queries.
- `FvdForceTargetProviderAdapter` uses permissive query behavior but requires `NormalG`.
- `ForceTargetProjection` maps `ForceTargets` to projected acceleration using `TrackFrame`.
- `AccelerationDecomposer` decomposes projected acceleration into:
  - tangential
  - normal
  - binormal
- `TrainStepLoop` remains deterministic and still uses the current 1D integration behavior.
- `TangentialAcceleration` is computed as an intermediate but is not yet used to drive motion.
- Sampled `TrainFollowerState` includes:
  - `ProjectedAcceleration`
  - `TangentialAcceleration`
  - `NormalAcceleration`
  - `BinormalAcceleration`
- `LateralG` and `RollRate` are currently diagnostics/data-path only and must not affect motion yet.

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
- Tangential-acceleration-driven integration.
- Time-domain FVD solver.
- Unity integration.
- Audio system.
- Production editor UI.

## Next Safe Milestone

Add an explicit integration-mode seam or strategy before switching `TrainStepLoop` to tangential-acceleration-driven motion.

Do not directly replace the current integration path without tests and a compatibility plan.

## AI Instructions

Before editing:
1. Inspect existing code.
2. Make a minimal plan.
3. Edit only necessary files.
4. Preserve architecture boundaries.
5. Add or update tests for behavior changes.
6. Keep refactors separate from behavior changes when possible.