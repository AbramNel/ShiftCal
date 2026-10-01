using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ShiftCal.Core;
using ShiftCal.Data;

namespace ShiftCal.UI
{
    public class CalendarController : MonoBehaviour
    {
        [SerializeField] private Text uiMonthLabel;
        [SerializeField] private List<CalendarDayCell> dayCells = new List<CalendarDayCell>(42);
        [SerializeField] private DayDetailsPopup dayDetailsPopup;
        [SerializeField] private GameObject shiftPickerPanel;
        [SerializeField] private List<Button> shiftPickerButtons = new List<Button>();
        [SerializeField] private List<Text> shiftPickerLabels = new List<Text>();
        [SerializeField] private GameObject repeatPanel;
        [SerializeField] private Text selectionLabel;
        [SerializeField] private Button shiftChoicePrefab;
        [SerializeField] private Transform shiftChoiceContent;
        private string selectionStart;
        private string selectionEnd;
        private void OnEnable() { if (App.AppSession.Instance != null) { App.AppSession.Instance.Changed += Refresh; if (currentMonth.Year < 2000) currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); Refresh(); } }
        private void OnDisable() { if (App.AppSession.Instance != null) App.AppSession.Instance.Changed -= Refresh; }

        public DateTime currentMonth;

        private readonly Dictionary<string, DayOverrideData> localOverrides = new Dictionary<string, DayOverrideData>();
        private readonly HashSet<string> selectedDateKeys = new HashSet<string>();
        private readonly Dictionary<string, CalendarDayData> visibleDaysByKey = new Dictionary<string, CalendarDayData>();
        private bool isSelecting;

        private Dictionary<string, DayOverrideData> Overrides
        {
            get
            {
                ShiftCal.App.AppSession session = ShiftCal.App.AppSession.Instance;
                return session != null ? session.CalendarOverrides : localOverrides;
            }
        }

        private void Start()
        {
            currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            HideShiftPicker();
            HideRepeatPanel();
            Refresh();
        }

        public void NextMonth()
        {
            currentMonth = currentMonth.AddMonths(1);
            Refresh();
        }

        public void PrevMonth()
        {
            currentMonth = currentMonth.AddMonths(-1);
            Refresh();
        }

        public void Refresh()
        {
            if (ShiftCal.App.AppSession.Instance == null)
            {
                Debug.LogWarning("AppSession is missing from the scene. Add an AppSession object before using the calendar.");
                return;
            }

            GroupData group = ShiftCal.App.AppSession.Instance.CurrentGroup;
            List<CalendarDayData> days = CalendarGenerator.Generate(currentMonth, group, Overrides);
            var eventCounts=new Dictionary<string,int>();
            var save=App.AppSession.Instance.Data;
            foreach(var e in save.events)foreach(var hit in RecurrenceEngine.EventDates(save,e,days[0].date,days[days.Count-1].date))eventCounts[hit.calendarDateKey]=eventCounts.TryGetValue(hit.calendarDateKey,out int n)?n+1:1;
            foreach(var day in days)day.eventCount=eventCounts.TryGetValue(day.dateKey,out int n)?n:0;
            visibleDaysByKey.Clear();

            if (uiMonthLabel != null)
                uiMonthLabel.text = currentMonth.ToString("MMMM yyyy");

            for (int i = 0; i < dayCells.Count; i++)
            {
                CalendarDayData day = i < days.Count ? days[i] : null;
                if (day != null)
                    visibleDaysByKey[day.dateKey] = day;

                dayCells[i].Bind(this, day);
                if (day != null)
                    dayCells[i].SetSelected(selectedDateKeys.Contains(day.dateKey));
            }

            UpdateSelectionLabel();
        }

        public void BeginDaySelection(string dateKey)
        {
            isSelecting = true;
            selectedDateKeys.Clear();
            selectionStart = selectionEnd = dateKey;
            selectedDateKeys.Add(dateKey);
            Refresh();
        }

        public void ExtendDaySelection(string dateKey)
        {
            if (!isSelecting)
                return;

            selectionEnd = dateKey;
            selectedDateKeys.Clear();
            DateTime a = DateKeyUtility.FromDateKey(selectionStart), b = DateKeyUtility.FromDateKey(dateKey);
            if (a > b) { var t = a; a = b; b = t; }
            for (var d = a; d <= b; d = d.AddDays(1)) selectedDateKeys.Add(DateKeyUtility.ToDateKey(d));
            Refresh();
        }

        public void EndDaySelection(string dateKey)
        {
            isSelecting = false;
            dateKey = selectionEnd ?? dateKey;
            Refresh();

            if (selectedDateKeys.Count == 1 && visibleDaysByKey.TryGetValue(dateKey, out CalendarDayData day))
            {
                dayDetailsPopup?.Show(day);
                return;
            }

            ShowShiftPicker();
        }

        public void OpenShiftPickerForSelection()
        {
            dayDetailsPopup?.Hide();
            ShowShiftPicker();
        }

        public void ApplyShiftByPickerIndex(int index)
        {
            GroupData group = ShiftCal.App.AppSession.Instance != null ? ShiftCal.App.AppSession.Instance.CurrentGroup : null;
            var choices=group?.shiftTypes?.FindAll(x=>!x.retired);
            if (choices == null || index < 0 || index >= choices.Count)
                return;
            ApplyShiftToSelection(choices[index].id);
        }

