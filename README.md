# ShiftCal

Unity 6000.3.0f1 Android portrait calendar combining a rotating work schedule with shared family activities. All ongoing work belongs on **main**.

Open `Assets/Calendar.unity`. The scene, row prefabs, and sprite assets are saved and wired. The permanent scene builder reproduces them; rebuilding replaces manual scene edits.

Tap a date for its compact preview and day details. Add a titled activity or note, choose one of 20 semantic icons, assign responsibility and participants, and optionally add a time, duration, recurrence, and a device alarm. Recurring edits support one occurrence, future occurrences, or the series. Family profiles have stable IDs, colors, optional account linkage, and explicit work-rotation ownership.

The month grid displays assignment-colored icons and overflow without hour labels. Swipe months in Normal mode; Edit mode retains drag selection, bulk shift changes, and pattern repetition. Shift hours and legacy day notes/events remain available in details.

Everyone, Mine, person, and Unassigned filters apply to family activities. Agenda covers the next seven dates. Settings provides profiles, local/shared activity templates, and a bounded availability planner that distinguishes recorded free time, missing work information, and unknown timing.

Wake rules, event/activity subscriptions, sound/snooze preferences, and native delivery ledgers are **device-local**. Sharing an activity never subscribes another updated phone to its alarm. Legacy migration keeps backups and stable identities; cloud alarm definitions with uncertain device ownership remain disabled until explicitly enabled.

Google account display uses Firebase identity and privately cached email. Sharing uses the dedicated `/shiftcal/v1` namespace, revision transactions, tombstones, and explicit conflict resolution. Updated rules must be deployed before family records can sync. No production deployment is automatic.

See [Family implementation and migration](FAMILY_CALENDAR.md) for workflows, changed files, security/deployment instructions, and limitations. [Firebase setup](FIREBASE_SETUP.md) covers configuration and signing.

Validation entry points: `ShiftCalChecks.RebuildAndRun`, `ShiftCalRuntimeChecks.Run`, native tests in `Tools/AndroidChecks`, and isolated emulator tests in `Tools/FirebaseChecks`. Unity renders cover all three themes at 360×640, 393×851, and 412×915. `ShiftCalChecks.BuildAndroid` builds a non-development ARM64 APK at `Logs/Android/ShiftCal-ui.apk` using the existing signing key. Logs, renders, and APKs are excluded from Git. Physical-phone acceptance testing remains required.
