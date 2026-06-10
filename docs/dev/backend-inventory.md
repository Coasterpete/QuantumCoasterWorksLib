# Backend Inventory / Polish Audit

Date: 2026-05-27

Scope: `Quantum.Core` plus nearby active backend projects: `Quantum.Math`, `Quantum.Splines`, `Quantum.Track`, `Quantum.Physics`, `Quantum.FVD`, `Quantum.IO`, and the optional `Quantum.Debug` CLI surface where it affects backend contracts. `Quantum.Geometry` is discussed as a reserved package name, not an active project.

This is an inventory and cleanup report only. No backend code was changed as part of this audit.

## Historical Validation Results

- `dotnet build QuantumCoasterWorks.sln`
  - Result: passed
  - Warnings: 0
  - Errors: 0
- `dotnet test QuantumCoasterWorks.sln --no-build`
  - Result: passed
  - Passed: 850 (historical snapshot)
  - Failed: 0
  - Skipped: 0

## Current Backend Shape

Project references are mostly layered and engine-agnostic:

| Project | References | Notes |
| --- | --- | --- |
| `Quantum.Core` | none | Small guard/numeric helper package. |
| `Quantum.Math` | none | Custom vector/matrix/transform primitives. |
| `Quantum.Splines` | `Quantum.Math`, `GShark` | Curve primitives, arc-length adapter, G-Shark adapter, support frame sampler. |
| `Quantum.Track` | `Quantum.Math`, `Quantum.Splines` | Main coaster-domain surface: tracks, sections, train transforms, debug geometry, cameras. |
| `Quantum.Physics` | `Quantum.Core`, `Quantum.Math`, `Quantum.Splines`, `Quantum.Track` | Train stepping and force/track adapters. |
| `Quantum.FVD` | `Quantum.Math`, `Quantum.Splines`, `Quantum.Physics` | FVD-style graph, sections, force-target adapter, prototype solver. |
| `Quantum.IO` | `Quantum.Math`, `Quantum.Track`, `System.Text.Json` | Versioned JSON contracts and fixture parsing. |
| `Quantum.Debug` | `Quantum.IO`, `Quantum.Math`, `Quantum.Physics`, `Quantum.Splines`, `Quantum.Track` | Optional CLI/debug artifact generator. |

No `Quantum.*` backend project references Unity, UnityEditor, Avalonia, Silk.NET, OpenTK, Unreal, or renderer packages. Unity usage is confined to `Assets/Scripts/QuantumVisualizer`.

## 1. Namespace And Package Organization Problems

### `Quantum.Core` Exists But Is Not Yet A Shared Foundation

`Quantum.Core` has the right intent, but usage is uneven. `Quantum.Physics` uses `Guard` and `Numeric`, while `Quantum.Track`, `Quantum.FVD`, `Quantum.Splines`, and `Quantum.IO` repeat local finite/positive validation helpers.

Examples:

- `Quantum.Core/Guard.cs:5`
- `Quantum.Core/Numeric.cs:5`
- `Quantum.Track/TrainValidation.cs:3`
- `Quantum.Track/TrackEvaluator.cs:421`
- `Quantum.Splines/TrackFrameSampler.cs:193`
- `Quantum.FVD/Fvd2dNormalGSolver.cs:399`
- `Quantum.IO/TrainPose/V1/TrainPoseExportV1Validator.cs:549`

This is not urgent behavior debt, but it is polish debt. The backend already has a small foundation package; cleanup should either adopt it consistently or keep it intentionally narrow and avoid pretending it is a universal core.

### `Quantum.Geometry` Is Reserved, Not Active

Milestone 30 removed the empty `Quantum.Geometry` project from the active solution and test graph. The package name remains reserved for a future narrow backend-only geometry role if one becomes necessary, but Quantum should not claim a geometry package until it has real coaster-domain responsibility.

### `Quantum.Track` Is Carrying Several Subsystems In One Namespace

`Quantum.Track` contains:

- Centerline documents and segments
- Section and force channel systems
- Train car, bogie, wheel, and articulated placement
- Debug gizmo builders
- Camera transform builders
- Smoothness diagnostics

The package can stay as the coaster-domain package, but the namespace is broad. Candidates for later sub-namespaces or folders:

- `Quantum.Track.Trains`
- `Quantum.Track.Sections`
- `Quantum.Track.Diagnostics`
- `Quantum.Track.Cameras`

