# Quantum CoasterWorks

Quantum CoasterWorks is an early-stage coaster design and simulation backend. The current repository is focused on deterministic track, spline, FVD, physics, IO, and train placement systems rather than a finished editor application.

## Current Status

- Early development / technical preview.
- Backend-first architecture: the `Quantum.*` projects should stay engine-agnostic C# libraries.
- Unity visualization is experimental and should be treated as an optional debug/prototype viewer, not the owner of backend architecture.
- Future Unity or Unreal adapters may remain valid for PBR previews, ride-through views, and presentation rendering, but those are not part of the current backend preview.
- No final frontend is selected. Avalonia remains a standalone editor shell candidate, with Silk.NET or OpenTK as possible technical viewport layers.

## Project Shape

- `Quantum.Core`, `Quantum.Math`, `Quantum.Splines`, `Quantum.Track`, `Quantum.FVD`, `Quantum.Physics`, and `Quantum.IO` contain backend/domain logic.
- `Quantum.Debug` contains backend diagnostics and command-line tooling.
- `Quantum.Tests` contains automated tests and contract fixtures.
- `Assets` contains the current Unity debug visualizer/prototype assets.

Testing fixture notes live under `docs/testing/`, including `docs/testing/train-pose-regression-fixtures.md` for the deterministic train pose and `TrainPoseExportV1` regression fixtures.

## Contributor Setup

Required local tools:

- Git.
- .NET SDK 8.0 or newer. The backend libraries target `netstandard2.1`; `Quantum.Debug` and `Quantum.Tests` target `net8.0`.
- Network access to NuGet package sources for a first restore, unless the required packages are already cached locally.

Unity, Blender, Visual Studio, and other renderer/editor tools are optional for the current backend preview. They are not required to restore, build, or test the solution.

From a fresh checkout:

```powershell
git clone <repo-url>
cd QuantumCoasterWorksLib
dotnet restore QuantumCoasterWorks.sln
dotnet build QuantumCoasterWorks.sln --no-restore --nologo
dotnet test QuantumCoasterWorks.sln --no-build --nologo
```

For the shorter everyday backend check:

```powershell
dotnet test QuantumCoasterWorks.sln --nologo
```

There is currently no `global.json`, so the installed .NET SDK selected by `dotnet` is used. If a release needs exact SDK reproducibility, record `dotnet --info` during the release gate.

## Backend Sample Workflow

The current Technical Preview 0.1 workflow is backend-only. It produces renderer-agnostic JSON that can be consumed by tests, inspectors, or optional thin debug viewers later.

Run the full backend demo script on Windows:

```powershell
.\tools\demo-technical-preview-0.1.cmd
```

