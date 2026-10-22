# Full-screen wake-up alarms — October 22, 2026

Implemented directly on `main`, starting at `367e8246d65134a8af6b551f24f6abb7df3fa42d`, preserving the newer floating day preview and worked-hours navigation. This update supersedes October 15's restriction on ringing alarm presentation, following the request for system-clock-style wake-up controls. `HANDOFF.md` remains empty.

## Behavior and setup

An actively ringing audible alarm uses Android's full-screen notification intent to open the native alarm screen, wake the display and show Dismiss/Snooze above the keyguard. These controls act on that occurrence without requiring unlock. Other simultaneous alarms and future repeats remain intact. Ringing audio/vibration continue in the existing native foreground service while Unity is closed.

The ringing screen and notification use generic alarm text. Open Calendar requests normal system unlock before consuming navigation or dismissing that alarm. Ordinary app screens, upcoming notices and reminders cannot wake/show private calendar content above the keyguard. Stale or wrong-account intents cannot obtain ringing controls, and stale notification Snooze cannot resurrect a dismissed alarm. A displayed alarm closes when its live ringing state ends.

After installing the update over the existing app:

1. Open **Events & alarms → Alarm readiness**.
2. Allow **Notifications** and **Exact alarms** if needed.
3. Tap **Full-screen alarms** and enable ShiftCal's full-screen access on Android 14+. On earlier versions this button opens the existing audible channel's settings.
4. Check **Alarm notification settings** for the existing audible channel's high importance/pop-up setting. The app preserves that channel and user choices.
5. Use **Test alarm (15 seconds)**, leave the app normally and lock the phone. Confirm that the display wakes into native controls and Dismiss stops ringing without unlocking. Repeat with Snooze, and verify that Open Calendar still requires normal unlock.

Readiness reports full-screen permission and channel importance separately from scheduling/audio readiness. Denied full-screen access does not disable scheduled ringing. Android controls full-screen delivery: while the phone is in active use, the system may show a heads-up notification instead. Alarm volume, Do Not Disturb and OEM power restrictions remain Android settings.

## Implementation and compatibility

- Declares `USE_FULL_SCREEN_INTENT`, checks Android 14+ access and links to the appropriate system settings.
- Supplies an immutable, account/occurrence-scoped full-screen notification PendingIntent only for current audible ringing. No service directly launches a background Activity.
- Enables display/keyguard flags programmatically only while the native ledger confirms active ringing; never dismisses keyguard for Dismiss/Snooze. Alarm Activity uses a separate task excluded from Recents.
- Keeps the existing exact-alarm scheduler, 45-day queue, reboot restoration, data schema, ledger format, package ID and signing key.
- Wires the saved Unity scene and permanent builder to Full-screen alarms and Alarm notification settings. The expanded readiness text sizes to its content.

No production Firebase access/deployment, uninstall, data reset, new branch or signing-key replacement was performed.

## Validation

All **91 native tests** passed: 35 alarm presentation/privacy checks, 10 native alarm regressions, 18 recurrence checks and 28 image export/save checks. They cover APIs 26, 33, 34 and 35, including permission denial, notification intent/authentication, live-state restrictions, locked Dismiss/Snooze, simultaneous occurrences, stale actions, service audio/vibration/wake lock, external dismissal and unlock-gated calendar navigation. Android lint reports **zero errors** and three existing dependency-version warnings. Logs: `Logs/fullscreen-alarm-native.log`.

The existing focused render checks also needed two stale assertions aligned with the floating preview already present on main: its existing 108-unit action height and right-edge anchoring. No day-preview behavior was changed here. Readiness capture now includes expanded Android status text and checks both new settings buttons at all three phone sizes/themes.

Play-mode preview geometry uses a 0.001-unit allowance when measuring the existing 108-unit targets through world-space transforms. The Editor-only portrait test helper also now removes its temporary resolution using Unity's required total index, avoiding a cleanup error ([Unity reference source](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3/Editor/Mono/GameView/GameViewSizeGroup.cs)). Neither adjustment changes the shipped day preview.

The focused Unity suite passed **2,109 assertions** (1,116 core and 993 family), including saved-scene wiring and readiness text/button geometry across three themes at 360×640, 393×851 and 412×915. Logs: `Logs/fullscreen-alarm-focused.log`; renders: `Logs/UI/QualityUpdate/*-Readiness-*.png`.

The final play-mode run passed **678 assertions with zero errors**, including real pointer interactions and the existing floating preview on every week row. Log: `Logs/fullscreen-alarm-runtime.log`. No final suite failed.

## Verified APK

Built `Logs/Android/ShiftCal-ui.apk`: non-development ARM64, **33,706,934 bytes**, package `com.abramnel.shiftcal`, version 1.1.0/code 3, minimum API 26 and target API 36. SHA-256: `F6F2EC454B7084D8C2EFE193531412843D3A0028963E921A2ED37F57CA2D34BE`.

APK signature verification passed with the existing, user-confirmed certificate SHA-256 `A4:C9:C4:97:18:F8:AF:4D:97:48:8C:43:E3:2B:E2:9C:80:8D:C7:14:20:9E:13:3E:86:74:54:D8:D9:58:1D:17`. The keystore and project signing settings are unchanged. Packaged-manifest inspection confirms `USE_FULL_SCREEN_INTENT`, the native alarm Activity and no unconditional keyguard/display attributes. All **13 generated native Java sources** match the checked sources byte for byte. Verification: `Logs/Android/fullscreen-alarm-apk-verification.txt`; build: `Logs/fullscreen-alarm-android-build.log`. These artifacts remain excluded from Git.

## Physical-phone acceptance

No Android phone is attached (`adb devices -l` returns no devices). Automated native lifecycle/keyguard tests cannot prove a physical phone's keyguard rendering, screen wake or OEM power behavior. The locked 15-second test above remains required on the user's phone. Also test two simultaneous alarms, snooze returning to full-screen, a real future wake-up alarm, reboot restoration and an in-place update preserving existing data.

Android references: [time-sensitive/full-screen notifications](https://developer.android.com/develop/ui/views/notifications/time-sensitive), [Android 14 full-screen access](https://developer.android.com/about/versions/14/behavior-changes-14#secure-fsi-notifications), and [Activity lock-screen presentation](https://developer.android.com/reference/android/app/Activity#setShowWhenLocked(boolean)).