Do not split this now unless the move is purely mechanical and covered by tests. The current train-placement milestone benefits more from stable behavior than from namespace churn.

### Legacy Spline `TrackFrame` Is Isolated Compatibility Debt

The public runtime boundary now uses one coaster frame:

- `Quantum.Track.TrackFrame` uses global station `Distance`
- `Quantum.Splines.CurveFrame` uses generic curve arc-length `S`

The old `Quantum.Splines.TrackFrame` name remains only through obsolete
compatibility types and `TrackEvaluator` overloads. Physics, train, debug, and
export runtime paths use `Quantum.Track.TrackFrame` directly.

This transition debt stays visible until a later breaking cleanup removes those
obsolete members.

### `Quantum.Debug` Public Types Look Like A Library Surface

The debug CLI command classes are public and undocumented:

- `Quantum.Debug/DebugCommandParser.cs:20`
- `Quantum.Debug/DebugViewportSnapshotV1SampleCommand.cs:10`
- `Quantum.Debug/SamplingPerfCommand.cs:9`
- `Quantum.Debug/TrainPoseExportV1Command.cs:12`

This is fine for tests today, but it exposes a lot of command implementation as public API. Longer term, either document these as supported debug APIs or make them internal with an explicit test-access strategy.

## 2. Public API Polish Issues

### Public Boundary Is Better Than The Raw Namespace Map

The best current public lane is already documented and tested:

- `Quantum.Track.TrackFrame`
- bound `TrackEvaluator` station-distance APIs
- `TrackDocument` / `TrackSegment`
- `TrainCarTransformProvider.EvaluateTrainPose`
- `TrainPoseExportV1`

`Quantum.Tests/Track/CoasterApiBoundaryContractTests.cs` is a useful guard for this.

### `TrackEvaluator` Has Overloads With Different Return Frame Types

These overloads are easy to confuse:

- `EvaluateFrameAtDistance(double)` returns `Quantum.Track.TrackFrame`
- `EvaluateFrameAtDistance(TrackDocument, double)` returns `Quantum.Splines.TrackFrame`
- `EvaluateFramesAtDistances(IReadOnlyList<double>)` returns `Quantum.Track.TrackFrame[]`
- `EvaluateFramesAtDistances(TrackDocument, IReadOnlyList<double>)` returns `Quantum.Splines.TrackFrame[]`

The `Quantum.Track.TrackFrame` overloads expose the public coaster-domain
contract: `Distance` is the requested clamped global station distance. The
`Quantum.Splines.TrackFrame` overloads are obsolete support-layer compatibility
APIs; their `S` value may be segment-local. Active coaster consumers use
`Quantum.Track.TrackFrame`, while generic spline sampling uses `CurveFrame`.

### `TrackDocument` Is Mutable At The Core Boundary

`TrackDocument.Segments` and `TrackDocument.Sections` expose `IList<T>`:

- `Quantum.Track/TrackDocument.cs:31`
- `Quantum.Track/TrackDocument.cs:36`

This is simple and useful during prototyping, but it means consumers can mutate documents after construction and between evaluator calls. That is probably acceptable now, but later stability could benefit from either a builder/snapshot pattern or clearer mutation expectations.

### Segment Construction Does Not Validate Length/Roll

`TrackSegment` accepts `length` and `rollRadians` without finite/non-negative validation:

- `Quantum.Track/TrackSegment.cs:20`

Several downstream evaluators reject bad values later. A small future cleanup could add constructor validation to `TrackSegment` or to concrete segment constructors, but this would be behavior-changing and should be test-backed.

### Train Transform API Has Both `Get*` And `Evaluate*`

`TrainCarTransformProvider` exposes both:

- `GetCarTransforms(...)`
- `EvaluateCarTransforms(...)`

`EvaluateCarTransforms` is the preferred deterministic API. `GetCarTransforms`
remains as an obsolete forwarding alias for compatibility.

### `TrainPoseResult` Could Guard Runtime Nulls More Explicitly

`TrainPoseResult` takes a non-nullable `TrainConsistDefinition` and array, but does not runtime-check either:

- `Quantum.Track/TrainPoseResult.cs:19`

This is low priority, but it is a good example of public API polish that can be fixed safely with tests.

### `ForceSection` Is A Compatibility Constructor With Many Optional Concepts

