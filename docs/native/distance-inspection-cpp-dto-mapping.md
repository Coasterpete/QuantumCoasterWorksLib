# Distance Inspection C++ DTO Mapping

This document sketches a plain native DTO shape for consumers of the `DistanceInspectionSnapshotV1` JSON contract. It is intended for C++, Unreal, BGFX, Unity native plugins, or other frontend adapters that need to read the v1 JSON handoff without porting backend behavior.

This is adapter guidance, not compiled production code and not a backend C++ port.

## Purpose

Native adapters can mirror the JSON contract with simple data-only structs, then translate those structs into renderer, editor, or engine-specific view models.

Keep the JSON contract shaped like the backend handoff:

- `std::string` for `contract` and enum-like labels.
- `int` for `version`.
- `double` for numeric values.
- `std::vector<T>` for arrays.

Do not add engine ownership, rendering state, editor concepts, or behavior to these DTOs.

## DTO Sketch

The field names below intentionally mirror the JSON property names.

```cpp
// Illustrative adapter DTOs only. Keep production code local to the native adapter.
#include <string>
#include <vector>

struct DistanceInspectionChannelValueV1Native
{
    std::string channel;
    double value;
};

struct DistanceInspectionSectionV1Native
{
    std::string kind;
    std::string domain;
    double startX;
    double endX;
    std::string diagnostic;
    std::vector<std::string> channels;
    std::vector<DistanceInspectionChannelValueV1Native> channelValues;
};

struct DistanceInspectionSnapshotV1Native
{
    std::string contract;
    int version;
    double distance;
    std::vector<DistanceInspectionSectionV1Native> sections;
};
```

## Parsing Rules

- Validate `contract` before reading the payload. It must be `quantum.distance_inspection_snapshot`.
- Validate `version` before reading the payload. For this mapping it must be `1`.
- Treat enum-like labels such as `kind`, `domain`, `diagnostic`, and `channel` as strings, not C++ enum ordinals.
- Preserve `sections` order from the JSON.
- Preserve `channels` and `channelValues` order from the JSON.
- Allow missing section kinds. A snapshot may omit `Force`, `Geometry`, or any other kind from the `sections` array.
- Treat the payload as read-only debug/inspection state, not an editable model or source of backend truth.

## Unreal Adaptation

An Unreal adapter can wrap equivalent data later with `USTRUCT`, `FString`, and `TArray` for reflection, Blueprint exposure, or editor UI.

Keep those Unreal-specific types inside the Unreal adapter or plugin. Do not put `USTRUCT`, `FString`, `TArray`, `UObject`, `Actor`, component, material, or viewport concepts into the backend JSON contract, schema, sample payload, manifest, or C# DTOs.

## Scope Warning

This mapping does not define a native backend implementation. It only describes the native DTO/adapter shape for consuming the existing `DistanceInspectionSnapshotV1` JSON handoff.
