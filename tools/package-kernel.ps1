param(
    [string]$Configuration = "Release",
    [string]$OutputDirectory = "artifacts/kernel",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not $SkipBuild) {
    dotnet restore SpatialViewer.3DMCore.sln
    dotnet build SpatialViewer.3DMCore.sln -c $Configuration --no-restore
}

[xml]$props = Get-Content (Join-Path $root "Directory.Build.props")
$version = [string]$props.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Unable to resolve kernel version from Directory.Build.props."
}

$outputRoot = Join-Path $root $OutputDirectory
$stage = Join-Path $outputRoot "ThreeDmCore-v$version-x64"
$zip = Join-Path $outputRoot "ThreeDmCore-v$version-x64.zip"
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $zip -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $stage -Force | Out-Null

$sourceDirectories = @(
    "src/SpatialViewer.Formats.ThreeDm.Rhino3dm/bin/$Configuration/net10.0",
    "src/SpatialViewer.ThreeDm.Integration/bin/$Configuration/net10.0",
    "src/SpatialViewer.ThreeDm.Rendering.Windows/bin/$Configuration/net10.0"
)

foreach ($relative in $sourceDirectories) {
    $source = Join-Path $root $relative
    if (-not (Test-Path $source)) {
        throw "Required build output directory is missing: $relative"
    }

    Copy-Item (Join-Path $source "*") $stage -Recurse -Force
}

$requiredAssemblies = @(
    "SpatialViewer.ThreeDm.Core.dll",
    "SpatialViewer.Formats.ThreeDm.dll",
    "SpatialViewer.Formats.ThreeDm.Rhino3dm.dll",
    "SpatialViewer.ThreeDm.Rendering.dll",
    "SpatialViewer.ThreeDm.Rendering.Windows.dll",
    "SpatialViewer.ThreeDm.Integration.dll"
)

$assemblyHashes = [ordered]@{}
foreach ($assembly in $requiredAssemblies) {
    $path = Join-Path $stage $assembly
    if (-not (Test-Path $path)) {
        throw "Required kernel assembly is missing from package: $assembly"
    }

    $assemblyHashes[$assembly] = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
}

$files = @()
Get-ChildItem $stage -File -Recurse | Sort-Object FullName | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($stage, $_.FullName).Replace("\", "/")
    $files += [ordered]@{
        path = $relative
        sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        size = $_.Length
    }
}

$manifest = [ordered]@{
    schemaVersion = 1
    version = $version
    runtime = "win-x64"
    framework = "net10.0"
    sourceRepository = "KiYouJyo/SpatialViewer.3DMCore"
    hostContract = [ordered]@{
        name = "SpatialViewer.ThreeDmHost"
        apiVersion = 1
        contractVersion = "1.0.0"
        minimumHostVersion = "1.0.0"
        maximumHostVersionExclusive = "2.0.0"
    }
    requiredAssemblies = $assemblyHashes
    files = $files
}

$manifestPath = Join-Path $stage "threedmcore-release.json"
$manifest | ConvertTo-Json -Depth 8 | Set-Content $manifestPath -Encoding utf8

Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip -CompressionLevel Optimal
Write-Host "Created $zip"
