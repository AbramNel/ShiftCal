# Quality-of-life update — October 16, 2026

This report describes the completed quality-of-life update. [Calendar image export](CALENDAR_IMAGE_EXPORT.md) records the subsequent export feature. [Full-screen alarms](FULL_SCREEN_ALARMS.md) records the current alarm behavior and APK verification, superseding this report's preserved lock-screen policy for actively ringing audible alarms.

Implemented directly in the existing `main` checkout, starting at `f3f232a45dc93585020f5013fd0ddc5d5ac8f4c4`. The saved scene, prefabs, sprites and permanent scene builder are integrated; no manual scene wiring is needed.

## Calendar and display

- Settings has a Display section alongside the existing Appearance controls. Show logged-hours total defaults ON and persists under `ShiftCal.Display.ShowLoggedHoursTotal.v1`, outside account/calendar data. OFF removes the header pill and its vertical space; the month title recenters and the grid gets that space. Daily badges, actual values and calculations remain unchanged. Review monthly hours remains available in Display.
- A roughly 500 ms hold uses unscaled time and a density-aware movement tolerance. It enters Edit Mode, clears stale selection and selects the held date, including adjacent-month cells. Continued dragging extends the selection. Release suppresses the ordinary preview tap. Pointer identity, movement, rebind/disable, overlays, normal month swipes and Back/Done are handled by the existing controllers.
- Actual-hours pills are compact and right-aligned in Preview and Day Details. Existing decimal, explicit-zero, clear, date context and device/account isolation behavior is retained.
- Set shift hours stages changes in the Shift Editor. Existing timed shifts start ON; untimed shifts start OFF. OFF hides the scheduled-time section, and saving clears start/end/duration consistently. OFF then ON preserves draft values; cancel preserves saved values. ON validates both times and supports overnight duration. No new model Boolean or migration is needed.
- Untimed shifts retain their names and colors and omit empty scheduled-hours labels. Start-relative alarms explain the missing start time; fixed-time alarms and manual actual hours still work. Untimed working shifts are not inferred to be days off.

## Active definitions, history and cleanup

History is computed from existing definitions rather than stored as copied occurrences or an archive flag. Expired one-time events/alarms and exhausted finite recurrence move out of the active Events & Alarms list while remaining on the calendar and in Past / History. Event end times, activity duration, all-day/untimed dates, zones and moved/canceled exceptions are considered. Paused definitions remain paused. Ringing or snoozed native deliveries protect their definitions. Unbounded recurrence is retained conservatively when no occurrence is found in the upcoming search window.

Cleanup actions are separate:

- Personal cleanup counts completed one-time alarms on this account/calendar/device and requires confirmation. It never deletes another phone's definitions or any native delivery-ledger records.
- Calendar-history cleanup requires the calendar owner, explicitly states the effect on everyone when shared, and requires separate confirmation. Only completed one-time entries without retained exceptions or pending/conflicting edits are eligible. Recurring definitions and exceptions remain available for review.

Both confirmations capture the eligible IDs and recheck account, calendar, ownership, completion and live delivery state before changing anything. Newly edited/future/ringing/snoozed entries are protected. Normal storage backups and native reconciliation are retained.

Because the shared payload is opaque JSON, rules cannot securely distinguish a historical event deletion from another event deletion. Shared event/activity/exception tombstones and restoration therefore require the owner. Members retain ordinary event/activity creation and edits, overrides and shared-template workflows. Editors explain owner-only removal and guard future-scope changes that would remove existing exceptions. Private alarms remain manageable on their own device.

## UI audit

Sound uses compact icon-and-description rows with a real checkmark. Single selection applies immediately and closes; X/Back cancel, and the large footer is omitted. Multi-selection retains staged choices and Done/Cancel. Short sheets size to content; long sheets scroll within the safe area. Iconless/descriptionless options leave no empty columns or subtitle rows.

The common components refresh the older Settings, Account, alarm preferences, event/alarm lists and editors, Manage Shifts, shift editor, details, profiles, templates, planner, Agenda, readiness panels and dialogs. Toggle rows have full-row tap targets and checkmark artwork; disclosure arrows use sprites. Untimed shift rows collapse, one-action list rows fit their actions, and confirmations fit longer explanations. A hidden-action binding error found during screenshot review was fixed in `ScheduleListRow`.

## Firebase diagnosis

The configured project ID is `shiftcal-c6d39`. The previous default `firebase.json` deployed the legacy `firestore.rules`, which contained no `/shiftcal/v1` namespace. Deploying that file would reject current app paths. This is a concrete repository configuration mismatch and a **suspected** cause of the phone's failure; it does not confirm which rules are live.

The default rules now include ShiftCal's current namespace/helpers while preserving the legacy match blocks. `firestore.shiftcal.rules` remains the dedicated-project version. The emulator tests cover both deployment targets, owner/member/outsider permissions, private calendars, offline queued writes, revisions/conflicts and private-data rejection.

