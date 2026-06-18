# Track Layout Package V2 Design

Date: 2026-06-18

Status: Milestone 147 PR1 design only. This document does not implement
`TrackLayoutPackageV2`, change `TrackLayoutPackageV1`, change schemas or DTOs,
or add runtime behavior.

## Purpose

`TrackLayoutPackageV1` is a narrow authored-layout contract. Milestone 147 uses
that contract as the compatibility baseline and asks what belongs in a future
`TrackLayoutPackageV2`.

The recommendation is to keep V2 focused on authored track layout and
frame/rider-reference inputs. V2 should not become a giant project file for
forces, trains, block operations, editor state, renderer state, or physics
state. Those areas should become separate contracts that can reference a layout
by stable IDs and station distances.

## 1. What V1 Supports Today

V1 is the backend-only JSON contract for authored layout input:

- `contract`: `quantum.track_layout_package`
- `version`: `1`
- `metadata`: currently `units` and optional `sourceName`
- `startPose`: position, tangent, normal, and binormal
- `sections`: ordered authored geometric sections
- `banking`: optional explicit authored banking keys, or `null`

Supported V1 section kinds are:

- `straight`
- `constantCurvature`
- `curvatureTransition`
- `spatial`

Every section has:

- `kind`
- `id`
- `length`
- `rollRadians`

Additional section fields are kind-specific:

- `constantCurvature`: `radius`
- `curvatureTransition`: `startCurvature`, `endCurvature`, and
  `interpolationMode`
- `spatial`: `degree`, `controlPoints`, and `weights`

V1 curvature transitions support only `linear` interpolation.

V1 banking is either `null` or an object with ordered `keys`. Each key stores a
station distance, an absolute unwrapped roll angle in radians, and the
interpolation mode to the next key. Banking must cover the authored station
domain exactly from distance `0` to the total authored section length.

The V1 import path validates the DTO, then maps to `TrackAuthoringDefinition`.
It does not compile the track, normalize inputs, repair malformed data, infer
missing spatial weights, rescale geometry, or generate fallback banking. Normal
compilation remains an explicit caller action through
`TrackAuthoringDocumentBuilder`.

## 2. What V1 Intentionally Excludes

V1 intentionally excludes:

- compiled runtime data
- generated fallback banking profiles
- diagnostics and sampled frames
- heartline offset or heartline authoring
- force sections, force channels, force targets, and FVD solver state
- train defaults, train consists, car art, meshes, bogies, wheels, and physics
  state
- block zones, dispatch rules, sensors, brakes, lift operations, and station
  logic
- editor selection, UI state, undo history, viewport state, and renderer state
- Unity, Unreal, Blender, browser, or native-adapter data
- terrain, supports, scenery, clearance envelopes, and collisions
- nonlinear curvature-transition modes beyond the current `linear` value
- custom NURBS knot vectors or broad spline-authoring options

That narrowness is a strength. V1 is easy to validate and is not coupled to
debug viewers, train placement experiments, or future simulation layers.

## 3. Candidate V2 Features

The main candidates are listed below with their relationship to the layout
contract.

### Heartline Offset And Heartline Authoring

Heartline offset is closely related to layout because it describes a
rider-reference path derived from the sampled track frame. The current backend
foundation is intentionally small:

- `HeartlineOffset`: normal and lateral offsets
- `HeartlineFrame`: sampled centerline station, centerline position, offset
  position, and inherited frame axes
- `HeartlineSampler`: opt-in sampling from centerline frames or
  profile-banked frames

V2 should include only a minimal optional authored heartline block. The first
shape should represent a constant offset in the existing centerline station
domain. It should not introduce heartline arc length, variable heartline
profiles, clearance envelopes, train defaults, or default runtime behavior.

### Force Sections Or Force Channels

Force targets are active backend concepts, but they are not layout geometry.
`ForceSection` currently carries compatibility scalar fields, easing functions,
distance/time domains, and newer channel containers. The force API is also
still transitional.

Forces should remain out of `TrackLayoutPackageV2`. A later force package can
reference a layout ID, section IDs, station ranges, or normalized domains. That
keeps layout import/export stable while force and FVD contracts evolve.

### Train Defaults Or Consist Metadata

Train consists are useful for debug visualization and deterministic train-pose
fixtures, but they describe a vehicle configuration rather than track layout.
`TrainConsistDefinition` is engine-agnostic and tested, but a layout package
should not need a train to be valid.

Train defaults should remain out of `TrackLayoutPackageV2`. A separate train
consist contract can store debug/default consists and optionally reference a
layout or project manifest.

### Block Zones

Block zones are not implemented as a backend domain model today. They will need
their own semantics for station ranges, sensors, brakes, dispatching, train
occupancy, direction, station behavior, and operations simulation.

Block zones should remain out of `TrackLayoutPackageV2`. They should start with
a separate design and domain model before any JSON contract is locked.

### Future Nonlinear Curvature Transitions

Curvature-transition interpolation is layout geometry. If the backend adds
well-defined nonlinear transition modes, those modes belong in the layout
section shape, not in a separate package.

