# ShiftCal

Unity 6000.3.0f1 Android portrait calendar combining a rotating work schedule with shared family activities. All ongoing work belongs on **main**.

Continuing on another PC? Start with [HANDOFF.md](HANDOFF.md) for the current goal, completed work, remaining checks and setup steps.

Open `Assets/Calendar.unity`. The scene, row prefabs, and sprite assets are saved and wired. The permanent scene builder reproduces them; rebuilding replaces manual scene edits.

Tap a date for its compact preview and day details. Add Activity opens a quick-start sheet for a new activity, appointment, note, saved templates and recent templates. The selected calendar date is retained through template creation. The compact editor keeps the title and one of 20 icons prominent, uses one No time / Specific time / All day choice, and reveals recurrence, participant and personal-reminder controls as needed.

Ordinary selectors open theme-aware, scrollable sheets with full-field tap targets, readable choices and highlighted selections. Repeat offers Once, Daily, Weekly and Monthly, with weekday chips, a compact interval stepper, optional endings and the next three actual dates. Assignment, participants, filters, templates, profile colors/accounts, alarm durations, sound and recurrence scopes use the same component. Cancel, X and Android Back close the sheet before the parent editor and preserve the draft.

Tap responsibility or participants in Activity Details to change them without reopening the editor. Recurring changes ask for occurrence, future or entire-series scope. A short Undo is available while the authoritative record and revisions remain unchanged. Me uses a linked authenticated profile, or an explicitly selected local device profile; family account links remain owner-managed. Existing profile IDs, colors, recurrence exceptions and local alarm subscriptions are preserved.

The month grid displays assignment-colored icons and overflow without hour labels. Swipe months in Normal mode; Edit mode retains drag selection, bulk shift changes, and pattern repetition. Shift hours and legacy day notes/events remain available in details.

Everyone, Mine, person, and Unassigned filters apply to family activities. Agenda groups the next seven days, collapses empty dates, and offers Show all dates. Who's Free uses one multi-person sheet and compact tappable results for a chosen time, family time, weekends or the next day off. It distinguishes recorded free time, known conflicts, incomplete work information and unknown timing. Small save/sync indicators report actual local writes, pending records, cached/offline data, server acknowledgements and conflicts.

Wake rules, event/activity subscriptions, sound/snooze preferences, and native delivery ledgers are **device-local**. Sharing an activity never subscribes another updated phone to its alarm. Legacy migration keeps backups and stable identities; cloud alarm definitions with uncertain device ownership remain disabled until explicitly enabled.

Google account display uses Firebase identity and privately cached email. Sharing uses the dedicated `/shiftcal/v1` namespace, revision transactions, tombstones, and explicit conflict resolution. Updated rules must be deployed before family records can sync. No production deployment is automatic.

See [Family implementation and migration](FAMILY_CALENDAR.md) for workflows, changed files, security/deployment instructions, and limitations. [Firebase setup](FIREBASE_SETUP.md) covers configuration and signing.

Validation entry points: `ShiftCalChecks.RebuildAndRun`, `ShiftCalRuntimeChecks.Run`, native tests in `Tools/AndroidChecks`, and isolated emulator tests in `Tools/FirebaseChecks`. Unity renders cover all three themes at 360×640, 393×851, and 412×915. `ShiftCalChecks.BuildAndroid` builds a non-development ARM64 APK at `Logs/Android/ShiftCal-ui.apk` using the existing signing key. Logs, renders, and APKs are excluded from Git. Physical-phone acceptance testing remains required.
