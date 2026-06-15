# Track Authoring Definitions

## Purpose

`Quantum.Track.Authoring` is a small engine-agnostic input layer for creating
simple evaluator-ready `TrackDocument` instances. It describes ordered straight,
planar constant-curvature, planar curvature-transition, and opt-in spatial
sections without exposing spline implementation types or adding editor, UI,
persistence, or engine concepts.

This layer does not replace or redesign `TrackDocument`. It validates authoring
input and adapts that input into the existing segment and evaluator contracts.

## Public Definitions

`TrackAuthoringDefinition` contains an ordered, copied list of
`GeometricSectionDefinition` values and a validated `TrackStartPose`.

The legacy constructor accepts only sections and delegates to
`TrackStartPose.Identity`. The explicit overload accepts sections plus a start
pose. `Identity` is the origin with positive-X tangent, positive-Y normal, and
positive-Z binormal, so existing M140-M142 authored layouts retain their prior
coordinate convention.

`TrackStartPose` contains:

- `Position`: the world-space start point
- `Tangent`: the local positive-X forward axis
- `Normal`: the local positive-Y bend-reference axis
- `Binormal`: the local positive-Z right/lateral axis

The basis is the unbanked construction frame. Its axes must be finite,
non-near-zero, unit length, mutually orthogonal, and consistently right-handed
with `Tangent x Normal = Binormal`. Invalid or left-handed bases are rejected.
Inputs are never silently normalized.

The supported section definitions are:

- `StraightSectionDefinition`
- `ConstantCurvatureSectionDefinition`
- `CurvatureTransitionSectionDefinition`
- `SpatialSectionDefinition`

Every section has:

- `Id`: a non-blank stable identifier, preserved exactly
- `Length`: a finite value greater than zero, in station-distance units
- `RollRadians`: a finite constant roll angle around the centerline tangent

Constant-curvature sections also have `Radius`. Radius is signed and non-zero:

- positive radius turns from positive X toward positive Y
- negative radius turns from positive X toward negative Y
- curvature is calculated as `1 / Radius`

`SignedRadius` is an explicit alias for the same value.

Curvature-transition sections have:

- `StartCurvature`: signed curvature at section distance `s = 0`
- `EndCurvature`: signed curvature at section distance `s = Length`
- `InterpolationMode`: currently `CurvatureTransitionInterpolationMode.Linear`

Curvature uses inverse station-distance units. If length is measured in meters,
curvature is measured in inverse meters. The sign convention matches signed
radius: positive curvature turns from positive X toward positive Y, and negative
curvature turns toward negative Y.

For section length `L`, local section distance `s`, start curvature `k0`, and end
curvature `k1`, linear interpolation is:

```text
t = s / L
k(s) = k0 + (k1 - k0) * t
heading(s) = k0 * s + (k1 - k0) * s^2 / (2 * L)
```

The centerline point is the distance integral of the unit tangent defined by
that heading. Equal zero endpoints reduce to a straight line. Equal nonzero
endpoints reduce to a constant-curvature arc. `SmoothStep` and other transition
modes are not part of this milestone.

`RollRadians` remains constant across each authored transition section. Curvature
interpolation does not interpolate or otherwise modify roll.

`SpatialSectionDefinition` describes one local three-dimensional NURBS
centerline with:

- `ControlPoints`: a copied read-only list of backend `Vector3d` values
- `Weights`: a copied read-only list with one positive finite value per point
- `Degree`: the NURBS polynomial degree, defaulting to `3`

Omitting weights creates unit weights. Degree must be at least `1`, and the
control point count must be at least `Degree + 1`. The authoring API does not
expose GShark or `Quantum.Splines` types. Compilation adapts these backend values
to the existing GShark-backed curve implementation internally.

Spatial control points use section-local construction coordinates:

```text
+X = incoming tangent / forward
+Y = incoming unbanked normal
+Z = incoming unbanked binormal
```

The first control point must be exactly the local origin. The local start
tangent must point along positive X; with the existing clamped adapter knot
policy this is validated from the first control-point direction. Invalid input
is rejected rather than silently translated, rotated, or normalized. Custom
knot vectors are not part of this API.

