[CmdletBinding()]
param(
    [string]$Source = "artifacts/debug-viewport",
    [string]$Target = "C:\Dev4\TestingGrounds",
    [switch]$Clean
)

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

function Resolve-AbsolutePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$BasePath
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $Path))
}

function Copy-DebugArtifacts {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourcePath,
        [Parameter(Mandatory = $true)]
        [string]$TargetPath,
        [Parameter(Mandatory = $true)]
        [string]$Filter
    )

    $count = 0
    $files = Get-ChildItem -LiteralPath $SourcePath -File -Filter $Filter | Sort-Object Name

    foreach ($file in $files) {
        Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $TargetPath $file.Name) -Force
        $count++
    }

    return $count
}

$repoRoot = Get-RepoRoot
$sourceBasePath = if ($PSBoundParameters.ContainsKey("Source")) {
    (Get-Location).Path
}
else {
    $repoRoot
}

$sourcePath = Resolve-AbsolutePath -Path $Source -BasePath $sourceBasePath
$targetProjectPath = Resolve-AbsolutePath -Path $Target -BasePath (Get-Location).Path
$targetAssetPath = Join-Path $targetProjectPath "Assets\DebugData"

if (-not (Test-Path -LiteralPath $sourcePath -PathType Container)) {
    throw "Debug artifact source folder was not found at '$sourcePath'. Run .\tools\demo-technical-preview-0.1.cmd first, or pass -Source."
}

if (-not (Test-Path -LiteralPath $targetProjectPath -PathType Container)) {
    throw "Target Unity project folder was not found at '$targetProjectPath'. Pass -Target to a local Unity project."
}

New-Item -ItemType Directory -Path $targetAssetPath -Force | Out-Null

$artifactFilters = [ordered]@{
    JSON = "*.json"
    SVG = "*.svg"
    HTML = "*.html"
}

if ($Clean) {
    foreach ($filter in $artifactFilters.Values) {
        $existingFiles = Get-ChildItem -LiteralPath $targetAssetPath -File -Filter $filter
        foreach ($existingFile in $existingFiles) {
            Remove-Item -LiteralPath $existingFile.FullName -Force
        }
    }
}

$jsonCopied = Copy-DebugArtifacts -SourcePath $sourcePath -TargetPath $targetAssetPath -Filter $artifactFilters.JSON
$svgCopied = Copy-DebugArtifacts -SourcePath $sourcePath -TargetPath $targetAssetPath -Filter $artifactFilters.SVG
$htmlCopied = Copy-DebugArtifacts -SourcePath $sourcePath -TargetPath $targetAssetPath -Filter $artifactFilters.HTML

Write-Host "Unity debug data sync complete."
Write-Host ("JSON copied count: {0}" -f $jsonCopied)
Write-Host ("SVG copied count:  {0}" -f $svgCopied)
Write-Host ("HTML copied count: {0}" -f $htmlCopied)
Write-Host ("Source path:       {0}" -f $sourcePath)
Write-Host ("Target path:       {0}" -f $targetAssetPath)
