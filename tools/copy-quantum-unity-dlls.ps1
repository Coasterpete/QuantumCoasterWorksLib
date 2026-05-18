[CmdletBinding()]
param(
    [string]$Target = "Assets/Plugins/Quantum",
    [string]$SourceRoot,
    [switch]$IncludeQuantumIO
)

$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location).Path $Path))
}

function Resolve-GSharkDllPath {
    param(
        [Parameter(Mandatory = $true)][string]$TrackReleaseOutput,
        [Parameter(Mandatory = $true)][string]$SplinesReleaseOutput
    )

    $releaseCandidate = Join-Path $TrackReleaseOutput "GShark.dll"
    if (Test-Path -LiteralPath $releaseCandidate) {
        return $releaseCandidate
    }

    $depsPath = Join-Path $SplinesReleaseOutput "Quantum.Splines.deps.json"
    if (-not (Test-Path -LiteralPath $depsPath)) {
        throw "Missing $depsPath. Build Quantum.Splines in Release first."
    }

    $deps = Get-Content -Raw -LiteralPath $depsPath | ConvertFrom-Json
    $gsharkLibrary = $deps.libraries.PSObject.Properties |
        Where-Object { $_.Name -like "GShark/*" } |
        Select-Object -First 1

    if ($null -eq $gsharkLibrary) {
        throw "GShark package metadata was not found in $depsPath."
    }

    $nugetRoot = Join-Path $env:USERPROFILE ".nuget\packages"
    $gsharkPath = Join-Path $nugetRoot ($gsharkLibrary.Value.path + "\lib\netstandard2.0\GShark.dll")

    if (-not (Test-Path -LiteralPath $gsharkPath)) {
        throw "Could not find GShark.dll at $gsharkPath."
    }

    return $gsharkPath
}

$effectiveSourceRoot = $SourceRoot
if ([string]::IsNullOrWhiteSpace($effectiveSourceRoot)) {
    if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
        $effectiveSourceRoot = (Get-Location).Path
    }
    else {
        $effectiveSourceRoot = Join-Path $PSScriptRoot ".."
    }
}

$sourceRootPath = Resolve-AbsolutePath -Path $effectiveSourceRoot
$targetPath = Resolve-AbsolutePath -Path $Target

$trackReleaseOutput = Join-Path $sourceRootPath "Quantum.Track\bin\Release\netstandard2.1"
$splinesReleaseOutput = Join-Path $sourceRootPath "Quantum.Splines\bin\Release\netstandard2.1"

$sourceMap = [ordered]@{
    "Quantum.Math.dll" = Join-Path $trackReleaseOutput "Quantum.Math.dll"
    "Quantum.Splines.dll" = Join-Path $trackReleaseOutput "Quantum.Splines.dll"
    "Quantum.Track.dll" = Join-Path $trackReleaseOutput "Quantum.Track.dll"
}

$sourceMap["GShark.dll"] = Resolve-GSharkDllPath -TrackReleaseOutput $trackReleaseOutput -SplinesReleaseOutput $splinesReleaseOutput

if ($IncludeQuantumIO) {
    $sourceMap["Quantum.IO.dll"] = Join-Path $sourceRootPath "Quantum.IO\bin\Release\netstandard2.1\Quantum.IO.dll"
}

$missing = @()
foreach ($entry in $sourceMap.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath $entry.Value)) {
        $missing += "$($entry.Key) (expected at: $($entry.Value))"
    }
}

if ($missing.Count -gt 0) {
    Write-Host "Missing required DLLs:"
    foreach ($item in $missing) {
        Write-Host " - $item"
    }
    throw "Build Release outputs first (example: dotnet build .\Quantum.Track\Quantum.Track.csproj -c Release)."
}

New-Item -ItemType Directory -Path $targetPath -Force | Out-Null

foreach ($entry in $sourceMap.GetEnumerator()) {
    $destination = Join-Path $targetPath $entry.Key
    Copy-Item -LiteralPath $entry.Value -Destination $destination -Force
    Write-Host ("Copied {0} -> {1}" -f $entry.Value, $destination)
}

Write-Host ""
Write-Host "Done. Unity plugin DLLs are in: $targetPath"
if (-not $IncludeQuantumIO) {
    Write-Host "Quantum.IO.dll was skipped (use -IncludeQuantumIO to include it)."
}
