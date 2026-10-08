# ShiftCal

Portrait Android calendar built in Unity 6000.3.0f1, with repeating work rotations, separate recurring events, durable local storage, native alarms and optional Google/Firebase sync.

Open Assets/Calendar.unity, press Play, and select Use on this device. The Calendar header contains Edit/Done and Settings. Settings is the navigation hub for Appearance, Manage Shifts, Events & Alarms, the Account/Profile popup, groups, backup and sync tools. There is no bottom tab bar. The committed scene and prefabs are already wired; the builder recreates the same interface.

The calendar opens in normal viewing mode. Dates with notes, people, events or active alarms open a scrollable preview; tap that date again or Open full details to edit. Edit/Done enables shift selection, including tap toggles and drag ranges across adjacent months. Selected dates have a thick theme-aware frame; Today has a small number pill. Leaving Edit clears the selection without changing data.

Change Shift opens a full modal with a horizontal date strip. From day details, it edits only the focused date, stays open after applying a shift and preserves unsaved detail fields underneath. Tap or swipe neighboring dates to recenter across month/year boundaries. From Edit Mode, it explicitly applies to the selected-date set; the strip previews dates without changing that bulk scope. Repeat Pattern retains the 1/3/6/12/24-month options. Saved notes, people and event definitions remain intact; SaveLocal retains sync and native alarm rescheduling.

Manage Shifts uses compact summaries and a separate staged editor with a color palette. Events shows one card per series or shift-alarm rule. Expand Upcoming to inspect native deliveries, skip/undo specific occurrences, and snooze or dismiss ringing alarms. Notification and exact-alarm controls are grouped under readiness details.

Settings → Appearance offers Midnight Graphite, Deep Teal and Soft Daylight through three working preview buttons with a selected marker. The preference persists on this device across account changes; existing light/dark saves are migrated on first launch. Android Back dismisses the keyboard, closes the top modal with discard confirmation when needed, and follows Calendar → Settings → secondary-screen history; at the root it backgrounds the app. Editors use a top-right close control and a primary Save action.

See [FIREBASE_SETUP.md](FIREBASE_SETUP.md) for configuration, signing fingerprints, time-zone rules, security/schema details and the phone checklist. Device validation of keyboard, Back gestures, cutouts, notifications and actual alarm ringing requires an attached Android phone.

Focused checks: Unity menu ShiftCal > Run Focused Checks; `ShiftCalRuntimeChecks.Run` in batch mode exercises real startup, canvas frames, pointer raycasts, repeated theme taps, horizontal drags and modal navigation; native Android Gradle checks in Tools/AndroidChecks; isolated Firestore emulator checks in Tools/FirebaseChecks. Portrait renders cover all three themes at 360×640, 393×851 and 412×915, with simulated safe areas. `ShiftCalChecks.BuildAndroid` produces a non-development APK under `Logs/Android` using the configured signing key; `BuildDevelopmentAndroid` enables development diagnostics. Renders, APKs and logs are ignored by Git. Ongoing work belongs on main.
