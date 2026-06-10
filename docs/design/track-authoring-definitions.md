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

## Document Generation

`TrackAuthoringDocumentBuilder.Build` and `BuildDocument` produce the same
`TrackDocument` shape:

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

Repeated builds from the same definition are deterministic and produce
equivalent canonical frame samples through `TrackEvaluator`.

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

TrackDocument document = TrackAuthoringDocumentBuilder.Build(definition);
var evaluator = new TrackEvaluator(document);
TrackFrame frame = evaluator.EvaluateFrameAtDistance(18.0);
```

This PR intentionally supports only simple straight and planar
constant-curvature authoring. More advanced interpolation, NURBS authoring,
force-driven geometry, UI workflows, and persistence remain outside this layer.
