# Privacy Policy — App Opener and Timer

**Effective date:** 8 July 2026
**Developer:** Ron Eaglin
**Contact:** ron.eaglin@gmail.com

## Summary

App Opener and Timer does **not** collect, store off your device, transmit, sell,
or share any personal information. There are no accounts, no analytics, no
advertising, and no third‑party SDKs. Everything the app does happens locally on
your device.

## Information the app handles

The app works entirely on your device. The only information it keeps is the
**app lists you create** — the names you give them, the apps you select, and the
launch interval. This is saved in the app's local storage on your device so your
lists are remembered between sessions. It is never uploaded anywhere and is
removed if you delete a list or uninstall the app.

The app does **not** collect your name, email, location, contacts, files, or any
advertising identifiers.

## Permissions and why they are used

- **See the list of installed apps** (via the Android `<queries>` launcher
  declaration): used only to show you a checklist of apps you can select and to
  start the ones you pick. The app reads which apps are launchable; it does not
  read their contents or send this list anywhere.
- **Display over other apps** (`SYSTEM_ALERT_WINDOW`): required so the app can
  launch the next scheduled app while it is running in the background. Android
  blocks background apps from starting other apps without this permission.
- **Usage access** (`PACKAGE_USAGE_STATS`): used only to detect which app is
  currently on screen, so a timer can stop when you close its app. This
  information is read in real time and used only on your device to update the
  timers. It is **never stored, logged, or transmitted**. This permission is
  optional — if you don't grant it, the timers simply keep counting from launch.
- **Run a foreground service** and **post notifications**: used to keep the
  launch schedule running and to show a progress notification while the app is
  launching your selected apps.

## Data sharing

None. No data is shared with the developer or any third party, because no data
leaves your device.

## Data security

Because all information stays on your device, it is protected by your device's
own security. Uninstalling the app removes the app lists it stored.

## Children's privacy

The app is not directed at children and does not knowingly collect any
information from anyone, including children under 13.

## Changes to this policy

If this policy changes, the updated version will be posted at this same location
with a new effective date.

## Contact

Questions about this policy? Email **ron.eaglin@gmail.com**.
