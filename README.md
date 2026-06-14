# VR Audio Switcher

[![CI](https://github.com/l-nnyy/VrAudioSwitcher/actions/workflows/ci.yml/badge.svg)](https://github.com/l-nnyy/VrAudioSwitcher/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/l-nnyy/VrAudioSwitcher?sort=semver)](https://github.com/l-nnyy/VrAudioSwitcher/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Lightweight Windows tray app that keeps your default audio devices sane around
SteamVR. When SteamVR starts it applies an audio profile (output **and**
microphone); when SteamVR closes it **restores your exact desktop audio state**.
No more hunting through Sound settings after every VR session.

**Download:** grab the latest installer or portable exe from the
[Releases page](https://github.com/l-nnyy/VrAudioSwitcher/releases/latest).

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
- Single ~1 MB exe, no installer required.

## Requirements

- Windows 10/11 (x64)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
  (the installer will prompt if missing)
- SteamVR

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
   switch profiles without taking off the headset.

Config lives at `%APPDATA%\VrAudioSwitcher\config.json`.

### Diagnostic

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

## License

MIT — see [LICENSE](LICENSE).
