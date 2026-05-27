# Quantum CoasterWorks Technical Preview 0.1 Release Notes

## What This Release Is

Technical Preview 0.1 is a backend-first release candidate for Quantum CoasterWorks. It proves a narrow, renderer-neutral workflow for stable centerline sampling, orientation frame diagnostics, distance-based train placeholder output, CSV fixture bridging, snapshot validation, simple SVG debug previews, a generated local debug gallery, and repeatable tests.

The preview is intended for backend inspection, test automation, and thin debug viewers that consume JSON contracts at the adapter boundary. The SVG previews and generated gallery are plain text local artifacts for quick technical inspection; they are not a renderer, editor, frontend scaffold, or polished visualization surface.

## What This Release Is Not

Technical Preview 0.1 is not production-ready software, a polished editor, a full NoLimits replacement, a paid alpha, or a renderer showcase. It does not commit the project to Unity, Unreal, Avalonia, Silk.NET, OpenTK, Veldrid, or any other final frontend or renderer.

It does not include PBR rendering, ray tracing, complete NoLimits project import, third-party layout ingestion, scenery import, scripting compatibility, or editor parity.

## Release Candidate Checklist

Use this checklist from a clean checkout before tagging or packaging Technical Preview 0.1:

- [ ] Clean checkout builds and restores successfully.
- [ ] `dotnet test QuantumCoasterWorks.sln --nologo` passes.
- [ ] Built-in `DebugViewportSnapshotV1` snapshot generation works.
- [ ] Milestone 7 CSV fixture pack to `DebugViewportSnapshotV1` snapshot generation works.
- [ ] Snapshot validation accepts the generated built-in and CSV-derived snapshots.
- [ ] SVG preview generation works for the built-in sample and Milestone 7 fixture pack snapshots.
- [ ] Optional generated gallery is written to `artifacts/debug-viewport/index.html`.
- [ ] `.\tools\demo-technical-preview-0.1.cmd` runs the backend demo flow successfully on Windows.
- [ ] Direct PowerShell fallback works when needed: `powershell -ExecutionPolicy Bypass -File .\tools\demo-technical-preview-0.1.ps1`.
- [ ] Generated artifacts stay local by default, especially under `artifacts/`.
- [ ] `Quantum.*` backend projects have no Unity, Unreal, Avalonia, Silk.NET, OpenTK, Veldrid, renderer, or frontend dependencies.
- [ ] Public docs clearly state Technical Preview 0.1 scope and non-goals.

## Dry Run From A Fresh Checkout

Run these commands from the repository root after checking out the intended Technical Preview 0.1 branch or tag:

```powershell
dotnet restore QuantumCoasterWorks.sln
.\tools\demo-technical-preview-0.1.cmd
git status --short
```

Direct PowerShell fallback:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\demo-technical-preview-0.1.ps1
```

Expected dry-run result:

- Tests pass.
- `Quantum.Debug -- help` lists the snapshot generation, CSV bridge, and validation commands.
- The demo writes simple SVG technical debug previews under `artifacts/debug-viewport/`.
- The demo writes a local static gallery at `artifacts/debug-viewport/index.html`.
- All generated snapshots validate successfully.
- Generated JSON, SVG, and HTML remain local under `artifacts/`.
- `git status --short` does not show generated artifacts as tracked changes.

Generated debug viewport outputs include the built-in `DebugViewportSnapshotV1.sample.svg` plus one SVG preview for each existing Milestone 7 synthetic fixture: `straight_line`, `simple_hill`, `banked_turn`, and `descending_ascending_curve`.

## Known Limitations

- The CSV path is a sampled-frame debug/test bridge for self-authored or synthetic fixtures, not a full NoLimits project importer.
- Debug viewport snapshots carry renderer-neutral centerline, frame, line, box, and optional train pose data only. They do not define cameras, materials, meshes, scene objects, editor UI, or coordinate conversion policy for a specific engine.
- The SVG previews and generated gallery are intentionally tiny and top-down. They help inspect centerline output without adding renderer or frontend dependencies.
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
