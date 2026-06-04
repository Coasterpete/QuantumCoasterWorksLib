# Blender MeshExportV1 Sample Viewer

Last updated: 2026-06-04

`tools/blender/import_mesh_export_v1.py` imports the deterministic
`MeshExportV1` sample JSON into Blender as generated diagnostic mesh objects.
This is a Blender/tooling adapter only. It is not a real track mesh exporter and
does not add Blender, GLTF, shader, renderer, Unity, or editor dependencies to
any `Quantum.*` backend project.

## Generate The Sample

From the repository root:

```powershell
dotnet run --project Quantum.Debug -- mesh-export-v1-sample artifacts/mesh-export/MeshExportV1.sample.json
```

The generated JSON is a tiny self-authored quad using the neutral
`quantum.mesh_export` v1 contract. It should stay under ignored `artifacts/`
unless a release milestone explicitly says otherwise.

## Import In Blender

Run Blender from the repository root:

```powershell
blender --background --factory-startup --python tools/blender/import_mesh_export_v1.py -- artifacts/mesh-export/MeshExportV1.sample.json
```

For interactive use, omit `--background --factory-startup`; if no path is
provided, the script opens a Blender JSON file picker.

The importer:

- verifies `contract == "quantum.mesh_export"` and `version == 1`
- creates or reuses a generated collection named `Quantum MeshExportV1`
- clears only that generated collection before rebuilding
- reads mesh names, vertices, flat `triangleIndices`, optional per-vertex
  normals, and neutral `materialSlotLabels`
- maps Quantum Y-up positions and normals to Blender Z-up coordinates
- reverses triangle winding at the adapter boundary to preserve visual normal
  direction after the axis swap
- creates Blender mesh objects from the sample topology
- assigns simple local diagnostic materials from neutral material slot labels
- prints mesh, vertex, triangle, normal, and material counts

## Boundary Notes

`MeshExportV1` material slot labels are semantic labels only. The Blender
importer maps them to local diagnostic materials such as
`Quantum.MeshExportV1.debug.surface`; those Blender materials are not part of
the backend contract.

The importer does not evaluate Quantum backend code inside Blender, create a
real track mesh, import Unity assets, export GLTF/GLB, save `.blend` files, or
modify `DebugViewportSnapshotV1` or `TrainPoseExportV1`.

## Validation

Useful checks for this path:

```powershell
python -m py_compile tools\blender\import_mesh_export_v1.py
dotnet run --project Quantum.Debug -- mesh-export-v1-sample artifacts/mesh-export/MeshExportV1.sample.json
blender --background --factory-startup --python tools/blender/import_mesh_export_v1.py -- artifacts/mesh-export/MeshExportV1.sample.json
```

Generated `.blend`, PNG, converted mesh, and ignored `artifacts/` outputs should
not be committed.
