# Quantum Library Capability Inventory

Date: 2026-05-29

Milestone: 29 - Quantum library capability inventory

Scope reviewed:

- `Quantum.Core`
- `Quantum.Math`
- `Quantum.Splines`
- `Quantum.Track`
- `Quantum.FVD`
- `Quantum.Physics`
- `Quantum.IO`
- `Quantum.Debug`
- `Quantum.Tests`

Adjacent note: `Quantum.Geometry` is not an active project after the Milestone 30 scope decision. The package name is reserved for a future narrow backend-only geometry role if one becomes necessary, but it should not be counted as a current library capability.

## Validation

Requested command:

```powershell
dotnet test Quantum.Tests\Quantum.Tests.csproj --no-restore
```

Result after a one-time restore in this fresh checkout:

- Passed: 886
- Failed: 0
- Skipped: 0
- Total: 886

Environment note: the first no-restore invocation in this checkout returned a zero exit code but produced only a build message because NuGet assets had not been restored yet under the installed .NET SDK 10.0.300. A one-time `dotnet restore Quantum.Tests\Quantum.Tests.csproj` was needed before the exact requested no-restore test command produced a real test run.

## 1. Current Working Capabilities

### Backend Project Structure

The backend projects are engine-agnostic C# libraries. `Quantum.Tests/Core/BackendDependencyContractTests.cs` explicitly checks that the backend assemblies do not reference Unity, UnityEditor, Unreal, Avalonia, Silk.NET, OpenTK, or Veldrid assemblies.

Current project layering is mostly coherent:

- `Quantum.Core`: small validation/numeric foundation.
- `Quantum.Math`: lightweight vector, matrix, and rigid transform support.
- `Quantum.Splines`: curve interfaces, simple curve types, arc-length adapters, frame sampling, and G-Shark NURBS adapter support.
- `Quantum.Track`: primary coaster-domain model and sampling/placement surface.
- `Quantum.FVD`: FVD-style control graph, section/channel evaluation, force target adapter, and prototype Normal-G solver.
- `Quantum.Physics`: early train follower stepping and force/track adapters.
- `Quantum.IO`: versioned JSON contracts and sampled-frame CSV fixture parsing.
- `Quantum.Debug`: backend-only CLI/debug artifact generation.
- `Quantum.Tests`: xUnit validation, contract tests, fixture tests, and regression tests.

### `Quantum.Core`

Working today:

- Finite, positive, non-negative, and minimum integer guard helpers.
- Basic finite-number helper through `Numeric.IsFinite`.

Capability level: small but usable. It is a support package, not a domain system.

### `Quantum.Math`

Working today:

- `Vector3d` with common vector operations.
- `Matrix3x3` basis matrix operations.
- `Matrix4x4d` double-precision storage and conversion from `System.Numerics.Matrix4x4`.
- `Transform3d` rigid transform operations and `FromTrackFrame`.
- `ITrackFrameBasis` bridge interface used to avoid reference cycles.
- `MathUtil` epsilon, clamp, and lerp helpers.

Capability level: sufficient for the current centerline/frame/train-pose milestone. It is intentionally lightweight and should not be described as a full math library.

### `Quantum.Splines`

Working today:

- Generic curve interfaces: `IParamCurve`, `IArcLengthCurve`, and curvature support.
- Simple curve implementations: line, quadratic Bezier, cubic Bezier, eased parameter curve.
- Arc-length lookup and adapter support.
- Track frame sampling over arc-length curves.
- G-Shark-backed NURBS adapter path.
- Custom B-spline/NURBS implementations that still act as compatibility/parity baselines.

Capability level: useful support layer for the current backend. The mature-library-backed NURBS path is the one to prefer for future growth; the custom B-spline/NURBS code should not be expanded as if Quantum were a general spline library.

### `Quantum.Track`

Working today:

- Mutable `TrackDocument` with ordered `TrackSegment` list and optional section list.
- Straight/curved segment shells with length, id, force reference, optional spline, and roll.
- Station-distance evaluation through `TrackEvaluator`.
- Segment-local evaluation through `TrackPosition` and `TrackEvaluationPoint`.
- Bound evaluator APIs that return the coaster-facing `Quantum.Track.TrackFrame`.
- Frame construction with tangent/normal/binormal basis, roll application, fallback axes, and matrix conversion.
- Batch sampling parity with scalar sampling.
- Distance semantics: finite out-of-range distances clamp, non-finite distances reject, empty documents reject.
- Deterministic section resolution, normalized section definitions, and channel evaluation.
- `ForceSection` sampling with scalar, easing, channel, multi-channel, blend-mode, distance-domain, and opt-in time-domain paths.
- `GeometricSection` to composite curve assembly for simple section-driven geometry.
- Distance-based train placement:
  - car body placement from lead distance and car spacing
  - front/rear bogie placement
  - wheel layout from bogie frames
  - articulated body frame generation
  - complete `TrainPoseResult`
- Renderer-neutral debug geometry:
  - frame axes
  - rail cross ties
  - banking ribbon
  - train wire boxes
- Backend frame diagnostics:
  - smoothness analysis
  - continuity analysis
- Renderer-neutral camera transform builders:
  - ride camera
  - target camera
  - fly-by camera
  - b-roll camera
  - fly-view and walk-view state transforms

Capability level: this is the strongest and most complete domain area. It can honestly support a technical debug preview of sampled centerlines, stable frames, and placeholder train poses.

Important limitation: track authoring is still primitive. The library has building blocks and deterministic sampling, not a complete coaster track designer.

### `Quantum.FVD`

Working today:

- FVD control node and force sample data shapes.
- `FvdGraph` validation for sorted control nodes, weights, positions, force samples, and section overlap.
- NURBS build result that exposes legacy and G-Shark-backed runtime curves.
- FVD-style section definitions and functions with deterministic channel evaluation.
- Strict and permissive force-target evaluation.
- Adapter from FVD force targets to `Quantum.Physics.IForceTargetProvider`.
- Prototype 2D Normal-G solver that performs a limited single-step adjustment of interior control-node Y values.

Capability level: useful early FVD data and section infrastructure. The graph/section pieces are more credible than the solver. The solver is explicitly prototype-quality and should not be described as a complete FVD optimizer.

### `Quantum.Physics`

Working today:

- `TrainFollowerState` tracks distance, speed, acceleration, loop behavior, and diagnostic acceleration fields.
- `TrainStepLoop` provides deterministic fixed-step updates.
- Two integration modes exist:
  - legacy normal-component behavior
  - tangential-projected behavior
- Force target projection into track-frame axes.
- Acceleration decomposition into tangential, normal, and binormal components.
- Track frame provider adapter from `TrackDocument`.
- Track physics adapter for transform/frame parity and curvature approximation.
- Section-based force target provider, including explicit elapsed-time sampling support.
- Basic train sample analytics.

Capability level: early simulation foundation. It is useful for deterministic stepping and diagnostics, but it is not yet a production ride physics model.

### `Quantum.IO`

Working today:

- `TrainPoseExportV1` DTO, mapper, JSON serializer/deserializer, and validator.
- `DebugViewportSnapshotV1` DTO, mapper, JSON serializer/deserializer, and validator.
- `TrackFrameContinuityDiagnosticsExportV1` DTO, mapper, and JSON support.
- Sampled centerline-frame CSV fixture parser for self-authored/synthetic debug fixtures.
- Strict contract/version checks for versioned JSON boundaries.
- Numeric validation for key exported frame/matrix/debug snapshot fields.

Capability level: strong for technical-preview contracts. The current IO system is credible for backend debug and test artifacts, not broad coaster interchange.

### `Quantum.Debug`

Working today:

