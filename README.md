# ShiftCal

Portrait Android calendar built in Unity 6000.3.0f1, with repeating work rotations, separate recurring events, durable local storage, native alarms and optional Google/Firebase sync.

Open Assets/Calendar.unity, press Play, and select Use on this device. Open Events from the calendar for events and shift-alarm rules. Profile contains themes, backup, Google sign-out and shared-group controls. Scene/prefab templates are permanent assets; repeating list entries use those templates dynamically.

See [FIREBASE_SETUP.md](FIREBASE_SETUP.md) for configuration, signing fingerprints, time-zone rules, security/schema details and the later phone checklist. No APK has been built or installed.

Focused checks: Unity menu ShiftCal > Run Focused Checks; native Android Gradle checks in Tools/AndroidChecks; isolated Firestore emulator checks in Tools/FirebaseChecks. Portrait UI renders and Unity logs are under Logs (ignored by Git).
