# Quantum Architecture Direction

- Quantum.Track is the product heart.
- Quantum.Math and Quantum.Splines are plumbing.
- Keep backend contracts engine-agnostic.
- Use mature libraries where practical.
- Use AI for boilerplate, docs, API explanations, audits, and review.
- Do not use AI to own architecture or coaster-specific logic.
- Avoid full rewrites; replace internals incrementally.

## Vision

What Quantum is trying to become.

## Product Heart

- Quantum.Track
- TrackDocument
- TrackEvaluator
- TrackFrame
- Banking
- Heartline
- Train Pose
- FVD

## Supporting Infrastructure

- Quantum.Math
- Quantum.Splines
- Quantum.IO

Goal:
Keep these thin.
Use mature libraries where practical.

## AI Philosophy

AI is used for:

- Boilerplate
- Documentation
- API explanations
- Library evaluation
- Code review

AI is NOT responsible for:

- Product architecture
- Coaster mathematics
- Core algorithms
- Domain contracts

## Library Philosophy

Prefer mature libraries for:

- NURBS
- Numerical methods
- Geometry kernels
- Rendering infrastructure

Keep Quantum-specific logic custom.

## Refactoring Rules

- Preserve public contracts.
- Replace internals incrementally.
- Avoid large rewrites.
- Ship milestones instead of redesigning continuously.