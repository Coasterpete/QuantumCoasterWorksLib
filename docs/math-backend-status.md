# Math Backend Status

As of Milestone 154, Quantum's math backend is stable for the current milestone (simple train boxes moving along centerline with stable frame sampling and distance-based placement), but it is intentionally mixed:
- core math primitives are custom and lightweight
- frame/placement logic is coaster-domain-specific and should stay custom
- some general numerical functionality is duplicated and can be migrated incrementally to mature libraries later

## Milestone 154 Public Surface Characterization

`Quantum.Math` remains a small support-layer package, not a general-purpose math
library. Its intended long-term public surface is:

- stable public: `Vector3d`, `MathUtil`
- transitional public: `Matrix4x4d`, `Matrix3x3`, `Transform3d`,
  `ITrackFrameBasis`

The transitional types remain public today to support existing backend
integration points and compatibility tests. New coaster-facing APIs should
prefer `Quantum.Track` or versioned `Quantum.IO` contracts rather than exposing
new generic math entrypoints.

## Current Custom Math Types

Core math primitives (`Quantum.Math`):
- `Vector3d` (`Quantum.Math/Vector3d.cs`): double-precision vector with dot/cross/normalize and scalar operators.
- `Matrix3x3` (`Quantum.Math/Matrix3x3.cs`): minimal basis matrix for local/world direction transforms.
- `Matrix4x4d` (`Quantum.Math/Matrix4x4d.cs`): double-precision 4x4 storage + conversion from `System.Numerics.Matrix4x4`.
- `Transform3d` (`Quantum.Math/Transform3d.cs`): rigid transform (`Matrix3x3` rotation + `Vector3d` position), including `FromTrackFrame`.
- `MathUtil` (`Quantum.Math/MathUtil.cs`): epsilon, clamp, lerp.
- `ITrackFrameBasis` (`Quantum.Math/ITrackFrameBasis.cs`): bridge interface to avoid project reference cycles.

Frame and transform domain types (coaster-facing, outside `Quantum.Math`):
- `Quantum.Splines.CurveFrame` (`Quantum.Splines/CurveFrame.cs`): generic curve-sampling frame (`S`, position, tangent/normal/binormal), implements `ITrackFrameBasis`.
- `Quantum.Splines.TrackFrame` (`Quantum.Splines/TrackFrame.cs`): obsolete compatibility name retained for existing spline callers.
- `Quantum.Track.TrackFrame` (`Quantum.Track/TrackFrame.cs`): canonical coaster-facing runtime frame with global station `Distance`, `ITrackFrameBasis`, and `ToMatrix4x4()` policy.
- Train transform records:
  - `TrainCarTransform` (`Quantum.Track/TrainCarTransform.cs`) uses `Matrix4x4` (float)
  - `BogieTransform` (`Quantum.Track/BogieTransform.cs`) uses `Matrix4x4d` (double)
  - `WheelTransform` (`Quantum.Track/WheelTransform.cs`) uses `Matrix4x4d` (double)
  - `ArticulatedTrainCarTransform` (`Quantum.Track/ArticulatedTrainCarTransform.cs`) uses `Matrix4x4d` (double)

## Where Matrices And Transforms Are Used

Track/frame evaluation:
- `TrackEvaluator.EvaluateTransform(...)` and `EvaluateTransformAtDistance(...)` return `Transform3d` for position + basis sampling (`Quantum.Track/TrackEvaluator.cs`).
- `TrackFrame.ToMatrix4x4()` and `TrackFrame.CreateFromFrame(...)` are canonical matrix emitters for track basis (`Quantum.Track/TrackFrame.cs`).

Train placement path:
- Body placement builds `TrainCarTransform` from distance-sampled `TrackFrame` and `frame.ToMatrix4x4()` (`Quantum.Track/Internal/TrainCarBodySampler.cs`).
- Bogie placement converts frame matrix to `Matrix4x4d.FromMatrix4x4(...)` (`Quantum.Track/Internal/TrainBogieTransformSolver.cs`).
- Articulated placement also converts from articulated frame to `Matrix4x4d` (`Quantum.Track/Internal/TrainArticulationFrameSolver.cs`).
- Wheel transforms currently inherit bogie frame/matrix (`Quantum.Track/Internal/TrainWheelTransformLayoutSolver.cs`).

