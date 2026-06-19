# Firebase Setup

This project can compile without Firebase installed. Until Firebase is installed, the app runs in local/offline mode and remote login/sync methods log warnings instead of crashing.

## Current Working Mode

- `FirebaseBootstrap` starts successfully without the Firebase SDK.
- `AuthService.SignIn()` signs in as `local-user` for development.
- Firestore writes are disabled until Firebase is installed.
- Add `SHIFT_CAL_USE_FIREBASE` to Unity's scripting define symbols only after importing the Firebase SDK packages.

## Firebase Steps

1. Create a Firebase project in the Firebase console.
2. Register your Unity app for the platforms you want to ship.
3. Download the platform config file and put it in `Assets`:
   - Android: `google-services.json`
   - iOS: `GoogleService-Info.plist`
4. Download the Firebase Unity SDK.
5. Import at least:
   - `FirebaseAuth.unitypackage`
   - `FirebaseFirestore.unitypackage`
6. In Firebase Authentication, enable the Google provider.
7. In Firestore, publish the rules in `firestore.rules`.
8. In Unity, add `SHIFT_CAL_USE_FIREBASE` to `Project Settings > Player > Scripting Define Symbols`.

## Google Login Note

Firebase Auth can finish Google login after the app obtains a Google ID token. The current code includes `AuthService.SignInWithGoogleTokens(idToken, accessToken)` for that final Firebase step. The next piece is adding a Google Sign-In token provider for your target platform.

## Security Shape

The starter rules deny everything by default. Users can read their own `/users/{uid}` document, and group calendar data is limited to users whose Firebase UID is in `groups/{groupId}.members`.

Firebase config files contain project identifiers, not admin secrets. Real protection comes from Authentication, Firestore Security Rules, and never shipping service-account keys in the app.
