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

function ConvertTo-HtmlText {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Value
    )

    return [System.Net.WebUtility]::HtmlEncode($Value)
}

function New-DebugViewportGallery {
    param(
        [Parameter(Mandatory = $true)]
        [string] $OutputPath,

        [Parameter(Mandatory = $true)]
        [object[]] $Previews
    )

    $builder = [System.Text.StringBuilder]::new()
    [void] $builder.AppendLine("<!doctype html>")
    [void] $builder.AppendLine("<html lang=""en"">")
    [void] $builder.AppendLine("<head>")
    [void] $builder.AppendLine("  <meta charset=""utf-8"">")
    [void] $builder.AppendLine("  <title>Quantum DebugViewportSnapshotV1 Gallery</title>")
    [void] $builder.AppendLine("  <style>")
    [void] $builder.AppendLine("    body { margin: 28px; font-family: Segoe UI, Arial, sans-serif; color: #111827; background: #f8fafc; }")
    [void] $builder.AppendLine("    h1 { margin: 0 0 8px; font-size: 24px; }")
    [void] $builder.AppendLine("    p { margin: 0 0 20px; max-width: 920px; color: #475569; line-height: 1.45; }")
    [void] $builder.AppendLine("    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(520px, 1fr)); gap: 20px; align-items: start; }")
    [void] $builder.AppendLine("    figure { margin: 0; padding: 14px; border: 1px solid #cbd5e1; border-radius: 8px; background: #ffffff; }")
    [void] $builder.AppendLine("    h2 { margin: 0 0 12px; font-size: 16px; }")
    [void] $builder.AppendLine("    img { display: block; width: 100%; height: auto; border: 1px solid #e2e8f0; background: #ffffff; }")
    [void] $builder.AppendLine("    figcaption { margin-top: 10px; font-size: 13px; color: #475569; }")
    [void] $builder.AppendLine("    a { color: #0f766e; }")
    [void] $builder.AppendLine("  </style>")
    [void] $builder.AppendLine("</head>")
    [void] $builder.AppendLine("<body>")
    [void] $builder.AppendLine("  <h1>Quantum DebugViewportSnapshotV1 Gallery</h1>")
    [void] $builder.AppendLine("  <p>Generated technical debug previews from renderer-neutral backend snapshots. Each SVG includes a top-down X/Z panel and an elevation/profile panel for public-demo sanity checks. This is not a finished editor, viewer, or renderer.</p>")
    [void] $builder.AppendLine("  <div class=""grid"">")

    foreach ($preview in $Previews) {
        $label = ConvertTo-HtmlText -Value $preview.Label
        $snapshotFile = ConvertTo-HtmlText -Value ([System.IO.Path]::GetFileName($preview.SnapshotPath))
        $svgFile = ConvertTo-HtmlText -Value ([System.IO.Path]::GetFileName($preview.SvgPath))

        [void] $builder.AppendLine("    <figure>")
        [void] $builder.AppendLine("      <h2>$label</h2>")
        [void] $builder.AppendLine("      <a href=""$svgFile""><img src=""$svgFile"" alt=""$label SVG preview""></a>")
        [void] $builder.AppendLine("      <figcaption><a href=""$snapshotFile"">$snapshotFile</a> | <a href=""$svgFile"">$svgFile</a></figcaption>")
        [void] $builder.AppendLine("    </figure>")
    }

    [void] $builder.AppendLine("  </div>")
    [void] $builder.AppendLine("</body>")
    [void] $builder.AppendLine("</html>")

    $parentDirectory = Split-Path -Parent $OutputPath
    if (-not [string]::IsNullOrWhiteSpace($parentDirectory)) {
        New-Item -ItemType Directory -Force -Path $parentDirectory | Out-Null
    }

    [System.IO.File]::WriteAllText($OutputPath, $builder.ToString(), [System.Text.UTF8Encoding]::new($false))
}

$repoRoot = Get-RepoRoot
$artifactDirectory = Join-Path $repoRoot "artifacts\debug-viewport"
$builtInSnapshotPath = Join-Path $artifactDirectory "DebugViewportSnapshotV1.sample.json"
$builtInSvgPath = Join-Path $artifactDirectory "DebugViewportSnapshotV1.sample.svg"
$galleryPath = Join-Path $artifactDirectory "index.html"
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

    New-DebugViewportGallery -OutputPath $galleryPath -Previews $generatedPreviews

    Write-Host ""
    Write-Host "SUCCESS: Technical Preview 0.1 backend demo completed."
    Write-Host "Generated snapshots:"
    Write-Host "  $builtInSnapshotPath"
    foreach ($preview in $generatedPreviews | Select-Object -Skip 1) {
        Write-Host "  $($preview.SnapshotPath)"
    }

    Write-Host "Generated SVG previews:"
    Write-Host "  $builtInSvgPath"
    foreach ($preview in $generatedPreviews | Select-Object -Skip 1) {
        Write-Host "  $($preview.SvgPath)"
    }

    Write-Host "Generated gallery:"
    Write-Host "  $galleryPath"
}
finally {
    Pop-Location
}
