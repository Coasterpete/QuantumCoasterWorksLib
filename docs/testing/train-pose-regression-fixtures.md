# Train Pose Regression Fixtures

Milestones 21 and 22 added deterministic train pose regression fixtures for the current backend priority: simple train boxes moving correctly along a centerline with stable point, tangent, frame, and distance-based car placement.

These fixtures are synthetic and self-authored. They are built in test code rather than loaded from external layout files, so they protect Quantum-owned backend behavior without adding renderer, Unity, Unreal, or third-party asset dependencies.

## Coverage Map

- `Quantum.Tests/Track/TrainPoseDeterministicRegressionFixtureTests.cs` validates the in-memory `TrainPoseResult` produced by `TrainCarTransformProvider`.
- `Quantum.Tests/IO/TrainPoseExportV1RegressionTests.cs` validates the same representative pose after it crosses the public `TrainPoseExportV1` DTO and JSON boundary.
- `Quantum.Tests/IO/TrainPoseExportV1CompositeDocumentBoundaryTests.cs` validates a composite-section train pose export round trip from the public geometric-section builder path.

## Straight Synthetic Layout

The straight fixture is the control case. It uses one straight segment, fixed X-forward/Y-up/Z-right axes, a three-car consist, fixed car spacing, fixed bogie spacing, and a deterministic wheel layout.

It validates:

- body placement by station distance from the lead car;
- front and rear bogie distances using half the bogie spacing;
- wheel count, wheel indexing, and local axle/side offsets;
- identity-like frame basis and translation-only matrices on a flat straight centerline;
- consistent body, bogie, articulation, and wheel hierarchy values.

This catches sign mistakes, axis swaps, spacing regressions, and accidental frame/matrix changes in the simplest layout where the expected answer should be obvious.

## Curved Synthetic Layout

The curved fixture is the representative nontrivial case. It combines straight and cubic curved segments with changing grade, heading, and roll, then evaluates a two-car train at a fixed lead distance.

It validates:

- deterministic distance resolution across a multi-segment centerline;
- non-axis-aligned position, tangent, normal, and binormal values;
- roll-influenced frame orientation and matrix output;
- articulated body center placement from the front and rear bogie frames;
- repeat evaluation stability for the same track, train definition, and lead distance.

This catches regressions that the straight control case cannot see, especially frame continuity, segment-local sampling mistakes, roll handling, and curved-track matrix changes.

## TrainPoseExportV1 Contract Protection

`TrainPoseExportV1` is a public/export contract, not just an internal debug object. The Milestone 22 regression coverage protects that boundary by formatting and comparing the exported contract identity, version, lead distance, train definition, car hierarchy, body transforms, bogie transforms, wheel transforms, frames, matrices, and local wheel offsets.

The export tests also serialize and deserialize the DTO, run the backend validator, assert canonical matrix bottom rows, and confirm that duplicated hierarchy views agree with each other. That makes accidental drift visible when a change would alter the public JSON shape or semantics, even if the in-memory train pose still looks plausible.

The composite document boundary test adds coverage for `GeometricSectionTrackDocumentBuilder` output so the export contract is protected through a second backend construction path.

For field-level contract details, see `docs/train_pose_export_v1_contract.md`.

## Maintenance Notes

- Do not update these snapshots as formatting cleanup. Update them only when train pose behavior intentionally changes.
- If behavior changes intentionally, keep the change small, describe why the expected pose changed, and run `dotnet test Quantum.Tests\Quantum.Tests.csproj --no-restore`.
- Do not change the `TrainPoseExportV1` v1 schema to satisfy a regression. Breaking field, type, or semantic changes need a new contract version.
- Keep these fixtures backend-only and deterministic. Do not add rendering dependencies or external authored assets to this coverage.
