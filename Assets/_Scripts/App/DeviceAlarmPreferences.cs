using UnityEngine;
using ShiftCal.Data;

namespace ShiftCal.App
{
    // Deliberately outside ScheduleSave/Firestore: these settings belong to this device.
    public static class DeviceAlarmPreferences
    {
        private const string Key = "ShiftCal.AlarmPreferences.v1";
        public static AlarmPreferences Current => PlayerPrefs.HasKey(Key)
            ? JsonUtility.FromJson<AlarmPreferences>(PlayerPrefs.GetString(Key)) ?? new AlarmPreferences()
            : new AlarmPreferences();
        public static void Save(AlarmPreferences value)
        {
            value.noticeMinutes = Mathf.Clamp(value.noticeMinutes, 1, 10080);
            value.snoozeMinutes = Mathf.Clamp(value.snoozeMinutes, 1, 120);
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(value)); PlayerPrefs.Save(); Publish();
        }
        public static void Publish() => AndroidBridge.Call<string>("preferences", JsonUtility.ToJson(Current));
        public static void Apply(Occurrence occurrence, bool defaults)
        {
            var p = Current;
            if (defaults) { occurrence.sound = p.sound; occurrence.vibration = p.vibration; occurrence.snoozeMinutes = p.snoozeMinutes; }
            occurrence.advanceMinutes = occurrence.audible && p.upcomingNotices ? p.noticeMinutes : 0;
        }
    }
}