V2 is the right contract family for future transition modes such as
`smoothStep`, but the schema must only allow modes that the backend can import,
validate, compile, and test. Do not add broad symbolic functions or custom
polynomial payloads to V2 unless there is a mature implementation and a clear
coaster-domain use case.

## 4. Recommended V2 Contents

V2 should include:

- the V1 contract family and authored layout fields
- `version: 2`
- optional stable layout identity in metadata, for example `layoutId`
- ordered geometric sections with the same basic discriminator model as V1
- explicit authored banking, preserving V1 semantics
- optional minimal heartline authoring
- future curvature-transition interpolation enum growth only when implemented
  in the backend

Recommended V2 heartline scope:

- `heartline` is required by schema but can be `null`, matching the V1
  `banking` pattern
- non-null `heartline.kind` starts with `constantOffset`
- offsets are finite scalar authored distances
- offset axes are sampled frame axes, not world axes
- distance domain is `centerlineStation`
- no heartline arc-length domain
- no variable offset keys in the first V2 contract
- no default train placement behavior change

Recommended V2 metadata additions:

- `layoutId`: optional stable string for companion contracts and manifests
- no required project file path, editor state, or renderer state

Recommended V2 transition scope:

- keep `linear` as the compatibility baseline
- add nonlinear enum values only after backend support exists
- prefer one deterministic mode first, such as `smoothStep`
- avoid generic equation strings, embedded scripts, or arbitrary math payloads

## 5. Recommended Separate Future Contracts

The following should remain separate contracts rather than fields inside V2.

### Force Profile Package

Force targets should use a separate package, tentatively:

```text
contract: quantum.force_profile
version: 1
```

Reasons:

- force sections may be distance-domain or time-domain
- force channels and blend modes are still evolving
- force targets may be solver input, diagnostic output, or physics input
- roll-rate force channels should not silently drive geometric banking
- FVD-style force authoring should not force a layout-package revision

### Train Consist Package

Train defaults should use a separate package, tentatively:

```text
contract: quantum.train_consist
version: 1
```

Reasons:

- a track layout should be valid without a train
- multiple trains may apply to one layout
- debug box geometry and future real train metadata will evolve at different
  speeds
- train placement depends on a sampled track/runtime, not on layout persistence
  alone

### Operations Or Block Zones Package

Block zones should use a separate operations package after the domain model
exists, tentatively:

```text
contract: quantum.operations_zones
version: 1
```

Reasons:

- block behavior is operations logic, not geometry
- station, brake, lift, dispatch, and sensor semantics need explicit design
- operations will likely reference train state and simulation state
- no current backend `BlockZone` type exists

### Project Or Bundle Manifest

If a future editor wants one file to tie these contracts together, add a thin
manifest rather than bloating the layout package:

```text
contract: quantum.project_manifest
version: 1
```

The manifest can reference a layout package, force profile, train consist,
operations zones, debug artifacts, and renderer assets. The manifest should be
the composition layer; each contract should remain independently versioned.

## 6. Compatibility Strategy With V1

V1 must remain stable:

- do not edit `TrackLayoutPackageV1` DTOs
- do not edit the V1 schema
- do not change V1 validation behavior
- do not change V1 import/export semantics
- do not add V2 fields to V1 with optional defaults

V2 should be a new versioned contract under the same contract family:

```json
{
  "contract": "quantum.track_layout_package",
  "version": 2
}
```

Compatibility rules:

- V1 readers continue to accept only version `1`.
- V2 readers accept only version `2`.
- A V1-to-V2 migration helper can copy V1 metadata, start pose, sections, and
  banking exactly.
- Migrated V1 packages set `heartline` to `null`.
- Migrated V1 packages may omit `metadata.layoutId` or generate one only when a
  caller explicitly asks for bundle references.
- V2 export from an existing `TrackAuthoringDefinition` should be able to emit
  V1-equivalent layout data with `heartline: null`.
- V2 import should not compile the track as a side effect, matching V1.
- JSON consumers should branch on both `contract` and `version` before reading
  fields.

The default behavior of centerline evaluation, frame generation, train
placement, and debug exports should remain unchanged until a later runtime PR
explicitly opts into V2 data.

## 7. Proposed JSON Shape Examples

### V2 Layout Package With Optional Heartline

This is the recommended direction for the layout package itself. The
`smoothStep` transition mode is shown as a V2 candidate only after matching
backend support exists.

