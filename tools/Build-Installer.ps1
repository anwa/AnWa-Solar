<#
.SYNOPSIS
  Builds the AnWa-Solar MSI.

.DESCRIPTION
  1. Publishes the WPF application self-contained for win-x64 into dist\publish.
  2. Builds the WiX 5 installer into dist\installer\AnWaSolar-Setup-<version>.msi.
  3. Writes the SHA-256 of the MSI next to it.

  The version comes from Directory.Build.props. Run from any folder:

    pwsh tools/Build-Installer.ps1
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    # Replace an existing MSI of the same version in dist\installer (normally a version is built once).
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$props = [xml](Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw)
$version = $props.SelectSingleNode('/Project/PropertyGroup/Version')?.InnerText
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Invalid or missing <Version> in Directory.Build.props: '$version'"
}

$publishDir = Join-Path $root 'dist\publish'
$installerDir = Join-Path $root 'dist\installer'
$appProject = Join-Path $root 'src\AnWaSolar.App\AnWaSolar.App.csproj'
$wixProject = Join-Path $root 'installer\AnWaSolar.Installer\AnWaSolar.Installer.wixproj'
$msiName = "AnWaSolar-Setup-$version.msi"
$msiPath = Join-Path $installerDir $msiName

Write-Host "AnWa-Solar $version" -ForegroundColor Cyan

# Never overwrite a built MSI by accident: a release MSI is recorded with its SHA-256 in docs\DEPLOYMENT.md.
if ((Test-Path -LiteralPath $msiPath) -and -not $Force) {
    throw "$msiPath already exists. Raise <Version> in Directory.Build.props for a new build, or use -Force to replace it."
}

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

Write-Host 'Publishing application (self-contained, win-x64)...'
dotnet publish $appProject -c $Configuration -r win-x64 --self-contained true -p:DebugType=none -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)." }

foreach ($required in 'AnWa-Solar.exe', 'appsettings.json', 'Data\Modules.json', 'Data\Inverters.json', 'Data\Batteries.json', 'Data\CableTables.json') {
    if (-not (Test-Path -LiteralPath (Join-Path $publishDir $required))) {
        throw "The publish output is missing $required."
    }
}

Write-Host 'Building MSI (WiX 5)...'
$wixOut = Join-Path $root 'dist\wix'
dotnet build $wixProject -c $Configuration "-p:PublishDir=$publishDir\" -o $wixOut
if ($LASTEXITCODE -ne 0) { throw "WiX build failed ($LASTEXITCODE)." }

New-Item -ItemType Directory -Force -Path $installerDir | Out-Null
# The project builds one culture (en-US); WiX puts the MSI into a culture subfolder.
$builtMsi = Join-Path $wixOut "en-US\$msiName"
Copy-Item -LiteralPath $builtMsi -Destination $msiPath -Force
$sha256 = (Get-FileHash -LiteralPath $msiPath -Algorithm SHA256).Hash
Set-Content -LiteralPath "$msiPath.sha256" -Value "$sha256  $msiName" -Encoding ascii

Write-Host "MSI:     $msiPath"
Write-Host "SHA-256: $sha256"
