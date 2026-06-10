# Track Authoring Definitions

## Purpose

`Quantum.Track.Authoring` is a small engine-agnostic input layer for creating
simple evaluator-ready `TrackDocument` instances. It describes ordered straight
and planar constant-curvature sections without exposing spline implementation
types or adding editor, UI, persistence, or engine concepts.

This layer does not replace or redesign `TrackDocument`. It validates authoring
input and adapts that input into the existing segment and evaluator contracts.

## Public Definitions

`TrackAuthoringDefinition` contains an ordered, copied list of
`GeometricSectionDefinition` values.

The supported section definitions are:

- `StraightSectionDefinition`
- `ConstantCurvatureSectionDefinition`

Every section has:

- `Id`: a non-blank stable identifier, preserved exactly
- `Length`: a finite value greater than zero, in station-distance units
- `RollRadians`: a finite constant roll angle around the centerline tangent

Constant-curvature sections also have `Radius`. Radius is signed and non-zero:

- positive radius turns from positive X toward positive Y
- negative radius turns from positive X toward negative Y
- curvature is calculated as `1 / Radius`

`SignedRadius` is an explicit alias for the same value.

## Validation

Section constructors reject invalid IDs, lengths, radii, and roll values.
`TrackAuthoringDefinition` rejects:

- a null section sequence
- an empty section sequence
- null section entries
- duplicate IDs using ordinal, case-sensitive comparison
- a combined length that is not finite

IDs are checked with `string.IsNullOrWhiteSpace`, but valid IDs are not trimmed or
normalized. This keeps ID preservation exact.

Validation completes before `TrackAuthoringDocumentBuilder` is called. The
definitions are immutable and the input sequence is copied, so later mutations
to the caller's collection do not alter the validated authoring definition.

## Compilation

`TrackAuthoringDocumentBuilder.Compile` produces a
`TrackAuthoringCompilation` containing:

- `Definition`: the exact validated `TrackAuthoringDefinition` instance
- `Document`: the evaluator-ready `TrackDocument` snapshot
- `ResolvedSections`: one ordered
  `ResolvedSectionInterval<GeometricSectionDefinition>` per authored section
- `TotalLength`: the compiled total in station-distance units

Station distances use the same units as authored section lengths and radii. For
example, lengths `6`, `8`, and `5` compile to ranges `0-6`, `6-14`, and
`14-19`, with a total length of `19`.

The compilation preserves exact source section references. For every index `i`:

- `ResolvedSections[i].Section` is the same instance as `Definition.Sections[i]`
- `Document.Segments[i]` is the generated centerline segment for that definition
- `Document.Sections[i]` is the generated `GeometricSection` for that definition

Resolved intervals are exposed through a defensive read-only copy. Shared
boundaries belong to the following section: the first two example intervals are
`[0, 6)` and `[6, 14)`. The final interval includes its endpoint, `[14, 19]`, so
the total track length resolves to the final authored section.

`TrackAuthoringDocumentBuilder.Build` and `BuildDocument` remain compatible and
forward to `Compile(definition).Document`. All three entry points produce the
same `TrackDocument` shape:

- one `TrackSegment` per authoring section
- original section order and exact IDs
- `StraightSegment` for straight definitions
- `CurvedSegment` for constant-curvature definitions
- exact authored segment lengths and constant roll values
- one generated `GeometricSection` per authoring section in `TrackDocument.Sections`

The builder reuses the existing geometric section assembly path to compose each
section from the prior section's endpoint and tangent. A narrow internal curve
window presents each composed section to the existing `TrackEvaluator` as its own
segment. This preserves point and tangent continuity while leaving evaluator,
spline, FVD, IO, and `TrackDocument` contracts unchanged.

Repeated compilations from the same definition are deterministic and produce
equivalent canonical frame samples through `TrackEvaluator`.

`Quantum.Debug.AuthoringPipelineProofScenario` is the built-in end-to-end proof
for this compilation path. It authors a zero-roll 12 m straight, 24 m
constant-curvature arc, and 12 m straight; compiles them through
`TrackAuthoringDocumentBuilder.Compile`; samples global station frames every
6 m from 0 m through 48 m; and evaluates a five-car train whose body centers are
at 36, 30, 24, 18, and 12 m. Its lead and trailing cars place bogies on both
sides of the 36 m and 12 m section boundaries respectively. The scenario then
uses the existing `TrainPoseExportV1Mapper` and
`DebugViewportSnapshotV1Mapper` boundaries without changing either V1
contract.

`TrackDocument` remains mutable under its existing contract. Mutating the
document returned by a compilation can break its index and distance alignment
with `Definition` and `ResolvedSections`. Compile the definition again to obtain
a fresh aligned document snapshot; the compilation does not silently track or
rewrite later document mutations.

## API Boundary

The public authoring APIs expose only backend domain types, scalar values, and
standard collection interfaces. They do not expose:

- `Quantum.Splines` types
- Unity or Unreal types
- Avalonia, Silk.NET, or OpenTK types
- editor or UI state
- persistence or serialization contracts

Spline composition remains an internal implementation detail behind the existing
`TrackDocument` and `TrackEvaluator` boundary.

## Example

```csharp
using Quantum.Track;
using Quantum.Track.Authoring;

var definition = new TrackAuthoringDefinition(new GeometricSectionDefinition[]
{
    new StraightSectionDefinition("entry", length: 12.0),
    new ConstantCurvatureSectionDefinition(
        "left-turn",
        length: 15.0,
        radius: 30.0,
        rollRadians: 0.2),
    new StraightSectionDefinition("exit", length: 8.0, rollRadians: 0.2)
});

TrackAuthoringCompilation compilation =
    TrackAuthoringDocumentBuilder.Compile(definition);
TrackDocument document = compilation.Document;
var evaluator = new TrackEvaluator(document);
TrackFrame frame = evaluator.EvaluateFrameAtDistance(18.0);
```

This PR intentionally supports only simple straight and planar
constant-curvature authoring. More advanced interpolation, NURBS authoring,
force-driven geometry, UI workflows, and persistence remain outside this layer.