The app now distinguishes access denied, authentication, missing membership, connectivity, conflicts and invalid-data failures. Shared membership and owner metadata are checked before uploads. Settings offers concise guidance and expandable project/account/path/operation/pending-edit diagnostics. Logs omit tokens, invitation codes and event payloads. Unknown queued edits remain in the local save and are excluded from upload. Obsolete private `rule/` queue records are preserved in immutable account-scoped `schedule-*.json.legacy-queue-*.json` recovery snapshots before the existing shared-save/load filter removes them. These files remain in private app storage and never enter Firestore. Failures do not clear supported queued edits or saved calendars.

**No live rules or data were changed.** No attached phone or Firebase CLI credentials are available here, so live authentication, membership and deployed policy remain unverified. Before a separately authorized deployment, back up the actual deployed rules, preserve all other applications' match blocks, merge the ShiftCal namespace/helpers and test the authoritative merged result. See [FIREBASE_SETUP.md](FIREBASE_SETUP.md) for exact commands and checks.

## Files and compatibility

- Display: `DisplayPreferences`, `DisplaySettingsPanel`, `CalendarController`.
- Gestures: `CalendarDayCell`, `CalendarGestureController`, `AppNavigation`.
- Scheduled/actual hours: `ShiftEditor`, shift rows/picker, `DayPreview`, `DayDetailsPopup`.
- History: `ScheduleLifecycle`, `ScheduleWorkbench.History` and existing workbench partials.
- Shared UI: selection sheet/row, `ToggleRow`, schedule rows, builder partials, saved scene/prefabs and navigation sprites.
- Sync: `FirestoreService`, `SyncDiagnostics`, `FirebaseBootstrap`, status UI, both rules files and emulator checks.
- Verification: quality-update model/runtime/render checks and the existing integrated entry points. Pointer tests use a temporary portrait Game View and restore its previous configuration.

No saved-data schema, actual-hours calculation/storage, recurrence representation, authentication provider, package ID or native ledger format changed. The native scheduler and secure lock-screen policy are preserved. No app uninstall, local-data reset or signing-key replacement was performed.

## Validation

The final integrated runs passed: **1,974 focused Unity assertions** (1,112 core and 862 family), **599 play-mode assertions with zero errors**, **46 native Android tests**, **lint with zero errors** (three existing dependency warnings), and **77 Firestore emulator assertions**. No final suite failed. Native privacy coverage runs on APIs 26, 33 and 35. Firestore checks run only against local emulators and test both the dedicated and merged-default policies.

Generated **738 current-run UI renders** across Midnight Graphite, Deep Teal and Soft Daylight at 360×640, 393×851 and 412×915. This includes 216 quality-update captures plus the existing calendar, family and presentation suites. Visual review covered compact Sound, Display ON/OFF, the collapsed calendar header, Preview/Details hours pills, timed/untimed shifts, untimed working-shift Agenda rows, history/cleanup and longer confirmation text. Pointer/raycast tests exercise the saved scene in portrait, including modal blocking, Back, single/multi-selection and long-press cancellation. Older images retained in the ignored output directories are excluded from this current-run count.

Reproduce with `ShiftCalChecks.Run`, `ShiftCalRuntimeChecks.Run`, `Tools/AndroidChecks` and `Tools/FirebaseChecks`. Logs, renders and APK output are ignored build artifacts and must be copied separately from Git.

## Android artifact

Built October 16: `Logs/Android/ShiftCal-ui.apk`, **33,603,106 bytes**, non-development ARM64, package `com.abramnel.shiftcal`, version 1.1.0/code 3, min API 26 and target 36. APK SHA-256: `BA900C745B6C00E766F3782396C8F0DF1B7FB1FA5CC36514DAE7D14D8721CAF4`.

`apksigner verify --verbose --print-certs` passed and matches the user's confirmed existing certificate: `A4:C9:C4:97:18:F8:AF:4D:97:48:8C:43:E3:2B:E2:9C:80:8D:C7:14:20:9E:13:3E:86:74:54:D8:D9:58:1D:17`. The keystore and project signing settings are unchanged. Packaged-manifest inspection found no `USE_FULL_SCREEN_INTENT`, `showWhenLocked` or `turnScreenOn` declarations. Verification output and the full manifest are under `Logs/Android/qol-*`. No phone was attached for installation or update testing.

## Acceptance still required

Real Android long-press/drag/haptic behavior, locked/sleeping-device ringing, unlock/dismiss/snooze, OEM battery restrictions and reboot behavior require a phone. Live Google login, deployed rules and two-device sync/account isolation also require acceptance. Do not uninstall to resolve an APK certificate mismatch; use the original installed app's signing key.
