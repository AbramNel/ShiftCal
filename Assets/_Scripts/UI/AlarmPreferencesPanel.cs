using ShiftCal.App;
using ShiftCal.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftCal.UI
{
    public class AlarmPreferencesPanel : MonoBehaviour
    {
        public Toggle upcoming, vibration;
        public DurationChoice notice, snooze;
        public Dropdown sound;
        public Text message;
        public GameObject noticeSection;
        private bool loading, hooked;
        private void OnEnable() => Load();
        public void Load()
        {
            if (upcoming == null) return;
            if (!hooked)
            {
                hooked = true;
                upcoming.onValueChanged.AddListener(_ => Save()); vibration.onValueChanged.AddListener(_ => Save()); sound.onValueChanged.AddListener(_ => Save());
                notice.choice.onValueChanged.AddListener(_ => Save()); snooze.choice.onValueChanged.AddListener(_ => Save());
                notice.custom.onEndEdit.AddListener(_ => Save()); snooze.custom.onEndEdit.AddListener(_ => Save());
            }
            loading = true; var p = DeviceAlarmPreferences.Current;
            upcoming.isOn = p.upcomingNotices; vibration.isOn = p.vibration;
            notice.Set(p.noticeMinutes); snooze.Set(p.snoozeMinutes); sound.value = p.sound == "notification" ? 1 : p.sound == "silent" ? 2 : 0;
            noticeSection.SetActive(upcoming.isOn); loading = false;
        }
        public void Save()
        {
            if (loading || !Application.isPlaying) return;
            try
            {
                DeviceAlarmPreferences.Save(new AlarmPreferences { upcomingNotices = upcoming.isOn, noticeMinutes = notice.Minutes,
                    snoozeMinutes = snooze.Minutes, vibration = vibration.isOn, sound = sound.value == 1 ? "notification" : sound.value == 2 ? "silent" : "alarm" });
                noticeSection.SetActive(upcoming.isOn); message.text = "Alarm preferences saved on this device.";
            }
            catch (System.Exception ex) { message.text = ex.Message; }
        }
    }
}
