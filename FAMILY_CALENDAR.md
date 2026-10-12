# Family calendar implementation

All work is on `main`. `Assets/Calendar.unity`, editable row prefabs, and the icon assets are committed. The permanent scene builder reproduces the wiring.

## October 12 usability overhaul

All ordinary Unity Dropdown components and their embedded templates were replaced with SelectionField buttons and one SelectionSheet. The audit found that selector backgrounds had raycastTarget disabled, so a scrolling viewport could consume Repeat taps. The new button backgrounds explicitly receive taps. The sheet lives under the existing safe-area root, comes to the front of the current modal, limits its height to the available screen and scrolls long lists. It uses explicit SelectionItem IDs, labels, descriptions, icon sprites and color swatches. Single selection commits immediately; multi-selection stages changes until Done. Cancel, X and Back discard the sheet selection while preserving the parent draft. IDs are rechecked against the current options when data changes while a sheet is open.

Activity Repeat now opens a real sheet, with conditional interval/weekday controls and an optional ending section. A preview uses the existing recurrence resolver to show readable cadence and up to three next dates, including long intervals and short-month clamping. Zero weekdays, invalid intervals and occurrence counts show validation. The editor uses one timing choice, retains hidden legacy time/duration/zone metadata for unrelated edits, and clears incompatible timing only after an explicit mode change. Participants, personal reminders and templates are collapsed optional sections; notes can expand. Personal reminders retain existing explicit subscription preferences and remain on this device.

Assignment and participant sheets show stable family IDs and colors. Activity Details supports quick assignment and participants with a separate recurrence-scope sheet. A 15-second Undo restores the prior authoritative definitions and local subscriptions only if the resulting records still match; another authoritative edit, revision/conflict, account change or group change disables it. Cached authenticated identity is resolved by UID. A local-only calendar can explicitly select a device profile without claiming a verified identity. Owner authorization is rechecked before saving account links/profiles. Profile settings opened from an activity draft return to that draft.

Add Activity preserves the supplied date through New Activity, Appointment, Note and template choices. Templates create fresh activity IDs, never alarm subscriptions, and keep shared/device storage separate. Recent template IDs stay in the private device file; repeated template saves remain deduplicated. Settings still supports template editing and deletion.

The agenda hides empty non-work dates by default, uses compact activity rows and one optional Show all dates switch, and keeps tappable dates/shifts and local alarms. Who's Free has one multi-person sheet, native date/time pickers and a duration sheet, with compact date results for Check this time, Find family time, Available weekends and Next day off. Missing timing/duration and other people's unknown work schedules remain uncertain. Ordinary untimed or unassigned activity drafts no longer display planner prompts or person-specific conflict warnings.

SaveSyncStatus uses AppSession's write error, pending/conflicting SyncRecords, Firestore upload state, cache metadata and server acknowledgements. It cannot report Synced while changes remain pending. Retry restarts listeners and flushes pending edits. No live rules or production data were changed. The only private-state additions are optional localProfileId and recentTemplates fields; version-3 schedule definitions and version-1 device storage remain backward compatible.

Core files: SelectionField, SelectionSheet, SelectionSheetRow, NumberStepper, FamilySelections, ActivityRecurrencePreview, ActivityAssignmentChange and SaveSyncStatus; ActivityEditor, FamilyWorkbench, FamilyProfileController, ActivityTemplateController, AvailabilityPlanner, DurationChoice, AppNavigation and FirestoreService; scene-builder Family/Scheduling/Selections partials; saved Calendar.unity and row prefabs. QualityOfLifeChecks adds actual EventSystem raycast/click workflows, not only direct value assignments.

## Using the calendar

