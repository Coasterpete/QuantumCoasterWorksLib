# Project Structure

## Solution

QuantumCoasterWorks.sln

## Libraries

- Quantum.Core = shared contracts / base systems
- Quantum.Math = vectors, matrices, utilities, and minimal spatial transforms
  - Includes `Matrix3x3`
  - Includes `Transform3d`
  - Includes `ITrackFrameBasis` to prevent `Quantum.Math -> Quantum.Splines` dependency cycles
- Quantum.Splines = curve logic and generic curve-frame sampling
  - `CurveFrame` implements `ITrackFrameBasis`
  - legacy `TrackFrame` / `TrackFrameSampler` names are obsolete compatibility surfaces
- Quantum.Track = active major backend project for track documents, canonical `TrackFrame`, segments, traversal, evaluation, and train transform placement
  - `Quantum.Track.TrackFrame` implements `ITrackFrameBasis`
  - frame-to-transform bridging is provided through `Transform3d.FromTrackFrame(...)`
- Quantum.Physics = train simulation
- Quantum.FVD = force vector design systems
- Quantum.IO = save/load/import/export
- Quantum.Debug = diagnostics
- Quantum.Editor.Avalonia = standalone Avalonia editor shell scaffolding

## Frontend / Host Direction

- Core backend libraries remain host-independent.
- Current Unity assets are an optional debug/prototype visualizer.
- Unity or Unreal may remain valid optional visualization targets for PBR, ride-through, and presentation rendering.
- No final frontend workflow is selected.
- `Quantum.Editor.Avalonia` contains the initial standalone Avalonia shell scaffolding.
- Future technical viewport candidate is Silk.NET, with OpenTK still available for evaluation.

## Rule

Core logic must remain independent from Unity, Unreal, Avalonia, renderer, and UI APIs.

## Near-Term Notes

- `Matrix4x4` is not required yet.
- Quaternion/roll transform systems can be introduced later as needed.
- `Quantum.Track` is active and should continue evolving in small, testable backend increments.
- `Quantum.Geometry` is not an active project; the package name is reserved for a future narrow backend-only geometry role if one becomes necessary.
