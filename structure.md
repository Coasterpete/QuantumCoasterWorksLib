# Project Structure

## Solution

QuantumCoasterWorks.sln

## Libraries

- Quantum.Core = shared contracts / base systems
- Quantum.Math = vectors, matrices, utilities, and minimal spatial transforms
  - Includes `Matrix3x3`
  - Includes `Transform3d`
  - Includes `ITrackFrameBasis` to prevent `Quantum.Math -> Quantum.Splines` dependency cycles
- Quantum.Splines = curve logic and track frame sampling
  - `TrackFrame` implements `ITrackFrameBasis`
  - `TrackFrame -> Transform3d` bridge is provided through `Transform3d.FromTrackFrame(...)`
- Quantum.Geometry = geometry helpers
- Quantum.Physics = train simulation
- Quantum.FVD = force vector design systems
- Quantum.IO = save/load/import/export
- Quantum.Debug = diagnostics

## Frontend / Host Direction

- Core backend libraries remain host-independent.
- Current Unity assets are an optional debug/prototype visualizer.
- Future standalone editor direction is an Avalonia UI shell.
- Future viewport direction is Silk.NET or OpenTK, to be evaluated later.

## Rule

Core logic must remain independent from Unity, Avalonia, renderer, and UI APIs.

## Near-Term Notes

- `Matrix4x4` is not required yet.
- Quaternion/roll transform systems can be introduced later as needed.
- `Quantum.Track` should wait until the track evaluation architecture is planned.
