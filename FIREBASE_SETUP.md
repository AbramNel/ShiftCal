See [FAMILY_CALENDAR.md](FAMILY_CALENDAR.md) for the family migration, updated deployment procedure, and device-only alarm boundary.

# ShiftCal Setup

## Run locally now

Open this project in Unity 6000.3.0f1, open Assets/Calendar.unity, press Play and choose "Use on this device". Calendar, notes, shifts, events and rule editors work without Firebase credentials. Native ringing is Android-only; the Editor does not simulate a successful Google login or claim to run Android alarms.

The scene and prefab templates are saved assets. Shift and agenda rows instantiate these editable templates as needed, with scrolling. Use ShiftCal > Run Focused Checks to verify the current scene. ShiftCal > Build Permanent Calendar App regenerates the scene: do not run that menu after making manual scene edits unless you intend to regenerate it.

## Values you must supply

The established Android package is **com.abramnel.shiftcal**. Preserve this identifier and the existing signing configuration when updating an installed app.

1. Select your Firebase project and record its actual project ID.
2. Add a new Android app for ShiftCal using the package above. Do not reuse another app's registration/configuration.
3. Add the signing SHA-1 and SHA-256 fingerprints to this Android registration.
4. Enable Authentication > Sign-in method > Google, including the support email and any OAuth consent/test-user requirements.
5. Download the updated ShiftCal google-services.json and put it at Assets/google-services.json.
6. Find the OAuth **Web/server client ID**, ending in .apps.googleusercontent.com. Put it in Assets/Resources/ShiftCalConfig.asset > Web Client Id. Do not use the Android OAuth client ID.
7. Create the Firestore database. Review and deploy the ShiftCal rules described below.
8. In Unity, run Assets > External Dependency Manager > Android Resolver > Force Resolve after configuration. Firebase Auth, Firestore, App and EDM4U are already installed from official local package archives; do not import duplicate copies.

Existing debug key: C:/Users/simyr/.android/debug.keystore, alias androiddebugkey.
SHA-1: 1C:15:7E:8A:18:C6:56:7E:82:E9:B8:F5:EB:C5:05:38:0C:17:7B:76
SHA-256: A4:C9:C4:97:18:F8:AF:4D:97:48:8C:43:E3:2B:E2:9C:80:8D:C7:14:20:9E:13:3E:86:74:54:D8:D9:58:1D:17

These fingerprints were rechecked against the existing keystore and the built APK. The key itself and the project's signing settings were not changed.

No release key was supplied or created. Before updating an existing installed app, verify its original package and signing certificate. Use that original keystore for an update. Do not uninstall to work around a signing mismatch. APK verification output is local under Logs/Android and is excluded from Git. No physical-phone installation has been performed.

## Dedicated Firestore namespace

All new cloud data uses /shiftcal/v1. The old firestore.rules and firebase.json are preserved. The implemented schema is in firestore.shiftcal.rules. If the Firebase project serves other apps, merge ONLY the ShiftCal match block and its helper functions into the existing deployed rules, retaining other apps' rules. Do not deploy the old starter rules or replace the whole shared project's rules with the standalone ShiftCal file.

Personal calendar: /shiftcal/v1/users/{uid}/calendar/{record}.
Legacy alarm backups only: /shiftcal/v1/users/{uid}/records/{record}. The updated app never listens to or writes this collection; wake rules and subscriptions are stored only on this device.
Shared calendar: /shiftcal/v1/groups/{group}/records/{record}.
Membership and invitations: the group's members and invites subcollections.

Records have key, json, deleted, revision and author fields, explicitly serialized as Firestore dictionaries. Each shift, override, event, exception, activity, activity exception, family profile, shared activity template and rotation is a separate record. Alarm definitions and device templates are excluded. Transactions compare revisions. Deletions remain tombstones. Conflicts keep both versions and require a Keep mine / Use remote choice in Settings > Data & Sharing > Shared Groups.

Owners manage group definitions, rotation and membership. Members can edit shared date overrides, calendar events, activities, assignment exceptions and shared templates. Only the owner manages family profiles. Anonymous authentication cannot write. Invitations are bound to a verified Google email and expire after seven days. The owner cannot remove themselves or leave their owned group; ownership transfer is not implemented. Other members can leave. Removed members cannot sync new shared changes; previously cached offline calendar copies are not remotely erased.

Cloud calendars remain accessible offline after sign-in. Switching/sign-out detaches listeners and cancels the previous account's native alarms. Existing local data imports only when you explicitly choose the import button. Group switches retain archive files. Private preferences cannot be read or changed by another UID.

## Recurrence and alarms

Work rotations use device-local shift start times and date overrides. New family activities preserve their calendar zone (America/Chicago by default); legacy event editors use "device" time; existing IANA zones, UTC and optional end times are preserved when saving the compact form. Monthly events clamp to the last day when the original day is unavailable. DST gaps advance by the transition length; repeated clock times use the earlier instant. End dates are inclusive; counts include canceled occurrences in the original series. Legacy event future edits retain their existing split behavior. Family activity future edits transfer future exceptions to the split series and adjust remaining occurrence counts.

