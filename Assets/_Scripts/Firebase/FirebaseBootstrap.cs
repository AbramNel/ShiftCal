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
        public string ConfigurationError { get; private set; }

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

#if SHIFT_CAL_USE_FIREBASE
            FirebaseEnabled = true;
            var config=Resources.Load<ShiftCal.App.ShiftCalConfig>("ShiftCalConfig");
            if(config==null||string.IsNullOrEmpty(config.webClientId)) { ConfigurationError="Google/Firebase setup required: set the Web/server OAuth client ID in Resources/ShiftCalConfig.";return; }
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (!task.IsFaulted && !task.IsCanceled && task.Result == DependencyStatus.Available)
                {
                    try{Auth = FirebaseAuth.DefaultInstance;DB = FirebaseFirestore.DefaultInstance;Ready = true;Debug.Log("Firebase Ready");}
                    catch(System.Exception ex){ConfigurationError="Firebase configuration required: "+ex.Message;}
                }
                else
                {
                    ConfigurationError="Firebase initialization failed. Check ShiftCal's google-services.json and dependencies.";
                    Debug.LogError("Firebase initialization failed. Local calendar remains available.");
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