Spatial sections retain an explicit authored `Length`. During compilation the
GShark curve is wrapped by the existing arc-length adapter, and normal runtime
compilation validates the declared section length against measured geometry.
The existing length policy accepts the greater of `1e-3` absolute tolerance or
`1e-6` relative tolerance. A mismatch fails compilation; control points are not
scaled to force the requested length.

## Validation

Section constructors reject invalid IDs, lengths, radii, curvatures, interpolation
modes, and roll values. Transition definitions also reject a non-finite curvature
delta or total heading sweep.

Spatial definitions reject null or empty control-point sequences, non-finite
points, invalid degree/count combinations, invalid weight counts, non-finite or
non-positive weights, and violations of the local origin/positive-X start
contract. Both input sequences are copied before being exposed as read-only
collections.

`TrackStartPose` rejects non-finite positions and basis vectors, near-zero or
non-unit axes, non-orthogonal axes, and left-handed or inconsistent bases.

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

## Boundary Continuity Diagnostics

`TrackAuthoringBoundaryContinuityDiagnostics` inspects authored values at every
adjacent section boundary without compiling geometry or changing the definition.
Diagnostics are non-fatal: the report describes discontinuities but does not
correct authored curvature or roll values and does not affect document building.

For `N` authored sections, the report contains `N - 1` ordered boundaries. A
boundary's `Station` is the cumulative end distance of its previous section.
Each boundary preserves the exact previous and next section IDs.

Curvature endpoints are mapped directly from the authoring definitions:

- `StraightSectionDefinition`: start and end curvature are `0`
- `ConstantCurvatureSectionDefinition`: start and end curvature are `1 / Radius`
- `CurvatureTransitionSectionDefinition`: start is `StartCurvature` and end is
  `EndCurvature`

Spatial geometry does not have one authored scalar boundary curvature, so
`TrackAuthoringBoundaryContinuityDiagnostics` does not support definitions that
contain `SpatialSectionDefinition`. Use the compiled-geometry diagnostics below
when a layout includes spatial sections or when actual generated centerline
continuity matters.

The signed boundary curvature delta is:

```text
curvatureDelta = nextStartCurvature - previousEndCurvature
```

The signed roll delta starts with `nextRollRadians - previousRollRadians` and is
wrapped to the shortest full-turn-equivalent difference in `(-pi, pi]`. Values
that differ by a whole number of turns are therefore continuous. At the exact
half-turn tie, the reported delta is positive `pi`.

`TrackAuthoringBoundaryContinuityTolerances` configures independent non-negative,
finite tolerances for curvature and roll. The default curvature tolerance is
`1e-9` inverse station-distance units and the default roll tolerance is `1e-9`
radians. A diagnostic is emitted only when the absolute delta is strictly greater
than its tolerance; equality is accepted. Invalid negative or non-finite
tolerances throw during tolerance construction.

Reports are deterministic and expose defensive read-only boundary and diagnostic
collections. Boundaries are ordered by authored section order. Diagnostics are
ordered by boundary, with `CurvatureDiscontinuity` before `RollDiscontinuity` when
both are present at the same boundary. Repeated analysis of the same definition
and tolerances produces equivalent values and ordering.

## Geometry Continuity Diagnostics

`TrackAuthoringGeometryContinuityDiagnostics` measures the actual generated
centerline geometry at every adjacent authored boundary. The definition
overloads call `TrackAuthoringDocumentBuilder.Compile` internally and treat the
resulting document curves as the source of truth. The compilation overloads
accept an existing `TrackAuthoringCompilation` and reuse its document/runtime
snapshot instead of compiling the definition again. Both paths support
straight, constant-curvature, curvature-transition, and spatial definitions in
the same mixed layout and produce equivalent reports for the same compilation
state and tolerances.

The two diagnostic paths answer different questions:

- `TrackAuthoringBoundaryContinuityDiagnostics` compares authored scalar
  curvature and roll values without compiling, and does not support spatial
  definitions.
- `TrackAuthoringGeometryContinuityDiagnostics` measures generated positions,
  tangent directions, three-dimensional curvature vectors, and roll values,
  either by compiling a definition internally or by reusing a supplied
  compilation.

