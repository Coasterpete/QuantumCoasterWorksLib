# Math And Geometry Migration Plan

As of 2026-05-14, Quantum should continue using a mixed backend while gradually moving generic math/geometry responsibilities to mature libraries behind stable adapter seams.

This plan intentionally avoids:
- production behavior rewrites
- legacy class deletion
- architecture redesign
- Unity visualizer breakage

It is aligned with the current milestone:
- stable centerline evaluation
- stable orientation frames
- distance-based train car placement
- debug-friendly Unity visualization

## Scope And Guardrails

Hard constraints for this migration track:
- Keep `TrackEvaluator` distance/frame semantics stable.
- Keep `TrainCarTransformProvider` and internal train placement solvers stable.
- Keep backend-to-Unity pose/matrix conventions stable.
- Migrate one call site or one public surface at a time.
- Preserve parity and contract tests at every migration step.

## What Stays Custom (Coaster Domain Logic)

These components encode coaster-specific semantics and should remain custom:
- Track distance resolution and clamping semantics:
  - `Quantum.Track/TrackEvaluator.cs`
  - `Quantum.Track/Internal/CompiledTrackSamplingContext.cs`
- Frame construction policy (tangent/normal/binormal, roll, fallback axes):
  - `Quantum.Track/TrackEvaluator.cs`
  - `Quantum.Splines/TrackFrameSampler.cs`
- Train placement semantics (body/bogie/wheel/articulation spacing and hierarchy):
  - `Quantum.Track/Internal/TrainCarBodySampler.cs`
  - `Quantum.Track/Internal/TrainBogieTransformSolver.cs`
  - `Quantum.Track/Internal/TrainArticulationFrameSolver.cs`
  - `Quantum.Track/Internal/TrainWheelTransformLayoutSolver.cs`
- Consumer-facing track/train contracts and export behavior:
  - `Quantum.Track/TrackFrame.cs`
  - `Quantum.IO/TrainPose/V1/*`

## What Moves Behind G-Shark Adapters

Primary migration seam:
- FVD NURBS construction and runtime sampling path:
  - `Quantum.FVD/FvdGraph.cs` (`BuildNurbsCurve`)
  - `Quantum.FVD/FvdNurbsBuildResult.cs` (build result surface)
  - `Quantum.FVD/Fvd2dNormalGSolver.cs` (runtime arc-length consumption)

Rules for this seam:
- Prefer `IParamCurve`/`IArcLengthCurve` surfaces at integration boundaries.
- Keep `GSharkNurbsCurveAdapter` as the runtime-backed evaluator for NURBS arc-length workflows.
- Keep legacy `NurbsCurve` available during transition as a compatibility/parity reference.

## What Could Eventually Use Math.NET

Math.NET is a future incremental option, not an immediate dependency.

Low-risk candidates:
- generic derivative/integration helpers now implemented as ad-hoc finite differences
- generic interpolation/easing internals that do not encode coaster semantics
- generic matrix/vector helper consolidation where no domain contracts are affected

Do not migrate these to Math.NET:
- `TrackEvaluator` behavior and frame policy
- train placement solvers
- track/train DTO and export contract semantics

## What Remains As Compatibility/Reference For Now

Keep these intact until adapter-first surfaces are fully adopted and parity remains green:
- `Quantum.Splines/Curves/NurbsCurve.cs`
- `Quantum.FVD/FvdNurbsBuildResult.cs` current legacy-bearing contract
- `Quantum.FVD/FvdGraph.cs::BuildNurbsCurve(int)` shape
- `Quantum.Splines/Curves/GSharkNurbsCurveAdapter.cs`
- `Quantum.Splines/GSharkVector3dConversions.cs`

