# BankingProfile Fixtures

Milestone 51 adds a small backend-only BankingProfile fixture catalog in
`Quantum.Debug/BankingProfileFixtures.cs`. Milestone 54 adds one deterministic
BankingProfile train-pose fixture in
`Quantum.Debug/BankingProfileTrainPoseFixtures.cs`.

The fixtures are self-authored deterministic roll profiles for diagnostics,
browser-viewer payloads, and compatibility tests. They do not change
`TrackEvaluator`, `TrackFrame`, `DebugViewportSnapshotV1`, `TrainPoseExportV1`,
or runtime banking behavior.

## Fixture Set

- `constant-flat`: zero-roll profile across the full station range.
- `constant-banked`: constant 25 degree bank across the full station range.
- `linear-roll-ramp`: linear 0 to 45 degree roll ramp.
- `smoothstep-roll-ramp`: smoothstep 0 to 45 degree roll ramp.
- `roll-hold-with-multiple-keys`: ramp to 30 degrees, hold that roll across
  multiple keys, then transition through smoothstep and linear intervals.
- `unwrapped-over-360-roll`: linear 0 to 450 degree profile that verifies roll
  values remain unwrapped.

Each fixture provides a validated `BankingProfile` plus reusable uniform sample
distances. The `banking-profile-diagnostics` command uses the catalog default
fixture instead of carrying its own private sample profile.

## Train Pose Fixture

- `banking-profile-train-pose`: self-authored track, consist, and
  `BankingProfile` fixture for the opt-in
  `EvaluateTrainPose(..., BankingProfile)` path.

The train-pose fixture is used to verify runtime body/bogie/wheel/articulated
frames, global station distances, matrix/frame consistency, `TrainPoseExportV1`
validation, JSON roundtrip stability, and `DebugViewportSnapshotV1` inspection.
It exports through the existing v1 contracts and does not add
`TrackDocument.BankingProfile`.

## What The Tests Validate

`BankingProfileFixtureTests` verifies that every fixture:

- appears in the requested catalog order
- exposes finite, strictly ordered keys
- exposes finite monotonic sample distances
- can be sampled through `BankingProfileDiagnostics`
- produces deterministic sample and summary values across repeated runs

Existing banking diagnostics tests now reuse the catalog where practical so the
diagnostics and browser commands stay aligned with shared backend fixtures.
`BankingProfileTrainPoseContractTests` verifies the train-pose fixture through
runtime pose evaluation, export, validation, JSON roundtrip, and debug snapshot
inspection.

## Non-Goals

These fixtures do not:

- change default track evaluation or frame generation
- change runtime banking interpolation behavior
- add Unity, frontend, browser-runtime, renderer, or package dependencies
- introduce imported third-party fixture data
