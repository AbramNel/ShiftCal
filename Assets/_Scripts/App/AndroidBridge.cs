using System;
using UnityEngine;
using ShiftCal.Data;

namespace ShiftCal.App
{
    public static class AndroidBridge
    {
        public static string Status = "Native alarms are available on Android.";
        public static T Call<T>(string method, params object[] args)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var bridge = new AndroidJavaClass("com.abramnel.shiftcal.NativeBridge"))
            {
                var activity = unity.GetStatic<AndroidJavaObject>("currentActivity");
                var all = new object[args.Length + 1]; all[0] = activity; Array.Copy(args, 0, all, 1, args.Length);
                return bridge.CallStatic<T>(method, all);
            }
#else
            return default;
#endif
        }
        public static void Save(ScheduleSave save)
        {
            string error = Call<string>("commit", JsonUtility.ToJson(DeviceCalendarStore.Effective(save)));
            if (!string.IsNullOrEmpty(error)) Status = error;
        }
        public static string Readiness()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return Call<string>("status");
#else
            return Status;
#endif
        }
        public static void Action(string action, string id = "") { Call<string>("action", action, id); }
        public static System.Collections.Generic.Dictionary<string, Occurrence> DeliveryStates()
        {
            var result = new System.Collections.Generic.Dictionary<string, Occurrence>();
            string json = Call<string>("deliveryStates");
            if (!string.IsNullOrEmpty(json)) foreach (var item in JsonUtility.FromJson<OccurrenceList>(json).items) result[item.id] = item;
            return result;
        }
    }
}
