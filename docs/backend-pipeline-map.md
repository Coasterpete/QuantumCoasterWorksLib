# Quantum Backend Pipeline Map

Purpose: quick plain-language map of how backend track data becomes engine-agnostic train pose output for debug frontends, export adapters, physics systems, and other consumers.

Authoring / compilation / runtime lane:
`TrackAuthoringDefinition -> TrackAuthoringDocumentBuilder.Compile -> TrackAuthoringCompilation -> TrackDocument or CompiledTrackRuntime`

Evaluation lane:
`TrackDocument or CompiledTrackRuntime -> TrackEvaluator -> TrackFrame -> TrainCarTransformProvider -> TrainPoseResult -> debug/export/physics/frontend consumers`

Opt-in heartline sampling lane:
`TrackEvaluator + HeartlineOffset -> HeartlineSampler -> HeartlineFrame`

Opt-in profile-banked heartline sampling lane:
`TrackEvaluator + BankingProfile + HeartlineOffset -> BankingProfileSampler -> HeartlineSampler -> HeartlineFrame`

Geometry continuity diagnostics can compile a `TrackAuthoringDefinition`
internally or reuse an existing `TrackAuthoringCompilation`. Reuse avoids a
duplicate document/runtime compilation and reads the supplied compiled document
curves without changing evaluator, runtime, train, IO, export, or frontend
behavior.

## 0) Track Authoring / Compilation / Runtime
What it owns:
- `TrackAuthoringDefinition` is the validated, engine-agnostic authored section input.
- `TrackAuthoringDocumentBuilder.Compile` creates one aligned `TrackAuthoringCompilation`.
- The compilation groups the evaluator-ready mutable `TrackDocument`, compile-time
  `CompiledTrackRuntime`, resolved section intervals, and total length.

What it assumes:
- Definition section order and lengths are valid and deterministic.
- Compiled document segment order remains aligned with source and resolved sections.
- Referenced curves are treated as immutable for the lifetime of the runtime snapshot.

What breaks if changed:
- Later document mutation can diverge from the runtime and resolved interval snapshot.
- Diagnostics or evaluators using different sides of a stale compilation can disagree.
- Recompile after mutation to restore one aligned authoring/document/runtime snapshot.

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

## 4) HeartlineSampler
What it owns:
- Optional rider-reference point sampling from existing track frames.
- Centerline station-distance semantics for `HeartlineFrame.Distance`.
- Normal/lateral offset application along sampled frame axes.
- An explicit profile-banked path that first samples frames through
  `BankingProfileSampler`.

What it assumes:
- `TrackEvaluator` and `BankingProfileSampler` own frame sampling and distance
  validation.
- Normal and lateral offsets are finite meter values.
- No separate heartline arc-length domain exists in PR3.

What breaks if changed:
- Rider-reference debug sampling can diverge from frame axes or banking
  conventions.
- Accidentally routing train placement through heartline positions would change
  the current centerline/default train path.

## 5) TrainCarTransformProvider
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

## 6) TrainPoseResult
What it owns:
- The backend pose snapshot handed to external consumers.
- The final hierarchy produced by transform provider evaluation.

What it assumes:
- Consumer-facing pose shape remains stable.
- Frame/matrix semantics remain consistent with current export contract usage.

What breaks if changed:
- Export mappers/validators and downstream readers can break.
- Unity/debug/physics adapters may misread or reject pose data.

## 7) Debug / Export / Physics / Frontend Consumers
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
