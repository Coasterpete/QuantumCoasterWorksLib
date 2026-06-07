# Distance Inspection Browser Preview

The distance inspection browser preview is a backend-generated HTML artifact for
quickly inspecting a `DistanceInspectionSnapshotV1` sample in a local browser. It
is a practical read-only preview for the same inspector concept described in the
Unreal UI/view-model guidance, without requiring Unreal, Unity, or a frontend
runtime.

This preview is documentation and debug workflow support only. It does not
change evaluator behavior, the v1 JSON contract, DTOs, schema, sample payload, or
manifest.

## Generate The JSON Artifact

Generate the default distance inspection JSON artifact when you want the raw
handoff payload beside the preview:

```powershell
dotnet run --project Quantum.Debug -- distance-inspection-json
```

The default JSON output path is
`artifacts/track/distance-inspection.sample.json`.

Pass an explicit output path when the artifact should be written somewhere else:

```powershell
dotnet run --project Quantum.Debug -- distance-inspection-json artifacts/track/my-distance-inspection.sample.json
```

## Generate The Browser Preview

Generate the default browser preview:

```powershell
dotnet run --project Quantum.Debug -- distance-inspection-browser
```

The default HTML output path is
`artifacts/track/distance-inspection.browser.html`.

Pass an explicit output path when the preview should be written somewhere else:

```powershell
dotnet run --project Quantum.Debug -- distance-inspection-browser artifacts/track/my-distance-inspection.browser.html
```

Open the generated HTML file in a browser to inspect the snapshot. The file is a
static backend artifact and does not require a dev server.

## Preview Contents

The preview presents the generated snapshot as a read-only inspector:

- Contract and version.
- Inspected centerline distance.
- Section count.
- Ordered section cards that preserve `sections` order.
- Section `kind`, `domain`, `[startX, endX]` range, and `diagnostic`.
- Ordered `channels`.
- Ordered `channelValues` table with channel names and numeric values.

## Relation To The Unreal Adapter

The browser preview mirrors the read-only inspector shape described in
`docs/native/distance-inspection-unreal-ui-view-model.md`: top-level snapshot
summary, ordered section cards, channel lists, and channel value rows.

It is backend-only and browser-based. It is not the final Unreal UI, does not
define Unreal widgets or view models, and does not add native adapter behavior.
Future Unreal tooling should continue to consume the v1 JSON contract through an
adapter-owned parser and view model.

## Artifact Policy

Generated `artifacts/` output remains local and ignored unless there is a clear
release, fixture, or handoff reason to check in a specific artifact.

## Safety Boundary

- The browser preview does not change evaluator behavior.
- It does not change the v1 JSON contract.
- It does not add frontend, browser runtime, Unity, or Unreal dependencies.
- It does not replace the checked-in schema, sample, manifest, or handoff guide.