`ForceSection` currently accepts scalar targets, start/end values, easing functions, channel containers, distance/time domain, and longitudinal fields in one constructor:

- `Quantum.Track/ForceSection.cs:3`

This preserves compatibility, but it reads as a prototype-friendly data bag. The section pipeline docs already point toward normalized channel sections. Future cleanup should add clearer construction paths before expanding this further.

### `Vector3d` Is Public And Mutable

`Vector3d` is a public `struct` with public fields:

- `Quantum.Math/Vector3d.cs:10`

The type is small and heavily used, so avoid changing it casually. Still, public mutable fields are a long-term API commitment. If `Quantum.Math` stays public, decide whether this is intentionally a lightweight data struct or whether it should become an immutable value type.

### Matrix Precision Policy Is Documented But Mixed

The current mix is intentional:

- `TrainCarTransform.Matrix` uses `System.Numerics.Matrix4x4`
- bogie, wheel, and articulated transforms use `Matrix4x4d`

This is documented in code and `docs/math-backend-status.md`, but downstream consumers will notice. Keep the policy explicit in docs and tests until a single policy becomes practical.

## 3. Naming Inconsistencies

- `Quantum.Splines.CurveFrame.S` vs `Quantum.Track.TrackFrame.Distance`; `S` is generic curve arc length, while public `Distance` is clamped global coaster station distance.
- `LocalT`, `t`, `U`, `EvaluationX`, `StartX`, `EndX`, `distance`, and `station` all appear as coordinate names. They mean different things, but the distinctions are not always discoverable from API names alone.
- `FVD` namespace vs `Fvd*` type prefix is acceptable C# casing, but it still reads slightly mixed.
- `GetFrameAtDistance`, `EvaluateFrameAtDistance`, `Sample`, `TryGetForceTargets`, and `Build*` are all present. Most are defensible individually, but new APIs should follow a documented convention:
  - `Evaluate*` for deterministic pure computation that may throw.
  - `Try*` for non-throwing optional lookup.
  - `Sample*` for producing time/distance samples from a provider.
  - `Build*` for assembling artifacts or static data structures.
- `NormalG`, `LateralG`, `LongitudinalG`, and `RollRateDegPerSec` are clear, but force channel enum/property names should keep exact casing across `Quantum.Track`, `Quantum.Physics`, `Quantum.FVD`, and `Quantum.IO`.
- `TrainCarWithBogiesTransform`, `TrainCarWithBogiesAndWheelsTransform`, and `ArticulatedTrainCarWithWheelsTransform` are long but descriptive. If they remain public, XML docs matter more than renaming.

## 4. Dead, Duplicate, Or Experimental Code

### Clear Dead Code

- No active `Quantum.Geometry` project remains in the solution.

### Duplicate Or Transitional Math/Spline Code

- `Quantum.Splines/Curves/NurbsCurve.cs:7`: custom legacy NURBS implementation.
- `Quantum.Splines/Curves/BSplineCurve.cs:7`: custom B-spline implementation.
- `Quantum.Splines/Curves/GSharkNurbsCurveAdapter.cs:12`: mature-library-backed NURBS path.
- `Quantum.FVD/FvdGraph.cs:145`: still builds legacy `NurbsCurve`.
- `Quantum.FVD/FvdGraph.cs:146`: also builds `GSharkNurbsCurveAdapter`.

The docs say the legacy NURBS curve remains a parity baseline. Keep it for now, but do not expand it unless there is a coaster-specific need.

### Duplicate Frame Contracts

- `Quantum.Splines/TrackFrame.cs:8`
- `Quantum.Track/TrackFrame.cs:16`

Both have a reason today, but every new public API should avoid adding a third frame-like shape unless it is a versioned IO DTO.

### Section Model Overlap

There are multiple section representations:

- `TrackSection` and `ForceSection`
- `GeometricSection`
- normalized `SectionDefinition` / `SectionFunction`
- FVD `FvdSectionDefinition` / `FvdSectionFunction`

This is known transition debt from the section pipeline work. The cleanup should be consolidation-by-adapter, not a rewrite.

### Prototype Solver Surface

`Fvd2dNormalGSolver` is explicitly described as a prototype:

- `Quantum.FVD/Fvd2dNormalGSolver.cs:9`
- `Quantum.FVD/Fvd2dNormalGSolverOptions.cs:6`

It is public and has tests, but it should remain marked experimental until its desired role is clearer.

### Debug And Preview Code In Backend Packages

