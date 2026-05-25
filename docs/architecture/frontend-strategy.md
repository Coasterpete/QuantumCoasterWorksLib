# Frontend Strategy

## Backend-First Host Strategy

Quantum CoasterWorks is intended to become a standalone coaster design / FVD-style / spline-track editor, not a normal video game. Unity has been useful as a fast debug visualizer because it can draw track frames, centerlines, train boxes, gizmos, and exported pose data with little custom viewport infrastructure.

No final frontend, renderer, or engine choice is locked yet. A coaster editor needs durable data workflows, precise domain tooling, editor-grade UI, deterministic backend behavior, and viewport/debug surfaces that can evolve independently. The backend should therefore remain independent from any host lifecycle, scene model, serialization model, editor assumptions, or rendering stack.

Unity, Unreal, Avalonia, Silk.NET, OpenTK, or another host may be evaluated through optional adapters as the product shape becomes clearer. Those hosts should consume backend outputs, not own backend data or architecture.

## Standalone Editor Candidate

Avalonia is being considered for a future desktop UI shell because it is a mature cross-platform .NET UI framework and fits a C# backend-first project. It can host editor panels, property inspectors, graphs, timelines, and file workflows without forcing the backend into a game-object model.

Silk.NET remains a candidate for a standalone technical viewport because it provides lower-level graphics/windowing access from .NET while allowing Quantum to keep rendering behind an adapter. OpenTK may also be evaluated if it better fits the viewport needs. Either option would let the standalone editor draw tracks, frames, train placeholders, handles, and diagnostics without turning the backend into a renderer.

Avalonia/Silk.NET is therefore a candidate editor-shell/technical-viewport path, not a final frontend decision. The next step is to keep the backend clean enough that competing host and viewport options can be evaluated later.

## Optional High-Fidelity Visualization Targets

Unity and Unreal may remain valid optional visualization targets for workflows where a game engine is useful: PBR materials, high-fidelity lighting, ride-through previews, cinematic camera paths, VR-style presentation, video capture, and presentation rendering.

Those workflows should stay in thin visualization adapters, prototype projects, or export/import bridges. They may use meshes, materials, shaders, engine cameras, scene objects, and engine-specific render tools, but those concepts must not leak into `Quantum.*` backend contracts.

## Future Layout

- `Quantum.Editor.Avalonia`: future desktop app shell for windows, panels, inspectors, commands, and host-level UI wiring.
- `Quantum.Editor.Core` or `Quantum.Editor.Workbench`: optional pure .NET editor state and services layer, if shared editor workflows need to live outside the UI shell.
- `Quantum.Viewport.SilkNet` or `Quantum.Viewport.OpenTK`: future renderer adapter for drawing sampled coaster data in a standalone viewport.
- `Quantum.Visualization.Unity`, `Quantum.Visualization.Unreal`, or external host projects: optional high-fidelity visualization, ride-through, PBR, and presentation-rendering adapters.
- Backend libraries: expose renderer-agnostic sampled data, transforms, frames, diagnostics, and DTOs for any frontend or adapter to consume.

Backend libraries must not contain host-tied cameras, materials, meshes, shaders, input loops, scene objects, draw calls, or other renderer/frontend resources. Those belong in frontend, viewport, debug, or export adapters.

## What Must Stay Engine-Agnostic

The `Quantum.*` backend projects should stay independent of Unity, Unreal, Avalonia, Silk.NET, OpenTK, or any other host framework. Backend APIs should describe coaster-domain concepts and simple data contracts, not UI widgets, scene objects, engine components, materials, or render resources.

Keep engine-agnostic:

- Track documents, sections, and segment definitions.
- Centerline sampling, tangents, orientation frames, transforms, and distance semantics.
- Train consist definitions, car spacing, bogie/wheel placement, and placeholder train transforms.
- FVD force sections, force target sampling, and physics adapters.
- Import/export DTOs and schema validation.
- Debug geometry descriptions that can be consumed by multiple frontends or visualization engines.

## What Must Not Leak From Visualization Engines

Unity-specific and Unreal-specific code belongs in visualization projects or thin adapter layers only. It should not be referenced by core backend projects.

Do not leak into the backend:

- `UnityEngine` or `UnityEditor` references.
- Unreal runtime/editor module references or engine object types.
- `MonoBehaviour`, `ScriptableObject`, `GameObject`, `Transform`, scene, prefab, material, or gizmo types.
- `UObject`, `Actor`, `Component`, Blueprint, level, asset, material, or renderer types.
- Engine serialization attributes or lifecycle assumptions.
- Engine coordinate conversion code except inside frontend/debug adapters.
- Visualizer-only colors, handles, UI toggles, or debug drawing policy.

Backend changes should remain testable through .NET tests and simple data outputs before any frontend consumes them.
