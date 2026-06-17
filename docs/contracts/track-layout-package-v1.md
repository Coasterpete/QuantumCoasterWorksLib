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

Supported banking interpolation values are `constant`, `linear`, `smoothStep`, `quadratic`, `cubic`, `quartic`, `quintic`, and `sinusoidal`. Banking must start exactly at distance `0` and end exactly at the authored total section length.

## Import Behavior

Import validates the DTO before constructing `TrackAuthoringDefinition`. It does not compile the track, normalize vectors, trim IDs, repair malformed data, rescale values, infer missing spatial weights, or generate fallback banking.

Compilation remains an explicit caller action through `TrackAuthoringDocumentBuilder`.

## Diagnostics

Validation diagnostics use stable `TrackLayoutPackageV1ValidationCode` values and stable dotted/indexed paths. New diagnostics may improve message wording, but V1 consumers should key automation on `Code` and `Path`, not on full message text.

Code groups:

- contract envelope: `InvalidContract`, `InvalidVersion`, `MissingMetadata`
- required objects and fields: `MissingStartPose`, `MissingSections`, `EmptySections`, `MissingObject`, `MissingRequiredField`, `MissingSectionId`
- section identity and kind shape: `DuplicateSectionId`, `UnknownSectionKind`, `UnexpectedSectionField`
- numeric and section semantics: `NonFiniteNumber`, `NonPositiveLength`, `InvalidRadius`, `InvalidCurvatureInterpolation`
- start pose and spatial semantics: `InvalidStartPoseBasis`, `InvalidSpatialDegree`, `InvalidSpatialControlPoints`, `InvalidSpatialWeights`, `InvalidSpatialStartContract`
- authored banking semantics: `InvalidBankingKeyCount`, `InvalidBankingKeyOrder`, `InvalidBankingInterpolation`, `InvalidBankingDomain`
- import/mapper guardrails: `MalformedJson`, `MappingFailed`

Path convention:

- root fields use their camelCase JSON name, for example `contract`, `version`, `startPose`
- object fields append with dots, for example `startPose.tangent`
- arrays use zero-based indexes, for example `sections[3].controlPoints[1]` and `banking.keys[2].distance`
- malformed JSON and unmapped JSON members report `MalformedJson` at path `json`
- mapper guardrail failures report `MappingFailed` at path `dto`

Section messages include contextual text when available:

- duplicate section id at `sections[1].id`: includes section index `1`, the duplicate id, the current section kind, and the previous section index that used the id
- invalid field at `sections[0].radius`: includes section index `0`, section id, and kind `straight`
- unknown kind at `sections[4].kind`: includes section index `4`, section id when present, and the unknown kind value
- spatial diagnostics such as `sections[3].controlPoints[1]`: include section index, section id, and kind `spatial`

Banking messages include key context:

- ordering errors at `banking.keys[1].distance`: include key index `1`, previous key index, and previous key distance
- final-domain errors at `banking.keys[n].distance`: include key index `n` and the expected total authored section length
- key-local interpolation and numeric errors include the banking key index

Schema validation and C# semantic validation intentionally cover different boundaries. The JSON schema is the external wire-shape contract: required JSON properties, discriminator shape, allowed enum strings, and basic array/count constraints. The C# validator runs after `System.Text.Json` materializes the DTO: it checks semantic rules such as duplicate section ids, basis orthonormality, finite numeric values, positive section lengths, spatial start contracts, strict banking key order, and banking coverage of the authored total length.

Because DTOs have default values, some raw JSON omissions are schema-invalid but still materialize to the same DTO defaults that existing imports have historically accepted. Examples include missing `banking`, missing `metadata.sourceName`, missing vector components that default to `0`, or missing `banking.keys[i].interpolationToNext` defaulting to `constant`. This is characterized behavior for V1 compatibility, not an endorsement for authored JSON. Producers should emit schema-valid packages.
