# Quantum Preview Viewer

`Quantum.PreviewViewer` is a separate prototype app for quick visual inspection of `DebugViewportSnapshotV1` JSON.

Technology:

- ASP.NET Core `net8.0` static host plus tiny repo-local snapshot API.
- Three.js in the browser for the 3D viewport and orbit controls.
- Viewer-local `preview-styles.json` manifest for optional train and future track styling.
- No Unity, Unreal, Avalonia, Silk.NET, or editor architecture commitment.

Run:

```powershell
dotnet run --project Quantum.PreviewViewer
```

Then open the printed localhost URL. The viewer lists valid generated snapshots under `artifacts/debug-viewport`. It can also open a `DebugViewportSnapshotV1` JSON file through the browser file picker.

Current capabilities:

- Shows catalog/file load status, empty/error states, snapshot metadata, scene counts, train metrics, and layer visibility state.
- Loads the viewer style manifest from `Quantum.PreviewViewer/preview-styles.json`.
- Serves repo-local style assets through `/style-assets/...` with `.glb` and `.gltf` model support.
- Renders centerline polylines from `centerlinePoints`.
- Renders frame axes from `frames`.
- Renders `lines` as colored diagnostics.
- Renders oriented train/debug boxes from `boxes`.
- Can replace configured train box roles with GLTF/GLB assets while preserving debug boxes as the fallback and optional overlay.
- Scrubs and plays a visual lead distance through the exported sampled frames with orbit and follow-train camera modes.
- Repositions train placeholder boxes by preserving their exported spacing offsets.
- Supports orbit, pan, zoom, camera reset, and optional follow camera.
- Captures PNG screenshots and WebM recordings through browser canvas APIs.
- Caps selected helper visuals such as sample markers, frame axes, and labels for responsiveness on larger snapshots.

Style manifest shape:

```json
{
  "version": 1,
  "defaultTrainStyle": "debug-boxes",
  "trainStyles": [
    {
      "id": "custom-train",
      "name": "Custom train",
      "debugBoxOverlay": false,
      "roles": {
        "train.lead": {
          "asset": "assets/trains/custom-lead.glb",
          "fitToBox": true,
          "fitMode": "uniform",
          "scale": 1.0,
          "offset": { "x": 0.0, "y": 0.0, "z": 0.0 },
          "rotationDegrees": { "x": 0.0, "y": 90.0, "z": 0.0 }
        },
        "train.middle": {
          "asset": "assets/trains/custom-middle.glb",
          "fitToBox": true,
          "fitMode": "uniform"
        },
        "train.rear": {
          "asset": "assets/trains/custom-rear.glb",
          "fitToBox": true,
          "fitMode": "uniform"
        },
        "train.body": {
          "asset": "assets/trains/custom-fallback-car.glb",
          "fitToBox": true,
          "fitMode": "uniform",
          "scale": 1.0,
          "offset": { "x": 0.0, "y": 0.0, "z": 0.0 },
          "rotationDegrees": { "x": 0.0, "y": 0.0, "z": 0.0 }
        }
      }
    }
  ],
  "trackStyles": []
}
```

Train body boxes are still exported as `train.body` by `DebugViewportSnapshotV1`. The viewer resolves the visual asset role per car: first car uses `train.lead`, last car uses `train.rear`, interior cars use `train.middle`, and any missing variant falls back to `train.body`. A one-car train uses `train.lead`. See [preview-viewer-train-styles.md](../docs/visualization/preview-viewer-train-styles.md) for configuration examples.

Asset paths are resolved under the configured preview asset root, which defaults to the manifest directory. `PreviewStyleManifest` and `PreviewStyleAssetRoot` can be set through app configuration if the manifest or assets need to live elsewhere in the repository.

Limitations:

- This is not a production editor.
- No authoring tools, supports, generated track mesh, or material editing.
- `trackStyles` is reserved in the manifest but not rendered yet.
- Styled train assets are viewer-side visuals only; snapshot contracts and backend train placement are unchanged.
- Moving train boxes are a viewer-side inspection aid based on exported sample interpolation, not a backend simulation change.
- Three.js modules are loaded from a pinned CDN URL for the spike; vendor or package them later if the viewer needs offline operation.
