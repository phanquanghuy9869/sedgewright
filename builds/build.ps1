<#
.SYNOPSIS
Builds a self-contained Sedgewright Velopack release for Windows.

.DESCRIPTION
Uses an installed .NET 10+ SDK or installs .NET 11 into the current user's home directory.
Installs the matching vpk CLI if it is missing, publishes the app, and packs a Velopack release.

.PARAMETER Version
Release version in semver format. Defaults to 0.0.1.

.PARAMETER RuntimeIdentifier
Target runtime identifier. Defaults to the host architecture (win-x64 or win-arm64).

.EXAMPLE
powershell -ExecutionPolicy Bypass -File builds/build.ps1 -Version 1.2.0
#>
[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+([-+][0-9A-Za-z.+-]+)?$')]
    [string]$Version = '0.0.1',

    [ValidateSet('win-x64', 'win-arm64')]
    [string]$RuntimeIdentifier
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$PackId = 'Sedgewright'
$PackTitle = 'Sedgewright'
$MainExe = 'Sedgewright.exe'
$VelopackVersion = '1.2.161'
$DotNetChannel = '11.0'
$MinimumSdkMajor = 10

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'src\Sedgewright\Sedgewright.csproj'
$iconPath = Join-Path $repoRoot 'src\Sedgewright\Assets\app-icon.ico'

if (-not [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
    throw 'build.ps1 must run on Windows. Use build-macos.sh or build-linux on the corresponding host.'
}

if (-not $RuntimeIdentifier) {
    if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq [System.Runtime.InteropServices.Architecture]::Arm64) {
        $RuntimeIdentifier = 'win-arm64'
    }
    else {
        $RuntimeIdentifier = 'win-x64'
    }
}

$publishDir = Join-Path $repoRoot "artifacts\publish\$RuntimeIdentifier"
$releaseDir = Join-Path $repoRoot "artifacts\releases\$RuntimeIdentifier"

function Write-Step([string]$Message) {
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Test-SdkAvailable {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        return $false
    }

    $sdks = & dotnet --list-sdks 2>$null
    if ($LASTEXITCODE -ne 0) {
        return $false
    }

    foreach ($sdk in $sdks) {
        if ($sdk -match '^(\d+)\.' -and [int]$Matches[1] -ge $MinimumSdkMajor) {
            return $true
        }
    }

    return $false
}

function Install-DotNetSdk {
    $installDir = Join-Path $HOME '.dotnet'
    $installScript = Join-Path ([System.IO.Path]::GetTempPath()) "dotnet-install-$([guid]::NewGuid()).ps1"

    Write-Step "Installing .NET SDK $DotNetChannel to $installDir"
    try {
        Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installScript -UseBasicParsing
        & $installScript -Channel $DotNetChannel -InstallDir $installDir -NoPath
    }
    finally {
        Remove-Item -Path $installScript -Force -ErrorAction SilentlyContinue
    }

    $env:DOTNET_ROOT = $installDir
    $env:PATH = "$installDir;$env:PATH"
}

function Install-Vpk {
    $toolsDir = Join-Path $HOME '.dotnet\tools'
    $env:PATH = "$toolsDir;$env:PATH"

    $globalTools = & dotnet tool list --global
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to list globally installed .NET tools.'
    }

    $vpkEntry = $globalTools | Where-Object { $_ -match '^\s*vpk\s+' } | Select-Object -First 1
    if ($vpkEntry -and $vpkEntry -match '^\s*vpk\s+(\S+)') {
        if ($Matches[1] -eq $VelopackVersion) {
            return
        }

        Write-Step "Updating vpk to $VelopackVersion to match the Velopack package"
        & dotnet tool update --global vpk --version $VelopackVersion
    }
    else {
        Write-Step "Installing vpk $VelopackVersion"
        & dotnet tool install --global vpk --version $VelopackVersion
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Installing or updating vpk failed with exit code $LASTEXITCODE."
    }
    if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
        throw 'vpk is not available on PATH after installation.'
    }
}

if (-not (Test-Path $projectPath)) {
    throw "Sedgewright project was not found at $projectPath."
}
if (-not (Test-Path $iconPath)) {
    throw "Windows app icon was not found at $iconPath."
}

Write-Step "Checking .NET SDK (requires $MinimumSdkMajor or newer)"
if (-not (Test-SdkAvailable)) {
    Install-DotNetSdk
    if (-not (Test-SdkAvailable)) {
        throw ".NET SDK $MinimumSdkMajor or newer is still unavailable after installation."
    }
}

Install-Vpk

foreach ($dir in @($publishDir, $releaseDir)) {
    if (Test-Path $dir) {
        Remove-Item -Path $dir -Recurse -Force
    }
}

Write-Step "Publishing $PackId $Version for $RuntimeIdentifier"
& dotnet publish $projectPath -c Release -r $RuntimeIdentifier --self-contained true "-p:Version=$Version" -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Write-Step "Packing Velopack release into $releaseDir"
& vpk pack --packId $PackId --packVersion $Version --packDir $publishDir --mainExe $MainExe --packTitle $PackTitle --icon $iconPath --runtime $RuntimeIdentifier --outputDir $releaseDir
if ($LASTEXITCODE -ne 0) {
    throw "vpk pack failed with exit code $LASTEXITCODE."
}

$releaseIndex = Get-ChildItem -Path $releaseDir -Filter 'releases.*.json' -ErrorAction SilentlyContinue
$fullPackage = Get-ChildItem -Path $releaseDir -Filter "$PackId-$Version*-full.nupkg" -ErrorAction SilentlyContinue
$setup = Get-ChildItem -Path $releaseDir -Filter '*Setup.exe' -ErrorAction SilentlyContinue
if (-not $releaseIndex -or -not $fullPackage -or -not $setup) {
    throw "Velopack output in $releaseDir is incomplete. Expected releases.*.json, a $PackId $Version full package, and a Setup.exe."
}

Write-Step 'Release files:'
Get-ChildItem -Path $releaseDir | ForEach-Object { Write-Host "  $($_.Name)" }