        public void ApplyShiftToSelection(int shiftType)
        {
            if (selectedDateKeys.Count == 0)
                return;

            foreach (string dateKey in selectedDateKeys)
            {
                App.AppSession.Instance.SetShift(dateKey, shiftType);
            }

            ShiftCal.App.AppSession.Instance?.SaveLocal();
            HideShiftPicker();
            Refresh();
        }

        public void ShowRepeatPanel()
        {
            if (repeatPanel != null)
                repeatPanel.SetActive(true);
        }

        public void HideRepeatPanel()
        {
            if (repeatPanel != null)
                repeatPanel.SetActive(false);
        }

        public void RepeatSelectedOneMonth() => RepeatSelectedPattern(1);
        public void RepeatSelectedThreeMonths() => RepeatSelectedPattern(3);
        public void RepeatSelectedSixMonths() => RepeatSelectedPattern(6);
        public void RepeatSelectedTwelveMonths() => RepeatSelectedPattern(12);
        public void RepeatSelectedTwentyFourMonths() => RepeatSelectedPattern(24);

        public void RepeatSelectedPattern(int months)
        {
            if (selectedDateKeys.Count == 0)
                return;

            months = Mathf.Clamp(months, 1, 24);
            List<string> sortedKeys = new List<string>(selectedDateKeys);
            sortedKeys.Sort(StringComparer.Ordinal);

            DateTime start = DateKeyUtility.FromDateKey(sortedKeys[0]);
            DateTime end = DateKeyUtility.FromDateKey(sortedKeys[sortedKeys.Count - 1]);
            int patternLength = (end - start).Days + 1;
            if (patternLength <= 0)
                return;

            int[] pattern = new int[patternLength];
            for (int i = 0; i < pattern.Length; i++)
            {
                string key = DateKeyUtility.ToDateKey(start.AddDays(i));
                pattern[i] = ResolveShiftForDate(key);
            }

            DateTime repeatEnd = start.AddMonths(months);
            for (DateTime date = start; date < repeatEnd; date = date.AddDays(1))
            {
                int offset = (date - start).Days % patternLength;
                string key = DateKeyUtility.ToDateKey(date);
                App.AppSession.Instance.SetShift(key, pattern[offset]);
            }

            ShiftCal.App.AppSession.Instance?.SaveLocal();
            HideRepeatPanel();
            HideShiftPicker();
            Refresh();
        }

        private int ResolveShiftForDate(string dateKey)
        {
            if (Overrides.TryGetValue(dateKey, out DayOverrideData data) && !data.scheduledShift)
                return data.shiftType;

            if (visibleDaysByKey.TryGetValue(dateKey, out CalendarDayData day))
                return day.ResolvedShift;

            GroupData group = ShiftCal.App.AppSession.Instance.CurrentGroup;
            return ShiftPatternUtility.Resolve(group.pattern, group.startDateKey, DateKeyUtility.FromDateKey(dateKey));
        }

        private void ShowShiftPicker()
        {
            GroupData group = ShiftCal.App.AppSession.Instance != null ? ShiftCal.App.AppSession.Instance.CurrentGroup : null;
            List<ShiftTypeDefinitionData> shiftTypes = group != null ? group.shiftTypes.FindAll(x=>!x.retired) : null;

            if (shiftPickerPanel != null)
                shiftPickerPanel.SetActive(true);
            while (shiftTypes != null && shiftPickerButtons.Count < shiftTypes.Count && shiftChoicePrefab != null)
            {
                var button = Instantiate(shiftChoicePrefab, shiftChoiceContent);
                shiftPickerButtons.Add(button); shiftPickerLabels.Add(button.GetComponentInChildren<Text>());
            }

            for (int i = 0; i < shiftPickerButtons.Count; i++)
            {
                bool active = shiftTypes != null && i < shiftTypes.Count;
                shiftPickerButtons[i].gameObject.SetActive(active);
                if (!active)
                    continue;

                int captured = i;
                shiftPickerButtons[i].onClick.RemoveAllListeners();
                shiftPickerButtons[i].onClick.AddListener(() => ApplyShiftByPickerIndex(captured));

                if (i < shiftPickerLabels.Count && shiftPickerLabels[i] != null)
                    shiftPickerLabels[i].text = shiftTypes[i].name + " " + ShiftTimeUtility.FormatHours(shiftTypes[i].hours);
                shiftPickerButtons[i].targetGraphic.color = ShiftStyleUtility.ToColor(shiftTypes[i].colorHex);
                shiftPickerLabels[i].color = Color.black;
            }
        }

        public void HideShiftPicker()
        {
            if (shiftPickerPanel != null)
                shiftPickerPanel.SetActive(false);
        }

        private void UpdateSelectionLabel()
        {
            if (selectionLabel != null)
                selectionLabel.text = !string.IsNullOrEmpty(App.AppSession.Instance?.Error) ? App.AppSession.Instance.Error : selectedDateKeys.Count == 0 ? "Select days" : selectedDateKeys.Count + " selected";
        }
    }
}
