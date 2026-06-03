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

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    Write-Host ""
    Write-Host "$FilePath $($Arguments -join ' ')"

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Assert-GeneratedFile {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Label
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Label was not generated at '$Path'."
    }

    $item = Get-Item -LiteralPath $Path
    if ($item.Length -le 0) {
        throw "$Label was generated at '$Path', but the file is empty."
    }
}

$repoRoot = Get-RepoRoot
$snapshotPath = Join-Path $repoRoot "artifacts\debug-viewport\DebugViewportSnapshotV1.sample.json"
$trainPosePath = Join-Path $repoRoot "artifacts\train-pose\TrainPoseExportV1.sample.json"
$blenderScriptDirectory = Join-Path $repoRoot "tools\blender"
$snapshotImporterPath = Join-Path $blenderScriptDirectory "import_debug_viewport_snapshot_v1.py"
$trainPoseImporterPath = Join-Path $blenderScriptDirectory "import_train_pose_export_v1.py"
$debugSceneImporterPath = Join-Path $blenderScriptDirectory "import_debug_scene.py"
$renderPath = Join-Path ([System.IO.Path]::GetTempPath()) ("quantum-blender-visualization-smoke-" + [Guid]::NewGuid().ToString("N") + ".png")

Push-Location $repoRoot
try {
    Write-Host "Quantum Blender visualization smoke"
    Write-Host "Repository: $repoRoot"
    Write-Host "Snapshot:   $snapshotPath"
    Write-Host "Train pose: $trainPosePath"

    Invoke-Checked -FilePath "dotnet" -Arguments @(
        "run",
        "--project",
        "Quantum.Debug",
        "--",
        "debug-viewport-snapshot-v1",
        $snapshotPath
    )
    Assert-GeneratedFile -Path $snapshotPath -Label "DebugViewportSnapshotV1 sample JSON"

    Invoke-Checked -FilePath "dotnet" -Arguments @(
        "run",
        "--project",
        "Quantum.Debug",
        "--",
        "debug-viewport-snapshot-v1-validate",
        $snapshotPath
    )

    Invoke-Checked -FilePath "dotnet" -Arguments @(
        "run",
        "--project",
        "Quantum.Debug",
        "--",
        "train-pose-export-v1",
        $trainPosePath
    )
    Assert-GeneratedFile -Path $trainPosePath -Label "TrainPoseExportV1 sample JSON"

    $blenderCommand = Get-Command "blender" -ErrorAction SilentlyContinue
    if ($null -eq $blenderCommand) {
        Write-Host ""
        Write-Host "SKIP: Blender-specific smoke checks were skipped because 'blender' is not on PATH."
        Write-Host "Generated and validated required JSON artifacts successfully."
        return
    }

    Write-Host ""
    Write-Host "Blender: $($blenderCommand.Source)"

    Invoke-Checked -FilePath $blenderCommand.Source -Arguments @(
        "--background",
        "--factory-startup",
        "--python",
        $snapshotImporterPath,
        "--",
        $snapshotPath
    )

    Invoke-Checked -FilePath $blenderCommand.Source -Arguments @(
        "--background",
        "--factory-startup",
        "--python",
        $trainPoseImporterPath,
        "--",
        "--pose",
        $trainPosePath
    )

    Invoke-Checked -FilePath $blenderCommand.Source -Arguments @(
        "--background",
        "--factory-startup",
        "--python",
        $debugSceneImporterPath,
        "--",
        "--snapshot",
        $snapshotPath,
        "--pose",
        $trainPosePath
    )

    Invoke-Checked -FilePath $blenderCommand.Source -Arguments @(
        "--background",
        "--factory-startup",
        "--python",
        $debugSceneImporterPath,
        "--",
        "--snapshot",
        $snapshotPath,
        "--pose",
        $trainPosePath,
        "--render-output",
        $renderPath,
        "--resolution-width",
        "640",
        "--resolution-height",
        "360",
        "--camera-mode",
        "diagnostic"
    )
    Assert-GeneratedFile -Path $renderPath -Label "Blender render smoke PNG"

    Write-Host ""
    Write-Host "SUCCESS: Blender visualization smoke completed."
}
finally {
    if (Test-Path -LiteralPath $renderPath -PathType Leaf) {
        Remove-Item -LiteralPath $renderPath -Force
        Write-Host "Removed temporary render smoke output: $renderPath"
    }

    Pop-Location
}
