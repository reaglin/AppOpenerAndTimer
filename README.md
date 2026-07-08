# App Opener and Timer

A simple .NET MAUI **Android** app. Pick apps from a checklist, press **Go**, and it
launches each one in turn — one launch per interval (default 60s) — while a per-app
timer records how long each app stays open. Your checklist is remembered between sessions.

## Features
- Checklist of all installed launchable apps, with the selection persisted (`Preferences`).
- **Go** starts a foreground service that launches the selected apps one interval apart.
- Each app gets a live timer; it **freezes when you close that app**, showing the
  launch-to-close duration. The service stops itself once every app is closed.
- Configurable seconds-between-launches; **Stop**, **Reset**, and **Apps** (rescan) buttons.

## Permissions
The app requests these at runtime — grant them for full functionality:

| Permission | Why |
|---|---|
| **Display over other apps** (`SYSTEM_ALERT_WINDOW`) | Android blocks background apps from launching activities; this grants the exception so the schedule can keep launching apps while this one is backgrounded. |
| **Usage access** (`PACKAGE_USAGE_STATS`) | Detects the foreground app so a timer stops the moment you close its app. Optional — without it, timers just keep counting from launch. |
| **Notifications** (`POST_NOTIFICATIONS`) | Foreground-service progress notification. |

## Build & run
Requires the .NET MAUI Android workload and an Android SDK.

```bash
# Build a signed debug APK
dotnet build -c Debug -f net9.0-android

# Build, install, and launch on a connected device
dotnet build -t:Run -f net9.0-android
```

Target framework `net9.0-android`, minimum Android 8.0 (API 26).

## Notes / limitations
"Closed" is detected as *the app leaving the foreground after being shown*. Because the
scheduler auto-launches the next app one interval later (which takes focus), an app you
haven't manually closed within its interval is marked closed when the next one launches —
correct for the intended open → use → close → next flow, but worth knowing if you let apps
stack in the background.
