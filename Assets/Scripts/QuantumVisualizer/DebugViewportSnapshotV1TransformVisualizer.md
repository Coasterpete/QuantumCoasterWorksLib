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

- wrapper local +X = backend tangent / box length
- wrapper local +Y = backend normal / box height
- wrapper local +Z = backend binormal / box width

## Prefab Slots

Each stable role has an optional prefab slot:

- `train.body`
- `train.body.banking-profile`
- `train.bogie`
- `train.wheel`
- `unknown`

When a prefab is assigned, the generated wrapper still drives pose and scale, and
the prefab is instantiated below it at local identity. The prefab convention is:

- Author the prefab pivot at the visual box center.
- Keep the prefab root local position at `0,0,0` under the generated wrapper.
- Keep the prefab root local rotation at identity under the generated wrapper.
- Keep the prefab root local scale at `1,1,1`.
- Let the generated wrapper own snapshot `length,height,width` scale.

When no prefab is assigned, a unit fallback cube is created below the wrapper at
local identity and inherits the wrapper scale.

## Validation Notes

The built-in `DebugViewportSnapshotV1.sample.json` should generate two
`train.body` wrappers. The BankingProfile sample should generate three
`train.body.banking-profile` wrappers. Assigning a simple self-authored cube prefab
to a role slot should leave the generated wrapper responsible for frame pose and
box scale.

The generated console summary reports total boxes, per-role counts, prefab
instances, fallback cubes, and skipped boxes. Use it as a quick check when swapping
between assigned prefabs and fallback cubes.

## TestingGrounds1 Manual Recipe

Use `C:\Dev4\TestingGrounds1` only as a local Unity validation project. Do not
commit that Unity project into this repository.

1. Generate current backend artifacts from the repository root:

```powershell
.\tools\demo-technical-preview-0.1.cmd
```

2. Copy the snapshot visualizer scripts into
   `C:\Dev4\TestingGrounds1\Assets\Scripts\QuantumVisualizer`.
3. Copy generated snapshot JSON files into a Unity `Assets` folder so Unity imports
   them as `TextAsset`s.
4. Add `DebugViewportSnapshotV1GizmoVisualizer` and
   `DebugViewportSnapshotV1TransformVisualizer` to the same empty GameObject and
   assign the same snapshot `TextAsset` to both.
5. Leave the relevant prefab slot empty, rebuild, and confirm generated boxes have
   `FallbackCube` children at local identity.
6. Assign a simple self-authored cube prefab with its pivot at the cube center to
   the relevant role slot, rebuild, and confirm generated boxes have `Prefab`
   children at local identity.
7. Confirm every generated wrapper local scale equals the snapshot dimensions in
   Unity order: `x = length`, `y = height`, `z = width`.

Expected sample counts:

- Built-in sample: `train.body` has 2 wrappers.
- BankingProfile sample: `train.body.banking-profile` has 3 wrappers.

## Optional GLB Readiness

GLB validation is optional and limited to self-authored cube-like assets for now.
This milestone does not add a production model pipeline.

For a self-authored cube GLB, import it into TestingGrounds1 and check only:

- pivot is at the visual box center
- orientation matches wrapper axes without corrective child rotation
- import scale allows the prefab root to stay at `1,1,1`
- wrapper scale remains the only source of snapshot length/height/width sizing
