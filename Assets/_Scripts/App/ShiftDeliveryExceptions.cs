using System;
using ShiftCal.Data;
using UnityEngine;

namespace ShiftCal.App
{
    public static class ShiftDeliveryExceptions
    {
        private static string Key => "ShiftCal.ShiftDeliveries.v1." + (AppSession.Instance?.Data?.account ?? "local");
        public static ShiftDeliveryChanges Read()
        {
            string json = AndroidBridge.Call<string>("shiftDeliveryChanges") ?? PlayerPrefs.GetString(Key, "{\"items\":[]}");
            return JsonUtility.FromJson<ShiftDeliveryChanges>(json) ?? new ShiftDeliveryChanges();
        }
        public static void Change(string id, long at)
        {
            string error = AndroidBridge.Call<string>("changeShiftDelivery", id, at);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
#if !UNITY_ANDROID || UNITY_EDITOR
            var changes = Read(); changes.items.RemoveAll(x => x.id == id); changes.items.Add(new ShiftDeliveryChange { id = id, at = at });
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(changes)); PlayerPrefs.Save();
#endif
        }
    }
}
