using UnityEngine;
using ShiftCal.Data;

namespace ShiftCal.App
{
    public class AppSession : MonoBehaviour
    {
        public static AppSession Instance;

        public GroupData CurrentGroup;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}
