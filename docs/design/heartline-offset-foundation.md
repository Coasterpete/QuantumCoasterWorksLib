# Heartline Offset Foundation

Date: 2026-06-16

Status: M144 PR3 backend-only opt-in foundation.

## Purpose

This note documents the first `Quantum.Track` heartline offset foundation. It
adds a small rider-reference sampling model without changing centerline
evaluation, train placement, runtime compilation, IO/schema contracts, Unity,
FVD, physics, editor tooling, rendering, persistence, or geometry generation.

The new surface is intentionally narrow:

- `HeartlineOffset`: normal/lateral offset distances in meters.
- `HeartlineFrame`: sampled centerline station plus centerline position,
  offset position, and inherited source-frame axes.
- `HeartlineSampler`: opt-in helpers that derive heartline frames from
  `TrackEvaluator` frames, or from profile-banked frames through
  `BankingProfileSampler`.

## Distance Domain

PR3 heartline sampling uses the existing centerline station-distance domain.
The input distance is the same station distance consumed by `TrackEvaluator`,
and `HeartlineFrame.Distance` stores the sampled centerline station distance.

No heartline arc-length domain exists yet. Sampling an offset point does not
create a separate length lookup table, does not reparameterize the path by
rider-reference length, and does not affect distance-based car placement.

## Offset Axes

Offsets are relative to sampled frame axes, not world-up or world-right:

```text
heartlinePosition =
    frame.Position
    + frame.Normal * offset.NormalOffsetMeters
    + frame.Binormal * offset.LateralOffsetMeters
```

Positive normal offset follows the sampled frame `Normal` axis. Positive
lateral offset follows the sampled frame `Binormal` axis. On an unbanked
straight fallback frame this maps to `+Y` and `+Z`, but that is a consequence
of that frame, not a world-axis rule.

`HeartlineFrame.Tangent`, `Normal`, and `Binormal` are inherited from the
sampled source frame. `Tangent` is not the mathematical derivative of the
offset curve when curvature or banking changes.

## Banking Interaction

The default sampler overload:

```text
HeartlineSampler.SampleAtDistance(evaluator, offset, distance)
HeartlineSampler.SampleAtDistances(evaluator, offset, distances)
```

uses the evaluator's current default frames. It does not consult a
`BankingProfile`.

The explicit banking-profile batch overload:

```text
HeartlineSampler.SampleAtDistances(evaluator, bankingProfile, offset, distances)
```

samples frames through `BankingProfileSampler`, then applies the offset to
those profile-banked axes. Banking affects heartline offset direction only
through this opt-in overload.

## M144 PR4 Profile-Banked Proof

M144 PR4 adds a backend-only `ProfileBankedHeartlineProofScenario` in
`Quantum.Debug`. The scenario combines self-authored spatial centerline
sections, explicit authored `TrackBankingDefinition` keys, authoring geometry
continuity diagnostics, authoring banking diagnostics, default centerline
frames, profile-banked frames, and default/profile-banked heartline samples.

This is a deterministic proof scenario and test fixture only. It is not a new
debug command, export schema, IO contract, persistence path, Unity path, train
placement default, evaluator default, renderer path, physics path, FVD path, or
editor feature. The proof continues to use centerline station distance for
heartline samples and does not introduce a heartline arc-length domain.

## Train And Runtime Behavior

Trains still run on the current centerline/default frame path. PR3 does not
make heartline the default train/path behavior and does not change
`TrainCarTransformProvider` defaults.

The heartline sampler is a read-only derived sampling helper:

- zero offset reproduces centerline positions and axes
- batch sampling preserves caller order
- empty batches return empty arrays
- non-finite distance validation follows the existing evaluator/profile frame
  sampling behavior

## Non-Goals

- No heartline arc-length or rider-path distance domain.
- No changes to `TrackDocument` or `CompiledTrackRuntime`.
- No IO/schema, Unity, FVD, physics, editor, rendering, persistence, or geometry
  generation changes.
- No default train, path, export, or physics behavior changes.
