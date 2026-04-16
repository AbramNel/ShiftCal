using UnityEngine;
using Firebase.Firestore;
using Firebase.Extensions;
using ShiftCal.Data;

namespace ShiftCal.Firebase
{
    public class FirestoreService : MonoBehaviour
    {
        public static FirestoreService Instance;

        private FirebaseFirestore DB => FirebaseBootstrap.Instance.DB;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SaveOverride(string groupId, DayOverrideData data)
        {
            DB.Collection("groups").Document(groupId)
                .Collection("overrides")
                .Document(data.dateKey)
                .SetAsync(data);
        }

        public void DeleteOverride(string groupId, string dateKey)
        {
            DB.Collection("groups").Document(groupId)
                .Collection("overrides")
                .Document(dateKey)
                .DeleteAsync();
        }
    }
}
