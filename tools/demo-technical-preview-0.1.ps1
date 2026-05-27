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
$csvSnapshotPath = Join-Path $artifactDirectory "Milestone7.synthetic.straight_line.snapshot.json"
$builtInSvgPath = Join-Path $artifactDirectory "DebugViewportSnapshotV1.sample.svg"
$csvFixturePath = Join-Path $repoRoot "Quantum.Tests\IO\Fixtures\Milestone7.synthetic.straight_line.centerline_frames.csv"

if (-not (Test-Path -LiteralPath $csvFixturePath -PathType Leaf)) {
    throw "CSV fixture was not found at '$csvFixturePath'."
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
        $builtInSnapshotPath
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
        $builtInSnapshotPath,
        $builtInSvgPath
    )

    Write-Host ""
    Write-Host "SUCCESS: Technical Preview 0.1 backend demo completed."
    Write-Host "Generated snapshots:"
    Write-Host "  $builtInSnapshotPath"
    Write-Host "  $csvSnapshotPath"
    Write-Host "Generated SVG preview:"
    Write-Host "  $builtInSvgPath"
}
finally {
    Pop-Location
}
