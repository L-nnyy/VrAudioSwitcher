# First push to GitHub

Everything (CI, release pipeline, templates) is already committed. These are the
one-time steps to put the repo on GitHub and trigger the pipelines.

Replace `OWNER` with your GitHub username/org.

## 1. Install + authenticate gh

```powershell
winget install GitHub.cli
gh auth login    # follow the browser prompt
```

## 2. Create the repo and push

From the repo root (`VrAudioSwitcher/`):

```powershell
# Creates the GitHub repo, sets it as 'origin', and pushes the current branch.
gh repo create VrAudioSwitcher --public --source . --remote origin --push
```

That push runs the **CI** workflow on `main`.

## 3. Push the existing tag to publish 1.0.1

```powershell
git push origin v1.0.1
```

This triggers the **Release** workflow → builds the installer + portable exe and
creates the GitHub Release automatically. No manual upload needed.

## 4. (Optional) Repo polish

- Set the repo description and topics:
  ```powershell
  gh repo edit l-nnyy/VrAudioSwitcher `
    --description "Keep default audio devices sane around SteamVR" `
    --add-topic steamvr --add-topic openvr --add-topic audio --add-topic windows --add-topic vr
  ```
- Enable branch protection on `main` (require CI to pass) in repo settings.

## Future releases

See `docs/RELEASING.md` — bump version, tag `vX.Y.Z`, push the tag.
