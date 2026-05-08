# TrainPoseExportV1 JSON Contract

- Contract name: `quantum.train_pose`
- Version: `1`
- Serialization naming: camelCase JSON properties

## Top-Level Fields
- `contract` (string, required): must equal `quantum.train_pose`.
- `version` (integer, required): must equal `1`.
- `leadDistance` (number, required): lead car station distance along the track coordinate.
- `definition` (object, required): train consist definition snapshot.
- `cars` (array, required): articulated car pose hierarchy.

## `definition` Fields
- `carCount` (integer, required)
- `carSpacing` (number, required)
- `carGeometry` (object, required)
- `carGeometry.length` (number, required)
- `carGeometry.width` (number, required)
- `carGeometry.height` (number, required)
- `bogieLayout` (object, required)
- `bogieLayout.bogieSpacing` (number, required)
- `wheelLayout` (object or null, optional)
- `wheelLayout.wheelCountPerBogie` (integer, required when `wheelLayout` is present)
- `wheelLayout.wheelRadius` (number, required when `wheelLayout` is present)
- `wheelLayout.wheelWidth` (number, required when `wheelLayout` is present)
- `wheelLayout.axleSpacing` (number, required when `wheelLayout` is present)

## Car/Body/Bogie/Wheel Hierarchy
- `cars[]`
- `cars[].body`
- `cars[].body.originalBody` (`carIndex`, `distance`, `frame`, `matrix`)
- `cars[].body.frontBogie` (`carIndex`, `bogieIndex`, `distance`, `frame`, `matrix`)
- `cars[].body.rearBogie` (`carIndex`, `bogieIndex`, `distance`, `frame`, `matrix`)
- `cars[].body.articulatedFrame` (`frame`)
- `cars[].body.articulatedMatrix` (`matrix`)
- `cars[].body.centerDistance`
- `cars[].frontBogie.bogie` (`carIndex`, `bogieIndex`, `distance`, `frame`, `matrix`)
- `cars[].frontBogie.wheels[]`
- `cars[].rearBogie.bogie` (`carIndex`, `bogieIndex`, `distance`, `frame`, `matrix`)
- `cars[].rearBogie.wheels[]`
- `wheels[]` element fields: `carIndex`, `bogieIndex`, `wheelIndex`, `localOffsetX`, `localOffsetY`, `localOffsetZ`, `frame`, `matrix`

## Frame Fields and Coordinate Convention
- `frame` object fields:
- `distance` (number)
- `position` (`x`, `y`, `z`)
- `tangent` (`x`, `y`, `z`)
- `normal` (`x`, `y`, `z`)
- `binormal` (`x`, `y`, `z`)

Frame convention in v1:
- `TrackFrame` is the authoritative pose basis.
- `tangent` = forward axis, `normal` = up axis, `binormal` = right/lateral axis.
- Basis vectors are expected to be orthonormal in producer output.
- `distance` follows track station coordinate `s` (backend track-space distance).

## Matrix Fields and Current Precision Policy
- `matrix` and `articulatedMatrix` are objects with 16 fields: `m11`..`m44`.
- Canonical frame->matrix convention is column-vector:
- first three columns are `tangent`, `normal`, `binormal`
- fourth column is `position`
- For canonical `TrackFrame.ToMatrix4x4()` output, translation is in `m14/m24/m34`; bottom row is `0,0,0,1`.

Current precision policy (v1):
- JSON matrix fields are serialized as numbers (DTO stores `double`).
- `originalBody.matrix` comes from `Matrix4x4` (float) and is widened to `double` in export.
- Bogie/wheel/articulated matrices come from `Matrix4x4d` (double).
- Export is direct field copy with no extra rounding or quantization policy.

## Units Assumptions
- No explicit unit metadata is embedded in v1 JSON.
- Distances, offsets, and geometry dimensions are assumed to use the backend track-space linear unit consistently (current backend convention: meters along track for station `s`).

## Compatibility and Versioning Rules
- Reader validation is strict for identity:
- `contract` must be `quantum.train_pose`
- `version` must be `1`
- Any mismatch is rejected during deserialize.
- Keep existing field names, types, and semantics stable within v1.
- Breaking changes (rename/remove/type/semantic change) require a new contract version (for example v2).

## What v1 Does Not Guarantee Yet
- No Unity/renderer integration guarantees (backend contract only).
- No embedded unit system tag or coordinate-system metadata beyond current conventions.
- No guarantee that all producer values are physically valid/orthonormal at deserialize time (current read-path enforcement is contract/version, not deep numeric validation).
- No single-precision vs double-precision unification across all matrix sources.
- No time-series/animation guarantees; payload is a snapshot contract.
