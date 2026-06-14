# Publishes the app (single-file, framework-dependent) and compiles the installer.
# Requires Inno Setup 6:  winget install JRSoftware.InnoSetup
#
# Usage:
#   pwsh installer\build-installer.ps1                 # uses csproj default version
#   pwsh installer\build-installer.ps1 -Version 1.0.1  # stamps exe + installer with this version

param(
    [string]$Version = ""
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

Write-Host "==> Publishing single-file build..." -ForegroundColor Cyan
$publishArgs = @(
    "$root\VrAudioSwitcher\VrAudioSwitcher.csproj",
    '-c', 'Release', '-r', 'win-x64',
    '--self-contained', 'false',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-o', "$root\publish"
)
if ($Version) {
    $publishArgs += "-p:Version=$Version"
    $publishArgs += "-p:FileVersion=$Version.0"
    $publishArgs += "-p:AssemblyVersion=$Version.0"
}
dotnet publish @publishArgs

# Locate the Inno Setup compiler (PATH, machine install, or per-user install).
$iscc = (Get-Command iscc -ErrorAction SilentlyContinue).Source
if (-not $iscc) {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    $iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $iscc) {
    throw "Inno Setup compiler (ISCC.exe) not found. Install it: winget install JRSoftware.InnoSetup"
}

Write-Host "==> Compiling installer..." -ForegroundColor Cyan
$isccArgs = @()
if ($Version) { $isccArgs += "/DMyAppVersion=$Version" }
$isccArgs += "$root\installer\VrAudioSwitcher.iss"
& $iscc @isccArgs

Write-Host "==> Done. Installer is in installer\dist\" -ForegroundColor Green
