# Distance Inspection Unreal Parsing Flow

This document describes how an Unreal adapter should read a
`DistanceInspectionSnapshotV1` JSON payload and turn it into Unreal-side
inspector or debug UI data.

This is documentation only. It does not add Unreal dependencies, compiled
Unreal code, or backend runtime behavior.

## Purpose

`DistanceInspectionSnapshotV1` is the JSON handoff for distance inspection
state. The C# backend remains the source of truth for evaluator behavior. An
Unreal adapter should consume the JSON snapshot, validate the contract boundary,
and build adapter-owned structs or view models for editor/debug presentation.

## Inputs

Use these files and outputs as the adapter's first targets:

- Checked-in sample JSON:
  `docs/contracts/distance-inspection-snapshot-v1.sample.json`
- Generated debug command output:
  `dotnet run --project Quantum.Debug -- distance-inspection-json`
- Default generated artifact:
  `artifacts/track/distance-inspection.sample.json`
- Manifest:
  `docs/contracts/distance-inspection-snapshot-v1.manifest.json`

The manifest records the v1 contract name, schema path, sample path, handoff
guide, debug command, and default artifact path.

## Recommended Flow

1. Load the JSON text from the checked-in sample or generated debug command
   output.
2. Parse the text into a JSON object using Unreal-side JSON utilities or
   adapter-owned parser code.
3. Validate `contract == "quantum.distance_inspection_snapshot"`.
4. Validate `version == 1`.
5. Read `distance`.
6. Iterate `sections` in JSON order.
7. For each section, read `kind`, `domain`, `startX`, `endX`, `diagnostic`,
   `channels`, and `channelValues`.
8. Preserve `channels` and `channelValues` order exactly as written.
9. Build Unreal adapter structs or view models.
10. Hand the parsed snapshot to inspector, editor, or debug UI code.

Treat enum-like values such as `kind`, `domain`, `diagnostic`, and `channel` as
strings at the JSON boundary. The adapter may map them to Unreal-specific
display state later, but the raw contract values should remain string-based.

## Error Handling

- Reject an unknown `contract`.
- Reject an unsupported `version`.
- Treat missing optional or future section kinds as normal. A valid snapshot
  does not need every possible section kind.
- Fail gracefully on malformed required fields. Report the error through the
  adapter's normal logging or UI path instead of calling backend evaluator code.
- Keep partial data out of inspector/debug UI unless the adapter has an explicit
  degraded display state for invalid snapshots.

## Illustrative Pseudocode

This sketch is intentionally not compiled Unreal code. Keep production parsing
inside the Unreal adapter or plugin, using Unreal JSON utilities or another
adapter-owned parser.

```cpp
// Illustrative only.
ParseResult ParseDistanceInspectionSnapshot(JsonText)
{
    JsonObject root = ParseJsonObject(JsonText);

    string contract = ReadRequiredString(root, "contract");
    if (contract != "quantum.distance_inspection_snapshot")
    {
        return Error("Unsupported distance inspection contract.");
    }

    int version = ReadRequiredInt(root, "version");
    if (version != 1)
    {
        return Error("Unsupported distance inspection version.");
    }

    SnapshotViewModel snapshot;
    snapshot.Distance = ReadRequiredDouble(root, "distance");

    foreach (JsonObject sectionJson in ReadRequiredArray(root, "sections"))
    {
        SectionViewModel section;
        section.Kind = ReadRequiredString(sectionJson, "kind");
        section.Domain = ReadRequiredString(sectionJson, "domain");
        section.StartX = ReadRequiredDouble(sectionJson, "startX");
        section.EndX = ReadRequiredDouble(sectionJson, "endX");
        section.Diagnostic = ReadRequiredString(sectionJson, "diagnostic");

        foreach (JsonValue channelJson in ReadRequiredArray(sectionJson, "channels"))
        {
            section.Channels.Add(ReadString(channelJson));
        }

        foreach (JsonObject valueJson in ReadRequiredArray(sectionJson, "channelValues"))
        {
            ChannelValueViewModel value;
            value.Channel = ReadRequiredString(valueJson, "channel");
            value.Value = ReadRequiredDouble(valueJson, "value");
            section.ChannelValues.Add(value);
        }

        snapshot.Sections.Add(section);
    }

    return Success(snapshot);
}
```

## Explicit Boundary

This flow consumes JSON only.

It does not call backend evaluator code, define a native backend, or prescribe
Unreal runtime/editor architecture. Unreal-specific parsing, structs, widgets,
editor panels, debug drawing, selection state, colors, coordinate conversion,
and viewport behavior remain adapter-owned.

Do not add Unreal references or dependencies to the backend projects for this
flow. Keep `UObject`, `Actor`, `Component`, Blueprint, material, viewport,
editor, renderer, and coordinate-system concerns outside the Quantum backend
contract, schema, sample, manifest, debug command, and C# DTOs.
