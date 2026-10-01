using ShiftCal.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftCal.UI
{
    public class DayDetailsPopup : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text shiftLabel;
        [SerializeField] private Text timeLabel;
        [SerializeField] private Text hoursLabel;
        [SerializeField] private Image colorSwatch;
        [SerializeField] private InputField noteInput;
        [SerializeField] private InputField personInput;
        [SerializeField] private Text eventsLabel;
        private CalendarDayData selected;

        private void Awake()
        {
            Hide();
        }

        public void Show(CalendarDayData day)
        {
            if (day == null)
                return;
            selected = day;
            if (noteInput != null) noteInput.text = day.note ?? "";
            if (personInput != null) personInput.text = day.personName ?? "";
            if (eventsLabel != null)
            {
                var names = new System.Collections.Generic.List<string>();
                var s = App.AppSession.Instance.Data;
                foreach (var e in s.events)
                {
                    foreach (var d in RecurrenceEngine.Dates(e, day.date, day.date))
                    {
                        var ex = s.exceptions.Find(x => x.seriesId == e.id && x.originalDate == day.dateKey);
                        if (ex == null) names.Add(e.title + " / " + e.startTime);
                    }
                    foreach (var ex in s.exceptions.FindAll(x => x.seriesId == e.id && !x.cancelled && x.replacement != null && x.replacement.dateKey == day.dateKey)) names.Add(ex.replacement.title + " / " + ex.replacement.startTime);
                }
                eventsLabel.text = string.Join("\n", names);
            }

            if (panel != null)
                panel.SetActive(true);
            else
                gameObject.SetActive(true);

            if (titleLabel != null)
                titleLabel.text = day.date.ToString("dddd, MMM d, yyyy");

            if (shiftLabel != null)
                shiftLabel.text = day.shiftName;

            if (timeLabel != null)
                timeLabel.text = string.IsNullOrWhiteSpace(day.startTime) || string.IsNullOrWhiteSpace(day.endTime)
                    ? "No time range"
                    : day.startTime + " - " + day.endTime;

            if (hoursLabel != null)
                hoursLabel.text = string.IsNullOrEmpty(ShiftTimeUtility.FormatHours(day.hours))
                    ? "0h"
                    : ShiftTimeUtility.FormatHours(day.hours);

            if (colorSwatch != null)
                colorSwatch.color = ShiftStyleUtility.ToColor(day.shiftColorHex);
        }

        public void Hide()
        {
            if (panel != null)
                panel.SetActive(false);
            else
                gameObject.SetActive(false);
        }
        public void SaveDetails()
        {
            if (selected == null) return;
            var session = App.AppSession.Instance;
            session.SetShift(selected.dateKey, selected.ResolvedShift);
            var data = session.CalendarOverrides[selected.dateKey];
            data.note = noteInput.text; data.personName = personInput.text;
            session.SaveLocal(); Hide();
        }
        public void Restore()
        {
            if (selected == null) return;
            var overrides=App.AppSession.Instance.CalendarOverrides;
            if(overrides.TryGetValue(selected.dateKey,out var details)&&(!string.IsNullOrEmpty(details.note)||!string.IsNullOrEmpty(details.personName)))details.scheduledShift=true;
            else overrides.Remove(selected.dateKey);
            App.AppSession.Instance.SaveLocal(); Hide();
        }
        public void AddEvent()
        {
            if (selected == null) return;
            Hide(); Object.FindFirstObjectByType<ScheduleWorkbench>(FindObjectsInactive.Include).NewEvent(selected.dateKey);
        }
    }
}
