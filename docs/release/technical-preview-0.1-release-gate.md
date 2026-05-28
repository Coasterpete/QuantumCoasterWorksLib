# Technical Preview 0.1 Release Gate

This is the lightweight release-readiness gate for Technical Preview 0.1. It checks that a contributor can start from a clean checkout, restore dependencies, build the backend solution, run tests, and generate the current backend debug preview artifacts without relying on machine-local project state.

## Required Tools

- Git.
- .NET SDK 8.0 or newer. `Quantum.Debug` and `Quantum.Tests` target `net8.0`; backend libraries target `netstandard2.1`.
- NuGet package access for first restore, unless dependencies are already cached locally.
- Windows PowerShell or PowerShell Core for the Technical Preview demo script. The checked-in `.cmd` wrapper is Windows-only.

Unity, Blender, Visual Studio, and renderer/editor tools are optional for this gate.

## Clean Checkout Validation

Run from a newly cloned repository:

```powershell
dotnet restore QuantumCoasterWorks.sln
dotnet build QuantumCoasterWorks.sln --no-restore --nologo
dotnet test QuantumCoasterWorks.sln --no-build --nologo
```

Then run the backend preview flow on Windows:

```powershell
.\tools\demo-technical-preview-0.1.cmd
```

If script execution policy blocks direct PowerShell execution, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\demo-technical-preview-0.1.ps1
```

## Release Checklist

- [ ] Fresh clone restores with `dotnet restore QuantumCoasterWorks.sln`.
- [ ] Fresh clone builds with `dotnet build QuantumCoasterWorks.sln --no-restore --nologo`.
- [ ] Fresh clone tests pass with `dotnet test QuantumCoasterWorks.sln --no-build --nologo`.
- [ ] `.\tools\demo-technical-preview-0.1.cmd` completes successfully on Windows.
- [ ] Generated files remain under ignored `artifacts/` paths unless intentionally attached to a release.
- [ ] `git status --short` shows only intentional source/doc changes before tagging.
- [ ] `Quantum.*` backend projects remain free of Unity, Unreal, Avalonia, Silk.NET, OpenTK, Veldrid, renderer, or frontend dependencies.
- [ ] Fixtures remain self-authored or synthetic, with no proprietary or permission-unclear assets added.
- [ ] Public docs describe Technical Preview 0.1 as backend-first, early, and not production-ready.

## Latest Local Gate Run

Validated on 2026-05-28 from a temporary local clone:

- `dotnet restore QuantumCoasterWorks.sln`: passed.
- `dotnet build QuantumCoasterWorks.sln --no-restore --nologo`: passed with 0 warnings and 0 errors.
- `dotnet test QuantumCoasterWorks.sln --no-build --nologo`: passed, 862 tests.
- `.\tools\demo-technical-preview-0.1.cmd`: passed, including generated built-in and Milestone 7 snapshot validation plus SVG/gallery/index generation.
- SDK observed during validation: .NET SDK 10.0.300.

## Assumptions And Fragile Steps

- There is no `global.json`; release validation uses whichever compatible SDK `dotnet` selects. Record `dotnet --info` for each release gate run.
- There is no checked-in package lock file. A first restore depends on NuGet source availability and package compatibility.
- The demo `.cmd` wrapper is Windows-specific. Non-Windows contributors can still run the documented `dotnet` commands, but the full scripted preview flow currently assumes PowerShell script compatibility.
- Generated previews, snapshots, galleries, and export files are local artifacts under `artifacts/` and should stay out of source control by default.
