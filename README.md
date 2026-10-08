# ShiftCal

Portrait Android calendar built in Unity 6000.3.0f1, with repeating work rotations, separate recurring events, durable local storage, native alarms and optional Google/Firebase sync.

Open Assets/Calendar.unity, press Play, and select Use on this device. Open Events from the calendar for events and shift-alarm rules. Profile contains themes, backup, Google sign-out and shared-group controls. The committed scene and prefabs are already wired; the builder recreates the same interface.

The calendar opens in normal viewing mode. Dates with notes, people, events or active alarms open a scrollable preview; tap that date again or Open full details to edit. Edit/Done enables shift selection, including tap toggles and drag ranges across adjacent months. Set Shift and Repeat operate on the selection; leaving Edit clears it without changing data.

Settings uses compact shift summaries with a separate editor and color palette. Events shows one card per series or shift-alarm rule. Expand Upcoming to inspect native deliveries, skip/undo specific occurrences, and snooze or dismiss ringing alarms. Notification and exact-alarm controls are grouped under readiness details.

Profile → Appearance offers Midnight Graphite, Deep Teal and Soft Daylight. The preference persists on this device across account changes; existing light/dark saves are migrated on first launch. Android Back dismisses the keyboard, closes the top modal with discard confirmation when needed, and follows the screen history; at the root it backgrounds the app.

See [FIREBASE_SETUP.md](FIREBASE_SETUP.md) for configuration, signing fingerprints, time-zone rules, security/schema details and the later phone checklist. Device validation of keyboard, Back gestures, cutouts, notifications and actual alarm ringing requires an attached Android phone.

Focused checks: Unity menu ShiftCal > Run Focused Checks; native Android Gradle checks in Tools/AndroidChecks; isolated Firestore emulator checks in Tools/FirebaseChecks. Portrait renders cover all three themes at 360×640, 393×851 and 412×915, with simulated safe areas. Run `ShiftCalChecks.BuildAndroid` in Unity batch mode for a development APK under `Logs/Android`; it retains the configured signing key. Renders, APKs and logs are ignored by Git.
