# Normalized Section Invariants

Normalized sections are the engine-agnostic representation used after shorthand track
sections have been resolved into explicit channel functions over a known interval. They
are intended to be small, deterministic data objects that downstream evaluators can
trust without re-validating every malformed state.

## Section Kinds

Valid `SectionKind` values are:

- `Force`
- `Geometry`

Force sections may carry `NormalG`, `LateralG`, `LongitudinalG`, and
`RollRateDegPerSec` channels. Geometry sections may carry `Curvature` and `Roll`
channels.

## Section Domains

Valid `SectionDomain` values are:

- `Distance`
- `Time`

`Distance` is the current runtime path for normalized section evaluation. `Time`
sections can still be represented by normalized definitions where the API accepts that
domain.

## Channels And Evaluations

Valid `SectionChannel` values are:

- `NormalG`
- `LateralG`
- `LongitudinalG`
- `RollRateDegPerSec`
- `Curvature`
- `Roll`

Unsupported channel enum values are rejected at construction or evaluation API
boundaries. `SectionChannelEvaluation` also requires a finite evaluated value.

## Samples

`SectionSample` values require:

- Finite `X`.
- Finite `Value`.

When samples are attached to a `SectionDefinition`, each sample `X` must be within the
owning section interval `[StartX, EndX]`.

## Functions

Public sample-backed `SectionFunction` construction requires:

- A supported `SectionChannel`.
- At least one sample.
- Strictly increasing sample `X` values.

Function-backed internal construction can still use an empty sample list when an
evaluator delegate is provided. That evaluator path must still be called with finite
input and must return finite output.

## Definitions

`SectionDefinition` requires:

- A supported `SectionKind`.
- A supported `SectionDomain`.
- Finite `StartX` and `EndX`.
- `StartX` strictly less than `EndX`.
- At least one function.
- No null functions.
- No duplicate channels within the same section.
- Each function channel must be valid for the section kind.
- Sample lists, when present, must be finite, inside `[StartX, EndX]`, and strictly
  increasing by `X`.

Definitions use half-open interval lookup semantics, `[StartX, EndX)`, with final
endpoint handling owned by the normalized evaluator.

## Direct Evaluation

Direct evaluation APIs require finite evaluation `x` values. Unsupported channels are
rejected before lookup. Evaluating a channel that the section does not contain is an
invalid operation, not an implicit default.

## Why Guards Live At Boundaries

Normalized sections are a contract between shorthand section builders, import/export
adapters, and evaluator code. Rejecting invalid states at constructors and public API
boundaries keeps later centerline, frame, and train-placement work from having to handle
NaN, infinity, unsupported enum values, duplicate channels, or ambiguous sample order in
the middle of evaluation.
