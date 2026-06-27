# Support Anchor Spacing V1

Support Anchor Spacing V1 is a small backend-only Quantum.Track domain utility for
placing support anchor candidates along canonical station distance.

It is not full support generation.

## Scope

The V1 API accepts:

- Start distance.
- End distance.
- Target spacing.
- Optional start offset.
- Excluded distance ranges.

It returns:

- Generated anchor candidate distances.
- Actual intervals between generated candidates.
- Start gap and end remainder information.
- Warnings for invalid inputs, excluded candidates or gaps, and uneven end
  remainders.

## Non-Goals

Support Anchor Spacing V1 does not:

- Snap anchors to terrain.
- Evaluate support geometry.
- Choose support prefabs.
- Create meshes.
- Depend on Unity, Unreal, or renderer APIs.
- Change `TrackEvaluator` behavior.

The output is intentionally just canonical station-distance data so later support
systems can consume it without coupling the Quantum backend to a visualization target.
