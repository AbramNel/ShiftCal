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

All new cloud data uses /shiftcal/v1. The default firebase.json points to firestore.rules, which now retains the legacy rules and adds the current ShiftCal namespace/helpers. The dedicated-project version is firestore.shiftcal.rules. If the Firebase project serves other apps, merge ONLY the ShiftCal match block and its helper functions into the authoritative currently deployed rules, retaining every other app's rules. The repository cannot establish that its legacy rules cover everything currently deployed; do not replace a shared project's rules with the standalone ShiftCal file.

The configured project is **shiftcal-c6d39**. Before October 16, the default deployment file omitted /shiftcal/v1 entirely, so deploying it would cause PERMISSION_DENIED for current app paths. This is a suspected cause of the reported phone error. Live rules, device UID and membership have not been confirmed: no phone or Firebase CLI credentials are available here.

Settings → Data & Sharing → Sync details shows the configured project, account match, personal/shared calendar, membership, pending count and last affected operation/path. Access denied is separate from offline connectivity; queued edits and cached calendars are retained. Shared event/history removal and restoration require the owner; members retain ordinary creation/edit rights, overrides and template workflows.

After separately authorizing production deployment, first save the currently deployed policy. For a dedicated ShiftCal project, the explicit command is:

```powershell
firebase deploy --only firestore:rules --config firebase.shiftcal.deploy.json --project shiftcal-c6d39
```

For a shared project, merge into its authoritative rules instead, test the merged file, then use that project's existing configuration with `firebase deploy --only firestore:rules --project shiftcal-c6d39`. Verify that project ID before either command. No deployment is performed by building the app or running local emulator checks.

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

Alarm volume, notification channel settings and Do Not Disturb remain under Android control. Force-stopping an app prevents Android delivery until it is launched again. ShiftCal does not request full-screen presentation or show Activities above the keyguard. Native ringing continues while locked; normal device unlock gives access to Dismiss/Snooze and calendar content.

## Local verification completed

The October 16 update passed 1,974 focused Unity assertions, 599 play-mode assertions with zero errors, 46 native tests and lint with zero errors (three existing dependency warnings). The integrated run generated 738 current-run Canvas renders across all three themes at 360x640, 393x851 and 412x915. Local demo-project emulators passed 77 security/revision/tombstone assertions, including both rules configurations, owner/member permissions, offline pending writes, conflicts and private-data rejection. No production data was accessed. See [QUALITY_OF_LIFE.md](QUALITY_OF_LIFE.md) for coverage and remaining acceptance.

A non-development ARM64 APK is built at `Logs/Android/ShiftCal-ui.apk` using the existing package and debug signing configuration. Current artifact details are recorded in [QUALITY_OF_LIFE.md](QUALITY_OF_LIFE.md). No Android device is attached. Real Google/cloud login, two-account sharing, lock-screen delivery, sound/vibration and OEM permission/battery behavior remain unverified on a phone.

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