Keep high-signal parity/contract test coverage active:
- `Quantum.Tests/FVD/FvdFoundationTests.cs`
- `Quantum.Tests/FVD/FvdSolverPrototypeTests.cs`
- `Quantum.Tests/Splines/GSharkNurbsCurveAdapterTests.cs`
- `Quantum.Tests/Track/GSharkTrainCarSpacingParityTests.cs`
- `Quantum.Tests/Track/TrackEvaluator*`
- `Quantum.Tests/Track/TrainCarTransformProviderTests.cs`
- `Quantum.Tests/Physics/TrackPhysicsAdapterTests.cs`

## Next 3 Smallest Implementation Steps

### Step 1: Add Non-Breaking Adapter-First Surface To FVD Build Result

Intent:
- Introduce adapter-first/interface-first accessors on `FvdNurbsBuildResult` without removing or changing current legacy properties.

Implementation shape:
- Keep existing `ParamCurve` (`NurbsCurve`) and `ArcCurve` (`ArcLengthCurveAdapter`) properties as-is.
- Add parallel runtime-facing properties typed as interfaces (`IParamCurve`, `IArcLengthCurve`) that point to G-Shark-backed curve objects where appropriate.
- Do not modify solver behavior in this step.

Tests required:
- Update `Quantum.Tests/FVD/FvdFoundationTests.cs`:
  - assert legacy `ParamCurve` property still exists and remains legacy-compatible
  - assert new adapter-first properties are present and non-null
  - assert runtime arc-length path remains G-Shark-backed
- Re-run:
  - `Quantum.Tests/Splines/GSharkNurbsCurveAdapterTests.cs`
  - `Quantum.Tests/Track/GSharkTrainCarSpacingParityTests.cs`

Exit criteria:
- No production behavior changes.
- All existing parity tests remain green.

### Step 2: Switch FVD Runtime Consumers To Interface Surface

Intent:
- Move runtime FVD consumers off concrete legacy-bearing properties and onto adapter-first interface properties.

Implementation shape:
- Update `Quantum.FVD/Fvd2dNormalGSolver.cs` to consume interface-first runtime property from `FvdNurbsBuildResult`.
- Keep fallback and compatibility properties unchanged.
- Do not alter curve sampling math, clamping, or solver stepping logic.

Tests required:
- Re-run and, if needed, tighten:
  - `Quantum.Tests/FVD/FvdSolverPrototypeTests.cs`
  - `Quantum.Tests/FVD/FvdFoundationTests.cs`
- Confirm no regression in train/frame parity:
  - `Quantum.Tests/Track/GSharkTrainCarSpacingParityTests.cs`
  - `Quantum.Tests/Track/TrainCarTransformProviderTests.cs`

Exit criteria:
- Solver outputs stay deterministic and contract-compatible.
- No Unity-facing pose/matrix behavior changes.

### Step 3: Add Characterization Baselines For Future Math.NET Candidates

Intent:
- Add explicit numeric baselines before touching generic low-level kernels, so future library swaps are guarded.

Implementation shape:
- Add deterministic value-table tests for force interpolation modes in `ForceInterpolationEvaluator`.
- Add additional fixed-point curvature characterization checks for `TrackPhysicsAdapter.TryGetCurvatureAtDistance(...)`.
- Keep production implementation unchanged in this step.

Tests required:
- Expand:
  - `Quantum.Tests/Track/ForceInterpolationEvaluatorTests.cs`
  - `Quantum.Tests/Physics/TrackPhysicsAdapterTests.cs`
- Re-run frame/placement contract suites:
  - `Quantum.Tests/Track/TrackEvaluatorFrameTests.cs`
  - `Quantum.Tests/Track/TrainCarTransformProviderTests.cs`

Exit criteria:
- New baselines are stable and deterministic.
- No drift in frame or car placement behavior.

## Rollout Discipline

For each step:
1. Land the smallest viable change set.
2. Keep legacy compatibility surfaces intact.
3. Run the targeted parity/contract tests.
4. Only proceed when results are stable and deterministic.

This keeps Quantum's backend and Unity visualizer operational while gradually reducing homemade low-level math/geometry risk.
