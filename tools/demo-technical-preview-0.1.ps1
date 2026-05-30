Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-RepoRoot {
    $scriptDirectory = if ($PSScriptRoot) {
        $PSScriptRoot
    }
    else {
        Split-Path -Parent $MyInvocation.MyCommand.Path
    }

    $repoRoot = (Resolve-Path -LiteralPath (Join-Path $scriptDirectory "..")).Path
    $solutionPath = Join-Path $repoRoot "QuantumCoasterWorks.sln"

    if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) {
        throw "Could not resolve repository root. Expected solution file at '$solutionPath'."
    }

    return $repoRoot
}

function Invoke-DotNetChecked {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    Write-Host ""
    Write-Host "dotnet $($Arguments -join ' ')"

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

$repoRoot = Get-RepoRoot
$artifactDirectory = Join-Path $repoRoot "artifacts\debug-viewport"
$builtInSnapshotPath = Join-Path $artifactDirectory "DebugViewportSnapshotV1.sample.json"
$builtInSvgPath = Join-Path $artifactDirectory "DebugViewportSnapshotV1.sample.svg"
$galleryPath = Join-Path $artifactDirectory "index.html"
$browserPath = Join-Path $artifactDirectory "browser.html"
$previewIndexPath = Join-Path $artifactDirectory "snapshot-preview-index.md"
$fixtureDirectory = Join-Path $repoRoot "Quantum.Tests\IO\Fixtures"
$fixturePreviews = @(
    [PSCustomObject]@{
        Label = "Milestone 7 straight line"
        BaseName = "Milestone7.synthetic.straight_line"
    },
    [PSCustomObject]@{
        Label = "Milestone 7 simple hill"
        BaseName = "Milestone7.synthetic.simple_hill"
    },
    [PSCustomObject]@{
        Label = "Milestone 7 banked turn"
        BaseName = "Milestone7.synthetic.banked_turn"
    },
    [PSCustomObject]@{
        Label = "Milestone 7 descending/ascending curve"
        BaseName = "Milestone7.synthetic.descending_ascending_curve"
    }
)

foreach ($fixture in $fixturePreviews) {
    $csvFixturePath = Join-Path $fixtureDirectory ($fixture.BaseName + ".centerline_frames.csv")
    if (-not (Test-Path -LiteralPath $csvFixturePath -PathType Leaf)) {
        throw "CSV fixture was not found at '$csvFixturePath'."
    }
}

New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null

Push-Location $repoRoot
try {
    Write-Host "Quantum CoasterWorks Technical Preview 0.1 backend demo"
    Write-Host "Repository: $repoRoot"
    Write-Host "Artifacts:  $artifactDirectory"

    Invoke-DotNetChecked -Arguments @(
        "test",
        "QuantumCoasterWorks.sln",
        "--nologo"
    )

    Invoke-DotNetChecked -Arguments @(
        "run",
        "--project",
        "Quantum.Debug",
        "--",
        "help"
    )

    Invoke-DotNetChecked -Arguments @(
        "run",
        "--project",
        "Quantum.Debug",
        "--",
        "debug-viewport-snapshot-v1",
        $builtInSnapshotPath
    )

    Invoke-DotNetChecked -Arguments @(
        "run",
        "--project",
        "Quantum.Debug",
        "--",
        "debug-viewport-snapshot-v1-validate",
        $builtInSnapshotPath
    )

    Invoke-DotNetChecked -Arguments @(
        "run",
        "--project",
        "Quantum.Debug",
        "--",
        "debug-viewport-snapshot-v1-svg",
        $builtInSnapshotPath,
        $builtInSvgPath
    )

    $generatedPreviews = @(
        [PSCustomObject]@{
            Label = "Built-in sample"
            SnapshotPath = $builtInSnapshotPath
            SvgPath = $builtInSvgPath
        }
    )

    foreach ($fixture in $fixturePreviews) {
        $csvFixturePath = Join-Path $fixtureDirectory ($fixture.BaseName + ".centerline_frames.csv")
        $csvSnapshotPath = Join-Path $artifactDirectory ($fixture.BaseName + ".snapshot.json")
        $csvSvgPath = Join-Path $artifactDirectory ($fixture.BaseName + ".snapshot.svg")

        Invoke-DotNetChecked -Arguments @(
            "run",
            "--project",
            "Quantum.Debug",
            "--",
            "debug-viewport-snapshot-v1-from-csv",
            $csvFixturePath,
            $csvSnapshotPath
        )

        Invoke-DotNetChecked -Arguments @(
            "run",
            "--project",
            "Quantum.Debug",
            "--",
            "debug-viewport-snapshot-v1-validate",
            $csvSnapshotPath
        )

        Invoke-DotNetChecked -Arguments @(
            "run",
            "--project",
            "Quantum.Debug",
            "--",
            "debug-viewport-snapshot-v1-svg",
            $csvSnapshotPath,
            $csvSvgPath
        )

        $generatedPreviews += [PSCustomObject]@{
            Label = $fixture.Label
            SnapshotPath = $csvSnapshotPath
            SvgPath = $csvSvgPath
        }
    }

    Invoke-DotNetChecked -Arguments @(
        "run",
        "--project",
        "Quantum.Debug",
        "--",
        "debug-viewport-snapshot-v1-gallery",
        $artifactDirectory,
        $galleryPath
    )

    Invoke-DotNetChecked -Arguments @(
        "run",
        "--project",
        "Quantum.Debug",
        "--",
        "debug-viewport-snapshot-v1-browser",
        $artifactDirectory,
        $browserPath
    )

    if (-not (Test-Path -LiteralPath $galleryPath -PathType Leaf)) {
        throw "Static SVG gallery was not generated at '$galleryPath'."
    }

    if (-not (Test-Path -LiteralPath $browserPath -PathType Leaf)) {
        throw "Browser debug viewer was not generated at '$browserPath'."
    }

    if (-not (Test-Path -LiteralPath $previewIndexPath -PathType Leaf)) {
        throw "Snapshot preview index was not generated at '$previewIndexPath'."
    }

    Write-Host ""
    Write-Host "SUCCESS: Technical Preview 0.1 backend demo completed."
    Write-Host "Open first:"
    Write-Host "  Artifact index / README: $previewIndexPath"
    Write-Host "  Static SVG gallery:      $galleryPath"
    Write-Host "  Browser debug viewer:    $browserPath"
    Write-Host "  Built-in sample JSON:    $builtInSnapshotPath"
    Write-Host "  Built-in sample SVG:     $builtInSvgPath"

    Write-Host ""
    Write-Host "Generated JSON snapshots (renderer-neutral DebugViewportSnapshotV1 data):"
    Write-Host "  $builtInSnapshotPath"
    foreach ($preview in $generatedPreviews | Select-Object -Skip 1) {
        Write-Host "  $($preview.SnapshotPath)"
    }

    Write-Host "Generated SVG previews (backend-only technical visual checks):"
    Write-Host "  $builtInSvgPath"
    foreach ($preview in $generatedPreviews | Select-Object -Skip 1) {
        Write-Host "  $($preview.SvgPath)"
    }
}
finally {
    Pop-Location
}
