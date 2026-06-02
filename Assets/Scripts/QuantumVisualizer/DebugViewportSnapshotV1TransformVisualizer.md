# DebugViewportSnapshotV1 Transform Visualizer

`DebugViewportSnapshotV1TransformVisualizer` is a Unity-only adapter for the `boxes`
array in `quantum.debug_viewport_snapshot` JSON. It does not evaluate backend track
logic and it does not draw centerlines, frame axes, or debug lines. Keep those layers
on `DebugViewportSnapshotV1GizmoVisualizer`.

## Setup

Editor window path:

1. Open `Window > Quantum > Snapshot Browser`.
2. Use Generated Artifacts to copy backend output from `artifacts/debug-viewport`
   into `Assets/DebugData`, or select an existing `DebugViewportSnapshotV1` JSON
   `TextAsset`.
3. Use the discovered snapshot list. Rows are grouped as Built-in,
   BankingProfile, CSV fixtures, Other valid snapshots, and invalid/unknown
   DebugData JSON warnings. Valid rows show `metadata.sourceFixtureName`,
   `metadata.sampleCount`, centerline, frame, box, train pose, and train pose car
   counts.
4. Click a row `Load` action to inspect stats, metadata, and role counts, or
   click row `Apply` to load the snapshot and apply it to the scene viewer.
5. Click `Create / Update Viewer`.
6. Click `Select Viewer` to select `Quantum Snapshot Viewer` in the scene.
7. Click `Rebuild Generated Boxes` or `Clear Generated Boxes`.

Manual component path:

1. Add `DebugViewportSnapshotV1GizmoVisualizer` and
   `DebugViewportSnapshotV1TransformVisualizer` to the same empty GameObject.
2. Assign the same snapshot `TextAsset` to both components, or use the snapshot
   browser to apply the selected asset to both components.
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

The Snapshot Browser also reports role counts before generation. Expected rows
include `train.body`, `train.body.banking-profile`, `train.bogie`, `train.wheel`,
and `unknown`. Use those counts to confirm the generated hierarchy groups match
the selected snapshot before inspecting the generated child objects.

## TestingGrounds1 Manual Recipe

Use `C:\Dev4\TestingGrounds1` only as a local Unity validation project. Do not
commit that Unity project into this repository.

1. Generate current backend artifacts from the repository root:

```powershell
.\tools\demo-technical-preview-0.1.cmd
```

2. Copy the snapshot visualizer scripts into
   `C:\Dev4\TestingGrounds1\Assets\Scripts\QuantumVisualizer`.
3. In `Window > Quantum > Snapshot Browser`, use Generated Artifacts to import
   JSON, SVG, and HTML output from `artifacts/debug-viewport` into
   `Assets/DebugData`. You can also run `tools/sync-unity-debug-data.ps1` from
   the repository if you are copying into a separate Unity project.
4. Add `DebugViewportSnapshotV1GizmoVisualizer` and
   `DebugViewportSnapshotV1TransformVisualizer` to the same empty GameObject and
   assign the same snapshot `TextAsset` to both, or open
   `Window > Quantum > Snapshot Browser` and click `Create / Update Viewer`.
5. In the snapshot browser, confirm the built-in sample reports 9 centerline
   points, 9 frames, 3 lines, 2 boxes, trainPose present, and 2 cars.
6. In the snapshot browser, confirm the BankingProfile sample reports 10
   centerline points, 10 frames, 3 lines, 3 boxes, trainPose present, and 3 cars.
7. Leave the relevant prefab slot empty, rebuild, and confirm generated boxes have
   `FallbackCube` children at local identity.
8. Assign a simple self-authored cube prefab with its pivot at the cube center to
   the relevant role slot, rebuild, and confirm generated boxes have `Prefab`
   children at local identity.
9. Confirm every generated wrapper local scale equals the snapshot dimensions in
   Unity order: `x = length`, `y = height`, `z = width`.
10. In the snapshot browser, click `Select Viewer` and confirm the viewer is
    selected. If `Assets/DebugData/index.html` or `Assets/DebugData/browser.html`
    exist, confirm the artifact launch buttons open the local pages.
11. Clean and reimport generated artifacts while the browser is open, then confirm
    the grouped snapshot rows refresh and the current selection reloads or clears
    without a stale-selection exception.

Expected sample counts:

- Built-in sample: `train.body` has 2 wrappers.
- BankingProfile sample: `train.body.banking-profile` has 3 wrappers.
- CSV fixture snapshots: centerline and frame counts match
  `metadata.sampleCount`, with no generated boxes and no nested train pose.

## Optional GLB Readiness

GLB validation is optional and limited to self-authored cube-like assets for now.
This milestone does not add a production model pipeline.

For a self-authored cube GLB, import it into TestingGrounds1 and check only:

- pivot is at the visual box center
- orientation matches wrapper axes without corrective child rotation
- import scale allows the prefab root to stay at `1,1,1`
- wrapper scale remains the only source of snapshot length/height/width sizing
