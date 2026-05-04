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

## Future Host

- Unity HDRP frontend
- Editor UI
- Rendering only

## Rule

Core logic should remain independent from Unity APIs where practical.

## Near-Term Notes

- `Matrix4x4` is not required yet.
- Quaternion/roll transform systems can be introduced later as needed.
- `Quantum.Track` should wait until the track evaluation architecture is planned.