Renderer-agnostic debug geometry is acceptable, but several debug/presentation helpers live as public types in backend packages:

- `Quantum.Track/DebugTrackContinuousSampler.cs:16`
- `Quantum.Track/TrackFrameDebugGizmoBuilder.cs:7`
- `Quantum.Track/TrainCarDebugGizmoBuilder.cs:7`
- `Quantum.Track/CameraFrameBuilder.cs:10`

These are useful for the current milestone. Later, consider whether they are stable backend diagnostics or should move to a `Quantum.Debugging` style package.

### Adjacent Adapter Duplication

`Assets/Scripts/QuantumVisualizer/TrainPoseExportV1Dtos.cs` duplicates DTOs for Unity consumption. This is outside backend, but it is worth tracking because versioned IO contracts can drift if copied manually.

## 5. Missing XML Docs On Public Types

There is no shared `Directory.Build.props`, `.editorconfig`, or documentation warning policy. The projects also do not set `GenerateDocumentationFile`, so missing XML docs do not produce build warnings.

Approximate public type doc coverage from source scanning:

| Project | Public types scanned | Public types missing type XML docs |
| --- | ---: | ---: |
| `Quantum.Core` | 2 | 2 |
| `Quantum.Math` | 6 | 0 |
| `Quantum.Splines` | 19 | 5 |
| `Quantum.Track` | 69 | 48 |
| `Quantum.Physics` | 14 | 0 |
| `Quantum.FVD` | 17 | 11 |
| `Quantum.IO` | 38 | 28 |
| `Quantum.Debug` | 15 | 15 |

Highest-priority missing docs:

- `Quantum.Core/Guard.cs:5`
- `Quantum.Core/Numeric.cs:5`
- `Quantum.Track/TrackPosition.cs:3`
- `Quantum.Track/TrackEvaluationPoint.cs:3`
- `Quantum.Track/TrackEvaluationResult.cs:3`
- `Quantum.Track/TrackSection.cs:3`
- `Quantum.Track/ForceSection.cs:3`
- `Quantum.Track/TrainConsistDefinition.cs:5`
- `Quantum.Track/TrainCarWithBogiesTransform.cs:3`
- `Quantum.Track/TrainCarWithBogiesAndWheelsTransform.cs:3`
- `Quantum.Track/ArticulatedTrainCarWithWheelsTransform.cs:3`
- `Quantum.IO/TrainPose/V1/TrainPoseExportV1Dtos.cs:36`
- `Quantum.IO/TrainPose/V1/TrainPoseExportV1Validator.cs:7`
- `Quantum.IO/DebugViewport/V1/DebugViewportSnapshotV1Dtos.cs:36`
- `Quantum.FVD/FvdSectionEnums.cs:3`
- `Quantum.FVD/Fvd2dNormalGSolverStatus.cs:3`

Do not try to document every member at once. Start with public boundary types and versioned IO contracts.

## 6. Missing Or Weak Tests

### Strong Existing Coverage

At the time of this historical inventory snapshot, the suite was strong for the milestone:

- `Quantum.Tests/Track`: 37 test files
- `Quantum.Tests/Physics`: 10 test files
- `Quantum.Tests/IO`: 6 test files
- `Quantum.Tests/FVD`: 6 test files
- 850 total passing tests in that snapshot

Particularly valuable:

- `CoasterApiBoundaryContractTests`
- `TrackEvaluator*Distance*` tests
- `TrackEvaluatorBatchSamplingParityTests`
- `TrackFrame*` tests
- `TrainCarTransformProviderTests`
- `TrainCarTransformProviderCompositeDocumentTests`
- `GSharkTrainCarSpacingParityTests`
- `TrainPoseExportV1*` tests

### Gaps To Add

- Backend-wide forbidden dependency test. Current guard only checks `Quantum.IO` and `Quantum.Debug` in `CenterlineFrameCsvFixtureParserTests`. Expand it to all backend assemblies.
- `Quantum.Geometry` has no tests because it is intentionally absent from the active solution.
- No automated XML-doc coverage or package public-surface approval test.
- No shared build/analyzer configuration to keep nullable, docs, formatting, and API warnings consistent across projects.
- Public constructor edge tests are incomplete for API polish cases:
  - `TrackSegment` invalid length/roll at construction if validation is added.
  - `TrainPoseResult` null definition/cars runtime behavior.
  - `ForceSection` invalid domain/duration/channel combinations.
  - DTO default validity expectations for versioned contracts.
