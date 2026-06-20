# Quantum Preview Viewer

`Quantum.PreviewViewer` is a separate prototype app for quick visual inspection of `DebugViewportSnapshotV1` JSON.

Technology:

- ASP.NET Core `net8.0` static host plus tiny repo-local snapshot API.
- Three.js in the browser for the 3D viewport and orbit controls.
- No Unity, Unreal, Avalonia, Silk.NET, or editor architecture commitment.

Run:

```powershell
dotnet run --project Quantum.PreviewViewer
```

Then open the printed localhost URL. The viewer lists valid generated snapshots under `artifacts/debug-viewport`. It can also open a `DebugViewportSnapshotV1` JSON file through the browser file picker.

Current capabilities:

- Shows catalog/file load status, empty/error states, snapshot metadata, scene counts, train metrics, and layer visibility state.
- Renders centerline polylines from `centerlinePoints`.
- Renders frame axes from `frames`.
- Renders `lines` as colored diagnostics.
- Renders oriented train/debug boxes from `boxes`.
- Scrubs and plays a visual lead distance through the exported sampled frames with orbit and follow-train camera modes.
- Repositions train placeholder boxes by preserving their exported spacing offsets.
- Supports orbit, pan, zoom, camera reset, and optional follow camera.
- Captures PNG screenshots and WebM recordings through browser canvas APIs.
- Caps selected helper visuals such as sample markers, frame axes, and labels for responsiveness on larger snapshots.

Limitations:

- This is not a production editor.
- No authoring tools, supports, track styles, mesh import, or material system.
- Moving train boxes are a viewer-side inspection aid based on exported sample interpolation, not a backend simulation change.
- Three.js modules are loaded from a pinned CDN URL for the spike; vendor or package them later if the viewer needs offline operation.
