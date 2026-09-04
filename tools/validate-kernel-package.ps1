param(
    [Parameter(Mandatory = $true)]
    [string]$ArchivePath
)

$ErrorActionPreference = "Stop"
$archive = (Resolve-Path $ArchivePath).Path
$temp = Join-Path ([IO.Path]::GetTempPath()) ("spatialviewer-threedm-validate-" + [Guid]::NewGuid().ToString("N"))

try {
    New-Item -ItemType Directory -Path $temp -Force | Out-Null
    Expand-Archive -Path $archive -DestinationPath $temp -Force

    $manifestPath = Join-Path $temp "threedmcore-release.json"
    if (-not (Test-Path $manifestPath)) {
        throw "Kernel package is missing threedmcore-release.json."
    }

    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.schemaVersion -ne 1) { throw "Unsupported manifest schema: $($manifest.schemaVersion)" }
    if ($manifest.runtime -ne "win-x64") { throw "Unexpected runtime: $($manifest.runtime)" }
    if ($manifest.framework -ne "net10.0") { throw "Unexpected framework: $($manifest.framework)" }
    if ($manifest.hostContract.name -ne "SpatialViewer.ThreeDmHost") { throw "Unexpected host contract." }
    if ($manifest.hostContract.apiVersion -ne 1) { throw "Unexpected host API version." }

    foreach ($property in $manifest.requiredAssemblies.PSObject.Properties) {
        $path = Join-Path $temp $property.Name
        if (-not (Test-Path $path)) { throw "Missing required assembly: $($property.Name)" }
        $actual = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne [string]$property.Value) {
            throw "SHA256 mismatch for required assembly: $($property.Name)"
        }
    }

    foreach ($file in $manifest.files) {
        $path = Join-Path $temp ([string]$file.path)
        if (-not (Test-Path $path)) { throw "Missing manifest file: $($file.path)" }
        $actualSize = (Get-Item $path).Length
        if ($actualSize -ne [long]$file.size) { throw "Size mismatch for $($file.path)" }
        $actualHash = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -ne [string]$file.sha256) { throw "SHA256 mismatch for $($file.path)" }
    }

    Write-Host "Validated kernel package: $archive"
}
finally {
    Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
}
