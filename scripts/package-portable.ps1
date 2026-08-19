param(
    [string]$RuntimeIdentifier = "win-x64",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$appProject = Join-Path $repoRoot "src/MicroKeyStudio.App/MicroKeyStudio.App.csproj"
$version = (Get-Content (Join-Path $repoRoot "VERSION") -Raw).Trim()
$releaseDate = Get-Date -Format "yyyy-MM-dd"
$publishName = "MicroKeyStudio-v$version-$releaseDate-$RuntimeIdentifier-portable"
$publishDir = Join-Path $repoRoot "artifacts/publish/$publishName"
$releaseDir = Join-Path $repoRoot "artifacts/releases"
$zipPath = Join-Path $releaseDir "$publishName.zip"

if (Test-Path $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

dotnet publish $appProject `
    --configuration $Configuration `
    --runtime $RuntimeIdentifier `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:Version=$version `
    -p:InformationalVersion=$version `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    --output $publishDir

Copy-Item -LiteralPath (Join-Path $repoRoot "README.md") -Destination $publishDir
Copy-Item -LiteralPath (Join-Path $repoRoot "README.en.md") -Destination $publishDir
Copy-Item -LiteralPath (Join-Path $repoRoot "README.ko.md") -Destination $publishDir
Copy-Item -LiteralPath (Join-Path $repoRoot "CHANGELOG.md") -Destination $publishDir
Copy-Item -LiteralPath (Join-Path $repoRoot "CHANGELOG.ko.md") -Destination $publishDir
Copy-Item -LiteralPath (Join-Path $repoRoot "docs/asset-sources.md") -Destination $publishDir
Copy-Item -LiteralPath (Join-Path $repoRoot "docs/asset-sources.ko.md") -Destination $publishDir
Copy-Item -LiteralPath (Join-Path $repoRoot "docs/feature-status.md") -Destination $publishDir
Copy-Item -LiteralPath (Join-Path $repoRoot "docs/feature-status.ko.md") -Destination $publishDir
Copy-Item -LiteralPath (Join-Path $repoRoot "VERSION") -Destination $publishDir

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

Write-Host "Portable folder: $publishDir"
Write-Host "Release zip: $zipPath"
