# Track Layout Package V2 Shape

Status: Milestone 147 PR2 shape lock. This document defines the intended JSON
contract shape before implementation. It does not add V2 DTOs, a V2 JSON
schema, validators, import/export behavior, or runtime behavior.

## Contract Identity

- Contract name: `quantum.track_layout_package`
- Version: `2`
- JSON naming: camelCase `System.Text.Json`
- Intended contract family: same authored-layout family as
  `TrackLayoutPackageV1`

V2 is a new versioned contract. It must not change `TrackLayoutPackageV1`.

## Required Root Fields

The intended V2 root object has these required fields:

- `contract`
- `version`
- `metadata`
- `startPose`
- `sections`
- `banking`
- `heartline`

The root shape is intentionally closed. Unknown root fields should be rejected
by the future V2 schema and validator.

## Metadata

`metadata` describes the authored layout package, not editor or renderer state.

Fields:

- `units`: required string. Distance units used by authored scalar values.
- `sourceName`: required string or `null`. Optional source label carried forward
  from V1.
- `layoutId`: optional string or `null`. Stable identity for companion
  contracts and manifests. Missing and `null` both mean no stable layout
  identity is authored.

V2 migration and export must not generate a `layoutId` by default. If a caller
does not supply one, `layoutId` remains `null` or omitted.

## Start Pose

`startPose` carries the authored centerline start frame. Its shape matches V1:

- `position`: vector object with required numeric `x`, `y`, and `z`
- `tangent`: vector object with required numeric `x`, `y`, and `z`
- `normal`: vector object with required numeric `x`, `y`, and `z`
- `binormal`: vector object with required numeric `x`, `y`, and `z`

The semantic contract remains the V1 contract: vectors are finite and form the
authored start basis. V2 import must not normalize or repair this pose as a side
effect.

## Sections

`sections` is a required non-empty ordered array of authored geometric section
objects. Sections use the same flat discriminator shape as V1.

Common section fields:

- `kind`
- `id`
- `length`
- `rollRadians`

Initial V2 section kinds:

- `straight`, with no additional fields
- `constantCurvature`, with `radius`
- `curvatureTransition`, with `startCurvature`, `endCurvature`, and
  `interpolationMode`
- `spatial`, with `degree`, `controlPoints`, and `weights`

The first implemented V2 should keep `curvatureTransition.interpolationMode` at
the V1-compatible `linear` value unless nonlinear transition support lands in
the backend first. New transition enum values belong in the layout contract only
after the backend can validate, import, compile, and test them.

## Banking

`banking` is a required root field. Its value is either `null` or an object with
ordered authored banking keys.

Non-null shape:

- `keys`: required array with at least two banking key objects

Banking key fields:

- `distance`
- `rollRadians`
- `interpolationToNext`

Supported banking interpolation values carry forward from V1:

- `constant`
- `linear`
- `smoothStep`
- `quadratic`
- `cubic`
- `quartic`
- `quintic`
- `sinusoidal`

Banking keys are authored in the centerline station domain and must cover the
authored section-length domain exactly from distance `0` to the total authored
layout length. `banking: null` means no explicit authored banking is persisted;
it does not persist generated fallback banking.

## Heartline

`heartline` is a required root field in the intended V2 schema, but its value is
nullable. `heartline: null` means no authored heartline offset is persisted.

The first non-null heartline shape is:

- `kind`: required discriminator, initially `constantOffset`
- `distanceDomain`: required string, initially `centerlineStation`
- `axisSource`: required string, initially `sampledFrame`
- `normalOffset`: required finite authored distance scalar
- `lateralOffset`: required finite authored distance scalar

The offsets are applied from sampled centerline/profile-banked frame axes, not
world axes. V2 does not introduce a heartline arc-length domain, variable
heartline keys, clearance envelopes, default train placement behavior, or any
runtime sampling side effect.

## Intentionally Not Included

`TrackLayoutPackageV2` remains an authored layout package. It intentionally does
not include:

- force profiles
- train consists
- block zones
- editor state
- renderer state
- runtime/debug artifacts

Those concerns should use separate versioned contracts or manifests that
reference the layout when needed.

## V1-To-V2 Migration

A V1-to-V2 migration copies only V1 authored layout fields:

- `metadata.units`
- `metadata.sourceName`
- `startPose`
- `sections`
- `banking`

Migration then sets:

- `contract`: `quantum.track_layout_package`
- `version`: `2`
- `heartline`: `null`
- `metadata.layoutId`: `null`, unless the caller supplies a stable layout ID

Migration must not compile the track, normalize data, repair invalid input,
generate fallback banking, infer a heartline, or generate a layout ID without an
explicit caller request.

## Compatibility Rules

- V1 readers continue to accept only `contract:
  "quantum.track_layout_package"` with `version: 1`.
- V2 readers accept only `contract: "quantum.track_layout_package"` with
  `version: 2`.
- JSON consumers must branch on both `contract` and `version` before reading
  version-specific fields.
- V2 DTOs, schema, validators, and mappers must live beside V1 rather than
  mutating V1.
- V2 import must not compile a runtime track as a side effect, matching V1.
- `heartline: null` is V1-equivalent for layout import/export. It must not
  change default centerline evaluation, frame generation, train placement, or
  debug export behavior.
- Unknown fields should be rejected by the future V2 schema and validator.

## Minimal V2 JSON

```json
{
  "contract": "quantum.track_layout_package",
  "version": 2,
  "metadata": {
    "units": "meters",
    "sourceName": "Minimal V2 layout",
    "layoutId": null
  },
  "startPose": {
    "position": { "x": 0.0, "y": 0.0, "z": 0.0 },
    "tangent": { "x": 1.0, "y": 0.0, "z": 0.0 },
    "normal": { "x": 0.0, "y": 1.0, "z": 0.0 },
    "binormal": { "x": 0.0, "y": 0.0, "z": 1.0 }
  },
  "sections": [
    {
      "kind": "straight",
      "id": "entry",
      "length": 12.0,
      "rollRadians": 0.0
    }
  ],
  "banking": null,
  "heartline": null
}
```

## V2 JSON With Constant Heartline Offset

```json
{
  "contract": "quantum.track_layout_package",
  "version": 2,
  "metadata": {
    "units": "meters",
    "sourceName": "Constant heartline V2 layout",
    "layoutId": "layout.m147.constant-heartline"
  },
  "startPose": {
    "position": { "x": 0.0, "y": 0.0, "z": 0.0 },
    "tangent": { "x": 1.0, "y": 0.0, "z": 0.0 },
    "normal": { "x": 0.0, "y": 1.0, "z": 0.0 },
    "binormal": { "x": 0.0, "y": 0.0, "z": 1.0 }
  },
  "sections": [
    {
      "kind": "straight",
      "id": "entry",
      "length": 12.0,
      "rollRadians": 0.0
    },
    {
      "kind": "constantCurvature",
      "id": "turn",
      "length": 18.0,
      "rollRadians": 0.2,
      "radius": 30.0
    }
  ],
  "banking": {
    "keys": [
      {
        "distance": 0.0,
        "rollRadians": 0.0,
        "interpolationToNext": "linear"
      },
      {
        "distance": 12.0,
        "rollRadians": 0.2,
        "interpolationToNext": "constant"
      },
      {
        "distance": 30.0,
        "rollRadians": 0.2,
        "interpolationToNext": "constant"
      }
    ]
  },
  "heartline": {
    "kind": "constantOffset",
    "distanceDomain": "centerlineStation",
    "axisSource": "sampledFrame",
    "normalOffset": 1.1,
    "lateralOffset": 0.0
  }
}
```
