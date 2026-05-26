# Quantum CoasterWorks Technical Preview 0.1 Release Notes

## What This Release Is

Technical Preview 0.1 is a backend-first release candidate for Quantum CoasterWorks. It proves a narrow, renderer-neutral workflow for stable centerline sampling, orientation frame diagnostics, distance-based train placeholder output, CSV fixture bridging, snapshot validation, and repeatable tests.

The preview is intended for backend inspection, test automation, and thin debug viewers that consume JSON contracts at the adapter boundary.

## What This Release Is Not

Technical Preview 0.1 is not production-ready software, a polished editor, a full NoLimits replacement, a paid alpha, or a renderer showcase. It does not commit the project to Unity, Unreal, Avalonia, Silk.NET, OpenTK, Veldrid, or any other final frontend or renderer.

It does not include PBR rendering, ray tracing, complete NoLimits project import, third-party layout ingestion, scenery import, scripting compatibility, or editor parity.

## Release Candidate Checklist

Use this checklist from a clean checkout before tagging or packaging Technical Preview 0.1:

- [ ] Clean checkout builds and restores successfully.
- [ ] `dotnet test QuantumCoasterWorks.sln --nologo` passes.
- [ ] Built-in `DebugViewportSnapshotV1` snapshot generation works.
- [ ] CSV fixture to `DebugViewportSnapshotV1` snapshot generation works.
- [ ] Snapshot validation accepts the generated built-in and CSV-derived snapshots.
- [ ] Generated artifacts stay local by default, especially under `artifacts/`.
- [ ] `Quantum.*` backend projects have no Unity, Unreal, Avalonia, Silk.NET, OpenTK, Veldrid, renderer, or frontend dependencies.
- [ ] Public docs clearly state Technical Preview 0.1 scope and non-goals.

## Dry Run From A Fresh Checkout

Run these commands from the repository root after checking out `milestone-10-technical-preview-0.1-rc`:

```powershell
dotnet restore QuantumCoasterWorks.sln
dotnet test QuantumCoasterWorks.sln --nologo

dotnet run --project Quantum.Debug -- help

dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1 artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json

dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-from-csv Quantum.Tests/IO/Fixtures/Milestone7.synthetic.straight_line.centerline_frames.csv artifacts/debug-viewport/Milestone7.synthetic.straight_line.snapshot.json

dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-validate artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json

dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-validate artifacts/debug-viewport/Milestone7.synthetic.straight_line.snapshot.json

git status --short
```

Expected dry-run result:

- Tests pass.
- `Quantum.Debug -- help` lists the snapshot generation, CSV bridge, and validation commands.
- Both generated snapshots validate successfully.
- Generated JSON remains local under `artifacts/`.
- `git status --short` does not show generated artifacts as tracked changes.

## Known Limitations

- The CSV path is a sampled-frame debug/test bridge for self-authored or synthetic fixtures, not a full NoLimits project importer.
- Debug viewport snapshots carry renderer-neutral centerline, frame, line, box, and optional train pose data only. They do not define cameras, materials, meshes, scene objects, editor UI, or coordinate conversion policy for a specific engine.
- Train visualization remains placeholder-oriented. Simple boxes and diagnostic transforms are acceptable for this preview.
- Fixture coverage is intentionally small and synthetic. It protects the current contract path but does not claim broad coaster layout compatibility.
- Backend physics, force, and train systems are still early technical foundations and should not be treated as production simulation guarantees.

## Next After TP 0.1

After Technical Preview 0.1, the conservative next step is to keep strengthening the backend slice before committing to larger product surfaces:

- Expand contract tests around distance semantics, frames, CSV fixture validation, and snapshot stability.
- Add more self-authored or synthetic fixtures only where they cover a concrete backend risk.
- Improve debug viewer adapters as thin consumers of existing JSON contracts without moving renderer concerns into `Quantum.*`.
- Refine train placeholder diagnostics and validation around car spacing, frame stability, and station-distance placement.
- Keep frontend, renderer, and editor choices exploratory until the backend boundaries are stable enough to justify them.
