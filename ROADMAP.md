# Quantum CoasterWorks Roadmap

## Current Architecture Direction

Quantum CoasterWorks is moving toward a standalone coaster design / FVD-style / spline-track editor while keeping the backend library independent from any frontend engine.

The intended split is:

- `QuantumCoasterWorksLib` / `Quantum.*`: pure C# backend/domain libraries for track, spline, FVD, physics, IO, train placement, and export/import adapters.
- Current `Assets/Scripts/QuantumVisualizer` Unity visualizer: optional debug/prototype viewer for inspection only. `QCWUnityDebug` is a conceptual/legacy debug surface name, not a current solution project.
- Future standalone editor: Avalonia UI shell.
- Future viewport: Silk.NET or OpenTK renderer, evaluated when viewport needs are clearer.

## Near-Term Priorities

1. Stable centerline evaluation.
2. Stable orientation frames.
3. Distance-based train car placement.
4. Debug visualization through thin adapters that do not change backend contracts.
5. Backend tests and fixtures that protect distance, frame, and train placement behavior.

## Frontend Direction

Unity should remain available for the current debug visualizer and prototype workflows, but it is not the long-term application foundation. Future editor work should assume a standalone desktop application where UI, viewport rendering, and coaster-domain logic are separate layers.

Do not scaffold the Avalonia application until the backend boundaries and first editor workflows are ready enough to justify it.

## Fixture Policy

NoLimits CSV files may be used only as temporary debug/test fixtures from self-authored coaster layouts. CSV fixture support is not a commitment to full NoLimits project import, project compatibility, or third-party asset/layout ingestion.

## Out Of Scope For Now

- Full production editor UI.
- Full custom renderer.
- Full NoLimits replacement.
- Third-party layout or asset import.
- Rewriting backend systems around a frontend engine.