For `N` sections, the geometry report contains `N - 1` ordered boundaries with
the same authored IDs, indices, and cumulative previous-end stations used by
the compilation. Measurements are:

```text
positionGap          = nextStartPosition - previousEndPosition
tangentAngle         = atan2(length(cross(previousEndTangent, nextStartTangent)),
                              dot(previousEndTangent, nextStartTangent))
curvatureVectorDelta = nextStartCurvatureVector - previousEndCurvatureVector
rollDelta            = shortest full-turn-wrapped(nextRoll - previousRoll)
```

Endpoint curvature vectors approximate `dT/ds` independently on each curve
with second-order one-sided three-point derivatives. The distance step is the
smaller of `1e-3` station units and `sectionLength / 1024`. Each derivative is
projected perpendicular to its normalized endpoint tangent before the two
vectors are compared. This removes finite-difference drift parallel to the
tangent and allows spatial curvature direction changes to be diagnosed in
world coordinates.

`TrackAuthoringGeometryContinuityTolerances` defaults are:

- position gap magnitude: `1e-7` station-distance units
- tangent angle: `1e-7` radians
- curvature-vector delta magnitude: `1e-4` inverse station-distance units
- wrapped roll delta magnitude: `1e-9` radians

A diagnostic is emitted only when its non-negative measured magnitude or angle
is strictly greater than the matching tolerance; equality is accepted.
Negative and non-finite tolerances are rejected. Diagnostics are ordered by
boundary, then position, tangent, curvature vector, and roll. Reports copy their
boundary and diagnostic collections into read-only snapshots and repeated
analysis is deterministic.

Geometry diagnostics are non-fatal and read-only. They do not modify authored
values, repair boundaries, alter compilation, or change generated curves. A
definition overload pays the full normal authoring compile cost, including
document and runtime compilation, before doing the endpoint measurements. A
compilation overload skips that duplicate compile and measures the supplied
compilation's current document curves using its source definitions and resolved
section intervals.

The compilation overloads do not refresh or repair a compilation after
mutation. `TrackDocument` is mutable under its existing contract, while
`Runtime`, `ResolvedSections`, and `TotalLength` remain the compile-time
snapshot. Replacing a document segment changes the geometry measured by these
diagnostics. Adding or removing segments fails the alignment check; replacing or
reordering segments can produce measurements that no longer match the source
definition or resolved intervals. Treat compiled curves and segment ordering as
immutable, or compile the definition again before analyzing.

Curvature vectors are numerical estimates, not exact symbolic derivatives.
Their accuracy is limited by the finite-difference step, curve tangent quality,
arc-length parameterization accuracy, floating-point scale, and very short or
high-curvature sections. Tolerances should account for those approximation
limits; the defaults are intended for the current authoring curve generators.

## Compilation

`TrackAuthoringDocumentBuilder.Compile` produces a
`TrackAuthoringCompilation` containing:

- `Definition`: the exact validated `TrackAuthoringDefinition` instance
- `Document`: the evaluator-ready `TrackDocument` snapshot
- `Runtime`: a non-null `CompiledTrackRuntime` sampling snapshot compiled from
  that document with `TrackSamplingOptions.Default`
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
- `CurvedSegment` for constant-curvature, curvature-transition, and spatial
  definitions
- exact authored segment lengths and constant roll values
- one generated `GeometricSection` per authoring section in `TrackDocument.Sections`

The builder composes each section from the prior section's endpoint and unbanked
construction frame. Every local section uses the same placement transform:

```text
+X = forward / tangent
+Y = normal / bend reference
+Z = binormal / right

worldPoint     = P + T*x  + N*y  + B*z
worldDirection =     T*dx + N*dy + B*dz
```

Planar section curves retain `z = 0`. For a planar section whose endpoint tangent
is `(tx, ty, 0)`, the following section starts from:

```text
P' = section endpoint
T' =  tx*T + ty*N
N' = -ty*T + tx*N
B' = B
```

