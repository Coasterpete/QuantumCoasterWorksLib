# Quantum CoasterWorks Roadmap

## Current Architecture Direction

Quantum CoasterWorks is moving toward a standalone coaster design / FVD-style / spline-track editor while keeping the backend library independent from any frontend engine.

The intended split is:

- `QuantumCoasterWorksLib` / `Quantum.*`: pure C# backend/domain libraries for track, spline, FVD, physics, IO, train placement, and export/import adapters.
- Current `Assets/Scripts/QuantumVisualizer` Unity visualizer: optional debug/prototype viewer for inspection only. `QCWUnityDebug` is a conceptual/legacy debug surface name, not a current solution project.
- Optional high-fidelity visualization targets: Unity and Unreal may remain valid for PBR previews, ride-through views, cinematic/presentation rendering, and prototype workflows through thin adapters.
- Standalone editor candidate: Avalonia UI shell.
- Technical viewport candidate: Silk.NET, with OpenTK still available for evaluation if it better fits the standalone viewport needs.

## Near-Term Priorities

1. Stable centerline evaluation.
2. Stable orientation frames.
3. Distance-based train car placement.
4. Debug visualization through thin adapters that do not change backend contracts.
5. Backend tests and fixtures that protect distance, frame, and train placement behavior.

## Frontend Direction

Do not choose a final frontend yet. Unity should remain available for the current debug visualizer and prototype workflows, and Unity or Unreal may remain useful optional rendering targets for PBR, ride-through, and presentation workflows. Future editor work should assume separated UI, viewport rendering, visualization adapters, and coaster-domain logic rather than making any one host the owner of backend contracts.

Do not scaffold the Avalonia application until the backend boundaries and first editor workflows are ready enough to justify it.

## Fixture Policy

NoLimits CSV files may be used only as temporary debug/test fixtures from self-authored coaster layouts. CSV fixture support is not a commitment to full NoLimits project import, project compatibility, or third-party asset/layout ingestion.

## Out Of Scope For Now

- Full production editor UI.
- Full custom renderer.
- Full NoLimits replacement.
- Third-party layout or asset import.
- Rewriting backend systems around a frontend engine.
- Choosing a final frontend, viewport, or presentation-rendering engine.
