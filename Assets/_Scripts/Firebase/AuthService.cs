using UnityEngine;

#if SHIFT_CAL_USE_FIREBASE
using Firebase.Extensions;
using Firebase.Auth;
#endif

namespace ShiftCal.Firebase
{
    public class AuthService : MonoBehaviour
    {
        public static AuthService Instance;

        public bool IsSignedIn { get; private set; }
        public string UserId { get; private set; }
        public string DisplayName { get; private set; }

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SignIn()
        {
            SignInWithGoogle();
        }

        public void SignInWithGoogle()
        {
            if (FirebaseBootstrap.Instance == null || !FirebaseBootstrap.Instance.Ready)
            {
                Debug.LogWarning("Auth is not ready yet.");
                return;
            }

#if SHIFT_CAL_USE_FIREBASE
            var auth = FirebaseBootstrap.Instance.Auth;

            if (auth.CurrentUser != null)
            {
                ApplyFirebaseUser(auth.CurrentUser);
                Debug.Log("Already signed in with Google/Firebase");
                return;
            }

            Debug.LogWarning("Google sign-in requires an Android Google Sign-In token provider. Use SignInWithGoogleTokens after the provider returns tokens.");
#else
#if UNITY_EDITOR
            IsSignedIn = true;
            UserId = "local-user";
            DisplayName = "Local Google User";
            Debug.LogWarning("Editor-only simulated Google sign-in as local-user. Real builds require Firebase/Google auth.");
#else
            Debug.LogError("Google sign-in is required. Firebase/Google auth is not configured for this build.");
#endif
#endif
        }

        public void SignInWithGoogleTokens(string idToken, string accessToken)
        {
            if (FirebaseBootstrap.Instance == null || !FirebaseBootstrap.Instance.Ready)
            {
                Debug.LogWarning("Auth is not ready yet.");
                return;
            }

#if SHIFT_CAL_USE_FIREBASE
            Credential credential = GoogleAuthProvider.GetCredential(idToken, accessToken);
            FirebaseBootstrap.Instance.Auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError("Google auth failed: " + task.Exception);
                    return;
                }

                ApplyFirebaseUser(task.Result);
                Debug.Log("Signed in with Google: " + UserId);
            });
#else
#if UNITY_EDITOR
            IsSignedIn = true;
            UserId = "local-user";
            DisplayName = "Local Google User";
            Debug.LogWarning("Editor-only simulated Google token sign-in as local-user. Real builds require Firebase/Google auth.");
#else
            Debug.LogError("Google sign-in is required. Firebase/Google auth is not configured for this build.");
#endif
#endif
        }

        public void RefreshExistingSignIn()
        {
#if SHIFT_CAL_USE_FIREBASE
            if (FirebaseBootstrap.Instance == null || !FirebaseBootstrap.Instance.Ready || FirebaseBootstrap.Instance.Auth == null)
                return;

            if (FirebaseBootstrap.Instance.Auth.CurrentUser != null)
                ApplyFirebaseUser(FirebaseBootstrap.Instance.Auth.CurrentUser);
#endif
        }

        public void SignOut()
        {
#if SHIFT_CAL_USE_FIREBASE
            if (FirebaseBootstrap.Instance != null && FirebaseBootstrap.Instance.Auth != null)
                FirebaseBootstrap.Instance.Auth.SignOut();
#endif
            IsSignedIn = false;
            UserId = string.Empty;
            DisplayName = string.Empty;
            Debug.Log("Signed out.");
        }

#if SHIFT_CAL_USE_FIREBASE
        private void ApplyFirebaseUser(FirebaseUser user)
        {
            if (user == null) return;

            IsSignedIn = true;
            UserId = user.UserId;
            DisplayName = string.IsNullOrEmpty(user.DisplayName) ? user.Email : user.DisplayName;
        }
#endif
    }
}