If direct PowerShell execution is blocked by local execution policy, the `.cmd` wrapper runs the same script with a process-local bypass. The direct fallback is:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\demo-technical-preview-0.1.ps1
```

The script runs the test suite, prints the `Quantum.Debug` command reference, generates the built-in `DebugViewportSnapshotV1` sample, generates snapshots from the Milestone 7 synthetic fixture pack, validates each snapshot, writes multi-panel SVG technical debug previews, refreshes a small Markdown preview index, and leaves generated output under ignored `artifacts/debug-viewport/`.

Generated debug viewport outputs include:

- `artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json`
- `artifacts/debug-viewport/DebugViewportSnapshotV1.sample.svg`
- `artifacts/debug-viewport/Milestone7.synthetic.straight_line.snapshot.json`
- `artifacts/debug-viewport/Milestone7.synthetic.straight_line.snapshot.svg`
- `artifacts/debug-viewport/Milestone7.synthetic.simple_hill.snapshot.json`
- `artifacts/debug-viewport/Milestone7.synthetic.simple_hill.snapshot.svg`
- `artifacts/debug-viewport/Milestone7.synthetic.banked_turn.snapshot.json`
- `artifacts/debug-viewport/Milestone7.synthetic.banked_turn.snapshot.svg`
- `artifacts/debug-viewport/Milestone7.synthetic.descending_ascending_curve.snapshot.json`
- `artifacts/debug-viewport/Milestone7.synthetic.descending_ascending_curve.snapshot.svg`
- `artifacts/debug-viewport/snapshot-preview-index.md`
- `artifacts/debug-viewport/index.html`

Open `artifacts/debug-viewport/snapshot-preview-index.md` for a generated Markdown list of snapshot JSON and SVG preview files, or open `artifacts/debug-viewport/index.html` locally for a simple static gallery of the generated SVG previews.

For optional Blender screenshots or renders from the same JSON snapshots, see `docs/visualization/blender-debug-viewer.md`.

Generate the built-in debug viewport snapshot:

```powershell
dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1 artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json
```

Generate a snapshot from a self-authored sampled-frame CSV fixture:

```powershell
dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-from-csv Quantum.Tests/IO/Fixtures/Milestone7.synthetic.straight_line.centerline_frames.csv artifacts/debug-viewport/Milestone7.synthetic.straight_line.snapshot.json
```

Validate and inspect a snapshot JSON file:

```powershell
dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-validate artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json
```

Generate a multi-panel SVG technical preview from a snapshot:

```powershell
dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-svg artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json artifacts/debug-viewport/DebugViewportSnapshotV1.sample.svg
```

The SVG previews, Markdown preview index, and generated gallery are backend-only debug aids for quick inspection. Current previews include top-down X/Z and elevation/profile panels so flat plan views can still show hills and drops. Raw exported centerline samples are shown as small markers with a faint raw polyline, and a Catmull-Rom smooth-preview path is drawn only as a visual approximation for readability. The smoothing does not change the JSON contract, backend spline behavior, track physics, or sampled data. The previews are not a renderer, editor, frontend scaffold, polished viewer, authoritative spline interpolation, or commitment to any visualization stack.

Generated JSON, SVG, Markdown, and HTML under `artifacts/` are local output by default and should not be committed unless there is a clear release reason.

## Quantum.Debug Command Reference

- `dotnet run --project Quantum.Debug -- help`: list supported commands, arguments, examples, and artifact guidance.
- `dotnet run --project Quantum.Debug -- --help`: same as `help`.
- `dotnet run --project Quantum.Debug -- -h`: same as `help`.
- `dotnet run --project Quantum.Debug -- help <command>`: show command-specific usage and examples.
- `dotnet run --project Quantum.Debug -- <command> --help`: show command-specific usage and examples.
- `dotnet run --project Quantum.Debug`: run the default backend validation smoke checks.
- `dotnet run --project Quantum.Debug -- sampling-perf`: run deterministic sampling performance diagnostics.
- `dotnet run --project Quantum.Debug -- train-pose-export-v1 [outputPath]`: write a deterministic `TrainPoseExportV1` sample JSON matching `Quantum.Tests/IO/Fixtures/TrainPoseExportV1.golden.json`.
- `dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1 [outputPath]`: write the built-in `DebugViewportSnapshotV1` sample JSON.
- `dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-from-csv <inputCsvPath> [outputJsonPath]`: bridge a sampled-frame CSV fixture to `DebugViewportSnapshotV1` JSON.
- `dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-validate <snapshotJsonPath>`: validate and summarize a snapshot JSON file.
- `dotnet run --project Quantum.Debug -- debug-viewport-snapshot-v1-svg <snapshotJsonPath> [outputSvgPath]`: write a multi-panel backend-only SVG preview from snapshot JSON.
- `dotnet run --project Quantum.Debug -- longitudinal-force-preview [preset] [outputPath]`: write force preview diagnostics with `soft`, `balanced`, or `punchy` presets.
- `dotnet run --project Quantum.Debug -- longitudinal-speed-preview [preset] [outputPath] [initialSpeedMps]`: write speed preview diagnostics with `soft`, `balanced`, or `punchy` presets.

See `ROADMAP.md`, `docs/release/v0.1.0-preview.md`, `docs/release/technical-preview-0.1-scope.md`, `docs/release/technical-preview-0.1-release-gate.md`, and `docs/architecture/frontend-strategy.md` for the current architecture direction and first public preview checklist.
