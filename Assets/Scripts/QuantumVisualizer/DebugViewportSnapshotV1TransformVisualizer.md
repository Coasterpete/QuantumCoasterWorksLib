# DebugViewportSnapshotV1 Transform Visualizer

`DebugViewportSnapshotV1TransformVisualizer` is a Unity-only adapter for the `boxes`
array in `quantum.debug_viewport_snapshot` JSON. It does not evaluate backend track
logic and it does not draw centerlines, frame axes, or debug lines. Keep those layers
on `DebugViewportSnapshotV1GizmoVisualizer`.

## Setup

1. Add `DebugViewportSnapshotV1GizmoVisualizer` and
   `DebugViewportSnapshotV1TransformVisualizer` to the same empty GameObject.
2. Assign the same snapshot `TextAsset` to both components.
3. Use the transform visualizer context menu actions:
   - `Rebuild`
   - `Clear`

## Generated Hierarchy

Rebuild clears and recreates one child root:

```text
GeneratedSnapshot
  train.body
  train.body.banking-profile
  train.bogie
  train.wheel
  unknown
```

Each valid snapshot box becomes a deterministic wrapper under its role group. The
wrapper receives the box frame position/rotation and a local scale of:

```text
length, height, width
```

This matches the backend box convention:

- length follows tangent
- height follows normal
- width follows binormal

## Prefab Slots

Each stable role has an optional prefab slot:

- `train.body`
- `train.body.banking-profile`
- `train.bogie`
- `train.wheel`
- `unknown`

When a prefab is assigned, the generated wrapper still drives pose and scale, and
the prefab is instantiated below it at local identity. When no prefab is assigned,
a unit fallback cube is created below the wrapper and inherits the wrapper scale.

## Validation Notes

The built-in `DebugViewportSnapshotV1.sample.json` should generate two
`train.body` wrappers. The BankingProfile sample should generate three
`train.body.banking-profile` wrappers. Assigning a simple self-authored cube prefab
to a role slot should leave the generated wrapper responsible for frame pose and
box scale.
