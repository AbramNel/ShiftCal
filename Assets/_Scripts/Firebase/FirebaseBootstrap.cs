using UnityEngine;

#if SHIFT_CAL_USE_FIREBASE
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
#endif

namespace ShiftCal.Firebase
{
    public class FirebaseBootstrap : MonoBehaviour
    {
        public static FirebaseBootstrap Instance;

#if SHIFT_CAL_USE_FIREBASE
        public FirebaseAuth Auth;
        public FirebaseFirestore DB;
#endif

        public bool Ready { get; private set; }
        public bool FirebaseEnabled { get; private set; }

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

#if SHIFT_CAL_USE_FIREBASE
            FirebaseEnabled = true;
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (task.Result == DependencyStatus.Available)
                {
                    Auth = FirebaseAuth.DefaultInstance;
                    DB = FirebaseFirestore.DefaultInstance;
                    Ready = true;
                    Debug.Log("Firebase Ready");
                }
                else
                {
                    Debug.LogError("Firebase Failed: " + task.Result);
                }
            });
#else
            FirebaseEnabled = false;
            Ready = true;
            Debug.LogWarning("Firebase SDK is not installed or SHIFT_CAL_USE_FIREBASE is not defined. Running in local/offline mode.");
#endif
        }
    }
}
