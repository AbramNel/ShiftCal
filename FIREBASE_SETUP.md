# ShiftCal Setup

## Run locally now

Open this project in Unity 6000.3.0f1, open Assets/Calendar.unity, press Play and choose "Use on this device". Calendar, notes, shifts, events and rule editors work without Firebase credentials. Native ringing is Android-only; the Editor does not simulate a successful Google login or claim to run Android alarms.

The scene and prefab templates are saved assets. Shift and agenda rows instantiate these editable templates as needed, with scrolling. Use ShiftCal > Run Focused Checks to verify the current scene. ShiftCal > Build Permanent Calendar App regenerates the scene: do not run that menu after making manual scene edits unless you intend to regenerate it.

## Values you must supply

The stable Android package is **com.abramnel.shiftcal**. No established package identifier was found. Register exactly this package, or decide on another permanent package before registration and before the first phone install.

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

No release key was supplied or created. Before updating an existing installed app, verify its original package and signing certificate. Use that original keystore for an update. Do not uninstall to work around a signing mismatch. Development APK verification output is local under Logs/Android and is excluded from Git. No physical-phone installation has been performed.

## Dedicated Firestore namespace

All new cloud data uses /shiftcal/v1. The old firestore.rules and firebase.json are preserved. The implemented schema is in firestore.shiftcal.rules. If the Firebase project serves other apps, merge ONLY the ShiftCal match block and its helper functions into the existing deployed rules, retaining other apps' rules. Do not deploy the old starter rules or replace the whole shared project's rules with the standalone ShiftCal file.

Personal calendar: /shiftcal/v1/users/{uid}/calendar/{record}.
Private alarm definitions: /shiftcal/v1/users/{uid}/records/{record}.
Shared calendar: /shiftcal/v1/groups/{group}/records/{record}.
Membership and invitations: the group's members and invites subcollections.

Records have key, json, deleted, revision and author fields, explicitly serialized as Firestore dictionaries. Each shift, override, event, exception, alarm rule and rotation is a separate record. Transactions compare revisions. Deletions remain tombstones. Conflicts keep both versions and require a Keep mine / Use remote choice in Options.

Owners manage group definitions, rotation and membership. Members can edit shared date overrides and calendar events. Invitations are bound to a verified Google email and expire after seven days. The owner cannot remove themselves or leave their owned group; ownership transfer is not implemented. Other members can leave. Removed members cannot sync new shared changes; previously cached offline calendar copies are not remotely erased.

Cloud calendars remain accessible offline after sign-in. Switching/sign-out detaches listeners and cancels the previous account's native alarms. Existing local data imports only when you explicitly choose the import button. Group switches retain archive files. Private preferences cannot be read or changed by another UID.

## Recurrence and alarms

Work rotations use device-local shift start times and date overrides. Events can use an IANA zone (default America/Chicago), UTC or "device". Monthly events clamp to the last day when the original day is unavailable. DST gaps advance by the transition length; repeated clock times use the earlier instant. End dates are inclusive; counts include canceled occurrences in the original series. This-and-future edits split the series and remove its future exceptions.

Calendar cancellation affects the shared event. Pause on phone and Skip this affect this device only. Separate event reminders and audible alarms have distinct occurrence identities. Shift rules follow the final resolved shift, including OFF overrides. Untimed shifts cannot be used for a new alarm rule.

Android owns a durable device delivery ledger in its private AtomicFile. Unity sends version-3 ScheduleSave definitions through NativeBridge.commit; native actions preserve that ledger and never write Firebase. OS backup is disabled to prevent another device inheriting a dismissal ledger; use Options > Export local backup for calendar backup. Desktop saves have recoverable previous files; the original PlayerPrefs calendar remains intact.

The native queue covers 45 days, up to 128 occurrences, replenishes after each firing and every 12 hours, and rebuilds after reboot, app replacement and clock/zone changes. Ringing stops after 10 minutes. Dismiss stops only the ringing occurrence; Snooze reschedules it; upcoming "Dismiss this alarm" skips only that occurrence. Swiping an upcoming notice does not skip it. Future skip Undo is available in Events & alarms, including after restarting.

Alarm volume, notification channel settings and Do Not Disturb remain under Android control. Force-stopping an app prevents Android delivery until it is launched again. Full-screen presentation requires the relevant Android access; notification Dismiss/Snooze remain available without it.

## Local verification completed

Unity compiled the actual Firebase-enabled code and passed 214 focused checks, including scene Save-button workflows, rotation/override resolution, migration/recovery, account separation, sync conflicts, DST and portrait geometry. Actual Canvas renders were reviewed at 360x640, 393x851 and 412x915 in dark/light themes. Native Android Java compilation, library manifest processing and lint completed successfully; all 10 recurrence/delivery-state unit tests passed. The isolated demo-shiftcal Firestore emulator passed 17 authorization/revision/tombstone checks without accessing a real Firebase project.

Lint has non-blocking warnings for available dependency updates, English-only native text and API-27 manifest attributes; Android 8 has explicit window-flag fallbacks. Firebase reports missing project configuration until you supply ShiftCal's own google-services.json. No full Unity Android player build, actual Google/cloud login or physical alarm delivery has been tested. No APK was produced.

## Phone verification later

- Allow notifications and exact-alarm access from Events & alarms; confirm the readiness view reports the actual permissions.
- Use Test alarm, close the app normally and lock the screen. Verify sound/vibration, Dismiss, and Snooze.
- Enable an upcoming notice, choose "Dismiss this alarm", and confirm the next workday stays enabled. Try Undo.
- Override a workday to OFF, then restore the scheduled shift; confirm its alarm changes and notes remain.
- Reboot and check the next scheduled alarm. Deny/regrant permissions and check status/reconciliation.
- Test the Thursday 1 PM Chicago meeting on an OFF day, DST examples, multiple events, and occurrence/future/series edits.
- Once configured, test Google cancellation, sign-out/account switching, group invitation, two-device edits/deletion conflicts, offline changes and sync recovery.
- Verify an update with the original signing key preserves schedules. No physical device tests have been performed yet.

Official references: [Firebase Unity setup](https://firebase.google.com/docs/unity/setup), [Credential Manager Google sign-in](https://developer.android.com/identity/sign-in/credential-manager-siwg-implementation), [Android alarm scheduling](https://developer.android.com/develop/background-work/services/alarms).