- Large "kitchen sink" test files are hard to review:
  - `Quantum.Tests/Physics/TrainStepLoopTests.cs`: 1550 lines
  - `Quantum.Tests/FVD/FvdSectionEvaluationTests.cs`: 1400 lines
  - `Quantum.Tests/Track/TrainCarTransformProviderTests.cs`: 1237 lines
  - `Quantum.Tests/Core/FoundationTests.cs`: 1042 lines

These do not need immediate splitting, but future changes will be easier if fixtures/helpers are extracted gradually.

## 7. Files That Feel Prototype-Quality

| File | Why it feels prototype-quality | Suggested handling |
| --- | --- | --- |
| `Quantum.Track/TrackSection.cs` | Empty abstract base. | Keep only if it remains the document extension point; add docs or normalize. |
| `Quantum.Track/ForceSection.cs` | Large optional constructor, multiple compatibility representations, mutable `Channels`. | Add clearer factories/builders before expanding. |
| `Quantum.Splines/Curves/BSplineCurve.cs` | Custom generic B-spline implementation, missing docs. | Keep as debug/sample support or replace with mature path. |
| `Quantum.Splines/Curves/NurbsCurve.cs` | Custom legacy NURBS implementation now paired with G-Shark. | Keep as parity baseline only; avoid new dependencies on it. |
| `Quantum.FVD/Fvd2dNormalGSolver.cs` | Public, long, explicitly prototype, finite-difference solver. | Keep experimental; document status and avoid making it core contract. |
| `Quantum.Track/DebugTrackContinuousSampler.cs` | Large debug sampler in `Quantum.Track`, public surface. | Keep during current debug milestone; consider diagnostic namespace/package later. |
| `Quantum.Track/TrackFrameSmoothnessDiagnostics.cs` | Large diagnostics class with many public report types missing docs. | Useful, but should be documented if public. |
| `Quantum.Debug/*Command.cs` | Public CLI command implementation types. | Internalize or document as debug API. |
| `Quantum.Tests/Core/FoundationTests.cs` | Broad reflection-based contract and behavior tests in one file. | Split opportunistically as touched. |

## 8. Suggested Cleanup Milestones

### P0 - Keep The Current Behavior Green

- Do not rewrite the backend architecture.
- Keep the train placement path focused on stable point, tangent, frame, and distance-based car placement.
- Preserve current `dotnet build` and `dotnet test` status.

### P1 - Tiny Inventory Cleanup

- Keep `Quantum.Geometry` out of the active solution until it has a real scoped backend purpose.
- Add XML docs to `Quantum.Core.Guard` and `Quantum.Core.Numeric`.
- Add a backend-wide forbidden dependency test for all `Quantum.*` assemblies.
- Add a short doc comment to the most visible missing train-pose wrapper types.

### P2 - Public API Boundary Polish

- Remove obsolete spline `TrackFrame` compatibility members in a future breaking release.
- Add runtime null guards to public result/data constructors where safe.
- Document `TrackDocument` mutation expectations.

### P3 - Section Model Consolidation

- Treat `ForceSection` scalar fields as compatibility shorthands over normalized channel sections.
- Keep `SectionDefinition` and FVD section semantics aligned.
- Avoid adding more optional constructor parameters to `ForceSection`; prefer named factories/builders.
- Add tests that lock down section domain and channel precedence before refactoring.

### P4 - Math And Spline Debt Reduction

- Keep `GSharkNurbsCurveAdapter` as the active mature NURBS path.
- Stop growing custom `NurbsCurve` and `BSplineCurve` beyond parity/debug needs.
- Consolidate repeated finite/vector validation helpers only where it reduces duplication without obscuring domain behavior.
- Consider a shared numeric/vector guard after the public boundary is stable.

### P5 - Debug Surface Separation

- Decide whether debug gizmo builders and camera builders are stable backend diagnostics or optional debug tooling.
- If stable, document them.
- If optional, consider a dedicated diagnostics/debug package later.
- Keep Unity-specific code outside backend packages.

### P6 - Test Suite Maintainability

- Split very large test files only when touching related behavior.
- Extract deterministic track/train fixture builders.
- Add public API approval or doc-coverage tests if API churn becomes a problem.
- Consider coverage thresholds only after the suite is less fixture-heavy.