Calendar cancellation affects the shared event. Pause on phone and Skip this affect this device only. Separate event reminders and audible alarms have distinct occurrence identities. Shift-linked events reuse ShiftAlarmRule and its stable ID, without generating duplicate EventSeries records. Relative timing subtracts real elapsed minutes from the resolved shift start and can deliver on the previous day; fixed timing uses the matching shift date's local clock time. Calendar indicators stay on the qualifying shift date. OFF or different-shift overrides remove the original rule's delivery; restoring the shift reconciles it without reviving explicitly dismissed/skipped occurrences. Untimed shifts support fixed-time rules; relative rules require a start time. Alarm OFF on a new linked event is calendar-only, while old silent-notification rules retain their behavior.

ScheduleSave remains version 3. Missing new model fields default to the original relative timing and explicit alarm settings. New timing, notes and calendar-only fields round-trip through the existing JSON Firestore records. Per-occurrence shift delivery changes and the skip/dismiss/snooze ledger stay in native account-local storage; they do not change the shared definition.

Alarm Preferences is device-local and readable by Android while Unity is closed. The global upcoming-notice toggle and duration control future notices for all audible alarms. Existing explicit sound/vibration/snooze settings take precedence; definitions marked Use device defaults inherit those three defaults. Changing preferences reconciles native deliveries without rewriting calendar records. Disabling notices cancels pending notices while keeping the real alarms. A durable notice ledger suppresses duplicate notices, and notices whose lead time has already elapsed (including the 15-second Test alarm) are not emitted retroactively.

Android owns a durable device delivery ledger in its private AtomicFile. Unity sends version-3 ScheduleSave definitions through NativeBridge.commit; native actions preserve that ledger and never write Firebase. OS backup is disabled to prevent another device inheriting a dismissal ledger; use Settings > Data & Sharing > Backup / Restore > Export local backup for calendar backup. Desktop saves have recoverable previous files; the original PlayerPrefs calendar remains intact.

The native queue covers 45 days, up to 128 occurrences, replenishes after each firing and every 12 hours, and rebuilds after reboot, app replacement and clock/zone changes. Ringing stops after 10 minutes. Dismiss stops only the ringing occurrence; Snooze reschedules it with its effective snooze duration. Open Calendar dismisses the specific actively ringing occurrence before launching Unity and preserves simultaneous alarms and future repeats. Open Calendar from a non-ringing upcoming notice preserves its future delivery. Upcoming "Dismiss this alarm" skips only that occurrence. Swiping an upcoming notice does not skip it. Future skip Undo is available in Events & alarms, including after restarting.

Alarm volume, notification channel settings and Do Not Disturb remain under Android control. Force-stopping an app prevents Android delivery until it is launched again. Full-screen presentation requires the relevant Android access; notification Dismiss/Snooze remain available without it.

## Local verification completed

Unity compiled the Firebase-enabled code and passed 974 focused assertions plus 74 family assertions covering shared activities, recurrence scopes, assignments, filters, conflict handling, simulated device alarm isolation, migration/recovery and portrait geometry. Play-mode checks passed 131 assertions with zero errors, including Back confirmation, icon selection, profiles, templates, agenda, planner, gestures and canvas frames. The integrated checks generated 315 Canvas renders across all three themes at 360x640, 393x851 and 412x915. All 24 native JUnit/Robolectric tests passed, including an account-scoped read-only migration test. The isolated demo-shiftcal emulator passed 53 security/revision/tombstone assertions without accessing production data.

A non-development ARM64 APK was built at `Logs/Android/ShiftCal-ui.apk` using the existing package and debug signing configuration. Certificate verification and the APK hash are saved in `Logs/Android/family-apk-verification.txt`. No Android device is attached. Real Google/cloud login, two-account sharing, lock-screen delivery, sound/vibration and OEM permission/battery behavior remain unverified on a phone.

## Phone verification later

- Allow notifications and exact-alarm access from Events & alarms; confirm the readiness view reports the actual permissions.
- Use Test alarm, close the app normally and lock the screen. Verify sound/vibration, Dismiss, and Snooze.
- Verify native date/time dialogs initialize correctly, Cancel preserves values, OK saves, device 12/24-hour formatting works and no keyboard opens. Test keyboard insets with title/notes focused and Save above the gesture region.
- Create one Day-12 Wake Up definition from Day Details, test fixed and relative timing (including a previous-day delivery), confirm one definition card and check per-occurrence delivery edits/skips.
- Change global notice and snooze preferences, confirm native reconciliation, disable notices while keeping the real alarm and ensure Test alarm has no immediate advance notice.
- Ring two alarms together. Open Calendar for one and confirm its audio/vibration stops, the other still rings and future repeats remain. Open a future notice and confirm its alarm stays scheduled. Check all three native themes and lock-screen/cutout/gesture insets.
- Enable an upcoming notice, choose "Dismiss this alarm", and confirm the next workday stays enabled. Try Undo.
- Override a workday to OFF, then restore the scheduled shift; confirm its alarm changes and notes remain.
- Reboot and check the next scheduled alarm. Deny/regrant permissions and check status/reconciliation.
- Test the Thursday 1 PM Chicago meeting on an OFF day, DST examples, multiple events, and occurrence/future/series edits.
- Once configured, test Google cancellation, sign-out/account switching, group invitation, two-device edits/deletion conflicts, offline changes and sync recovery.
- Verify an update with the original signing key preserves schedules. No physical device tests have been performed yet.

Official references: [Firebase Unity setup](https://firebase.google.com/docs/unity/setup), [Credential Manager Google sign-in](https://developer.android.com/identity/sign-in/credential-manager-siwg-implementation), [Android alarm scheduling](https://developer.android.com/develop/background-work/services/alarms).
