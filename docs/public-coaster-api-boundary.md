# Public Coaster API Boundary

Milestone 1 freezes the consumer-facing backend lanes that make Quantum read as a
coaster-domain library. This does not change runtime math, spline evaluation, or
train placement behavior.

## Stable Boundary Lanes

### 1) `Quantum.Track.TrackFrame`

`TrackFrame` is the public pose basis for sampled coaster track and train
transforms.

- `Distance`: clamped global station distance associated with the frame.
- `Position`: centerline position.
- `Tangent`: forward axis.
- `Normal`: up axis.
- `Binormal`: right/lateral axis.
- `ToMatrix4x4()`: canonical frame-to-matrix conversion for interop.

Consumers should treat `Quantum.Track.TrackFrame` as authoritative. The similarly
named `Quantum.Splines.TrackFrame` is a support-layer implementation type.

### 2) `TrackEvaluator` Station-Distance Sampling

The public station-distance lane is:

- `new TrackEvaluator(trackDocument)`
- `EvaluateFrameAtDistance(double distance)`
- `EvaluateFramesAtDistances(IReadOnlyList<double> distances)`
- `GetBoundTrackTotalLength()`

`EvaluateAtDistance(TrackDocument, double)` remains the stable resolver for
station distance to `TrackEvaluationPoint` (`TrackSegment` + local `t`).

Current public distance behavior:

- finite out-of-range distances clamp to the track extents, and public
  `Quantum.Track.TrackFrame.Distance` stores that clamped global station value
- non-finite distances are rejected
- empty documents are rejected for sampling

Document overloads that return `Quantum.Splines.TrackFrame` are compatibility
support-layer APIs, not the preferred consumer boundary. Their `S` value may be
segment-local and must not be treated as the public frame distance contract.

### 3) `TrackDocument` / `TrackSegment` Centerline Evaluation

`TrackDocument` owns the ordered coaster track content. Its segment order and
segment lengths define the station-distance coordinate consumed by
`TrackEvaluator`.

`TrackSegment` exposes coaster-domain identity and sampling inputs:

- `Length`
- `Id`
- `ForceSegmentReference`
- `RollRadians`

The current `Spline` property is a support-layer centerline carrier. Consumers
should enter through `TrackEvaluator` instead of depending on spline internals.

### 4) `TrainCarTransformProvider.EvaluateTrainPose`

`EvaluateTrainPose(double leadDistance, TrainConsistDefinition definition)` is
the public train-pose entrypoint. It evaluates the existing distance-based body,
bogie, wheel, and articulation hierarchy and returns a `TrainPoseResult`.

This lane owns coaster train placement semantics:

- car 0 is evaluated at `leadDistance`
- following cars are placed by station-distance spacing
- body, bogie, wheel, and articulated public frames preserve global station
  distance semantics

`EvaluateTrainPose(double leadDistance, TrainConsistDefinition definition,
BankingProfile bankingProfile)` is an explicit opt-in variant for callers that
want runtime train poses evaluated with a separate `BankingProfile` roll source.
The default overload remains segment/evaluator-backed, and `TrackDocument` does
not own a `BankingProfile`.

### 5) `TrainPoseExportV1`

`TrainPoseExportV1` is the public JSON snapshot contract:

- `TrainPoseExportV1Dto.ContractName == "quantum.train_pose"`
- `TrainPoseExportV1Dto.ContractVersion == 1`
- `TrainPoseExportV1Mapper.Export(TrainPoseResult)`
- `TrainPoseExportV1Json.Serialize(...)`
- `TrainPoseExportV1Json.Deserialize(...)`

Schema and field semantics remain documented in
`docs/train_pose_export_v1_contract.md`.

### 6) Geometry Interchange Roadmap

`Quantum.IO.GeometryInterchange` is the backend-only holding boundary for future
external curve import/export adapters. The current surface models external curve
document metadata, import/export results, control points, degree/order metadata,
knot vectors, and diagnostics while keeping Rhino/openNURBS out of the direct
backend dependency graph.

The current `Rhino3dmGeometryAdapter` is a placeholder only. It returns stable
unsupported diagnostics for import and export until a real rhino3dm/openNURBS
implementation is intentionally added behind this boundary.

## Support-Layer Rule

`Quantum.Splines` and `Quantum.Math` remain implementation/support layers. They
can exist behind track evaluation and value storage, but new consumer-facing
coaster APIs should be described in `Quantum.Track` or versioned `Quantum.IO`
contracts.

Do not introduce new generic spline/math entrypoints as the primary way to move
trains, sample a centerline, or export poses.
