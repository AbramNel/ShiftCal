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
        public ShiftPickerController shiftPicker;
        [SerializeField] private GameObject repeatPanel;
        [SerializeField] private Text selectionLabel;
        private string selectionStart;
        private string selectionEnd;
        private void OnEnable() { if (App.AppSession.Instance != null) { App.AppSession.Instance.Changed += Refresh; ThemeManager.Changed += Refresh; if (currentMonth.Year < 2000) currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); Refresh(); } }
        private void OnDisable() { if (App.AppSession.Instance != null) App.AppSession.Instance.Changed -= Refresh; ThemeManager.Changed -= Refresh; SetEditing(false); }

        public DateTime currentMonth;

        private readonly Dictionary<string, DayOverrideData> localOverrides = new Dictionary<string, DayOverrideData>();
        private readonly HashSet<string> selectedDateKeys = new HashSet<string>();
        private readonly Dictionary<string, CalendarDayData> visibleDaysByKey = new Dictionary<string, CalendarDayData>();
        private bool isSelecting, selectionDragged, initiallySelected;
        private readonly HashSet<string> gestureSelection = new HashSet<string>();
        public bool IsEditing { get; private set; }
        public int SelectedCount => selectedDateKeys.Count;
        public Text editLabel;
        public GameObject selectionBar;
        public DayPreview preview;
        private Dictionary<string, DayInformation> information = new Dictionary<string, DayInformation>();
        public void ToggleEdit() => SetEditing(!IsEditing);
        public void SetEditing(bool editing)
        {
            IsEditing = editing; ClearSelection(); preview?.Hide(); dayDetailsPopup?.Hide();
            if (editLabel != null) editLabel.text = editing ? "Done" : "Edit";
            if (selectionBar != null) selectionBar.SetActive(editing);
            var grid=GetComponentInChildren<ResponsiveCalendarGrid>(true);if(grid!=null){var rect=(RectTransform)grid.transform;rect.offsetMin=new Vector2(rect.offsetMin.x,editing?174:24);grid.Fit();}
        }
        public void ClearSelection()
        {
            selectedDateKeys.Clear(); isSelecting = false; selectionStart = selectionEnd = null;
            HideShiftPicker(); HideRepeatPanel(); UpdateSelectionVisuals();
        }
        public void TapDay(string key)
        {
            if (IsEditing || !visibleDaysByKey.TryGetValue(key, out var day)) return;
            var info = information.TryGetValue(key, out var value) ? value : new DayInformation();
            if (preview != null && preview.gameObject.activeSelf && preview.DateKey == key) { preview.OpenDetails(); return; }
            preview?.Hide();
            if (info.HasExtra(day) && preview != null) preview.Show(day, info);
            else dayDetailsPopup?.Show(day, info);
        }
        private void UpdateSelectionVisuals()
        {
            foreach (var cell in dayCells) cell.SetSelected(IsEditing && selectedDateKeys.Contains(cell.DateKey ?? ""));
            UpdateSelectionLabel();
        }

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
            SetEditing(false);
            Refresh();
        }

        public void NextMonth()
        {
            preview?.Hide();currentMonth = currentMonth.AddMonths(1);
            Refresh();
        }

        public void PrevMonth()
        {
            preview?.Hide();currentMonth = currentMonth.AddMonths(-1);
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
            // Include in-memory details before SaveLocal synchronizes the serializable list.
            var save = App.AppSession.Instance.Data;
            var snapshot = JsonUtility.FromJson<ScheduleSave>(JsonUtility.ToJson(save));
            snapshot.overrides = new List<DayOverrideData>(Overrides.Values);
            information = DayInformation.Resolve(snapshot, days[0].date, days[days.Count - 1].date);
            foreach (var day in days) day.eventCount = information.TryGetValue(day.dateKey, out var info) ? info.events.Count : 0;
            visibleDaysByKey.Clear();

            if (uiMonthLabel != null)
                uiMonthLabel.text = currentMonth.ToString("MMMM yyyy");

            for (int i = 0; i < dayCells.Count; i++)
            {
                CalendarDayData day = i < days.Count ? days[i] : null;
                if (day != null)
                    visibleDaysByKey[day.dateKey] = day;

                dayCells[i].Bind(this, day, day != null && information.TryGetValue(day.dateKey, out var info) && info.alarms.Count > 0);
                if (day != null)
                    dayCells[i].SetSelected(IsEditing && selectedDateKeys.Contains(day.dateKey));
            }

            UpdateSelectionLabel();
        }

        public void BeginDaySelection(string dateKey)
        {
            if (!IsEditing) return;
            isSelecting = true;
            selectionDragged = false; initiallySelected = selectedDateKeys.Contains(dateKey);
            gestureSelection.Clear();gestureSelection.UnionWith(selectedDateKeys);
            selectionStart = selectionEnd = dateKey;
            selectedDateKeys.Add(dateKey);
            UpdateSelectionVisuals();
        }

        public void ExtendDaySelection(string dateKey)
        {
            if (!IsEditing || !isSelecting)
                return;

            selectionDragged = selectionDragged || dateKey != selectionStart;
            selectionEnd = dateKey;
            selectedDateKeys.Clear();selectedDateKeys.UnionWith(gestureSelection);
            DateTime a = DateKeyUtility.FromDateKey(selectionStart), b = DateKeyUtility.FromDateKey(dateKey);
            if (a > b) { var t = a; a = b; b = t; }
            for (var d = a; d <= b; d = d.AddDays(1)) selectedDateKeys.Add(DateKeyUtility.ToDateKey(d));
            UpdateSelectionVisuals();
        }

        public void EndDaySelection(string dateKey)
        {
            if (!IsEditing || !isSelecting) return;
            isSelecting = false;if(!selectionDragged && initiallySelected)selectedDateKeys.Remove(selectionStart); UpdateSelectionVisuals();
        }

        public void OpenShiftPickerForSelection()
        {
            if (IsEditing && selectedDateKeys.Count > 0) shiftPicker.OpenBulk(selectedDateKeys);
        }
        public void OpenShiftPickerForDate(string dateKey, Action closed) => shiftPicker.OpenSingle(DateKeyUtility.FromDateKey(dateKey), closed);
        public IEnumerable<string> SelectedDates => selectedDateKeys;
        public void ShowRepeatPanel()
        {
            if (selectedDateKeys.Count > 0 && repeatPanel != null) { repeatPanel.SetActive(true); repeatPanel.transform.SetAsLastSibling(); }
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

        public void HideShiftPicker() { if (shiftPicker != null) shiftPicker.gameObject.SetActive(false); }

        private void UpdateSelectionLabel()
        {
            if (selectionLabel != null)
                selectionLabel.text = !string.IsNullOrEmpty(App.AppSession.Instance?.Error) ? App.AppSession.Instance.Error : selectedDateKeys.Count == 0 ? "Tap or drag dates" : selectedDateKeys.Count + " selected";
        }
    }
}
