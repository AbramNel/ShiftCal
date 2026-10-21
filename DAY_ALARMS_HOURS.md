# Day, alarms and actual hours update — October 14, 2026

Implemented directly on main, starting from cec4293f39544535448e9a2ce5371d0dae5892f5.

## Workflows

Tap any calendar date for its compact preview. The shift and start/end times remain visible; the clock row opens a small decimal hours editor. Saving closes both the hours editor and its matching day preview, returning directly to the calendar with the cell badge and month total updated immediately. Cancel returns to the preview; invalid input or a failed write keeps the hours editor open. Clear removes an entry; saving zero retains a 0h badge. Another date updates the preview, a second tap opens full details, and Details, X and Back retain their existing navigation roles. The preview fits its content within the safe area and scrolls when needed.

Day Details removes the scheduled-hours total and obsolete Person input. It retains notes, shift name/color/times, Change Shift, contextual Restore Scheduled Shift, events and family activities. Notes save through UpdateDayNotes and preserve the stored personName and existing shift state. Restoring the rotation retains notes, hidden person data, activities and actual hours.

Add Event opens icon-led New Event, Add Alarm and Saved Templates rows, with recent templates afterward. New Event creates one shared CalendarActivity, preserving all 20 icons, responsibility and participant support. Add Alarm opens the existing scheduling editor in private mode for the selected date. It defaults to one date; recurring weekdays or shifts require an explicit choice. Templates retain the chosen date and never copy personal subscriptions. Note-type icons and existing note templates remain supported.

For a Shutdown Meeting at 13:00 on Thursdays assigned to Days: choose Weekdays on a matching shift, select the stable Days shift, select Thursday, and enable the personal alarm with Ring before event = 30 minutes. The next three qualifying dates use the actual rotation and date overrides. There is one shared series, and this phone rings once at 12:30. Another updated phone sees the event with no audible subscription until its user enables one. A separate silent reminder remains optional.

Private alarms support a single fixed date, recurring weekdays, every matching shift, and selected weekdays on a matching shift. Matching-shift alarms offer a specific time or an offset before shift start. Existing ShiftAlarmRule definitions remain authoritative for shift alarms. Local EventSeries definitions handle ordinary private dates and retain single-occurrence, future and entire-series edit/delete scopes. Both reuse RecurrenceEngine and the native OccurrenceEngine/AlarmScheduler.

The monthly clock total sums actual records for the displayed calendar month, including hours entered on OFF days and excluding adjacent-month cells. Tapping it opens dated entries for quick editing. Hours use their own row above the existing activity icon row; no icons are displaced to make room. Assignment colors, overflow, notes, alarms, adjacent shading and today/selection remain present.

## Data and compatibility

- CalendarActivity and SharedEventDefinition gain optional hasShiftConstraint and requiredShiftType fields. EventSeries carries the same constraint into the existing engine. An explicit flag keeps shift ID 0 valid. RecurrenceKind values remain Once=0, Daily=1, Weekly=2, Monthly=3.
- A Daily series plus the constraint matches every selected shift; Weekly plus the constraint requires both its weekdays/cadence and the resolved shift. Counts apply after qualification. The original series/occurrence IDs and exception keys remain stable; future splits consume qualifying base occurrences only. Cancellations still occupy their base recurrence count, as with existing recurrence behavior.
- A moved single-occurrence replacement uses its explicit replacement date, rather than requiring that moved date to match the original weekday. Its optional shift constraint still applies. Replacements do not generate a second base occurrence. Missing shift definitions produce no qualifying occurrences; retired definitions with their stable IDs still resolve.
- LocalAlarmSubscription.alarmOffsetMinutes is private. Effective EventSeries snapshots carry that field to native scheduling; shared DTOs and Firestore records exclude it. The existing :alarm delivery ID now points to the early audible instant, with no additional event-time ring.
- ShiftAlarmRule gains hasWeekdayConstraint and weekdays. Missing/false flags retain the existing every-matching-shift behavior. Native delivery, snooze, dismissal, skip and Open Calendar handling keep their existing IDs and ledger.
- DeviceCalendarState remains version 1 and gains personalAlarms and workedHours lists. Absent new fields are initialized on load, preserving existing rules, subscriptions and templates. Shared ScheduleSave remains version 3. No Firebase key type, rule deployment, package ID or signing key changes are required by this update.
- PersonalAlarmDefinition contains calendar/group scope, an existing EventSeries schedule and its exceptions. It is composed into the native snapshot only for the active account/calendar and never inserted into shared events.
- WorkedHoursEntry stores groupId, ISO dateKey, integer hundredths and updatedAt inside the account-scoped private file. Valid input is 0–24 hours with at most two decimal places. No entry and saved zero are distinct. Values are never inferred from scheduled hours, copied into DayOverrideData, or sent to Firestore.

All participating phones need the updated APK to understand shift constraints. Existing unconstrained records keep their prior dates. Renaming shifts does not change constraints. Local shift changes, received shared data and existing startup/resume/native reconciliation paths rebuild eligibility; a disconnected device can apply remote edits only after receiving them.

