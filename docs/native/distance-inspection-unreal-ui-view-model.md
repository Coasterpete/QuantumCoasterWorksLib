# Distance Inspection Unreal UI View Model

This document describes how an Unreal adapter can turn parsed
`DistanceInspectionSnapshotV1` data into inspector/debug UI state.

This is adapter guidance only. It is not compiled Unreal code, not a backend
port, and not a request to add Unreal dependencies to the Quantum backend.

## Purpose

`DistanceInspectionSnapshotV1` is a read-only JSON handoff from the C# backend.
After an Unreal adapter validates and parses the payload, it may build
adapter-owned view models for editor panels, debug inspectors, and optional
viewport highlights.

The backend remains the source of truth for distance inspection behavior. The UI
view model should shape that data for presentation without changing the JSON
contract or backend DTOs.

## Recommended UI Shape

- Top-level distance readout for the inspected centerline distance.
- Ordered section cards, preserving `sections` order from the parsed payload.
- Section header showing `kind`, `domain`, `[startX, endX]`, and `diagnostic`.
- Channel list, preserving the parsed `channels` order.
- Channel/value table, preserving the parsed `channelValues` order.
- Optional diagnostic badge derived from `diagnostic`.
- Optional viewport/debug highlight owned by the Unreal adapter.

The viewport highlight might select or draw the inspected interval, active
section, or sampled distance in an Unreal debug scene. That state belongs to the
adapter and should not be added to the JSON payload.

## Suggested View Models

Use names and types that fit the adapter, but keep the view model separate from
the parsed contract DTO. These sketches are illustrative only.

```cpp
// Illustrative adapter guidance only. Do not add this to Quantum backend projects.

struct DistanceInspectionPanelViewModel
{
    double Distance;
    string DistanceText;
    string StateMessage;
    bool HasUsableSnapshot;
    vector<DistanceInspectionSectionCardViewModel> Sections;
};

struct DistanceInspectionSectionCardViewModel
{
    string Kind;
    string KindLabel;
    string Domain;
    string DomainLabel;
    double StartX;
    double EndX;
    string IntervalText;
    string Diagnostic;
    string DiagnosticLabel;
    string DiagnosticBadgeStyle;
    vector<string> Channels;
    vector<DistanceInspectionChannelValueRowViewModel> ChannelValues;
};

struct DistanceInspectionChannelValueRowViewModel
{
    string Channel;
    string ChannelLabel;
    double Value;
    string ValueText;
};
```

## Field Guidance

- Keep raw contract strings available: `kind`, `domain`, `diagnostic`, and
  `channel` should survive in the view model.
- Add display labels separately when the UI needs friendlier text.
- Add colors, icons, badges, sorting affordances, filters, and selection state
  only in the Unreal adapter.
- Preserve section order from the parsed payload.
- Preserve channel order from both `channels` and `channelValues`.
- Do not map enum-like contract strings to numeric ordinals at the UI boundary.
- Format distances and values for display in separate text fields instead of
  replacing the raw numeric values.

## Mapping Flow

1. Consume a validated parsed `DistanceInspectionSnapshotV1` adapter DTO.
2. Create `DistanceInspectionPanelViewModel`.
3. Copy `distance` into the raw distance field and format `DistanceText`.
4. Iterate parsed `sections` in order.
5. For each section, copy raw fields into a
   `DistanceInspectionSectionCardViewModel`.
6. Derive labels, interval text, diagnostic badge style, and optional UI state
   inside the adapter.
7. Copy `channels` and `channelValues` in their original order.
8. Hand the view model to the Unreal editor/debug UI.

## Error And Degraded States

Represent degraded states in the adapter-owned panel view model rather than by
mutating the contract DTO.

- Unsupported `contract` or `version`: show an unsupported payload state and do
  not build section cards.
- Malformed payload: show a parse/validation error state and keep partial data
  out of normal inspector rows unless the adapter has an explicit degraded UI.
- Empty `sections`: show the inspected distance plus an empty-section state.
- Missing section kinds: treat as normal. A valid snapshot is not required to
  contain every possible section kind.
- Missing or unknown `kind`, `domain`, `diagnostic`, or `channel` strings: show
  the raw value when present, or an adapter-owned missing-value label when absent
  during degraded display.

## Read-Only Boundary

The UI should treat this payload as inspection/debug output only. It should not
edit backend state through `DistanceInspectionSnapshotV1`.

If editing tools are added later, they need a separate command and model
contract with explicit validation, ownership, and backend application rules.

## Explicit Boundary

Do not add Unreal UI or view-model concerns to:

- `DistanceInspectionSnapshotV1`.
- The v1 JSON schema.
- The checked-in sample payload.
- The contract manifest.
- The `distance-inspection-json` debug command.
- C# DTOs or backend runtime code.

Unreal-specific widgets, editor panels, colors, icons, badges, materials,
selection state, viewport drawing, and coordinate conversion belong in the
Unreal adapter or plugin. This document does not define compiled Unreal code,
does not port backend behavior, and does not change the Quantum backend
contract.