- CLI command parser and help.
- Deterministic backend smoke scenario.
- Train pose export sample command.
- Debug viewport snapshot generation.
- Debug viewport snapshot generation from sampled-frame CSV fixtures.
- Debug viewport snapshot validation command.
- Multi-panel SVG preview generation from snapshot JSON.
- Frame continuity diagnostics command.
- Longitudinal force and speed preview artifact generation.
- Sampling performance smoke command and timing stats.

Capability level: useful backend diagnostics and artifact generation. This is a debug/preview surface, not a product UI.

### `Quantum.Tests`

Working today:

- 886 tests pass after restore with the requested no-restore command.
- Tests cover project dependency boundaries, public API boundaries, math transforms, spline sampling, track distance semantics, train placement, section resolution, FVD sections, physics stepping, IO contracts, debug commands, and fixture paths.

Capability level: the test suite is one of the library's strengths. It gives real confidence in the narrow backend slice, while also making clear where the library remains early.

## 2. Strongest Backend Areas

The strongest backend areas are:

1. Station-distance centerline evaluation.
2. Track frame generation and matrix conversion.
3. Deterministic train body/bogie/wheel/articulated placement.
4. Versioned train-pose and debug-viewport JSON contracts.
5. Section/channel evaluation contracts.
6. Engine-agnostic dependency boundary.
7. Debug artifact workflow for technical preview validation.

The most credible current pipeline is:

```text
TrackDocument -> TrackEvaluator -> TrackFrame -> TrainCarTransformProvider -> TrainPoseResult -> Quantum.IO / Quantum.Debug
```

That pipeline is documented, tested, and reflected in the public API boundary tests.

## 3. Weakest Or Prototype Areas

Weakest/prototype areas:

- `Quantum.Geometry` is intentionally absent from the active solution and should not be claimed.
- Full coaster authoring is not present. There is no complete editor model, node graph, constraint workflow, or interactive section authoring system.
- FVD solving is prototype-only. `Fvd2dNormalGSolver` is intentionally tiny and limited to a one-step 2D Normal-G adjustment.
- `ForceSection` has accumulated several compatibility layers. It works, but the API shape is still transitional.
- Physics is deterministic and testable but not a validated ride dynamics simulator.
- Train visuals are placeholders: boxes, lines, bogie/wheel transforms, and debug markers rather than real train assets.
- Wheel transforms currently inherit bogie frame/matrix policy; they are useful layout/debug outputs, not physical wheel/rail contact simulation.
- CSV import is a sampled-frame fixture bridge, not a NoLimits project importer.
- Unity assets are optional prototype/debug consumers outside the backend. They should not define backend architecture.
- Camera transforms are useful renderer-neutral outputs, but they are not a finished viewer/editor camera system.
- Performance diagnostics are smoke-level diagnostics, not a benchmark suite or optimization guarantee.

Missing or not currently evident as implemented backend systems:

- Block zones.
- Support generation.
- Terrain systems.
- Full heartline-offset model as a core backend contract.
- Full banking-profile authoring model beyond current roll/frame/debug-ribbon support.
- Collision, clearance, envelope, or restraint systems.
- Real train meshes, bogies, wheel assemblies, or asset import.
- Full NoLimits, NL2, or third-party project compatibility.
- Scripting system.
- Production editor UI.
- Production renderer or ride-through renderer.

## 4. Test-Proven Versus Lightly Exercised

### Test-Proven

These areas have strong test evidence:

- Backend projects avoid frontend/renderer dependencies.
- Public coaster API boundary favors `Quantum.Track.TrackFrame`, bound station-distance frame sampling, `EvaluateTrainPose`, and `TrainPoseExportV1`.
- Math basis/matrix/transform behavior is covered by focused math tests.
- Track frame construction, orthonormality, roll behavior, fallback behavior, and matrix conversion are covered.
- Station-distance semantics are covered by contract and characterization tests.
- Batch sampling parity is covered.
- Train placement is heavily covered for body, bogie, wheel, articulated, matrix, spacing, and deterministic regression snapshots.
- G-Shark NURBS train-spacing parity is covered.
- Section resolution, normalization, force target sampling, force channel blending, interpolation, keyframed easing, and domain behavior are covered.
- FVD section definitions, channel evaluation, force target adapter behavior, and the prototype solver have tests.
- Physics stepping, force projection, section force target provider behavior, track frame provider semantics, gravity projection, curvature diagnostics, and analytics have tests.
- `TrainPoseExportV1`, `DebugViewportSnapshotV1`, and `TrackFrameContinuityDiagnosticsExportV1` mapping/serialization/validation are covered.
- Debug commands for snapshots, CSV bridge, validation, SVG generation, previews, continuity diagnostics, and sampling performance have tests.
- Synthetic/self-authored CSV fixture pack paths are parsed, mapped, validated, and round-tripped in tests.

### Lightly Exercised Or Narrowly Proven

These areas have some tests but should be claimed carefully:

- FVD Normal-G solving: tests exist, but the algorithm is explicitly a prototype.
- Real-world coaster geometry: synthetic and deterministic fixtures are covered; broad real-layout validation is not.
- Time-domain force sampling: explicit opt-in behavior is tested, but it is not the default runtime model.
- Physics realism: stepping and projections are tested, but the model is not validated against measured ride dynamics.
- Camera transforms: there are tests, but no production viewer/editor camera workflow.
- SVG previews and debug visualizations: command and artifact generation are tested, but they are debug aids only.
- Performance: smoke diagnostics exist; there is no sustained profiling or performance target.
- Versioned IO contracts: v1 contracts are tested, but there is no broad compatibility matrix across external consumers.

### Not Test-Proven

These should not be treated as proven:

- Full simulator accuracy.
- Full coaster design/editor workflow.
- Complete FVD-style authoring and optimization.
- Full NoLimits import/export compatibility.
- Real train dynamics, wheel contact, bogie mechanics, suspension, clearance, block logic, dispatching, or operations.
- Renderer integration beyond thin debug adapters.
- Production packaging or public API stability beyond the explicitly documented v1 contracts and boundary tests.

## 5. What Can Be Shown In Technical Preview 0.1

Technical Preview 0.1 can honestly show:

- Engine-agnostic backend libraries with no hard Unity/Unreal/editor dependency.
- Deterministic centerline sampling from `TrackDocument`.
- Stable frame output with tangent, normal, binormal, position, and station distance.
- Batch frame sampling and frame diagnostics.
- Distance-based placeholder train placement along a centerline.
- Body, bogie, wheel, and articulated train-pose hierarchy as backend data.
- `TrainPoseExportV1` JSON generation, validation, and deterministic regression behavior.
- `DebugViewportSnapshotV1` JSON generation and validation.
- Snapshot generation from tiny self-authored/synthetic sampled-frame CSV fixtures.
- Backend-only SVG debug previews from snapshot JSON.
- Frame continuity diagnostics export.
- Longitudinal force/speed preview artifacts as debug data.
- A repeatable test-backed backend demo path.

Good phrasing:

- "Backend-first technical preview."
- "Renderer-neutral debug/export contracts."
- "Deterministic centerline, frame, and placeholder train-pose pipeline."
- "Early coaster-domain foundation."
- "Not production-ready."

## 6. What Should Not Be Claimed Yet

Do not claim:

- Finished coaster simulator.
- Complete coaster editor.
- NoLimits replacement.
- NoLimits project importer.
- Accurate production ride physics.
- Full FVD optimizer.
- Full force-vector design workflow.
- Final renderer, final frontend, or final engine choice.
- Production Unity/Unreal integration.
- High-fidelity PBR, ride-through, or production rendering feature.
- Terrain/scenery/support generation.
- Real train mesh/asset pipeline.
- Block-zone or operations simulation.
- General-purpose math, spline, or geometry library.
- Public API freeze for the whole repository.

The honest claim is narrower and stronger: Quantum can already evaluate a simple backend track representation, produce stable frames, place simple train boxes by distance, emit versioned debug/export artifacts, and protect that flow with tests.