- Open Settings → Family calendar → Family profiles. The owner can add, rename, color, link, or archive people. Unlinked people need no account. Archive retains their stable IDs and existing assignments. Select the person who owns the work rotation explicitly; the planner never guesses from a display name.
- Tap a date, then open its details and choose Add activity / note. Add a title, notes, an icon, optional time and duration, assignee, and participants. Untimed notes and all-day activities are supported. Legacy day notes and events remain readable.
- Choose Once, Daily, Weekly, or Monthly, with an interval, selected weekdays, end date, or occurrence count. Monthly dates clamp to the end of shorter months. Edit, reassign, or cancel one occurrence, this and future occurrences, or the series. Explicit occurrence exceptions remain separate from the default assignment.
- Each day shows a bounded icon row with overflow. Assignment borders use family colors; unassigned borders are gray. Tap the whole date to open details. The month grid omits hour labels; shift hours remain available in details.
- Swipe horizontally to change months in Normal mode. Vertical motion and small movements do not navigate. Edit-mode drags select dates. The shift picker's date strip operates independently.
- Choose Everyone, Mine, a person, or Unassigned in the calendar or agenda. Mine uses the signed-in/cached account UID linked to a profile. Filters are saved on this device, and shifts remain visible.
- Agenda groups the next seven dates, including resolved activities, legacy events, work shifts, and this device's alarms. Activity rows open their details; date/shift rows return to the calendar.
- Who's free? checks a chosen time and duration for one person, multiple selected people, or everyone. Search covers at most 90 days and returns at most 12 suggestions. Weekend searches check Saturday and Sunday. Next OFF uses the explicitly selected work-rotation owner and date overrides. Results distinguish busy, known free in the recorded schedule, no known conflicts with incomplete work information, and unknown because of missing activity timing or duration.
- Save a draft as a named template. Choose explicitly between this device and the shared calendar. Templates contain activity fields, never alarm subscriptions. Editing a template saves the template without creating an activity; Use template creates a fresh activity ID.

## Device alarm boundary and migration

Shared records contain rotations, shifts, overrides, legacy event definitions, activities, recurrence exceptions, family profiles, and shared templates. Shared event JSON uses DTOs without alarm, sound, vibration, snooze, reminder, or enabled-delivery fields. Activities have no delivery fields.

`DeviceCalendarStore` writes an account-scoped `schedule-<account hash>.json.device.json` in the app's private persistent directory. It owns wake rules, event/activity subscriptions, local templates, filter preferences, and muted sources. Sound/snooze/notice defaults remain in `DeviceAlarmPreferences`. The native AtomicFile continues to own delivery, skip, dismissal, snooze, and notice ledgers. Android OS backup remains disabled.

On first load, migration retains an untouched `.before-family-migration.json` backup of the cached schedule. Legacy PlayerPrefs and their `.legacy.json` backup are retained. If a legacy import stops after writing shared data but before creating the private alarm file, the retained legacy backup recovers its definitions on the next load. A separately saved private state makes subsequent loads idempotent. Both shared and private saves have recoverable previous files; invalid originals are retained during recovery.

Cached local definitions retain IDs. Account-scoped native saved definitions provide evidence that cloud-account alarms belonged to this installation; migration also recovers local subscriptions from that native snapshot. Legacy cloud wake definitions without native evidence are retained disabled. Legacy cloud event delivery flags without such evidence do not subscribe this device. Enable the alarm deliberately in its editor or resume the retained rule. Shared activities and events are never deleted because of uncertain alarm ownership.

The app no longer listens to or writes `/users/{uid}/records` wake records. Cached `rule/` SyncRecords are excluded from sync and cannot become remote tombstones or reappear through remote merges. Existing remote records are left in place for recovery. Shared updates rehydrate private rules without replacing the private file. Group changes retain private definitions with their group scope. Sign-out and account switching deactivate the previous native account without erasing its definitions or ledger.

`AndroidBridge.Save` composes an effective version-3 scheduling snapshot using this device's subscriptions. Activities reuse EventSeries and the existing recurrence/native engine through stable synthetic IDs. Existing native event and shift identities and enum values remain unchanged; there is no second alarm scheduler. Creating a shared activity with Alarm enabled subscribes only the creating device. A second updated device has no subscription unless its user opts in.

Update every device before relying on this boundary: older installed builds still implement their older private-record sync behavior.

## Firebase deployment

No production deployment or production-data test writes were performed.

New shared key types are `activity/`, `activityException/`, `familyProfile/`, and `activityTemplate/`, alongside existing calendar key types. Records use stable keys, JSON definitions, revisions, authors, and deletion tombstones. Revisions are compared transactionally; concurrent edits retain both versions for the existing conflict-choice UI. Record document IDs must match their encoded keys.

Owners manage profiles and work-rotation ownership; authorized members edit activities, assignments, exceptions, and shared templates. Anonymous sessions and outsiders cannot write. Profile linkage does not grant group membership: authorization comes from the structured group owner/member documents and record keys, not opaque JSON. Legacy personal wake records are readable only by their account and reject all writes.

For a Firebase project shared with other apps, copy only the `/shiftcal/v1` match block and `scSigned`, `scSelf`, `scOwner`, `scMember`, and `scValidRecord` helpers from `firestore.shiftcal.rules` into the project's authoritative rules. Preserve all unrelated match blocks. Review overlapping permissive rules, because Firestore grants access if any matching allow expression permits it. Test the merged rules, then deploy that project's existing rules configuration with `firebase deploy --only firestore:rules --project ACTUAL_PROJECT_ID`.

