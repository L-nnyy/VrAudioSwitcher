# Changelog

## 1.0.1 — 2026-06-14

- Single instance: launching the app again opens the config window of the running
  instance instead of starting a second copy.
- Lighter background footprint: desktop-baseline tracking is now event-driven
  (`IMMNotificationClient`) instead of polling every second; `InvariantGlobalization`
  and other runtime feature switches; working-set trim after startup
  (~48 MB → ~10 MB reported memory); watcher poll relaxed to 2 s.
- Better Windows integration: embedded app icon, version/publisher metadata, and
  cooperation with the Task Manager Startup tab (StartupApproved flag).
- Config window: fixed hotkey capture; modern flat theme; sound on switch.

## 1.0.0

- Initial release: auto-apply audio profile on SteamVR start, restore desktop
  audio on quit; profiles with optional HMD auto-switch; global cycle hotkey;
  SteamVR dashboard overlay; tray app; per-user installer.