Physics and debug:
- Physics reads canonical `Quantum.Track.TrackFrame` values and transform snapshots through `TrackPhysicsAdapter` (`Quantum.Physics/TrackPhysicsAdapter.cs`).
- Camera builders emit `System.Numerics.Matrix4x4` from frame basis (`Quantum.Track/CameraFrameBuilder.cs`).

IO/export:
- Train pose export serializes both float and double matrix sources into DTO `Matrix4x4V1Dto` (`Quantum.IO/TrainPose/V1/TrainPoseExportV1Mapper.cs`).
- Export validator enforces matrix finiteness and canonical bottom row (`Quantum.IO/TrainPose/V1/TrainPoseExportV1Validator.cs`).

## Coaster-Domain-Specific Logic That Should Stay Custom

These are not generic math-library replacements; they encode Quantum coaster semantics and should remain custom:
- Global-distance to segment/local-`t` resolution and clamping semantics (`Quantum.Track/Internal/CompiledTrackSamplingContext.cs`, `Quantum.Track/TrackEvaluator.cs`).
- Frame construction rules with roll application and fallback axes (`Quantum.Track/TrackEvaluator.cs`).
- Generic curve-frame sampling policy and fallback normal behavior (`Quantum.Splines/CurveFrameSampler.cs`).
- Distance-based train placement and spacing semantics (body/bogie/articulation/wheel layout solvers under `Quantum.Track/Internal`).
- Canonical transported coaster-frame history and post-transport roll application (`Quantum.Track/Internal/CanonicalTransportedFrameSampler.cs`).
- Train pose data contracts and export mapping (`Quantum.IO/TrainPose/V1/*`).

## What Currently Duplicates General Numerical Library Behavior

General-purpose behavior currently implemented in-house:
- Basic vector/matrix primitives and operations (`Vector3d`, `Matrix3x3`, `Transform3d`).
- Finite checks and normalization helpers repeated across modules (`TrackEvaluator`, `TrackFrameSampler`, `CameraFrameBuilder`, articulation solver).
- Generic spline basis evaluation:
  - custom `BSplineCurve` basis/span implementation (`Quantum.Splines/Curves/BSplineCurve.cs`)
  - custom legacy `NurbsCurve` basis/span rational evaluation (`Quantum.Splines/Curves/NurbsCurve.cs`)
- Numerical approximation paths:
  - finite-difference tangents in B-spline/NURBS implementations
  - finite-difference curvature fallback in `TrackPhysicsAdapter.TryGetCurvatureAtDistance(...)`
  - sampled arc-length LUT mapping in `ArcLengthLUT`/`ArcLengthCurveAdapter`
- Generic easing/interpolation kernels:
  - `CurveEasing` (`Quantum.Splines/CurveEasing.cs`)
  - `ForceInterpolationEvaluator` + keyframed easing (`Quantum.Track/ForceInterpolationEvaluator.cs`, `Quantum.Track/KeyframedForceEasingFunction.cs`)

## What Might Use Math.NET Later

Math.NET is not currently referenced in project files. If introduced later, low-risk candidates are:
- Linear algebra utility consolidation:
  - matrix/vector helper operations where this is truly generic, not domain contract behavior.
- Numerical derivative/integration helpers:
  - replace ad-hoc finite-difference patterns where stability gains are measurable.
- Interpolation kernels for non-domain-specific channels:
  - force/easing internals that do not encode coaster rules.

Areas to avoid replacing with Math.NET:
- `TrackEvaluator` distance semantics and frame policy.
- train placement solvers (`TrainCarBodySampler`, `TrainBogieTransformSolver`, `TrainArticulationFrameSolver`, `TrainWheelTransformLayoutSolver`).
- track/export contracts and data model shapes.

