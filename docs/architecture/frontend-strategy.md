# Frontend Strategy

## Why Unity Is Being Downgraded

Quantum CoasterWorks is intended to become a standalone coaster design / FVD-style / spline-track editor, not a normal video game. Unity has been useful as a fast debug visualizer because it can draw track frames, centerlines, train boxes, gizmos, and exported pose data with little custom viewport infrastructure.

That usefulness does not make Unity the right long-term foundation. A coaster editor needs durable data workflows, precise domain tooling, editor-grade UI, deterministic backend behavior, and viewport/debug surfaces that can evolve independently. Keeping Unity as the application root would risk coupling coaster-domain contracts to a game engine lifecycle, scene model, serialization model, and editor assumptions.

Unity should therefore remain optional: a debug/prototype viewer that consumes backend outputs, not the owner of backend data or architecture.

## Why Avalonia Plus Silk.NET Or OpenTK

Avalonia is being considered for the future desktop UI shell because it is a mature cross-platform .NET UI framework and fits a C# backend-first project. It can host editor panels, property inspectors, graphs, timelines, and file workflows without forcing the backend into a game-object model.

Silk.NET or OpenTK are being considered for the future viewport because they provide lower-level graphics/windowing access from .NET while allowing Quantum to keep rendering behind an adapter. Either option would let the standalone editor draw tracks, frames, train placeholders, handles, and diagnostics without turning the backend into a renderer.

No final renderer choice is locked yet. The next step is to keep the backend clean enough that either viewport option can be evaluated later.

## Future Layout

- `Quantum.Editor.Avalonia`: future desktop app shell for windows, panels, inspectors, commands, and host-level UI wiring.
- `Quantum.Editor.Core` or `Quantum.Editor.Workbench`: optional pure .NET editor state and services layer, if shared editor workflows need to live outside the UI shell.
- `Quantum.Viewport.SilkNet` or `Quantum.Viewport.OpenTK`: future renderer adapter for drawing sampled coaster data in a standalone viewport.
- Backend libraries: expose renderer-agnostic sampled data, transforms, frames, diagnostics, and DTOs for any frontend or adapter to consume.

Backend libraries must not contain host-tied cameras, materials, meshes, shaders, input loops, scene objects, draw calls, or other renderer/frontend resources. Those belong in frontend, viewport, debug, or export adapters.

## What Must Stay Engine-Agnostic

The `Quantum.*` backend projects should stay independent of Unity, Avalonia, Silk.NET, OpenTK, or any other host framework. Backend APIs should describe coaster-domain concepts and simple data contracts, not UI widgets, scene objects, engine components, materials, or render resources.

Keep engine-agnostic:

- Track documents, sections, and segment definitions.
- Centerline sampling, tangents, orientation frames, transforms, and distance semantics.
- Train consist definitions, car spacing, bogie/wheel placement, and placeholder train transforms.
- FVD force sections, force target sampling, and physics adapters.
- Import/export DTOs and schema validation.
- Debug geometry descriptions that can be consumed by multiple frontends.

## What Must Not Leak From Unity

Unity-specific code belongs in the Unity debug viewer or thin adapter layers only. It should not be referenced by core backend projects.

Do not leak into the backend:

- `UnityEngine` or `UnityEditor` references.
- `MonoBehaviour`, `ScriptableObject`, `GameObject`, `Transform`, scene, prefab, material, or gizmo types.
- Unity serialization attributes or lifecycle assumptions.
- Unity coordinate conversion code except inside frontend/debug adapters.
- Visualizer-only colors, handles, UI toggles, or debug drawing policy.

Backend changes should remain testable through .NET tests and simple data outputs before any frontend consumes them.