## Personal storage, backup and recovery

Private data stays in the existing account-hashed schedule JSON.device.json file under Application.persistentDataPath. Writes flush a temporary file and atomically replace the current file, retaining a valid .previous copy. Invalid originals are preserved during recovery. Hours entry writes and private-alarm save/delete operations roll back their in-memory change if the private write fails. A remote refresh rehydrates rules without replacing the private hours or standalone alarms.

Switching accounts uses a different private file. Switching calendars filters by group ID. Creating a shared group from the same existing calendar remaps its private records to the new identity; joining another calendar does not transfer hours into it. Uninstalling the app or clearing its storage can erase this device-only data. Android OS backup remains disabled.

Settings → Data & Sharing → Backup / Restore offers an explicitly labeled Export personal backup (hours & alarms). Save the exported ShiftCal-personal-backup.json outside the app. Its versioned wrapper includes this account’s private state, local templates/subscriptions and device alarm defaults. Treat it as personal data. The shared calendar export excludes private definitions. Neither export synchronizes the personal file to Firestore.

Automatic .previous recovery works on normal load. There is no new in-app arbitrary-file import: manual recovery from a personal export requires extracting its device object into the matching account's private state file while the app is stopped, preserving a copy of existing files and verifying account and group IDs. The preferences object represents device defaults. Native delivery/dismissal/snooze ledgers are deliberately not exported or reset; importing definitions onto a different installation cannot recreate another phone's dismissal history.

## Validation

- ShiftCalChecks.RebuildAndRun: 1,070 general focused assertions plus 279 family/day/hours assertions, all passed.
- ShiftCalRuntimeChecks.Run: 400 assertions, zero errors. Actual GraphicRaycaster hits and pointer clicks cover preview Details/Add Alarm/X, full-day Add Alarm/hours, numeric save/zero/clear and monthly entry selection, plus existing selectors, template dates, responsibility, recurrence scopes, navigation and agenda behavior.
- 405 portrait PNG renders: all three themes at 360×640, 393×851 and 412×915. Dense-day geometry checks verify separate day/shift/hour/activity rectangles and retained icon capacity, +N, note/alarm indicators, selection and six rows. Outputs are in ignored Logs/UI and Logs/UI/Family.
- Native testDebugUnitTest: 18 pure recurrence/policy tests plus 10 Robolectric tests, all passed. Native lint passed. New coverage includes qualifying counts after weekday/shift filtering, valid zero shift IDs, overrides/restoration, retired/missing shifts, one audible 12:30 delivery for a 13:00 meeting, offsets across midnight, private weekday/shift rules, ordinary oneoffs and reconciliation retaining skipped state. Existing DST, snooze/dismiss and Open Calendar regressions pass.
- Firestore emulator: 53 assertions passed in demo-shiftcal only. No live data or rules were changed.

The Unity checks additionally cover precision, absence versus zero, OFF-day logging, edit/clear, account/calendar isolation, reload recovery, hidden-person preservation, no accidental note shift overrides, monthly exclusion of adjacent dates, private snapshots, shared DTO boundaries and one/future recurrence edits without duplicates.

No physical phone was connected. Lock-screen ringing, real audio/vibration, OEM alarm/battery permissions, reboot behavior, IME keyboard behavior and live two-phone Firebase sync require device acceptance testing. Automated native tests verify scheduling and state transitions, not hardware delivery.

## Changed files

Runtime changes are in AppSession, DeviceCalendarStore, new PersonalAlarmStore/WorkedHoursStore; FamilyData/ScheduleData; ActivityResolver/RecurrenceEngine; DayPreview, DayDetailsPopup, CalendarController, CalendarDayCell, PreviewOutsideDismiss, ActivityEditor/ActivityRecurrencePreview, ActivityTemplateController, FamilyWorkbench, ScheduleWorkbench and its new Personal partial, and new WorkedHoursEditor.

Scene changes are in ShiftCalSceneBuilder, its Family/Refinement/Scheduling partials and new Personal partial; committed Assets/Calendar.unity; CalendarDayCell.prefab; and calendar/template navigation sprite assets. Focused/runtime checks are in new DayAndHoursChecks/DayAndHoursRuntimeChecks, FamilyCalendarChecks, QualityOfLifeChecks and ShiftCalRuntimeProbe. Native changes are in OccurrenceEngine, NativeBridge and both Java test suites. README, HANDOFF and this document describe the delivered update.

The non-development ARM64 update APK is generated at Logs/Android/ShiftCal-ui.apk with the existing com.abramnel.shiftcal identity and certificate. The build passed and the 33,559,334-byte APK verified with v2 signing. Certificate SHA-256 remains FA:DE:29:64:21:8D:28:30:94:82:69:04:A6:6D:92:97:F4:14:29:B4:1C:13:11:47:37:6B:5D:8A:1E:1A:77:F0. APK SHA-256: 29C17266F3B616AAF123873A6E4ABC6DD33BC6D9E3F5E38D0AFFC94B234E4B8C. Verification output is in ignored Logs/Android/oct14-apk-verification.txt.
