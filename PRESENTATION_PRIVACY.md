# Calendar presentation and Android privacy — October 15, 2026

Historical report: the [October 22 full-screen alarm update](FULL_SCREEN_ALARMS.md) supersedes the restrictions below for actively ringing audible alarms. Those alarms now request full-screen presentation with Dismiss/Snooze over the lock screen. Calendar content and non-ringing details still require unlock. APK/certificate details below describe the October 15 artifact, not the current build.

Implemented in the existing `main` checkout, starting at `2785a7afd27b91ffbb167618d27a72ca8cdb6934`. The saved scene, prefabs and permanent scene builder contain the same wiring; no manual setup is required.

## Calendar and Agenda

- A reusable clipboard/timesheet sprite replaces the worked-hours clock everywhere: calendar badges, preview, Day Details, month total/summary and hours editor. Its committed PNG and Unity import metadata live in `Assets/_UI/WorkedHoursTimesheet.*`; the existing artwork generator reproduces it. Alarm artwork is unchanged.
- Hours controls fit their labels, have subtle input surfaces and comfortable touch targets. Preview and Details show Add hours or the recorded value with a pencil; both open the existing date-aware editor. The monthly pill opens the existing entry review. Integer-hundredths storage, decimals, explicit zero, clearing, account/calendar isolation and calculation are unchanged. No hours are shared or uploaded.
- The top row, month navigation/total, compact family filter, Agenda shortcut and weekday headings have consistent spacing. All six calendar rows fit in normal and Edit modes. Timesheet badges keep a separate position above activity icons.
- Agenda retains the existing seven-day resolver, filters and Show all dates option. Grouped day cards pair the date with the shift and times. Compact activity rows retain assignment colors, icons, titles, timing and family names. Private alarm rows show an alarm icon, time and This device, and open their existing editor/details. Day headings open that calendar date. The small Event action opens the shared event editor. A data/filter/minute/delivery-state signature avoids recreating unchanged rows; theme changes repaint existing rows.

## Android privacy and alarm delivery

The manifest no longer declares `USE_FULL_SCREEN_INTENT`, `showWhenLocked` or `turnScreenOn`. Ringing notifications have no full-screen intent. Settings no longer request full-screen alarm access or treat its absence as an error.

`LockScreenPrivacy` clears legacy show/dismiss-keyguard/turn-screen-on flags, explicitly disables the API 27+ Activity properties and applies the policy throughout Activity lifecycle transitions. Unity installs the guard at startup and reapplies it on pause/resume/focus. Native alarm controls remain behind the system keyguard, clear their view on pause and render private details only when unlocked. There is no custom authentication or keyguard dismissal.

Notifications use generic text at all times, secret lock-screen visibility and the existing normal content tap. Generic content also protects installations whose older notification channels retain user-selected visibility. On API 31+ notification actions require system authentication; native action handlers also reject manual actions while locked. Tapping a notification cannot consume pending calendar navigation before unlock.

The existing AlarmManager scheduler, durable ledger, foreground ringing service, audio, vibration, sound/snooze preferences, account checks and occurrence identities are retained. Locking does not cancel delivery. After unlock, the native controls provide Dismiss, Snooze and Open Calendar. Open Calendar still dismisses only an actively ringing occurrence; future alarms and independent simultaneous alarms retain their state.

Android references: [Activity keyguard/display properties](https://developer.android.com/reference/android/app/Activity#setShowWhenLocked(boolean)), [authenticated notification actions](https://developer.android.com/reference/android/app/Notification.Action.Builder#setAuthenticationRequired(boolean)), [notification lock-screen visibility](https://developer.android.com/develop/ui/compose/notifications/create-notification).

## Changed files

- Native: `AlarmActivity.java`, `AlarmReceiver.java`, `NativeBridge.java`, new `LockScreenPrivacy.java`, Android library manifest, new `LockScreenPrivacyTest.java` and the API-specific Windows AtomicFile test shim.
- Runtime UI: `ContentPill.cs`, `AgendaDayCard.cs`, `FamilyWorkbench.cs`, `FamilyActivityRow.cs`, `DayPreview.cs`, `DayDetailsPopup.cs`, `CalendarController.cs`, `WorkedHoursEditor.cs`, `ScheduleWorkbench.cs` and `AppSession.cs`.
- Authoring: `ShiftCalSceneBuilder` partials, new Agenda partial, `Assets/Calendar.unity`, calendar/activity row prefabs, new Agenda card prefab and timesheet/navigation artwork with metadata.
- Verification: `PresentationChecks.cs`, `PresentationRuntimeChecks.cs`, existing focused/runtime check entry points and this report/README/handoff.

No authentication, Firestore schema/rules, recurrence definitions, worked-hours model/store, native ledger format, package name or signing key was changed. No production Firebase operation, uninstall or native-ledger reset was performed.

## Verification and remaining acceptance

Run `ShiftCalChecks.RebuildAndRun`, `ShiftCalRuntimeChecks.Run`, `Tools/AndroidChecks` native unit tests/lint, and `npm test` in `Tools/FirebaseChecks`. Logs, screenshots and the APK are local ignored artifacts.

The native suite passes 46 tests, including privacy/audio/vibration/keyguard/action/navigation checks on Android APIs 26, 33 and 35; Android lint has zero errors and three existing dependency-update warnings. The isolated `demo-shiftcal` Firestore emulator passes 53 assertions. Expected permission-denied logs belong to negative authorization tests. Final runs have no failed assertions.

Final Unity checks pass **1,786 focused assertions** (1,057 core/UI plus 729 family/presentation) and **460 play-mode assertions with zero errors**. No Canvas rebuild-loop warnings occurred. Rendered verification covers Graphite, Teal and Daylight at 360×640, 393×851 and 412×915. The presentation suite adds 117 captures to the existing 405, including blank/recorded hours, dense calendar, Edit mode, September with 720 actual hours, both preview states, grouped/empty/all-date/assigned/private-alarm Agenda, hours editor and monthly review. Real EventSystem pointer tests cover the pencil/pill, filter sheets, Agenda shortcut, day navigation, event/alarm detail routing and Add Event. Reviewed captures show separated header controls, all six rows, unobscured cell badges, grouped compact Agenda rows and one-line hours pills. Screenshot review caught a wrapped preview label; the width allowance and explicit single-line label now have regression assertions at every tested theme/size.

**Real Android lock-screen acceptance remains unverified:** no phone is attached and no configured AVD is available. Robolectric lifecycle/keyguard tests and the merged APK manifest audit cannot prove physical keyguard rendering, OEM power behavior or audible delivery on a sleeping phone. Install the update over the existing app using the unchanged certificate, then verify locked ringing, normal authentication on notification tap, unlock/resume, dismiss/snooze, simultaneous alarms and Open Calendar. Live Google sign-in/two-phone sync and reboot/battery-restriction acceptance also remain hardware tasks.

Final APK: `Logs/Android/ShiftCal-ui.apk`, non-development ARM64, 33,569,878 bytes. Package `com.abramnel.shiftcal`, version 1.1.0/code 3, minimum API 26, target API 36. The merged manifest audit found no show/turn-screen-on attributes or full-screen-intent permission on any Activity. APK Signature Scheme v2 verifies with the unchanged certificate SHA-256 `FA:DE:29:64:21:8D:28:30:94:82:69:04:A6:6D:92:97:F4:14:29:B4:1C:13:11:47:37:6B:5D:8A:1E:1A:77:F0`. APK SHA-256: `6FB129D2C10E5AA021C726AF0ADCD741610CAFB7D535E643F1B460705C798115`.
