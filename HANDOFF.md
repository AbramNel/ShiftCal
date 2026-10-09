# ShiftCal handoff — October 9, 2026

## Goal

Build a simple shared family calendar around the work rotation: see shifts, add activities, assign responsibility and find convenient dates. Calendar information is shared; wake-up alarms and reminders stay on each phone. Existing data must survive updates.

**Next milestone:** verify two updated Android phones can share and edit activities without sharing their personal alarms.

## Where we are

The family update is implemented on **main**, commit `6ac8e14b9c2cead753cc47be17b66e950244dc05`. The saved scene and prefabs are wired. Continue on main; the next phase is real-device and live-cloud acceptance testing.

Completed:

- Family profiles, colors, account links and work-rotation ownership; unlinked people need no Google account.
- Activities, notes, 20 icons including karate, assignments, participants, timing/duration, recurrence and exceptions.
- Month gestures, filters, seven-day agenda, templates, availability planning and updated details/navigation in all three themes.
- Device-local alarms, account isolation and migration backups. Uncertain legacy cloud alarms remain disabled until explicitly enabled.
- Firebase records, permissions, revision conflicts and tombstones.

Validation passed: **1,048 focused Unity assertions, 131 play-mode assertions with zero errors, 24 native tests and 53 Firebase emulator assertions.** Generated 315 UI renders across three themes and 360×640, 393×851 and 412×915 layouts.

## What still needs doing

1. **Deploy updated Firebase rules.** Production was untouched. In a shared project, preserve other apps' rules and merge only ShiftCal's namespace/helpers. See [FAMILY_CALENDAR.md](FAMILY_CALENDAR.md).
2. **Verify live Google login and two-phone sync:** invitations, assignments, offline edits, conflicts, deletion and account switching. Live end-to-end sync remains unverified.
3. **Verify Android hardware:** gestures/Back, permissions, lock-screen ringing, sound, snooze/dismiss/skip, reboot and battery restrictions. Confirm Phone B does not inherit Phone A's alarms and can opt into reminders separately.
4. **Verify existing-data migration.** Keep backups and the original signing key. Update every sharing phone before relying on alarm isolation.

Limits: planning uses recorded activities and one person's work rotation. Group ownership transfer is absent. The APK is debug-signed; no release key was supplied.

## Continue on the home PC

1. Clone `https://github.com/AbramNel/ShiftCal` if needed. In the existing checkout, preserve any local changes, then:

   ```powershell
   git switch main
   git pull --ff-only origin main
   ```

   Reconcile divergence on main; do not reset or force-push.
2. Install **Unity 6000.3.0f1** with Android Build Support, SDK/NDK and OpenJDK. Open `Assets/Calendar.unity`; Play → **Use on this device** checks local functionality. Firebase SDKs, configuration and web client ID are already committed; verify project and signing fingerprints before live login.
3. Securely copy **`C:/Users/simyr/.android/debug.keystore`**, alias `androiddebugkey`, and adjust Unity's key path if needed. Do not commit or replace it. Verified fingerprints are in [FIREBASE_SETUP.md](FIREBASE_SETUP.md).
4. APK: **`Logs/Android/ShiftCal-ui.apk`**, ARM64, non-development, about 32 MiB. **Git does not carry the APK, logs/screenshots, signing key or app-private data.** Copy the APK separately or rebuild with `ShiftCalChecks.BuildAndroid`. Personal alarms remain on each phone.

Use the saved scene; the builder replaces manual scene edits. Checks: `ShiftCalChecks.Run`, `ShiftCalRuntimeChecks.Run`, `Tools/AndroidChecks` and `Tools/FirebaseChecks`.

## Where to look

- [FAMILY_CALENDAR.md](FAMILY_CALENDAR.md): workflows, migration, file map and rules deployment.
- [FIREBASE_SETUP.md](FIREBASE_SETUP.md): Firebase setup, verified signing fingerprints and phone test checklist.
- `Assets/_Scripts/App/DeviceCalendarStore.cs`: alarm privacy and migration.
- `Assets/_Scripts/Core/ActivityResolver.cs`, `FamilyAvailability.cs`: activities and planning.
- `Assets/_Scripts/UI/` and `Assets/_Scripts/Firebase/`: screens and sync.