## 7. Near-Term Milestones That Would Make The Library More Powerful

Recommended near-term milestones:

1. Keep hardening centerline/frame semantics.
   - Add more representative synthetic curves.
   - Add continuity checks for transitions between mixed segment types.
   - Keep scalar/batch sampling parity locked.

2. Clarify and consolidate section authoring contracts.
   - Keep `ForceSection` compatibility behavior stable.
   - Add clearer factory/build paths for normalized force/geometric sections.
   - Avoid adding more optional constructor complexity.

3. Promote geometric sections from simple assembly support to a stronger track-generation path.
   - Make it clear when `GeometricSection` is authoring data versus evaluated centerline geometry.
   - Add tests that compare section-derived curves to expected distance/frame behavior.

4. Keep replacing generic spline/math responsibility with mature libraries where practical.
   - Prefer G-Shark-backed NURBS paths.
   - Keep custom NURBS/B-spline code as parity/debug baseline unless there is a coaster-specific reason.

5. Strengthen train placement scenarios.
   - Cover longer trains on multi-segment tracks.
   - Cover boundary behavior near start/end more explicitly.
   - Define current limitations for bogie counts, wheel layout, and articulation assumptions.

6. Make physics claims more precise.
   - Separate "deterministic debug stepping" from "validated ride dynamics."
   - Add scenario docs for what each integration mode means.
   - Add more cross-checks between force targets, track frames, and follower diagnostics.

7. Improve debug/export contract confidence.
   - Keep generated artifacts out of source control by default.
   - Add schema/documentation parity checks for v1 contracts if contract churn increases.
   - Add compatibility samples only when they are self-authored or synthetic.

8. Keep placeholder packages out of the active build.
   - `Quantum.Geometry` is reserved by name only until a real scoped backend purpose exists.

9. Define a minimal authoring workflow before building an editor.
   - The next powerful step is not a full UI; it is a clearer backend story for creating sections, sampling them, and seeing predictable output.

## 8. Honest Comparison To Early Coaster Design/Simulation Tooling Needs

Early coaster design/simulation tools usually need several layers:

- Authoring model: track sections, handles/nodes, banking, shaping, constraints, metadata.
- Geometry model: smooth centerline generation, continuous derivatives, heartline/banking semantics, transitions.
- Force model: normal/lateral/longitudinal force targets, speed assumptions, smoothing, domain switching, solver feedback.
- Train model: car spacing, bogies, wheels, articulation, clearance/envelope, block/operations logic.
- Simulation model: speed integration, gravity, friction/drag, force decomposition, validation against expected behavior.
- Visualization: inspectable centerline, frames, banking, train placement, force graphs, ride-through/debug views.
- IO: robust import/export and versioned contracts.
- Editor workflow: selection, undo/redo, property editing, timeline/section editing, viewport interaction.

Quantum today covers a credible backend slice of that stack:

- Good: deterministic centerline sampling, frame output, train placeholder placement, debug/export contracts, section/channel evaluation, and tests.
- Early but promising: force-section plumbing, FVD-style section data, physics stepping, debug previews, and G-Shark spline integration.
- Not there yet: complete authoring workflow, production solver, validated ride simulation, full interchange, real train/scene systems, and polished editor/renderer.

Compared to early coaster tooling needs, Quantum is best described as a backend foundation for coaster-domain evaluation and debug contracts. It is not yet a designer-facing tool, but it has enough deterministic behavior to start building one carefully.

## Summary

QuantumCoasterWorksLib can currently do a narrow but real thing: evaluate simple backend track documents, produce stable sampled frames, place placeholder train bodies/bogies/wheels/articulated cars by station distance, serialize that pose/debug data through versioned contracts, and validate the flow with a substantial test suite.

Its strongest value today is not breadth; it is a grounded, engine-agnostic backend pipeline. The next work should keep strengthening that pipeline before claiming editor, simulator, importer, renderer, or full FVD capabilities.
