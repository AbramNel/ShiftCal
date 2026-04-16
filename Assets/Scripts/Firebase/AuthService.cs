using UnityEngine;
using Firebase.Extensions;

namespace ShiftCal.Firebase
{
    public class AuthService : MonoBehaviour
    {
        public static AuthService Instance;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SignIn()
        {
            var auth = FirebaseBootstrap.Instance.Auth;

            if (auth.CurrentUser != null)
            {
                Debug.Log("Already signed in");
                return;
            }

            auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError("Auth failed");
                    return;
                }

                Debug.Log("Signed in: " + task.Result.UserId);
            });
        }
    }
}