This full-basis placement applies to straight, constant-curvature, and
curvature-transition sections. Positive curvature bends from `T` toward `N`;
negative curvature bends toward `-N`. All samples remain in the plane whose
normal is the start binormal. Point and tangent continuity are preserved at
section boundaries.

Spatial sections use the same transform but may have nonzero local Z and leave
the incoming construction plane. Their outgoing unbanked normal is advanced
through the curve with the same rotation-minimizing transport operation and
default per-segment transport sampling density used by canonical runtime frame
compilation. The outgoing tangent, transported normal, and derived binormal then
become the construction basis for the next planar or spatial section. Roll is
not included in this construction-basis transport.

`RollRadians` is not applied during centerline construction and does not alter
section placement. It remains segment metadata and is applied afterward by the
runtime frame evaluator, preserving the distinction between the unbanked
construction frame and the banked sampled frame.

`TrackDocument.Sections` still contains one `GeometricSection` metadata entry per
authored definition. A transition entry has no single constant curvature value,
so its `GeometricSection.Curvature` is null rather than an inaccurate scalar.

Every `Compile` call builds a new document and a new runtime. Repeated
compilations from the same definition are deterministic and produce equivalent
canonical frame samples through `TrackEvaluator`, but the returned `Document`
and `Runtime` objects are distinct snapshots.

Authoring-generated documents populate nullable `TrackDocument.StartPose` with
the exact pose reference from the definition. Manually constructed documents
retain `StartPose == null`. During runtime compilation, the authored start normal
is copied into the compiled sampling context. Canonical frame transport for an
authored document begins by projecting that unbanked normal onto the first
centerline tangent; segment roll is then applied through the existing runtime
path. Null-pose documents retain the previous canonical reference-axis seeding.

This split is the compatibility boundary: the legacy authoring constructor and
an explicit identity pose produce equivalent geometry and canonical frames,
while existing manually constructed documents, train fixtures, and export
snapshots remain on their unchanged null-pose behavior.

The runtime captures segment membership, order, measured lengths, rolls, and
sampling state at compile time. It is not a deep clone of the segment curves:
curve objects are retained by reference and should be treated as immutable for
the lifetime of the runtime. Mutating a referenced curve can invalidate the
runtime's compiled measurements. Recompile after curve mutation.

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

`Quantum.Debug.TransitionAuthoringProofScenario` is the sibling end-to-end proof
for transition authoring. It compiles a zero-roll 12 m entry straight, 6 m
linear transition from curvature 0 to `1/20`, 12 m constant-curvature arc, 6 m
linear transition back to curvature 0, and 12 m exit straight. The four authored
boundaries at 12, 18, 30, and 36 m are curvature- and roll-continuous. It samples
17 frames every 3 m from 0 through 48 m and evaluates five train cars centered at
36, 30, 24, 18, and 12 m, with bogie pairs straddling each section boundary.
Generate its existing-contract debug snapshot with:

```powershell
dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-transition-authoring
```

This dedicated command leaves `AuthoringPipelineProofScenario` and the default
`debug-viewport-snapshot-v1` sample unchanged. It reuses `TrainPoseExportV1` and
`DebugViewportSnapshotV1` without adding contract fields or visualization
behavior.

`Quantum.Debug.SpatialLayoutProofScenario` is the deterministic three-dimensional
authoring proof. It starts from a translated pose yawed 45 degrees with world-up
as the authored normal, then compiles five zero-roll sections: a 12 m entry
straight, an 18 m rising/turning `SpatialSectionDefinition`, a 12 m elevated
straight, an 18 m descending/counter-turning spatial section, and a 12 m exit
straight. The exact section stations are 0, 12, 30, 42, 60, and 72 m. Each
spatial section uses collinear control-point runs at its start and end so its
curvature approaches zero at the neighboring straight joins. The existing
compiled-geometry continuity diagnostics reuse this compilation and report all
four boundaries with no default diagnostics.

The scenario evaluates `compilation.Runtime`, samples 25 frames every 3 m from
0 through 72 m, and verifies displacement along both the authored normal and
binormal axes. Its nine-car train uses 6 m center spacing at stations 60, 54,
48, 42, 36, 30, 24, 18, and 12 m. Cars centered at 60, 42, 30, and 12 m have
bogies on opposite sides of those four section boundaries. Generate the proof
snapshot with:

