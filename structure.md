# Project Structure

## Solution

QuantumCoasterWorks.sln

## Libraries

- Quantum.Core = shared contracts / base systems
- Quantum.Math = vectors, matrices, utilities
- Quantum.Splines = curve logic
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