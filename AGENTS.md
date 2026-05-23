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
- Keep backend projects engine-agnostic.
- Do not add UnityEngine or UnityEditor dependencies to core Quantum.* projects.
- Use self-authored test assets/fixtures only unless permission is explicit.

## Current Architecture Direction

Quantum uses existing frameworks for visualization and tooling, but the backend remains engine-agnostic.

Planned/considered stack:
- QuantumCoasterWorksLib / Quantum.* = pure C# backend/domain logic
- QCWUnityDebug / current Unity assets = optional/legacy debug viewer and prototype only
- Future standalone editor = Avalonia UI shell
- Future viewport = Silk.NET or OpenTK renderer
- Mature external math/spline libraries where appropriate

Quantum is NOT currently:
- a full standalone game engine
- a Vulkan renderer
- a production Avalonia editor
- a complete NoLimits replacement

## Current Priorities

Highest priority:
1. Stable centerline evaluation
2. Stable orientation frames
3. Distance-based train car placement
4. Debug visualization through optional thin adapters, currently Unity

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
Unity visualization is a debug/prototype surface only and should not define backend architecture.

## Important

Do not attempt to redesign the entire project architecture unless explicitly requested.

Focus on helping the existing prototype become more stable and testable.
Keep Unity-specific work outside the backend library.
