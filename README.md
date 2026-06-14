# VR Audio Switcher

<!-- After pushing to GitHub, replace OWNER with your username/org. -->
[![CI](https://github.com/OWNER/VrAudioSwitcher/actions/workflows/ci.yml/badge.svg)](https://github.com/OWNER/VrAudioSwitcher/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/OWNER/VrAudioSwitcher?sort=semver)](https://github.com/OWNER/VrAudioSwitcher/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Lightweight Windows tray app that keeps your default audio devices sane around
SteamVR. When SteamVR starts it applies an audio profile (output **and**
microphone); when SteamVR closes it **restores your exact desktop audio state**.
No more hunting through Sound settings after every VR session.

Inspired by ovrAdvancedSettings / OVR Toolkit — runs in the background, configured
once.

**Download:** grab the latest installer or portable exe from the
[Releases page](https://github.com/OWNER/VrAudioSwitcher/releases/latest).

## Features

- **Auto apply on SteamVR start, auto restore on quit.** The desktop baseline is
  snapshotted *continuously while VR is off*, so it captures your real defaults
  before SteamVR hijacks them — and puts them all back on exit.
- **Output + microphone, all three Windows roles** (Console / Multimedia /
  Communications).
- **Profiles** for different headsets / setups. Optionally bind a profile to an
  HMD model and have it auto-select when that headset is detected; otherwise the
  last used profile is kept.
- **Global hotkey** to cycle profiles.
- **SteamVR dashboard overlay** to switch profiles from inside the headset.
- **Launch at Windows startup** (per-user, optional).
- Single ~1 MB exe, no installer.

## How it works

- Connects to OpenVR as a *Background* app, so it never launches SteamVR itself.
  It polls for the runtime, reads the connected HMD model, and listens for
  `VREvent_Quit`.
- Audio is controlled directly via the Windows Core Audio API
  (`IMMDeviceEnumerator` to enumerate/read, `IPolicyConfig::SetDefaultEndpoint`
  to set defaults) — no third-party audio dependency.
- The overlay is a dashboard overlay drawn to a CPU bitmap and pushed via
  `SetOverlayRaw`; laser clicks are read from overlay mouse events.

## Requirements

- Windows 10/11 (x64)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
  (framework-dependent build)
- SteamVR

## Build

```powershell
dotnet build VrAudioSwitcher.sln -c Debug
```

## Publish (single file)

```powershell
dotnet publish VrAudioSwitcher/VrAudioSwitcher.csproj -c Release -r win-x64 `
  --self-contained false -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

## Installer

A clean per-user installer (Inno Setup) is scaffolded under `installer/`:

```powershell
winget install JRSoftware.InnoSetup   # one-time
pwsh installer\build-installer.ps1     # publishes + builds installer\dist\VrAudioSwitcher-Setup-1.0.1.exe
```

The installer is per-user (no admin), offers desktop/startup shortcuts, and checks
for the .NET 8 Desktop Runtime (pointing to the download if missing).

## Usage

1. Run `VrAudioSwitcher.exe`. A tray icon appears.
2. Right-click → **Configuration…**
   - Create a profile, pick output + microphone.
   - Optional: with SteamVR running, click **Use current** to bind the profile to
     your HMD and tick **Auto-switch**.
   - Optional: set a **cycle hotkey** and **Launch at startup**.
3. Start SteamVR → the profile is applied. Close SteamVR → desktop audio is
   restored.
4. In VR, open the SteamVR dashboard → the VR Audio Switcher overlay lets you
   switch profiles.

Config lives at `%APPDATA%\VrAudioSwitcher\config.json`.

### Hidden diagnostic

```powershell
VrAudioSwitcher.exe --list-audio
```

Dumps all endpoints + current defaults to stdout.

## Notes / limitations

- `IPolicyConfig` is undocumented (but is what the Windows Sound control panel
  uses). Stable across current Windows; could change in a future release.
- If the app is started *after* SteamVR is already running, it can't know the
  pre-VR desktop state and will snapshot whatever is current as the baseline.
- Profile editing is desktop-only; the VR overlay is switch-only by design.

## Contributing & releasing

- CI builds every push/PR (`.github/workflows/ci.yml`).
- Releases are tag-driven — see [docs/RELEASING.md](docs/RELEASING.md).
- First-time GitHub setup — see [docs/GITHUB_SETUP.md](docs/GITHUB_SETUP.md).

## License

MIT — see [LICENSE](LICENSE).
