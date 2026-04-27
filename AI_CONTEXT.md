# Quantum CoasterWorks

Advanced roller coaster editor and simulation platform.

## Core Rules

- Keep code modular and clean
- Favor reusable libraries
- Unity HDRP is host/render layer only
- Core coaster logic belongs in Quantum.* libraries
- Keep files small and single responsibility
- Reuse before rewriting
- Avoid overengineering
- Treat all code as current final state

## Architecture

Read `structure.md` before making changes.

## Priority Systems

1. Quantum.Math
2. Quantum.Splines
3. Quantum.FVD
4. Quantum.Physics
5. Quantum.IO

## AI Instructions

Before editing:
1. Inspect existing code
2. Make minimal plan
3. Edit only necessary files
4. Preserve architecture boundaries
