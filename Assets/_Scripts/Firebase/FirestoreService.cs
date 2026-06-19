using UnityEngine;

#if SHIFT_CAL_USE_FIREBASE
using Firebase.Extensions;
using Firebase.Firestore;
#endif

using ShiftCal.Data;

namespace ShiftCal.Firebase
{
    public class FirestoreService : MonoBehaviour
    {
        public static FirestoreService Instance;

#if SHIFT_CAL_USE_FIREBASE
        private FirebaseFirestore DB => FirebaseBootstrap.Instance.DB;
#endif

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SaveOverride(string groupId, DayOverrideData data)
        {
#if SHIFT_CAL_USE_FIREBASE
            if (!CanUseFirestore()) return;

            DB.Collection("groups").Document(groupId)
                .Collection("overrides")
                .Document(data.dateKey)
                .SetAsync(data)
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                        Debug.LogError("Failed to save override: " + task.Exception);
                });
#else
            Debug.LogWarning("Firestore is disabled. Override was not uploaded.");
#endif
        }

        public void DeleteOverride(string groupId, string dateKey)
        {
#if SHIFT_CAL_USE_FIREBASE
            if (!CanUseFirestore()) return;

            DB.Collection("groups").Document(groupId)
                .Collection("overrides")
                .Document(dateKey)
                .DeleteAsync()
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                        Debug.LogError("Failed to delete override: " + task.Exception);
                });
#else
            Debug.LogWarning("Firestore is disabled. Override was not deleted remotely.");
#endif
        }

#if SHIFT_CAL_USE_FIREBASE
        private bool CanUseFirestore()
        {
            if (FirebaseBootstrap.Instance == null || !FirebaseBootstrap.Instance.Ready || DB == null)
            {
                Debug.LogWarning("Firestore is not ready yet.");
                return false;
            }

            return true;
        }
#endif
    }
}
