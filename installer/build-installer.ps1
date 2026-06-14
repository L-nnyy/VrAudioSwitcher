# Publishes the app (single-file, framework-dependent) and compiles the installer.
# Requires Inno Setup 6:  winget install JRSoftware.InnoSetup
#
# Usage:  pwsh installer\build-installer.ps1   (run from the repo root)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

Write-Host "==> Publishing single-file build..." -ForegroundColor Cyan
dotnet publish "$root\VrAudioSwitcher\VrAudioSwitcher.csproj" -c Release -r win-x64 `
    --self-contained false -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true -o "$root\publish"

# Locate the Inno Setup compiler.
$iscc = (Get-Command iscc -ErrorAction SilentlyContinue).Source
if (-not $iscc) {
    $candidate = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    if (Test-Path $candidate) { $iscc = $candidate }
}
if (-not $iscc) {
    throw "Inno Setup compiler (ISCC.exe) not found. Install it: winget install JRSoftware.InnoSetup"
}

Write-Host "==> Compiling installer..." -ForegroundColor Cyan
& $iscc "$root\installer\VrAudioSwitcher.iss"

Write-Host "==> Done. Installer is in installer\dist\" -ForegroundColor Green
