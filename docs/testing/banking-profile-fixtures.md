# BankingProfile Fixtures

Milestone 51 adds a small backend-only BankingProfile fixture catalog in
`Quantum.Debug/BankingProfileFixtures.cs`.

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

## What The Tests Validate

`BankingProfileFixtureTests` verifies that every fixture:

- appears in the requested catalog order
- exposes finite, strictly ordered keys
- exposes finite monotonic sample distances
- can be sampled through `BankingProfileDiagnostics`
- produces deterministic sample and summary values across repeated runs

Existing banking diagnostics tests now reuse the catalog where practical so the
diagnostics and browser commands stay aligned with shared backend fixtures.

## Non-Goals

These fixtures do not:

- change default track evaluation or frame generation
- change runtime banking interpolation behavior
- add Unity, frontend, browser-runtime, renderer, or package dependencies
- introduce imported third-party fixture data
