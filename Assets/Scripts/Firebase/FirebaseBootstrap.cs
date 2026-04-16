using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;

namespace ShiftCal.Firebase
{
    public class FirebaseBootstrap : MonoBehaviour
    {
        public static FirebaseBootstrap Instance;

        public FirebaseAuth Auth;
        public FirebaseFirestore DB;

        public bool Ready { get; private set; }

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

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
        }
    }
}
