using ShiftCal.Core;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    public class DayDetailsPopup : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleLabel, shiftLabel, timeLabel, eventsLabel;
        [SerializeField] private Image colorSwatch;
        [SerializeField] private InputField noteInput;
        public Button restoreButton;
        public Text workedHours;
        public ContentPill hoursPill;
        public CalendarController calendar;
        public FamilyWorkbench family;
        public Transform activityContent;
        private CalendarDayData selected;
        private string originalNote,account,group;
        bool Current=>selected!=null&&account==App.AppSession.Instance.Data.account&&group==App.AppSession.Instance.CurrentGroup.groupId;
        private void OnEnable(){if(App.AppSession.Instance!=null)App.AppSession.Instance.Changed+=RefreshActivities;App.WorkedHoursStore.Changed+=RefreshHours;}
        private void OnDisable(){if(App.AppSession.Instance!=null)App.AppSession.Instance.Changed-=RefreshActivities;App.WorkedHoursStore.Changed-=RefreshHours;}
        private void RefreshActivities(){if(selected!=null&&!Current){Hide();return;}if(selected!=null){if(activityContent!=null)family?.FillDay(selected.dateKey,activityContent);RefreshShift();var date=selected.date;var infos=DayInformation.Resolve(App.AppSession.Instance.Data,date,date);RefreshEntries(infos.TryGetValue(selected.dateKey,out var info)?info:new DayInformation());}}
        private void RefreshHours(){if(selected!=null&&workedHours!=null){var h=App.WorkedHoursStore.Get(selected.dateKey);hoursPill.SetValue(h==null?"Add hours":App.WorkedHoursStore.Format(h.hundredths)+"h worked",h!=null);}}
        public void EditHours(){if(Current)WorkedHoursEditor.Instance.Open(selected.dateKey);}
        public bool HasChanges => noteInput.text != originalNote;
        public void Show(CalendarDayData day) => Show(day, null);
        public void Show(CalendarDayData day, DayInformation info)
        {
            if (day == null) return; selected = day;account=App.AppSession.Instance.Data.account;group=App.AppSession.Instance.CurrentGroup.groupId;
            originalNote = day.note ?? "";
            noteInput.text = originalNote;
            titleLabel.text = day.date.ToString("dddd, MMM d, yyyy"); shiftLabel.text = day.shiftName;
            timeLabel.text = string.IsNullOrWhiteSpace(day.startTime) ? "No scheduled hours" : day.startTime + " – " + day.endTime;
            colorSwatch.color = ShiftStyleUtility.ToColor(day.shiftColorHex);RefreshHours();RefreshRestore();
            if (info == null) { var data = DayInformation.Resolve(App.AppSession.Instance.Data, day.date, day.date); info = data.TryGetValue(day.dateKey, out var value) ? value : new DayInformation(); }
            RefreshEntries(info);
            family?.FillDay(day.dateKey,activityContent);
            panel.SetActive(true); panel.transform.SetAsLastSibling();
            var modal = panel.GetComponent<ModalPanel>(); modal.HasChanges = () => HasChanges;
        }
        void RefreshEntries(DayInformation info)
        {
            if (eventsLabel != null)
            {
                eventsLabel.text = string.Join("\n\n", info.events.Select(DayInformation.EventLine)
                    .Concat(info.alarms.Where(o=>o.isEvent).Select(o=>o.title+" • "+DayInformation.Time(o))));
                eventsLabel.gameObject.SetActive(info.events.Count + info.alarms.Count > 0);
            }
        }
        public void Hide() => panel.SetActive(false);
        public void Cancel() => AppNavigation.Instance.Back();
        public void SaveDetails()
        {
            if (!Current) return;
            App.AppSession.Instance.UpdateDayNotes(selected.dateKey, noteInput.text);
            App.AppSession.Instance.SaveLocal(); Hide();
        }
        public void Restore()
        {
            if (!Current) return;
            App.AppSession.Instance.UpdateDayNotes(selected.dateKey, noteInput.text);
            App.AppSession.Instance.RestoreScheduledShift(selected.dateKey); App.AppSession.Instance.SaveLocal(); Hide();
        }
        public void ChangeShift()
        {
            if (!Current) return;
            calendar.OpenShiftPickerForDate(selected.dateKey, RefreshShift);
        }
        private void RefreshShift()
        {
            if (!Current) return;
            var date = selected.date;
            selected = CalendarGenerator.Generate(new System.DateTime(date.Year, date.Month, 1), App.AppSession.Instance.CurrentGroup, App.AppSession.Instance.CalendarOverrides).Find(d => d.dateKey == selected.dateKey);
            shiftLabel.text = selected.shiftName;
            timeLabel.text = string.IsNullOrWhiteSpace(selected.startTime) ? "No scheduled hours" : selected.startTime + " – " + selected.endTime;
            colorSwatch.color = ShiftStyleUtility.ToColor(selected.shiftColorHex);RefreshRestore();RefreshHours();
        }
        void RefreshRestore(){restoreButton.gameObject.SetActive(selected!=null&&App.AppSession.Instance.CalendarOverrides.TryGetValue(selected.dateKey,out var value)&&!value.scheduledShift);}
        public void AddAlarm(){if(Current)FindFirstObjectByType<ScheduleWorkbench>().NewPersonalAlarm(selected.dateKey);}
        public void AddEvent()
        {
            if (!Current) return;
            System.Action open = () => { Hide(); family.BeginNew(selected.dateKey); };
            if (HasChanges) AppNavigation.Instance.Confirm("Discard unsaved notes before adding an event?", open); else open();
        }
    }
}
