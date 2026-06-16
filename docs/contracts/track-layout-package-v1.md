# Track Layout Package V1

`TrackLayoutPackageV1` is the backend-only authored layout save/load contract for Quantum track authoring definitions.

- Contract name: `quantum.track_layout_package`
- Version: `1`
- DTO root: `TrackLayoutPackageV1Dto`
- Namespace: `Quantum.IO.TrackLayout.V1`
- JSON naming: camelCase `System.Text.Json`
- Schema: `docs/contracts/track-layout-package-v1.schema.json`

## Persisted Data

The package stores only authored layout input:

- metadata, currently `units` and optional `sourceName`
- `startPose`, with position, tangent, normal, and binormal
- ordered geometric sections
- optional explicit authored banking

The package does not persist compiled runtime data, generated fallback banking profiles, diagnostics, heartline data, train data, force sections, UI state, renderer state, editor state, or physics state.

## Section Kinds

All sections use a flat `kind` discriminator plus common authored fields:

- `kind`
- `id`
- `length`
- `rollRadians`

Supported kinds:

- `straight`
- `constantCurvature`, with `radius`
- `curvatureTransition`, with `startCurvature`, `endCurvature`, and `interpolationMode`
- `spatial`, with `degree`, `controlPoints`, and `weights`

The only curvature-transition interpolation value in V1 is `linear`.

## Banking

`banking` is either `null` or an object with ordered `keys`.

Each key stores:

- `distance`
- `rollRadians`
- `interpolationToNext`

Supported banking interpolation values are `constant`, `linear`, and `smoothStep`. Banking must start exactly at distance `0` and end exactly at the authored total section length.

## Import Behavior

Import validates the DTO before constructing `TrackAuthoringDefinition`. It does not compile the track, normalize vectors, trim IDs, repair malformed data, rescale values, infer missing spatial weights, or generate fallback banking.

Compilation remains an explicit caller action through `TrackAuthoringDocumentBuilder`.
