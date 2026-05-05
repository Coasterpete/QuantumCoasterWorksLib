# Section Pipeline (Planning Milestone)

## Goal
Define the future FVD-style section architecture for Quantum without changing runtime behavior yet.

## Current Baseline
- `Quantum.Track` has lightweight `TrackSection` types (`ForceSection`, `GeometricSection`) stored in `TrackDocument.Sections`.
- These section objects are currently data-only and are not yet mapped into `TrackEvaluator` geometry evaluation.
- `Quantum.FVD` already has a richer section model:
  - `FvdSectionDefinition` with `Kind`, `Domain`, `StartX`, `EndX`, and channel functions.
  - Non-overlap guarantees per `(Kind, Domain)` and deterministic boundary rules.
  - Channel evaluation for force targets (strict and permissive).
- `Quantum.Physics` consumes force targets through `IForceTargetProvider` and `ForceTargets`.

## Current Implementation Status

✔ `SectionResolver` provides deterministic interval resolution.  
✔ `SectionCurveAssembler` maps `GeometricSection` data into `CompositeSectionCurve`.  
✔ `ForceTargetResolver` resolves `ForceSection` intervals.  
✔ `ForceTargetSampler` converts force intervals into sampled force targets.  
✔ `SectionForceTargetProvider` adapts section-based targets to `IForceTargetProvider`.  
✔ `TrainStepLoop` can consume section force targets through explicit opt-in provider injection.

Default physics behavior remains unchanged.  
Section-driven physics is currently opt-in only.

## 1) How `ForceSection` and `GeometricSection` Should Evolve

### Design Direction
Evolve both section types from scalar payloads into timeline/channel sections that can be sampled deterministically in either distance or time domain.

### `ForceSection` Evolution
- Keep current constructor shape as a compatibility convenience layer.
- Add an FVD-style core representation behind it:
  - `Domain`: `Distance` or `Time`.
  - `StartX`, `EndX`: section coverage interval.
  - Channel functions for:
    - `NormalG`
    - `LateralG`
    - `RollRateDegPerSec`
- Existing scalar fields (`TargetNormalG`, `TargetLateralG`, `Length`, `Duration`) become shorthand for constant channel sections.
- Preserve current adapter semantics:
  - Missing `NormalG` is a hard failure for strict provider reads.
  - Missing lateral/roll channels can remain permissive defaults where required.

### `GeometricSection` Evolution
- Keep current `{Length, Curvature, Roll}` usage as compatibility shorthand.
- Add the same interval/domain shell used by force sections.
- Channel functions initially cover:
  - `Curvature`
  - `RollAngleDeg` (or roll equivalent in radians at the integration boundary)
- Future extension channels can be added later (for example yaw/pitch rates) without changing the section pipeline contract.

### Shared Section Contract
Long-term, `TrackSection` should normalize to a common definition shape:
- `Kind` (Force or Geometry)
- `Domain` (Distance or Time)
- `StartX`, `EndX`
- Channel function list

This aligns `Quantum.Track` with the proven structure already present in `Quantum.FVD`.

## 2) How Sections Map to Splines

### Canonical Coordinate
- Use track station distance `s` (meters along track) as the primary runtime coordinate.
- Keep time-domain sections supported as data, but gate runtime use until time-domain integration is intentionally introduced.

### Mapping Pipeline
1. Resolve active section by `(Kind, Domain, x)` where `x` is usually `s`.
2. Map `s` to segment-local position via `TrackEvaluator.EvaluateAtDistance`.
3. If the segment has a spline, evaluate frame/transform from spline-backed data.
4. If spline is absent, use existing deterministic fallback frame behavior.

### Coverage and Boundaries
- Use half-open intervals `[StartX, EndX)` for normal lookup.
- At touching boundaries, right-hand section wins.
- Final section end remains inclusive to avoid an uncovered terminal sample.
- Section intervals are independent of segment boundaries and may span multiple segments.

## 3) How Sections Map to Physics Targets

### Force Target Mapping
- Force section channels map directly to `ForceTargets`:
  - `NormalG -> ForceTargets.NormalG`
  - `LateralG -> ForceTargets.LateralG`
  - `RollRateDegPerSec -> ForceTargets.RollRateDegPerSec`

### Runtime Read Modes
- Strict read mode:
  - Fails when required channels are missing.
  - Keeps existing `NormalG` contract intact.
- Permissive read mode:
  - Missing lateral/roll channels default to zero.
  - Missing section coverage still returns false.

### Physics Scope Guardrails
- Keep `TrainStepLoop` integration behavior unchanged in this milestone.
- Continue treating lateral/roll as diagnostics/data-path unless explicitly upgraded in a later milestone.

## 4) Conceptual Comparison to KexEngine-Style Nodes/Sections

### Similarities
- Section-first authoring where Force and Geometric sections are the main design tools.
- Channels are timeline-driven values sampled over a section interval.
- Duration can be distance- or time-based.
- Deterministic section evaluation at any query position.

### Differences (Current Quantum)
- Quantum is currently library-first and document-based, not yet graph-node-first.
- `TrackDocument` and `FvdGraph` are separate data roots today.
- Node graph concepts such as anchor/path routing and branch priority are not yet part of `Quantum.Track` runtime contracts.

### Practical Alignment Strategy
- Match Kex-style behavior at the section evaluation layer first.
- Delay graph/node orchestration until section-to-spline and section-to-physics mappings are stable and test-covered.

## 5) Smallest Safe Next Milestone

### Objective
Add the minimum new contracts needed to make section evaluation deterministic and testable, without changing simulation behavior.

### Scope
1. Introduce a read-only section resolution layer in `Quantum.Track` (domain + interval + boundary semantics).
2. Add adapters that translate existing `ForceSection`/`GeometricSection` shorthand into normalized channel sections.
3. Add tests for:
   - overlap rejection
   - boundary handoff behavior
   - strict vs permissive force channel reads
   - no-coverage diagnostics
4. Keep `TrainStepLoop` and motion integration untouched.

### Why This Is Safest
- It reuses existing FVD section semantics already validated by tests.
- It avoids coupling geometry solving and motion integration in the same milestone.
- It creates a stable base for later milestones (time-domain targets, geometric channel expansion, node-graph orchestration).

## 6) Force Channel Interpolation

### Status
Phase 1 complete:
- Constant
- Linear
- SmoothStep

All interpolation modes are opt-in and preserve existing behavior by default.

### Next Phase
Expand interpolation support with additional preset modes:
- Quadratic
- Cubic
- Quartic
- Quintic
- Sinusoidal
- Plateau

### Future Direction
- Custom easings
- Keyframed channel curves
- FVD-style channel functions

Plateau interpolation is reserved for future force-profile shapes where a channel rises to a target, holds, then optionally falls. It should not be added to the basic `start → end` interpolation enum until the API supports multi-stage channel profiles.

### Guardrails
- No default physics behavior changes.
- No automatic section-to-physics coupling.
- Existing constant-section tests must remain unchanged.

## References
- KexEdit node graph and section concepts: <https://individualkex.github.io/KexEdit/reference/node-graph.html>

