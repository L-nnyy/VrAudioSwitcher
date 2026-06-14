# Releasing

Releases are tag-driven and fully automated by `.github/workflows/release.yml`.

## Cut a release

1. Update `CHANGELOG.md` with the new version section.
2. Bump the fallback version (so local builds match):
   - `VrAudioSwitcher/VrAudioSwitcher.csproj` → `<Version>`, `<FileVersion>`, `<AssemblyVersion>`
   - `installer/VrAudioSwitcher.iss` → `MyAppVersion` fallback (optional; CI overrides it)
3. Commit, then tag and push:
   ```bash
   git commit -am "Release X.Y.Z"
   git tag -a vX.Y.Z -m "VR Audio Switcher X.Y.Z"
   git push origin main
   git push origin vX.Y.Z
   ```

Pushing the `vX.Y.Z` tag triggers the **Release** workflow, which:

- publishes the single-file portable exe (version stamped from the tag),
- installs Inno Setup and compiles `VrAudioSwitcher-Setup-X.Y.Z.exe`,
- generates `SHA256SUMS.txt`,
- creates a GitHub Release with those files and auto-generated notes.

## Local dry run

```powershell
pwsh installer\build-installer.ps1 -Version X.Y.Z
# -> publish\VrAudioSwitcher.exe  and  installer\dist\VrAudioSwitcher-Setup-X.Y.Z.exe
```

## Notes

- The version is taken from the git tag (`v` prefix stripped); the csproj/iss
  values are only fallbacks for local builds.
- The installer is unsigned. To avoid SmartScreen warnings, sign the exe and the
  setup with a code-signing certificate (add a signing step before the release
  step). Not currently configured.
