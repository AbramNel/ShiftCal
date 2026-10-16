# ShiftCal

Unity 6000.3.0f1 Android portrait calendar combining a rotating work schedule with shared family activities. All ongoing work belongs on **main**.

Continuing on another PC? Start with [HANDOFF.md](HANDOFF.md) for the current goal, completed work, remaining checks and setup steps.

Open `Assets/Calendar.unity`. The scene, row prefabs, and sprite assets are saved and wired. The permanent scene builder reproduces them; rebuilding replaces manual scene edits.

Tap a date for a compact preview with shift times, actual worked hours, activity icons, Add Alarm and a right-aligned Details button. A second date tap opens Day Details. Add Event offers New Event, Add Alarm and Saved Templates, retaining the selected date. Shared events retain icons, notes, family assignments, participants and templates; standalone alarms stay private on this device.

Ordinary selectors open theme-aware, scrollable sheets with full-field tap targets, readable choices and highlighted selections. Repeat offers Once, Daily, Weekly and Monthly, with weekday chips, a compact interval stepper, optional endings and the next three actual dates. Assignment, participants, filters, templates, profile colors/accounts, alarm durations, sound and recurrence scopes use the same component. Cancel, X and Android Back close the sheet before the parent editor and preserve the draft.

Tap responsibility or participants in Activity Details to change them without reopening the editor. Recurring changes ask for occurrence, future or entire-series scope. A short Undo is available while the authoritative record and revisions remain unchanged. Me uses a linked authenticated profile, or an explicitly selected local device profile; family account links remain owner-managed. Existing profile IDs, colors, recurrence exceptions and local alarm subscriptions are preserved.

The month grid displays assignment-colored activity icons and overflow alongside a distinct timesheet badge for manually recorded actual hours. Compact preview/Details pills open the existing hours editor; the month header's content-sized total opens entry review. No hours are inferred from the rotation. Swipe months in Normal mode; Edit mode retains drag selection, bulk shift changes, and pattern repetition. Shift hours and legacy day notes/events remain available in details.

Everyone, Mine, person, and Unassigned filters apply to family activities. Agenda groups the next seven days into compact day cards with shift summaries, colored activity rows and tappable private alarm rows. It collapses empty dates and offers Show all dates. Who's Free uses one multi-person sheet and compact tappable results for a chosen time, family time, weekends or the next day off. It distinguishes recorded free time, known conflicts, incomplete work information and unknown timing. Small save/sync indicators report actual local writes, pending records, cached/offline data, server acknowledgements and conflicts.

Worked hours, standalone personal alarms, wake rules, event/activity subscriptions, audible ring offsets, sound/snooze preferences, and native delivery ledgers are **device-local**. Sharing an activity never subscribes another updated phone to its alarm. Legacy migration keeps backups and stable identities; cloud alarm definitions with uncertain device ownership remain disabled until explicitly enabled.

Google account display uses Firebase identity and privately cached email. Sharing uses the dedicated `/shiftcal/v1` namespace, revision transactions, tombstones, and explicit conflict resolution. Updated rules must be deployed before family records can sync. No production deployment is automatic.

Events can repeat on selected weekdays, matching resolved shift IDs, or both. Rotation and per-date overrides determine eligibility; occurrence counts include qualifying dates only. A private audible alarm can ring before a shared event without subscribing anyone else.

Android alarm audio/vibration continue while locked. ShiftCal no longer launches full-screen alarm interfaces, wakes the display for its UI or displays Activities above the system keyguard. Notifications contain generic information; normal device unlock gives access to Dismiss, Snooze and Open Calendar.

See [October 15 presentation and privacy update](PRESENTATION_PRIVACY.md) for the latest implementation, changed files, validation and remaining hardware acceptance. [Day, alarm and hours update](DAY_ALARMS_HOURS.md) covers data compatibility and personal backup/recovery. [Family implementation and migration](FAMILY_CALENDAR.md) covers workflows, security/deployment and limitations. [Firebase setup](FIREBASE_SETUP.md) covers configuration and signing.

Validation entry points: `ShiftCalChecks.RebuildAndRun`, `ShiftCalRuntimeChecks.Run`, native tests in `Tools/AndroidChecks`, and isolated emulator tests in `Tools/FirebaseChecks`. Unity renders cover all three themes at 360×640, 393×851, and 412×915. `ShiftCalChecks.BuildAndroid` builds a non-development ARM64 APK at `Logs/Android/ShiftCal-ui.apk` using the existing signing key. Logs, renders, and APKs are excluded from Git. Physical-phone acceptance testing remains required.