## Tests Protecting Frame/Transform/Train Placement Behavior

Core matrix/transform correctness:
- `Quantum.Tests/Math/Matrix3x3Tests.cs`: identity, basis mapping, orthonormal frame-basis behavior.
- `Quantum.Tests/Math/Transform3dTests.cs`: transform direction/point behavior, `FromTrackFrame` basis mapping.
- `Quantum.Tests/Track/TrackFrameTests.cs`: `TrackFrame -> Matrix4x4` convention, orthonormality, non-finite guards.

Frame sampling and distance semantics:
- `Quantum.Tests/Splines/TrackFrameSamplerTests.cs`: generic `CurveFrame` sampling orthonormality.
- `Quantum.Tests/Track/TrackEvaluatorFrameTests.cs`: frame orthonormality, tangent alignment, roll behavior, fallback behavior.
- `Quantum.Tests/Track/TrackEvaluatorSplineTransformTests.cs`: transform position and rotation behavior for spline/fallback paths.
- `Quantum.Tests/Track/TrackEvaluatorDistanceSemanticsContractTests.cs`: public frame distances remain clamped global station distances while support-layer spline frames preserve local `S`.
- `Quantum.Tests/Track/TrackEvaluatorEvaluateAtDistanceCharacterizationTests.cs`: edge-case behavior (negative, non-finite, empty document, zero-length segment).
- `Quantum.Tests/Track/TrackEvaluatorBatchSamplingParityTests.cs`: batch APIs preserve scalar semantics and ordering.
- `Quantum.Tests/Track/TrackEvaluatorTrackFrameOutputTests.cs`: bound evaluator frame output finite + orthonormal.

Train placement and matrix policy:
- `Quantum.Tests/Track/TrainCarTransformProviderTests.cs`: car/bogie/wheel/articulated placement, distance spacing, matrix parity (`ToMatrix4x4` / `Matrix4x4d.FromMatrix4x4`), orthonormal/finite checks.
- `Quantum.Tests/Track/GSharkTrainCarSpacingParityTests.cs`: train spacing/frame parity for legacy vs G-Shark NURBS paths.

Physics/adapter transform parity:
- `Quantum.Tests/Physics/TrackPhysicsAdapterTests.cs`: adapter frame/transform parity with `TrackEvaluator`, curvature stability.
- `Quantum.Tests/Physics/TrackFrameProviderDistanceSemanticsContractTests.cs`: physics provider preserves canonical clamped global station distance semantics.

## What Should Not Be Deleted Yet

Do not delete these yet; they remain active integration points or parity baselines:
- `Quantum.Math/Matrix4x4d.cs`: required by bogie/wheel/articulated transform models and IO export mapping.
- `Quantum.Math/Transform3d.cs`: still used by `TrackEvaluator`, physics adapter, and transform contract tests.
- `Quantum.Math/Matrix3x3.cs`: required by `Transform3d`, `TrackEvaluator` fallback transform path, and math tests.
- `Quantum.Math/ITrackFrameBasis.cs`: required cross-project bridge for `Transform3d.FromTrackFrame(...)` without project cycles.
- `Quantum.Splines.TrackFrame` and `TrackFrameSampler`: obsolete compatibility surfaces retained until a later breaking cleanup.
- `Quantum.Splines/Curves/NurbsCurve.cs`: still used as legacy parity baseline in FVD/adapter migration path.
- `Quantum.Splines/Curves/GSharkNurbsCurveAdapter.cs` and `Quantum.Splines/GSharkVector3dConversions.cs`: active production integration path and parity coverage.
- `Quantum.FVD/FvdNurbsBuildResult.cs` and `Quantum.FVD/FvdGraph.cs` legacy return surfaces: still expose/use legacy `NurbsCurve` while arc-length runtime path also uses G-Shark.
- High-signal contract tests listed above, especially:
  - `TrackEvaluator*Distance*` contract tests
  - `TrainCarTransformProviderTests`
  - `GShark*ParityTests`