```json
{
  "contract": "quantum.track_layout_package",
  "version": 2,
  "metadata": {
    "units": "meters",
    "sourceName": "M147 layout package v2 sketch",
    "layoutId": "layout.m147.example"
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
      "kind": "curvatureTransition",
      "id": "transition-in",
      "length": 10.0,
      "rollRadians": 0.0,
      "startCurvature": 0.0,
      "endCurvature": 0.03333333333333333,
      "interpolationMode": "smoothStep"
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
        "rollRadians": 0.0,
        "interpolationToNext": "smoothStep"
      },
      {
        "distance": 22.0,
        "rollRadians": 0.2,
        "interpolationToNext": "constant"
      },
      {
        "distance": 40.0,
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

If no heartline is authored:

```json
{
  "contract": "quantum.track_layout_package",
  "version": 2,
  "metadata": {
    "units": "meters",
    "sourceName": "V1-equivalent migrated layout",
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

### Separate Force Profile Package

This should not be embedded in V2. It is shown only to illustrate how a future
contract can reference the layout without expanding the layout package.

```json
{
  "contract": "quantum.force_profile",
  "version": 1,
  "layoutRef": {
    "contract": "quantum.track_layout_package",
    "version": 2,
    "layoutId": "layout.m147.example"
  },
  "sections": [
    {
      "id": "pullout-force",
      "domain": "distance",
      "startDistance": 12.0,
      "endDistance": 40.0,
      "channels": {
        "normalG": [
          {
            "kind": "keyframed",
            "blendMode": "override",
            "keys": [
              { "x": 12.0, "value": 1.0 },
              { "x": 24.0, "value": 3.2 },
              { "x": 40.0, "value": 2.4 }
            ]
          }
        ],
        "lateralG": [],
        "longitudinalG": [],
        "rollRateDegPerSec": []
      }
    }
  ]
}
```

### Separate Train Consist Package

This should not be embedded in V2.

```json
{
  "contract": "quantum.train_consist",
  "version": 1,
  "layoutRef": {
    "layoutId": "layout.m147.example"
  },
  "defaultConsistId": "debug-five-car",
  "consists": [
    {
      "id": "debug-five-car",
      "carCount": 5,
      "carSpacing": 6.0,
      "carGeometry": {
        "length": 4.5,
        "width": 1.6,
        "height": 1.4
      },
      "bogieLayout": {
        "bogieSpacing": 3.2
      }
    }
  ]
}
```

### Separate Operations Zones Package

This should not be embedded in V2 and should wait until the `BlockZone` domain
model exists.

```json
{
  "contract": "quantum.operations_zones",
  "version": 1,
  "layoutRef": {
    "layoutId": "layout.m147.example"
  },
  "blockZones": [
    {
      "id": "station",
      "startDistance": 0.0,
      "endDistance": 18.0,
      "entrySensorDistance": 0.0,
      "exitSensorDistance": 18.0,
      "kind": "station"
    },
    {
      "id": "main-brake",
      "startDistance": 240.0,
      "endDistance": 275.0,
      "entrySensorDistance": 240.0,
      "exitSensorDistance": 275.0,
      "kind": "brake"
    }
  ]
}
```

## 8. Recommended Implementation PR Breakdown

1. M147 PR1: design only.
   - Add this document.
   - Do not change V1, schemas, DTOs, tests, or runtime behavior.

2. V2 shape lock.
   - Finalize exact field names and enum strings.
   - Decide whether the first shipped V2 includes only `linear` transitions or
     also one implemented nonlinear mode.
   - Add a short contract note under `docs/contracts` before code if the shape
     changes from this design.

3. Nonlinear curvature transition backend support, if included in V2.
   - Add the new interpolation mode to the backend authoring/runtime path.
   - Add focused tests for expected positions, tangents, curvature continuity,
     validation, and fallback parity.
   - Do this before allowing the mode in any V2 schema.

4. Minimal V2 DTO/schema/json skeleton.
   - Add `Quantum.IO.TrackLayout.V2` DTOs, JSON helpers, vocabulary, and schema.
   - Keep import/export behavior V1-equivalent at first.
   - Add schema and DTO contract tests.
   - Do not mutate V1.

5. V2 mapper and validation.
   - Map V2 geometry and banking to existing authoring definitions.
   - Add V1-to-V2 migration tests.
   - Add V2 golden fixtures that prove V1-equivalent layouts round-trip.
   - Keep import from compiling the layout as a side effect.

6. Minimal V2 heartline authoring.
   - Add the optional `heartline` block.
   - Map only constant offsets to the existing heartline foundation or a tiny
     authoring value object.
   - Add validation tests for finite offsets and accepted domain/axis strings.
   - Keep default train placement and default evaluator behavior unchanged.

7. Separate force-profile design and contract PR.
   - Design a force package around current normalized force-channel direction.
   - Reference layout IDs and station ranges instead of embedding in V2.
   - Add DTO/schema/tests only after the force shape is stable enough.

8. Separate train-consist design and contract PR.
   - Persist engine-agnostic consist metadata and debug defaults.
   - Keep real meshes and renderer assets outside the backend contract.

9. Separate block-zone domain design PR.
   - Define backend `BlockZone` semantics before any JSON contract.
   - Keep operations simulation separate from layout authoring.

## Summary Decision

`TrackLayoutPackageV2` should be a careful layout evolution, not a project-file
grab bag. Include minimal heartline authoring and geometry-owned transition
growth. Keep force profiles, train consists, and block operations as separate
contracts that reference the layout when needed.
