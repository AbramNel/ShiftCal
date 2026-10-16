# ShiftCal handoff — October 16, 2026

## Goal

Build a simple shared family calendar around the work rotation: see shifts, add activities, assign responsibility and find convenient dates. Calendar information is shared; wake-up alarms and reminders stay on each phone. Existing data must survive updates.

**Next milestone:** verify two updated Android phones can share and edit activities without sharing their personal alarms.

## Where we are

The family calendar and subsequent updates are implemented on **main**. The October 16 quality-of-life update starts from `f3f232a45dc93585020f5013fd0ddc5d5ac8f4c4`. The saved scene and prefabs are wired. Continue on main. See [QUALITY_OF_LIFE.md](QUALITY_OF_LIFE.md) for current behavior, file map, Firebase diagnosis and acceptance checks; [PRESENTATION_PRIVACY.md](PRESENTATION_PRIVACY.md) covers the preserved Android privacy policy, and [DAY_ALARMS_HOURS.md](DAY_ALARMS_HOURS.md) covers data compatibility.

Completed:

- Display settings and a persistent optional monthly total, long-press date selection, right-aligned actual-hours pills and optional scheduled shift hours.
- Computed active/history lists, confirmed personal cleanup and separate owner-only shared-history cleanup; recurring definitions, exceptions and in-flight alarms are retained.
- Compact sound/selection sheets, full-row toggle targets, refreshed older screens and specific sync diagnostics. The default deployment file now includes /shiftcal/v1 while retaining its legacy rules; production remains untouched.

- Compact timesheet/pencil hours pills, coherent calendar header and six-row layout in normal/Edit modes.
- Grouped Agenda day cards, compact filters/Event action, tappable event/private alarm rows and cached presentation with assignment colors preserved across themes.
- Android privacy: no above-keyguard or forced-screen-on Activity behavior, no full-screen notification intents/permission/settings request, generic secret notifications and unlock-guarded actions. Native scheduling, ringing, audio/vibration, ledgers, snooze/dismiss and independent alarm handling remain intact.
- Compact preview and cleaner Day Details, preserving hidden person data when notes are saved.
- Weekday AND resolved-shift shared recurrence with private audible alarm offsets.
- Device-local oneoff/weekly alarms and weekday-restricted shift alarm rules through the existing engine.
- Manual actual hours in integer hundredths, separate calendar badges, monthly totals and a personal backup export.

- Family profiles, colors, account links and work-rotation ownership; unlinked people need no Google account.
- Activities, notes, 20 icons including karate, assignments, participants, timing/duration, recurrence and exceptions.
- Month gestures, filters, seven-day agenda, templates, availability planning and updated details/navigation in all three themes.
- Device-local alarms, account isolation and migration backups. Uncertain legacy cloud alarms remain disabled until explicitly enabled.
- Firebase records, permissions, revision conflicts and tombstones.
- Reusable tap-based selection sheets, working Activity Repeat, conditional recurrence preview, compact timing/assignment/participant workflows, date-aware quick-start templates, quick reassignment with guarded Undo, collapsed agenda dates, one multi-person planner selector and truthful save/sync status.

Latest validation passed: **1,974 focused Unity assertions, 599 play-mode assertions with zero errors, 46 native tests, lint with zero errors and three existing dependency warnings, and 77 Firestore emulator assertions.** Generated 738 current-run UI renders across three themes at 360×640, 393×851 and 412×915. Dense-day checks preserve activity capacity, overflow, notes, alarms, today/selection and all six rows. Real EventSystem tests cover display preferences, unscaled long-press and range dragging, optional shift hours, cleanup confirmation/account isolation, hours save/zero/clear, monthly review/edit, selectors, Agenda and existing family workflows. Native privacy tests exercise APIs 26, 33 and 35.

No physical phone was attached (`adb devices -l` returned an empty list), and no configured AVD was available. Real lock-screen rendering and sleeping-device ringing remain unverified. No production Firebase data or rules were modified. Install the newly built APK for these changes; rebuilding an old checkout will not include them.

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
3. Securely copy **`C:/Users/simyr/.android/debug.keystore`**, alias `androiddebugkey`, and adjust Unity's key path if needed. The user confirmed this PC's **A4:C9…** certificate matches the installed app. Do not commit or replace it. Verified fingerprints are in [FIREBASE_SETUP.md](FIREBASE_SETUP.md).
4. Fresh October 16 APK: **`Logs/Android/ShiftCal-ui.apk`**, ARM64, non-development, **33,603,106 bytes** (about 32 MiB), package `com.abramnel.shiftcal`, version 1.1.0/code 3, min API 26/target 36. Its verified certificate is **A4:C9:C4:97:18:F8:AF:4D:97:48:8C:43:E3:2B:E2:9C:80:8D:C7:14:20:9E:13:3E:86:74:54:D8:D9:58:1D:17**. APK SHA-256: `BA900C745B6C00E766F3782396C8F0DF1B7FB1FA5CC36514DAE7D14D8721CAF4`. Verification is in `Logs/Android/qol-apk-verification.txt`. **Git does not carry the APK, logs/screenshots, signing key or app-private data.** Copy the APK separately or rebuild with `ShiftCalChecks.BuildAndroid`. Personal alarms remain on each phone.

Use the saved scene; the builder replaces manual scene edits. Checks: `ShiftCalChecks.Run`, `ShiftCalRuntimeChecks.Run`, `Tools/AndroidChecks` and `Tools/FirebaseChecks`.

## Where to look

- [FAMILY_CALENDAR.md](FAMILY_CALENDAR.md): workflows, migration, file map and rules deployment.
- [FIREBASE_SETUP.md](FIREBASE_SETUP.md): Firebase setup, verified signing fingerprints and phone test checklist.
- `Assets/_Scripts/App/DeviceCalendarStore.cs`: alarm privacy and migration.
- `Assets/_Scripts/Core/ActivityResolver.cs`, `FamilyAvailability.cs`: activities and planning.
- `Assets/_Scripts/UI/` and `Assets/_Scripts/Firebase/`: screens and sync.
