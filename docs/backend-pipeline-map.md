# Quantum Backend Pipeline Map

Purpose: quick plain-language map of how backend track data becomes train pose output for Unity/debug/export/physics.

Pipeline:
`TrackDocument -> TrackEvaluator -> TrackFrame -> TrainCarTransformProvider -> TrainPoseResult -> Unity/debug/export/physics consumers`

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
- Core distance-to-sample logic (`s` along track to segment/local evaluation).
- Point/tangent/frame/transform evaluation policy, including fallback behavior.

What it assumes:
- Input document/segment data is valid enough to sample.
- Clamping and edge-case behavior stay deterministic.

What breaks if changed:
- Car spacing and placement can shift or jitter.
- Physics/debug/export parity with backend frames can diverge.
- Distance/edge-case contract tests are likely to fail.

## 3) TrackFrame
What it owns:
- Canonical sampled pose basis: position + tangent/normal/binormal + distance.
- Canonical matrix conversion convention used downstream.

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

What it assumes:
- `TrackEvaluator` frame and distance semantics are stable.
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

## 6) Unity / Debug / Export / Physics Consumers
What they own:
- Presentation, diagnostics, serialization, and simulation usage of backend output.
- Host-specific behavior (drawing, schema validation, runtime integration).

What they assume:
- Upstream pipeline semantics are stable (distance, frame axes, matrix convention).
- Export contract identity/version remains stable for JSON consumers.

What breaks if changed:
- Visual orientation and debug views can stop matching backend truth.
- Export contract compatibility can fail.
- Physics sampling adapters can diverge from track evaluation behavior.

---

DTO note (current, minimal):
- The current JSON boundary is `quantum.train_pose` version `1` (`docs/train_pose_export_v1_contract.md`), with strict contract/version checks.
