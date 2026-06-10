# Quantum Backend Pipeline Map

Purpose: quick plain-language map of how backend track data becomes engine-agnostic train pose output for debug frontends, export adapters, physics systems, and other consumers.

Pipeline:
`TrackDocument -> TrackEvaluator -> TrackFrame -> TrainCarTransformProvider -> TrainPoseResult -> debug/export/physics/frontend consumers`

## 1) TrackDocument
What it owns:
- Source-of-truth track content (segments/sections and their ordering).
- The data shape that sampling starts from.

What it assumes:
- Segment data is finite/valid enough to evaluate.
- Segment ordering and lengths are coherent for station-distance queries.

What breaks if changed:
- Distance semantics can drift immediately.
- All downstream sampling, frame generation, and car placement can become inconsistent.

## 2) TrackEvaluator
What it owns:
- True geometric arc-length station resolution to segment/local evaluation.
- Canonical transported-frame history shared by scalar and batch queries.
- Point/tangent/frame/transform evaluation policy, including fallback and roll behavior.

What it assumes:
- Input document/segment data is valid enough to sample.
- Clamping and edge-case behavior stay deterministic.
- Declared spline segment lengths match measured geometry within the configured tolerance.

What breaks if changed:
- Car spacing and placement can shift or jitter.
- Physics/debug/export parity with backend frames can diverge.
- Distance/edge-case contract tests are likely to fail.

## 3) TrackFrame
What it owns:
- Canonical sampled pose basis: position + tangent/normal/binormal + distance.
- Canonical matrix conversion convention used downstream.
- The only coaster-facing frame contract used by Track, Physics, train, debug,
  and export runtime paths.

What it assumes:
- Basis vectors are finite and near-orthonormal.
- Axis meaning stays stable (tangent forward, normal up, binormal right/lateral).

What breaks if changed:
- Unity/debug orientation can flip or skew.
- Exported matrix/frame interpretation becomes incompatible with existing consumers.

## 4) TrainCarTransformProvider
What it owns:
- Distance-based car placement orchestration over sampled track frames.
- Composition of body/bogie/wheel/articulated transforms into one pose result.
- The default train-pose path plus the explicit opt-in
  `EvaluateTrainPose(..., BankingProfile)` roll-source path.

What it assumes:
- `TrackEvaluator` frame and distance semantics are stable.
- `BankingProfile` remains runtime opt-in and is not stored on `TrackDocument`.
- Train definition inputs (counts/spacing/layout) are valid and deterministic.

What breaks if changed:
- Car ordering, spacing, and articulation can regress.
- Wheel/bogie placement and matrix parity expectations can fail.

## 5) TrainPoseResult
What it owns:
- The backend pose snapshot handed to external consumers.
- The final hierarchy produced by transform provider evaluation.

What it assumes:
- Consumer-facing pose shape remains stable.
- Frame/matrix semantics remain consistent with current export contract usage.

What breaks if changed:
- Export mappers/validators and downstream readers can break.
- Unity/debug/physics adapters may misread or reject pose data.

## 6) Debug / Export / Physics / Frontend Consumers
What they own:
- Presentation, diagnostics, serialization, simulation usage, and host integration built on backend output.
- Host-specific behavior such as drawing, schema validation, runtime integration, and optional visualization adapters.

What they assume:
- Upstream pipeline semantics are stable (distance, frame axes, matrix convention).
- Export contract identity/version remains stable for JSON consumers.
- Unity is one optional debug/prototype frontend consumer, not the owner of backend pipeline semantics.

What breaks if changed:
- Visual orientation and debug views can stop matching backend truth.
- Export contract compatibility can fail.
- Physics sampling adapters and other consumers can diverge from track evaluation behavior.

---

DTO note (current, minimal):
- The current JSON boundary is `quantum.train_pose` version `1` (`docs/train_pose_export_v1_contract.md`), with strict contract/version checks.
