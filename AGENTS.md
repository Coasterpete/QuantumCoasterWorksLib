# Quantum CoasterWorks Agent Instructions

Quantum is a coaster-domain backend, not a general-purpose math library.

Do not rebuild mature math/geometry systems unless necessary.

Prefer mature libraries for:
- matrices
- numerical methods
- interpolation
- NURBS/B-spline evaluation
- geometry utilities

Keep custom:
- TrackSection
- ForceSection
- BankingProfile
- HeartlineOffset
- TrainOnTrack
- CarSpacing
- BlockZone
- coaster-specific transforms
- export/import adapters

Current milestone:
Make simple train boxes move correctly along a centerline using stable point, tangent, frame, and distance-based car placement.

Rules:
- Do not rewrite the whole project.
- Make small, reviewable changes.
- Explain what changed and why.
- Add or update tests when behavior changes.

## Current Architecture Direction

Quantum uses existing engines/frameworks for visualization and tooling.

Planned stack:
- Unity frontend/editor sandbox
- C# backend/domain logic
- Mature external math/spline libraries where appropriate

Quantum is NOT currently:
- a full standalone game engine
- a Vulkan renderer
- a complete NoLimits replacement

## Current Priorities

Highest priority:
1. Stable centerline evaluation
2. Stable orientation frames
3. Distance-based train car placement
4. Debug visualization in Unity

Lower priority:
- real train meshes
- bogies/wheels
- support generation
- terrain systems
- advanced rendering
- full UI editor
- scripting systems

## Code Philosophy

Prefer:
- simple readable systems
- deterministic behavior
- incremental progress
- debug-friendly architecture

Avoid:
- overengineering
- premature optimization
- rewriting working systems
- introducing unnecessary abstraction layers

## Visualization Notes

Current train visualization may use:
- simple cubes/boxes
- gizmos
- placeholder transforms

This is acceptable during backend prototyping.

## Important

Do not attempt to redesign the entire project architecture unless explicitly requested.

Focus on helping the existing prototype become more stable and testable.