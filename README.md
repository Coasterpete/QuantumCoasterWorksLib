# Quantum CoasterWorks

Quantum CoasterWorks is an early-stage coaster design and simulation backend. The current repository is focused on deterministic track, spline, FVD, physics, IO, and train placement systems rather than a finished editor application.

## Current Status

- Early development / technical preview.
- Backend-first architecture: the `Quantum.*` projects should stay engine-agnostic C# libraries.
- Unity visualization is experimental and should be treated as an optional debug/prototype viewer, not the owner of backend architecture.
- Unity and Unreal may remain valid optional visualization targets for PBR previews, ride-through views, and presentation rendering through thin adapters.
- No final frontend is selected. Avalonia remains a standalone editor shell candidate, with Silk.NET or OpenTK as possible technical viewport layers.

## Project Shape

- `Quantum.Core`, `Quantum.Math`, `Quantum.Splines`, `Quantum.Track`, `Quantum.FVD`, `Quantum.Physics`, and `Quantum.IO` contain backend/domain logic.
- `Quantum.Debug` contains backend diagnostics and command-line tooling.
- `Quantum.Tests` contains automated tests and contract fixtures.
- `Assets` contains the current Unity debug visualizer/prototype assets.

See `ROADMAP.md` and `docs/architecture/frontend-strategy.md` for the current architecture direction.
