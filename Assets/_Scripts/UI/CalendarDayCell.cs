using System.Collections.Generic;
using ShiftCal.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    public class CalendarDayCell : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        [SerializeField] private Image uiBackground, selectedOutline, dimOverlay, todayOutline, noteIcon, alarmIcon;
        [SerializeField] private Text uiDayNumberLabel, uiShiftNameLabel, uiNoteLabel, uiHoursLabel;
        private CalendarController controller;
        private CalendarDayData day;
        private Color normalColor;
        private Vector2 pressed;
        private bool dragged, presentationPending = true;
        private Vector2 fittedSize;
        private System.DateTime shownToday;
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        public Image[] activityIcons,activityFrames;
        public Text activityOverflow;
        private List<ActivityOccurrence> activities=new List<ActivityOccurrence>();
        public void Activities(List<ActivityOccurrence> items){activities=items;presentationPending=true;}
        public string DateKey => day?.dateKey;
        public bool IsSelected => selectedOutline != null && selectedOutline.enabled;
        public void Bind(CalendarController owner, CalendarDayData data, bool hasAlarm = false)
        {
            controller = owner; day = data; gameObject.SetActive(day != null); if (day == null) return;
            normalColor = ThemeManager.ShiftColor(day.shiftColorHex);
            uiBackground.color = normalColor;
            foreach (var text in new[]{uiDayNumberLabel,uiShiftNameLabel,uiHoursLabel,uiNoteLabel}) if (text != null) text.color = ThemeManager.Legible(normalColor);
            uiDayNumberLabel.text = day.date.Day.ToString(); uiShiftNameLabel.text = Short(day.shiftName, 9);
            if (uiHoursLabel != null) {uiHoursLabel.text="";uiHoursLabel.gameObject.SetActive(false);}
            if (uiNoteLabel != null) uiNoteLabel.text = Short(day.note, 11);
            if (dimOverlay != null) { dimOverlay.gameObject.SetActive(!day.isCurrentMonth); dimOverlay.color = ThemeManager.Token(ThemeManager.Role.DimmedDate); }
            if (noteIcon != null) { noteIcon.gameObject.SetActive(!string.IsNullOrWhiteSpace(day.note)); noteIcon.color = ThemeManager.Legible(normalColor); }
            if (alarmIcon != null) { alarmIcon.gameObject.SetActive(hasAlarm); alarmIcon.color = ThemeManager.Legible(normalColor); }

            SetSelected(false); presentationPending = true;
        }
        private static string Short(string text, int length)
        {
            text = (text ?? "").Replace('\n',' ').Replace('\r',' ').Trim();
            return text.Length > length ? text.Substring(0, length - 1) + "…" : text;
        }
        private void OnRectTransformDimensionsChange() => presentationPending = true;
        private void LateUpdate() => FlushPresentation();
        public void FlushPresentation()
        {
            if (CanvasUpdateRegistry.IsRebuildingLayout() || CanvasUpdateRegistry.IsRebuildingGraphics()) return;
            var size = ((RectTransform)transform).rect.size;
            if (day != null && shownToday != System.DateTime.Today) UpdateToday(System.DateTime.Today);
            if (!presentationPending && size == fittedSize) return;
            presentationPending = false; fittedSize = size;
            if (uiNoteLabel == null || day == null) return;
            string shift = FitText(uiShiftNameLabel, day.shiftName), note = FitText(uiNoteLabel, day.note);
            if (uiShiftNameLabel.text != shift) uiShiftNameLabel.text = shift;
            if (uiNoteLabel.text != note) uiNoteLabel.text = note;
            SetVisible(uiNoteLabel.gameObject, activities.Count==0 && size.y >= 160 && !string.IsNullOrWhiteSpace(day.note));
            if (uiHoursLabel != null) SetVisible(uiHoursLabel.gameObject,false);
            if(activityIcons!=null){int capacity=size.x>=125?3:2;int shown=Mathf.Min(capacity,activities.Count);if(activities.Count>capacity)shown--;
                for(int i=0;i<activityIcons.Length;i++){activityIcons[i].transform.parent.gameObject.SetActive(i<shown);if(i>=shown)continue;var a=activities[i].activity;activityIcons[i].sprite=ActivityIconSet.Load().Get(a.icon);activityIcons[i].color=ThemeManager.Legible(normalColor);Color border;if(!ColorUtility.TryParseHtmlString(App.AppSession.Instance.Data.profiles.Find(x=>x.id==a.assigneeId)?.color??"#64748B",out border))border=Color.gray;activityFrames[i].color=border;}
                activityOverflow.text=activities.Count>shown?"+"+(activities.Count-shown):"";activityOverflow.color=ThemeManager.Legible(normalColor);
            }
        }
        private static void SetVisible(GameObject target, bool visible) { if (target.activeSelf != visible) target.SetActive(visible); }
        public void UpdateToday(System.DateTime today)
        {
            shownToday = today.Date;
            bool current = day != null && day.date.Date == shownToday;
            if (todayOutline != null) { todayOutline.enabled = current; todayOutline.color = ThemeManager.Token(ThemeManager.Role.Accent); }
            uiDayNumberLabel.color = current ? ThemeManager.Legible(ThemeManager.Token(ThemeManager.Role.Accent)) : ThemeManager.Legible(uiBackground.color);
        }
        private static string FitText(Text label, string value)
        {
            value = (value ?? "").Replace('\n', ' ').Replace('\r', ' ').Trim();
            float width = Mathf.Max(1, label.rectTransform.rect.width - 4);
            var settings = label.GetGenerationSettings(Vector2.zero);
            bool cut = false;
            while (value.Length > 0 && label.cachedTextGeneratorForLayout.GetPreferredWidth(value + (cut ? "…" : ""), settings) / label.pixelsPerUnit > width) { value = value.Substring(0, value.Length - 1); cut = true; }
            return value + (cut ? "…" : "");
        }
        public void SetSelected(bool selected)
        {
            if (selectedOutline != null)
            {
                selectedOutline.enabled = selected;
                selectedOutline.color = ThemeManager.Token(ThemeManager.Role.Selection);
                var outline = selectedOutline.GetComponent<Outline>();
                if (outline != null) outline.effectColor = ThemeManager.Token(ThemeManager.Role.Background);
            }
            uiBackground.color = selected ? Color.Lerp(normalColor, ThemeManager.Token(ThemeManager.Role.Selection), .12f) : normalColor;
            foreach (var text in new[]{uiShiftNameLabel, uiHoursLabel, uiNoteLabel}) if (text != null) text.color = ThemeManager.Legible(uiBackground.color);
            UpdateToday(System.DateTime.Today);
        }
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            pressed = data.position; dragged = false; controller?.gesture?.Begin(data.position);
            if (controller != null && day != null && controller.IsEditing) controller.BeginDaySelection(day.dateKey);
        }
        public void OnPointerUp(PointerEventData data)
        {
            if (controller == null || day == null || data.button != PointerEventData.InputButton.Left) return;
            if (controller.IsEditing) controller.EndDaySelection(day.dateKey);
            else if (controller.gesture?.Finish(data.position)==true) {}
            else if (!dragged && Vector2.Distance(pressed, data.position) < 16) controller.TapDay(day.dateKey);
        }
        public void OnBeginDrag(PointerEventData data) => dragged = true;
        public void OnEndDrag(PointerEventData data) { if (controller != null && controller.IsEditing) controller.EndDaySelection(day.dateKey); }
        public void OnDrag(PointerEventData data)
        {
            dragged = true; if (controller == null || !controller.IsEditing) return;
            hits.Clear(); EventSystem.current.RaycastAll(data, hits);
            foreach (var hit in hits)
            {
                var cell = hit.gameObject.GetComponentInParent<CalendarDayCell>();
                if (cell?.day != null) { controller.ExtendDaySelection(cell.day.dateKey); break; }
            }
        }
    }
}
