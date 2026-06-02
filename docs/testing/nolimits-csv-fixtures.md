# NoLimits CSV Fixtures

## Purpose

NoLimits CSV imports, if added, are for temporary debug and testing workflows only. They may help compare authored centerlines, sample spacing, frame behavior, or train placement against known coaster layouts during backend development.

## Allowed Fixture Sources

Only self-authored NoLimits coaster CSV files should be committed or used as repository fixtures. A fixture should come from a layout created by the project owner or contributor who is adding it, with permission to include it in this repository.

## Not Allowed

Do not include third-party layouts, exported tracks, scenery, media, or other assets unless explicit permission is documented. Avoid treating public downloads, commercial layouts, workshop content, or community recreations as free test data.

## Scope Boundaries

CSV support is not a promise of full NoLimits project import. It should not imply support for complete project files, assets, trains, environment data, scripting, or exact simulator compatibility.

For now, CSV data should be treated as a narrow fixture format for backend diagnostics and tests. Any importer should map into Quantum-owned data contracts and keep NoLimits-specific assumptions contained in IO/test adapter code.

## Unity Snapshot Inspection

Unity does not parse NoLimits CSV fixtures. The backend `Quantum.Debug`
`debug-viewport-snapshot-v1-from-csv` command converts approved, self-authored
CSV fixture data into `DebugViewportSnapshotV1` JSON under
`artifacts/debug-viewport`. The Unity Snapshot Browser may then copy/import the
generated JSON, SVG, and HTML artifacts into `Assets/DebugData`, group CSV
fixture snapshots, and inspect or apply the resulting renderer-neutral snapshot
data.
