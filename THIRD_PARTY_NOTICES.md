# Third-Party Notices

This repository is backend-first source code plus optional debug/prototype Unity assets. Third-party binaries or assets should only be kept when their source and license are documented here.

## Committed Third-Party Binaries Or Assets

None currently.

## Restore Dependencies

### GShark

- Package: `GShark`
- Version: `2.3.1`
- License: MIT, per NuGet package metadata
- NuGet: https://www.nuget.org/packages/GShark/2.3.1
- Source repository: https://github.com/GSharker/G-Shark
- Project page: https://gsharker.github.io/G-Shark
- Current use: spline/NURBS evaluation through `Quantum.Splines`

### Dock

- Packages: `Dock.Avalonia`, `Dock.Avalonia.Themes.Fluent`, `Dock.Model.Mvvm`, and `Dock.Serializer.Newtonsoft`
- Version: `12.0.0.2`
- License: MIT, per NuGet package metadata
- NuGet: https://www.nuget.org/packages/Dock.Avalonia/12.0.0.2
- Source repository: https://github.com/wieslawsoltes/Dock
- Current use: dockable pane composition and frontend-only layout serialization in `Quantum.Editor.Avalonia`

The optional Unity debug/prototype visualizer may use a local copied DLL at `Assets/Plugins/Quantum/GShark.dll`. That file is generated/copied by `tools/copy-quantum-unity-dlls.ps1` from restored outputs and is intentionally ignored rather than committed.

## Project-Built DLLs

The following DLLs under `Assets/Plugins/Quantum` are built from this repository and copied for the optional Unity debug/prototype visualizer:

- `Quantum.Math.dll`
- `Quantum.Splines.dll`
- `Quantum.Track.dll`

These are build outputs, not separate third-party assets, and are intentionally ignored rather than committed.

## NuGet Restore Dependencies

The solution also restores NuGet dependencies declared in project files, including test-only packages. Those packages are not vendored in this repository unless a binary copy is explicitly listed above.
