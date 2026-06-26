# Preview Viewer Train Styles

`Quantum.PreviewViewer` train styles are viewer-side only. They do not change
`DebugViewportSnapshotV1`, train placement, physics, or backend domain projects.

The style manifest defaults to `Quantum.PreviewViewer/preview-styles.json`.
Asset paths are resolved under the configured preview asset root, which defaults
to the manifest directory. Assets must stay inside that root and primary train
models must be `.glb` or `.gltf`.

## Train Role Variants

Snapshot train body boxes keep the stable exported role `train.body`. The viewer
resolves a styling role per car:

- First car: `train.lead`
- Interior cars: `train.middle`
- Last car: `train.rear`
- Missing variant: `train.body`

A one-car train uses `train.lead`. Existing `train.body.banking-profile` debug
boxes also participate in the same lead/middle/rear resolution and can fall back
to `train.body`.

## Lead, Middle, Rear Example

```json
{
  "version": 1,
  "defaultTrainStyle": "role-variant-train",
  "trainStyles": [
    {
      "id": "role-variant-train",
      "name": "Role variant train",
      "roles": {
        "train.lead": {
          "asset": "assets/trains/lead-car.glb",
          "fitToBox": true,
          "fitMode": "uniform",
          "rotationDegrees": { "x": 0.0, "y": 90.0, "z": 0.0 },
          "offset": { "x": 0.0, "y": 0.0, "z": 0.0 },
          "scale": 1.0
        },
        "train.middle": {
          "asset": "assets/trains/middle-car.glb",
          "fitToBox": true,
          "fitMode": "uniform"
        },
        "train.rear": {
          "asset": "assets/trains/rear-car.glb",
          "fitToBox": true,
          "fitMode": "uniform"
        },
        "train.body": {
          "asset": "assets/trains/fallback-car.glb",
          "fitToBox": true,
          "fitMode": "uniform"
        }
      }
    }
  ],
  "trackStyles": []
}
```

## Fallback-Only Example

Use only `train.body` when every car can share the same model or when you want a
safe fallback before adding variants:

```json
{
  "version": 1,
  "defaultTrainStyle": "simple-train",
  "trainStyles": [
    {
      "id": "simple-train",
      "name": "Simple train",
      "roles": {
        "train.body": {
          "asset": "assets/trains/shared-car.glb",
          "fitToBox": true,
          "fitMode": "uniform"
        }
      }
    }
  ],
  "trackStyles": []
}
```

## Fitting And Alignment

Each role entry can use:

- `fitToBox`: defaults to `true`; set `false` to keep model size.
- `fitMode`: `uniform`, `stretch`, `cover`, or `none`.
- `rotationDegrees`: local model rotation before bounds fitting.
- `scale`: uniform number or `{ "x": 1.0, "y": 1.0, "z": 1.0 }` after fitting.
- `offset`: local `{ "x": 0.0, "y": 0.0, "z": 0.0 }` offset in meters.
- `center`: defaults to `true`; set `false` to preserve the model origin when fitting is disabled.
- `debugBoxOverlay`: set `true` on a role or train style to keep a thin debug box over loaded assets.

Quantum local train axes are `+X` forward along the tangent, `+Y` up along the
normal, and `+Z` across the binormal. Use `rotationDegrees` when a GLTF asset has
a different forward or up axis.

If an asset cannot be loaded, the viewer keeps the generated debug box for that
car. This fallback is intentional and does not mark the snapshot invalid.

## Playback Smoothing

Train styles do not change playback placement. The viewer moves every dynamic
train visual by preserving its exported spacing offset from the lead car and
sampling the preview path at the current lead distance plus that offset.

For sparse snapshots, the viewer builds a denser evaluated centerline before
applying the selected train style:

- With three or more strictly increasing `frames`, train positions use cubic
  Hermite interpolation over frame distance, using exported tangents as endpoint
  derivatives. The active tangent comes from the smoothed position curve, while
  exported normal/binormal axes are interpolated and re-orthonormalized for
  stable placeholder orientation.
- If no usable frames are exported, the same smooth position interpolation is
  applied to `centerlinePoints` and a simple Y-up frame is rebuilt.
- With fewer samples or repeated distances, playback falls back to the older
  linear interpolation.

The Centerline layer defaults to the smoothed evaluated path. The Layers panel
can switch it back to Raw to inspect the original `centerlinePoints` polyline and
sample markers, and the inspector reports raw versus smoothed sample counts.
Style role metadata and debug-box fallback behavior are unchanged. This is a
preview aid only; it does not rewrite `DebugViewportSnapshotV1` JSON or change
backend train pose evaluation.
