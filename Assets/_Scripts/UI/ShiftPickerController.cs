using System;
using System.Collections.Generic;
using System.Linq;
using ShiftCal.App;
using ShiftCal.Core;
using UnityEngine;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    public class ShiftPickerController : MonoBehaviour
    {
        public CalendarController calendar;
        public ShiftDayNavigator navigator;
        public List<Button> dayButtons = new List<Button>();
        public Text dateLabel, currentShift, scopeLabel, feedback;
        public Button repeat;
        public Transform choices;
        public ShiftChoiceRow choicePrefab;
        public DateTime FocusedDate { get; private set; }
        public bool IsBulk { get; private set; }
        public int BulkCount => selectedDates.Count;
        public string FocusedKey => DateKeyUtility.ToDateKey(FocusedDate);
        private readonly HashSet<string> selectedDates = new HashSet<string>();
        private readonly List<ShiftChoiceRow> rows = new List<ShiftChoiceRow>();
        private readonly List<DateTime> dates = new List<DateTime>();
        private Action closed;
        private bool presentationPending, layoutPending, snapPending, resetChoices;
        private float viewportWidth;
        private void OnEnable()
        {
            ThemeManager.Changed += Invalidate; AppSession.Instance.Changed += Invalidate;
            presentationPending = layoutPending = true;
        }
        private void OnDisable()
        {
            ThemeManager.Changed -= Invalidate;
            if (AppSession.Instance != null) AppSession.Instance.Changed -= Invalidate;
            var callback = closed; closed = null; callback?.Invoke();
        }
        public void OpenSingle(DateTime date, Action onClosed = null)
        {
            IsBulk = false; selectedDates.Clear(); Open(date, onClosed);
        }
        public void OpenBulk(IEnumerable<string> keys)
        {
            selectedDates.Clear(); selectedDates.UnionWith(keys);
            if (selectedDates.Count == 0) return;
            IsBulk = true; Open(DateKeyUtility.FromDateKey(selectedDates.OrderBy(k => k).First()), null);
        }
        private void Open(DateTime date, Action onClosed)
        {
            // A close during a swipe must never shift the date when this modal reopens.
            snapPending = false; navigator.StopMovement();
            closed = onClosed; FocusedDate = date.Date; feedback.text = "";
            gameObject.SetActive(true); transform.SetAsLastSibling();
            RefreshPresentation(); layoutPending = resetChoices = true;
        }
        public void Focus(DateTime date)
        {
            FocusedDate = date.Date; navigator.StopMovement();
            RefreshPresentation(); layoutPending = true;
        }
        public void RequestSnap() => snapPending = true;
        private void Invalidate() => presentationPending = true;
        private void LateUpdate() => FlushLayout();
        public void FlushLayout()
        {
            if (CanvasUpdateRegistry.IsRebuildingLayout() || CanvasUpdateRegistry.IsRebuildingGraphics()) return;
            if (snapPending)
            {
                snapPending = false;
                var center = navigator.viewport.TransformPoint(navigator.viewport.rect.center);
                int nearest = 0; float distance = float.MaxValue;
                for (int i = 0; i < dayButtons.Count; i++)
                {
                    var rect = (RectTransform)dayButtons[i].transform;
                    float candidate = Mathf.Abs(rect.TransformPoint(rect.rect.center).x - center.x);
                    if (candidate < distance) { nearest = i; distance = candidate; }
                }
                Focus(dates[nearest]);
            }
            if (presentationPending) { presentationPending = false; RefreshPresentation(); }
            if (resetChoices) { resetChoices = false; choices.GetComponentInParent<ScrollRect>().verticalNormalizedPosition = 1; }
            float width = navigator.viewport.rect.width;
            if (!layoutPending && Mathf.Abs(width - viewportWidth) < .1f) return;
            if (width <= 0) return;
            layoutPending = false; viewportWidth = width;
            float gap = navigator.content.GetComponent<HorizontalLayoutGroup>().spacing;
            float cell = (width - gap * 6) / 7;
            foreach (var button in dayButtons) button.GetComponent<LayoutElement>().preferredWidth = cell;
            navigator.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cell * dayButtons.Count + gap * (dayButtons.Count - 1));
            // Symmetric dates put the focused day exactly in the viewport's center.
            navigator.content.anchoredPosition = new Vector2((width - navigator.content.rect.width) / 2, 0);
        }
        public void RefreshPresentation()
        {
            var session = AppSession.Instance;
            if (session == null || session.CurrentGroup == null) return;
            var group = session.CurrentGroup;
            dates.Clear();
            for (int i = 0; i < dayButtons.Count; i++)
            {
                var date = FocusedDate.AddDays(i - dayButtons.Count / 2); dates.Add(date);
                var day = ResolveDay(date);
                var button = dayButtons[i];
                bool focused = date == FocusedDate;
                var color = ThemeManager.ShiftColor(day.shiftColorHex);
                button.targetGraphic.color = color;
                var labels = button.GetComponentsInChildren<Text>();
                labels[0].text = date.Day.ToString(); labels[1].text = date.ToString("MMM");
                foreach (var label in labels) label.color = ThemeManager.Legible(color);
                var frame = button.transform.Find("Focused date").GetComponent<Image>(); frame.enabled = focused; frame.color = ThemeManager.Token(ThemeManager.Role.Selection);
                int index = i;
                button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => Focus(dates[index]));
            }
            var current = ResolveDay(FocusedDate);
            dateLabel.text = FocusedDate.ToString("dddd, MMMM d, yyyy");
            currentShift.text = "Current shift: " + current.shiftName + (string.IsNullOrWhiteSpace(current.startTime) ? " · No scheduled hours" : " · " + current.startTime + "–" + current.endTime);
            scopeLabel.text = IsBulk ? "Apply to " + selectedDates.Count + " selected days · dates below are a preview" : "Apply to the focused day";
            repeat.gameObject.SetActive(IsBulk);
            feedback.rectTransform.offsetMax = new Vector2(IsBulk ? -270 : -32, feedback.rectTransform.offsetMax.y);
            var shifts = group.shiftTypes.Where(s => !s.retired).ToList();
            while (rows.Count < shifts.Count) rows.Add(Instantiate(choicePrefab, choices));
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].gameObject.SetActive(i < shifts.Count);
                if (i < shifts.Count) { int id = shifts[i].id; rows[i].Bind(shifts[i], () => ApplyShift(id)); }
            }
        }
        private CalendarDayData ResolveDay(DateTime date)
        {
            return CalendarGenerator.Generate(new DateTime(date.Year, date.Month, 1), AppSession.Instance.CurrentGroup, AppSession.Instance.CalendarOverrides).Find(d => d.date.Date == date.Date);
        }
        public void ApplyShift(int id)
        {
            var session = AppSession.Instance;
            if (!session.CurrentGroup.shiftTypes.Exists(s => s.id == id && !s.retired)) return;
            var keys = IsBulk ? selectedDates.ToArray() : new[]{FocusedKey};
            foreach (var key in keys) session.SetShift(key, id);
            session.SaveLocal(); // Retains metadata, sync tracking and native alarm rescheduling.
            calendar.Refresh(); RefreshPresentation();
            feedback.text = string.IsNullOrEmpty(session.Error) ? "Applied " + session.CurrentGroup.shiftTypes.Find(s => s.id == id).name + (IsBulk ? " to " + keys.Length + " selected days" : " to " + FocusedDate.ToString("MMM d")) : session.Error;
        }
        public void RepeatPattern() { if (IsBulk) calendar.ShowRepeatPanel(); }
    }
}
