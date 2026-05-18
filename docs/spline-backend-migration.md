# Spline Backend Migration Status

As of 2026-05-12, the spline backend is in a mixed state: runtime arc-length sampling in FVD is already using the G-Shark adapter, while legacy `NurbsCurve` is still kept for compatibility and parity.

## Current Active Backend Paths

1. Track runtime path (train boxes, frames, transforms):
   - `Quantum.Track/TrackSegment.cs:27` stores segment spline as `IParamCurve`.
   - `Quantum.Track/TrackEvaluator.cs:198` and `Quantum.Track/TrackEvaluator.cs:246` evaluate `segment.Spline` directly when present.
   - `Quantum.Track/Internal/TrainCarBodySampler.cs:61` and `Quantum.Track/Internal/TrainBogieTransformSolver.cs:45` consume distance-sampled frames from `TrackEvaluator`.
   - `Quantum.Physics/TrackPhysicsAdapter.cs:26` samples frame/transform from `TrackEvaluator` for physics.

2. FVD NURBS build path:
   - `Quantum.FVD/FvdGraph.cs:145` builds a legacy `NurbsCurve` (`ParamCurve` compatibility surface).
   - `Quantum.FVD/FvdGraph.cs:146` builds `GSharkNurbsCurveAdapter`.
   - `Quantum.FVD/FvdGraph.cs:147` wraps the G-Shark curve in `ArcLengthCurveAdapter` for distance sampling.
   - `Quantum.FVD/Fvd2dNormalGSolver.cs:275` consumes `BuildNurbsCurve(...).ArcCurve`.

3. Section-generated geometric fallback path (non-NURBS):
   - `Quantum.Track/GeometricSection.cs:24` generates section curves (`LineCurve` or constant-curvature arc).
   - `Quantum.Track/CompositeSectionCurve.cs:12` composes resolved section intervals into an `IArcLengthCurve`.
   - `Quantum.Track/SectionCurveAssembler.cs:8` is the assembly entrypoint.

## Legacy Reference Classes

- `Quantum.Splines/Curves/NurbsCurve.cs` remains the legacy in-house NURBS evaluator.
- `Quantum.FVD/FvdNurbsBuildResult.cs:8` still exposes `ParamCurve` as concrete `NurbsCurve`.
- `Quantum.FVD/FvdNurbsBuildResult.cs:12` constructor still requires `NurbsCurve`.
- Contract/parity tests still reference legacy behavior and type presence:
  - `Quantum.Tests/FVD/FvdFoundationTests.cs:187`
  - `Quantum.Tests/Splines/GSharkNurbsCurveAdapterTests.cs:33`
  - `Quantum.Tests/Track/GSharkTrainCarSpacingParityTests.cs:25`
  - `Quantum.Tests/Core/FoundationTests.cs:1215`

## G-Shark Adapter Status

- Package dependency is active: `Quantum.Splines/Quantum.Splines.csproj:13` (`GShark` 2.3.1).
- Adapter implementation is present: `Quantum.Splines/Curves/GSharkNurbsCurveAdapter.cs`.
- Conversion bridge is present: `Quantum.Splines/GSharkVector3dConversions.cs`.
- Production usage is active in FVD arc-length sampling path (`FvdGraph -> ArcLengthCurveAdapter -> Fvd2dNormalGSolver`).
- Parity coverage exists (legacy-vs-adapter value checks and train spacing/frame checks):
  - `Quantum.Tests/Splines/GSharkNurbsCurveAdapterTests.cs`
  - `Quantum.Tests/Track/GSharkTrainCarSpacingParityTests.cs`

## Remaining Call Sites

Production legacy-touching call sites:
- `Quantum.FVD/FvdGraph.cs:145` (`new NurbsCurve(...)`)
- `Quantum.FVD/FvdNurbsBuildResult.cs:8` (`ParamCurve` concrete type)
- `Quantum.FVD/FvdNurbsBuildResult.cs:12` (constructor signature requires `NurbsCurve`)

Test/reference legacy call sites:
- `Quantum.Tests/Splines/GSharkNurbsCurveAdapterTests.cs:33`
- `Quantum.Tests/Track/GSharkTrainCarSpacingParityTests.cs:25`
- `Quantum.Tests/FVD/FvdFoundationTests.cs:187`
- `Quantum.Tests/Core/FoundationTests.cs:266`

Production G-Shark call sites:
- `Quantum.FVD/FvdGraph.cs:146`
- `Quantum.FVD/FvdGraph.cs:147`

## Migration Rules

- Keep changes small and reviewable; migrate one call site or one public surface at a time.
- Preserve deterministic behavior for point/tangent/frame and distance-based placement.
- Prefer `IParamCurve`/`IArcLengthCurve` abstractions at integration boundaries.
- Do not rewrite `TrackEvaluator`, `TrainCarTransformProvider`, or physics integration as part of spline backend migration.
- Maintain parity tests whenever a legacy-backed call site is migrated.
- Keep adapter semantics and clamp/finite validation behavior stable during migration.

## What Should Not Be Deleted Yet

- `Quantum.Splines/Curves/NurbsCurve.cs` (legacy parity baseline and compatibility surface).
- `Quantum.FVD/FvdNurbsBuildResult.cs` (current contract surface still exposes `ParamCurve`).
- `Quantum.FVD/FvdGraph.cs::BuildNurbsCurve(int)` public API shape.
- `Quantum.Splines/Curves/GSharkNurbsCurveAdapter.cs` and `Quantum.Splines/GSharkVector3dConversions.cs`.
- Migration/parity tests:
  - `Quantum.Tests/FVD/FvdFoundationTests.cs`
  - `Quantum.Tests/Splines/GSharkNurbsCurveAdapterTests.cs`
  - `Quantum.Tests/Track/GSharkTrainCarSpacingParityTests.cs`