```powershell
dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-spatial-layout
```

The command exports the existing `DebugViewportSnapshotV1` shape with nine
train-body boxes and the unchanged nested `TrainPoseExportV1` payload. It does
not add geometry types, alter IO contracts, or change runtime, viewer, renderer,
or persistence behavior.

`TrackDocument` remains mutable under its existing contract. A
`TrackEvaluator` constructed from `compilation.Document` remains live and
observes later segment-list mutations on subsequent calls. A `TrackEvaluator`
constructed from `compilation.Runtime` uses the compile-time sampling snapshot
instead.

Adding, removing, replacing, clearing, or reordering `Document.Segments` does
not update `Runtime`, `ResolvedSections`, or `TotalLength`. Such mutations can
therefore break the compilation's index, distance, and geometry alignment. The
runtime continues to sample its captured segment state, while the live document
evaluator follows the mutated document. Compile the definition again to obtain
a fresh aligned document/runtime pair; the compilation does not silently track
or rewrite later document mutations.

## API Boundary

The public authoring APIs expose only backend domain types, scalar values, and
standard collection interfaces. They do not expose:

- `Quantum.Splines` types
- Unity or Unreal types
- Avalonia, Silk.NET, or OpenTK types
- editor or UI state
- persistence or serialization contracts

Spline composition remains an internal implementation detail behind the existing
`TrackDocument` and `TrackEvaluator` boundary. Spatial authoring currently uses
only the existing adapter knot policy: custom knots, interpolation constraints,
and alternative spline backends are outside this milestone.

## Example

```csharp
using Quantum.Math;
using Quantum.Track;
using Quantum.Track.Authoring;

var startPose = new TrackStartPose(
    position: new Vector3d(10.0, 2.0, 5.0),
    tangent: Vector3d.UnitZ,
    normal: Vector3d.UnitY,
    binormal: new Vector3d(-1.0, 0.0, 0.0));

var definition = new TrackAuthoringDefinition(
    new GeometricSectionDefinition[]
    {
        new StraightSectionDefinition("entry", length: 12.0),
        new CurvatureTransitionSectionDefinition(
            "transition-in",
            length: 10.0,
            startCurvature: 0.0,
            endCurvature: 1.0 / 30.0,
            rollRadians: 0.2),
        new ConstantCurvatureSectionDefinition(
            "left-turn",
            length: 15.0,
            radius: 30.0,
            rollRadians: 0.2),
        new StraightSectionDefinition("exit", length: 8.0, rollRadians: 0.2)
    },
    startPose);

TrackAuthoringCompilation compilation =
    TrackAuthoringDocumentBuilder.Compile(definition);
TrackDocument document = compilation.Document;
var runtimeEvaluator = new TrackEvaluator(compilation.Runtime);
var liveDocumentEvaluator = new TrackEvaluator(document);
TrackFrame frame = runtimeEvaluator.EvaluateFrameAtDistance(18.0);

TrackAuthoringBoundaryContinuityReport continuity =
    TrackAuthoringBoundaryContinuityDiagnostics.Analyze(
        definition,
        new TrackAuthoringBoundaryContinuityTolerances(
            curvatureTolerance: 1e-8,
            rollToleranceRadians: 1e-6));

foreach (TrackAuthoringBoundaryContinuityDiagnostic diagnostic in continuity.Diagnostics)
{
    Console.WriteLine(
        $"{diagnostic.Kind} at station {diagnostic.Station}: " +
        $"{diagnostic.PreviousSectionId} -> {diagnostic.NextSectionId}, " +
        $"delta={diagnostic.Delta}");
}

TrackAuthoringGeometryContinuityReport geometryContinuity =
    TrackAuthoringGeometryContinuityDiagnostics.Analyze(definition);
```

This produces a straight followed by a linear curvature transition into a
constant-radius left arc. `SpatialSectionDefinition` can be inserted in the same
ordered section list when an explicitly measured three-dimensional NURBS section
is needed. Custom knots, force-driven geometry, UI workflows, and persistence
remain outside this layer.
