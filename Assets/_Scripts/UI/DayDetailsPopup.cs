using ShiftCal.Core;
using UnityEngine;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    public class DayDetailsPopup : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleLabel, shiftLabel, timeLabel, hoursLabel, eventsLabel;
        [SerializeField] private Image colorSwatch;
        [SerializeField] private InputField noteInput, personInput;
        public CalendarController calendar;
        private CalendarDayData selected;
        private string originalNote, originalPerson;
        public bool HasChanges => noteInput.text != originalNote || personInput.text != originalPerson;
        public void Show(CalendarDayData day) => Show(day, null);
        public void Show(CalendarDayData day, DayInformation info)
        {
            if (day == null) return; selected = day;
            originalNote = day.note ?? ""; originalPerson = day.personName ?? "";
            noteInput.text = originalNote; personInput.text = originalPerson;
            titleLabel.text = day.date.ToString("dddd, MMM d, yyyy"); shiftLabel.text = day.shiftName;
            timeLabel.text = string.IsNullOrWhiteSpace(day.startTime) ? "No scheduled hours" : day.startTime + " – " + day.endTime;
            hoursLabel.text = ShiftTimeUtility.FormatHours(day.hours); colorSwatch.color = ShiftStyleUtility.ToColor(day.shiftColorHex);
            if (info == null) { var data = DayInformation.Resolve(App.AppSession.Instance.Data, day.date, day.date); info = data.TryGetValue(day.dateKey, out var value) ? value : new DayInformation(); }
            if (eventsLabel != null) { eventsLabel.text = string.Join("\n", info.events.ConvertAll(o => o.title + " • " + DayInformation.Time(o))) + "\n" + string.Join("\n", info.alarms.ConvertAll(o => o.title + " • " + DayInformation.Time(o))); eventsLabel.gameObject.SetActive(info.events.Count + info.alarms.Count > 0); }
            panel.SetActive(true); panel.transform.SetAsLastSibling();
            var modal = panel.GetComponent<ModalPanel>(); modal.HasChanges = () => HasChanges;
        }
        public void Hide() => panel.SetActive(false);
        public void Cancel() => AppNavigation.Instance.Back();
        public void SaveDetails()
        {
            if (selected == null) return;
            App.AppSession.Instance.UpdateDayDetails(selected.dateKey, noteInput.text, personInput.text);
            App.AppSession.Instance.SaveLocal(); Hide();
        }
        public void Restore()
        {
            if (selected == null) return;
            App.AppSession.Instance.UpdateDayDetails(selected.dateKey, noteInput.text, personInput.text);
            App.AppSession.Instance.RestoreScheduledShift(selected.dateKey); App.AppSession.Instance.SaveLocal(); Hide();
        }
        public void ChangeShift()
        {
            if (selected == null) return;
            calendar.OpenShiftPickerForDate(selected.dateKey, RefreshShift);
        }
        private void RefreshShift()
        {
            if (selected == null) return;
            var date = selected.date;
            selected = CalendarGenerator.Generate(new System.DateTime(date.Year, date.Month, 1), App.AppSession.Instance.CurrentGroup, App.AppSession.Instance.CalendarOverrides).Find(d => d.dateKey == selected.dateKey);
            shiftLabel.text = selected.shiftName;
            timeLabel.text = string.IsNullOrWhiteSpace(selected.startTime) ? "No scheduled hours" : selected.startTime + " – " + selected.endTime;
            hoursLabel.text = ShiftTimeUtility.FormatHours(selected.hours); colorSwatch.color = ShiftStyleUtility.ToColor(selected.shiftColorHex);
        }
        public void AddEvent()
        {
            if (selected == null) return;
            System.Action open = () => { Hide(); Object.FindFirstObjectByType<ScheduleWorkbench>(FindObjectsInactive.Include).NewEvent(selected.dateKey); };
            if (HasChanges) AppNavigation.Instance.Confirm("Discard unsaved notes before adding an event?", open); else open();
        }
    }
}
