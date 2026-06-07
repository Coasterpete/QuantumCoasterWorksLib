# Distance Inspection Unreal Adapter Mapping

This document sketches an Unreal-friendly adapter shape for consuming the
`DistanceInspectionSnapshotV1` JSON contract.

This is adapter guidance only. It is not compiled Unreal code, not a backend
port, and not a request to add Unreal dependencies to the Quantum backend.

## Purpose

Unreal should consume `DistanceInspectionSnapshotV1` through an adapter layer.
The backend remains the source of truth for distance inspection behavior; the
Unreal side parses the JSON handoff and builds engine-specific inspector UI,
debug views, and coordinate conversion around that parsed data.

Contract files:

- Schema: `docs/contracts/distance-inspection-snapshot-v1.schema.json`
- Sample: `docs/contracts/distance-inspection-snapshot-v1.sample.json`
- Manifest: `docs/contracts/distance-inspection-snapshot-v1.manifest.json`
- Handoff guide: `docs/contracts/distance-inspection-json-handoff.md`

## Unreal-Style DTO Sketch

The sketches below use Unreal naming and common Unreal container/string types,
but they should live only in an Unreal adapter or plugin. Field names may be
PascalCase in C++ while mapping directly to the lower-camel JSON properties.

```cpp
// Illustrative adapter structs only. Do not add these to Quantum backend projects.

USTRUCT()
struct FDistanceInspectionChannelValueV1
{
    GENERATED_BODY()

    FString Channel;
    double Value;
};

USTRUCT()
struct FDistanceInspectionSectionV1
{
    GENERATED_BODY()

    FString Kind;
    FString Domain;
    double StartX;
    double EndX;
    FString Diagnostic;
    TArray<FString> Channels;
    TArray<FDistanceInspectionChannelValueV1> ChannelValues;
};

USTRUCT()
struct FDistanceInspectionSnapshotV1
{
    GENERATED_BODY()

    FString Contract;
    int32 Version;
    double Distance;
    TArray<FDistanceInspectionSectionV1> Sections;
};
```

Recommended field types:

- `FString` for `contract` and enum-like labels.
- `int32` for `version`.
- `double` for numeric values.
- `TArray<T>` for arrays.

## Parsing And Validation Rules

- Validate `contract` before using the payload. It must be
  `quantum.distance_inspection_snapshot`.
- Validate `version` before using the payload. For this mapping it must be `1`.
- Treat `kind`, `domain`, `diagnostic`, and `channel` as strings, not Unreal
  enum ordinals.
- Preserve `sections` order from the JSON.
- Preserve `channels` and `channelValues` order from the JSON.
- Treat missing section kinds as normal. A snapshot may omit `Force`,
  `Geometry`, or any future section kind.
- Treat the parsed snapshot as read-only debug/inspection state, not an editable
  backend model.

## Unreal UI Adapter Guidance

After parsing and validation, the Unreal adapter can translate the snapshot into
whatever the Unreal-side UI needs:

- Inspector rows or cards for each section.
- Channel/value tables from `channels` and `channelValues`.
- Diagnostic badges from `diagnostic`.
- Adapter-owned colors, icons, table formatting, filtering, and selection state.
- Adapter-owned viewport drawing or debug overlays.
- Adapter-owned coordinate conversion from Quantum backend coordinates into
  Unreal world coordinates.

Keep these presentation decisions in the Unreal adapter. The JSON contract should
remain a renderer-neutral snapshot of backend debug state.

## Explicit Boundary

Do not add `UObject`, `Actor`, `Component`, Blueprint, material, viewport,
editor, or renderer concerns to the backend JSON contract, schema, sample,
manifest, debug command, or C# DTOs.

This mapping does not define a native backend implementation. It only describes
how an Unreal adapter can consume the existing `DistanceInspectionSnapshotV1`
JSON handoff without porting backend logic.
