# Backend Milestone Summary (Post Train Pose JSON Export v1)

Date: 2026-05-08  
Scope: Backend handoff after commit `24d15e0` (`Add train pose export JSON contract`)

## 1) Current Backend Architecture

- Track evaluation path:
  - `TrackDocument` -> `TrackEvaluator`
  - Distance sampling uses `CompiledTrackSamplingContext` + `ResolvedTrackDistance` to clamp/resolve segment + local `t`.
- Frame/matrix contract:
  - `Quantum.Track.TrackFrame` is the authoritative pose basis (`Distance`, `Position`, `Tangent`, `Normal`, `Binormal`).
  - `TrackFrame.ToMatrix4x4()` is the canonical interop conversion (column-vector convention).
- Train pose hierarchy:
  - `TrainCarTransformProvider` is the orchestration entrypoint.
  - Output hierarchy flows through `TrainCarTransform` -> bogies -> articulated body -> wheels -> `TrainPoseResult`.
- Provider decomposition (implemented):
  - `TrainCarBodySampler`
  - `TrainBogieTransformSolver`
  - `TrainArticulationFrameSolver`
  - `TrainWheelTransformLayoutSolver`
  - `TrainPoseAssembler`
- Export path:
  - `TrainPoseExportV1Mapper.Export(TrainPoseResult)` -> `TrainPoseExportV1Dto`
  - `TrainPoseExportV1Json.Serialize/Deserialize` provides JSON with contract/version validation.

## 2) Major Completed Systems

- Track distance semantics and edge-case characterization.
- Compiled track sampling context for deterministic distance->segment resolution.
- Train pose hierarchy with articulated body + bogie + wheel transforms.
- Provider decomposition into focused internal solver/sampler components.
- Matrix/frame contract documented and tested for train export shape consistency.
- `Quantum.IO` train pose export DTO contract (`v1`) and JSON contract validation.

## 3) Current Commit Checkpoints

- `e24f148` Lock down track distance semantics.
- `ed91c5b` Characterize track evaluator distance edge cases.
- `5338f26` Add compiled track sampling context.
- `705da16` Add articulated train pose API.
- `58dd028` / `e44c56e` / `685f162` / `a2cd9da` Provider decomposition into bogie/wheel/articulation/assembly components.
- `db73ea3` Document train matrix frame contract.
- `3d4306c` Add train pose export DTO contract.
- `24d15e0` Add train pose export JSON contract.

## 4) Test Count / Status

- Current result (2026-05-08): `Passed: 629, Failed: 0, Skipped: 0, Total: 629`.
- Validation command used: `dotnet test QuantumCoasterWorks.sln --nologo`.

## 5) Intentionally Not Implemented Yet

- Unity bridge implementation is not started; backend remains renderer/host-agnostic.
- Frontend/Unity integration is intentionally paused.
- Full 3D train motion and lateral/roll coupling into runtime motion are intentionally deferred.
- Time-domain FVD solver/runtime coupling remains deferred.

## 6) Recommended Next Backend Milestones

- Stabilize train pose export contract with golden JSON fixtures and compatibility tests for future changes.
- Add explicit export schema notes (field semantics, units, coordinate/frame conventions) as a versioned doc.
- Add optional batch/compiled sampling reuse path in train pose evaluation to avoid recompiling sampling context per query.
- Add backend-side ingest/validation pathway for train pose JSON `v1` (read + validate + diagnostics), without Unity coupling.
- Define `v2` versioning policy now (backward-compatible additions only, explicit breaking-change gate).

## 7) Recommended “Do Not Touch Yet” Areas

- Do not couple train pose export work to Unity/frontend workstreams.
- Do not change `quantum.train_pose` contract name or `version = 1` behavior without formal version bump planning.
- Do not alter `TrainStepLoop` integration behavior as part of export milestones.
- Do not merge lateral/roll/time-domain runtime coupling into this lane until explicitly scheduled.
- Avoid large namespace/surface refactors in `Quantum.Track` during contract hardening; prioritize stability first.