For a project exclusively dedicated to ShiftCal, `firebase.shiftcal.deploy.json` points to the standalone rules. After selecting and verifying the real project ID, deploy with:

```powershell
firebase deploy --only firestore:rules --config firebase.shiftcal.deploy.json --project ACTUAL_PROJECT_ID
```

The original `firestore.rules` and `firebase.json` are retained and are not the new family schema's deployment target. Deploy the updated rules before expecting family records to sync.

## Implementation files

- Data: `Data/FamilyData.cs`, `ScheduleData.cs`, `GroupData.cs`.
- Persistence and native boundary: `App/DeviceCalendarStore.cs`, `ScheduleStorage.cs`, `AppSession.cs`, `AndroidBridge.cs`, and `NativeBridge.java`'s read-only account-scoped migration helper.
- Resolution and planning: `Core/ActivityResolver.cs`, `FamilyAvailability.cs`, plus the existing RecurrenceEngine and native engine.
- UI: `ActivityEditor`, `ActivityIconSet`, `ActivityIconChoice`, `FamilyActivityRow`, `FamilyWorkbench`, `FamilyProfileController`, `ActivityTemplateController`, `AvailabilityPlanner`, and `CalendarGestureController`; calendar cells, day information/details, navigation, account display, and the legacy workbench integration were updated.
- Assets: permanent scene, calendar/shift/schedule prefabs, new family row/participant prefabs, Resources/ActivityIcons, 20 semantic icon PNGs including a high-kick silhouette, and navigation sprites.
- Cloud: FirestoreService, AuthService, dedicated rules, and emulator assertions.
- Validation: FamilyCalendarChecks, FamilyRuntimeChecks, existing focused/runtime probes, and the native Android suites.

## Validation and practical limits

Latest validation passed 1,030 focused Unity assertions plus 94 family/usability assertions, 298 play-mode assertions with zero errors, 24 native JUnit/Robolectric tests, and 53 Firebase emulator assertions. Android lint reported 0 errors and 5 non-blocking warnings. The integrated run generated 369 portrait Canvas renders, including 144 family/editor/sheet views.

Development checks caught the disabled selector raycast targets, an old all-dates agenda expectation, a Windows importer file lock during optional YAML cleanup, an Undo fingerprint affected by canceled exceptions, and missing-profile draft-return lifecycle bugs. These were corrected and the final focused/runtime suites passed. No outstanding automated-test failures remain. Real pointer tests include Repeat → Weekly → Monday/Wednesday → Save → Reopen, Me/participants, quick occurrence reassignment/Undo, profile color swatches, sound/snooze sheets, calendar filters, Back cancellation, Add Activity → saved template on the chosen date, and profile-route draft preservation.

Focused Unity checks cover recurrence, exception scopes, filters, stable assignments, conflict handling, two simulated devices, local subscriptions, private migration/recovery, and saved wiring. Real Unity renders cover all three themes at 360×640, 393×851, and 412×915. Runtime checks exercise startup, canvas cycles, pointer targets, icon selection, profiles, templates, agenda, planner, gestures, and keyboard-first Back/dirty confirmation. Firebase tests use only the local `demo-shiftcal` emulator. Native tests use JUnit and Robolectric.

Logs and screenshots are under `Logs/`, and the freshly rebuilt non-development ARM64 APK is under `Logs/Android/ShiftCal-ui.apk`; these artifacts are ignored by Git. The October 12 APK is 33,519,562 bytes (about 32 MiB), package com.abramnel.shiftcal, version 1.1.0/code 3. It uses the unchanged existing Android debug certificate, SHA-256 FA:DE:29:64:21:8D:28:30:94:82:69:04:A6:6D:92:97:F4:14:29:B4:1C:13:11:47:37:6B:5D:8A:1E:1A:77:F0. APK SHA-256: 91DC95C59FC5FFDA2C86D2194DD1ACA4B98E6A5A97DC163EE061C7A454FD7DFF. Certificate verification is recorded in Logs/Android/qol-apk-verification.txt. The package, signing key and signing settings were not changed.

No physical Android phone validation or live two-account Firebase sign-in/sync was performed. Lock-screen delivery, OEM permission/battery behavior, sound/vibration, Google login, and hardware gestures still need device acceptance testing. Availability is based on recorded information, and only one work rotation is currently associated with a family person. Existing Google/Firebase configuration and the new rules deployment are required for live sharing.
