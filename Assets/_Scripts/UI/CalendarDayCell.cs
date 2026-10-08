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
        private bool dragged;
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        public string DateKey => day?.dateKey;
        public bool IsSelected => selectedOutline != null && selectedOutline.enabled;
        public void Bind(CalendarController owner, CalendarDayData data, bool hasAlarm = false)
        {
            controller = owner; day = data; gameObject.SetActive(day != null); if (day == null) return;
            normalColor = ThemeManager.ShiftColor(day.shiftColorHex);
            uiBackground.color = normalColor;
            foreach (var text in new[]{uiDayNumberLabel,uiShiftNameLabel,uiHoursLabel,uiNoteLabel}) if (text != null) text.color = ThemeManager.Legible(normalColor);
            uiDayNumberLabel.text = day.date.Day.ToString(); uiShiftNameLabel.text = Short(day.shiftName, 9);
            if (uiHoursLabel != null) uiHoursLabel.text = ShiftTimeUtility.FormatHours(day.hours);
            if (uiNoteLabel != null) uiNoteLabel.text = Short(day.note, 11);
            if (dimOverlay != null) { dimOverlay.gameObject.SetActive(!day.isCurrentMonth); dimOverlay.color = ThemeManager.Token(ThemeManager.Role.DimmedDate); }
            if (noteIcon != null) { noteIcon.gameObject.SetActive(!string.IsNullOrWhiteSpace(day.note)); noteIcon.color = ThemeManager.Legible(normalColor); }
            if (alarmIcon != null) { alarmIcon.gameObject.SetActive(hasAlarm); alarmIcon.color = ThemeManager.Legible(normalColor); }
            if (todayOutline != null) { todayOutline.enabled = day.date.Date == System.DateTime.Today; todayOutline.color = ThemeManager.Token(ThemeManager.Role.Accent); }
            SetSelected(false); Adapt();
        }
        private static string Short(string text, int length)
        {
            text = (text ?? "").Replace('\n',' ').Replace('\r',' ').Trim();
            return text.Length > length ? text.Substring(0, length - 1) + "…" : text;
        }
        private void OnRectTransformDimensionsChange() => Adapt();
        private void Adapt()
        {
            if (uiNoteLabel == null) return;
            float height = ((RectTransform)transform).rect.height;
            if (day != null) { uiShiftNameLabel.text = FitText(uiShiftNameLabel, day.shiftName); uiNoteLabel.text = FitText(uiNoteLabel, day.note); }
            uiNoteLabel.gameObject.SetActive(height >= 160 && day != null && !string.IsNullOrWhiteSpace(day.note));
            if (uiHoursLabel != null) uiHoursLabel.gameObject.SetActive(height >= 185 && day != null && day.hours > 0);
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
            if (selectedOutline != null) { selectedOutline.enabled = selected; selectedOutline.color = ThemeManager.Token(ThemeManager.Role.Selection); var outline = selectedOutline.GetComponent<Outline>(); if (outline != null) outline.effectColor = new Color(.3f,.65f,1); }
        }
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            pressed = data.position; dragged = false;
            if (controller != null && day != null && controller.IsEditing) controller.BeginDaySelection(day.dateKey);
        }
        public void OnPointerUp(PointerEventData data)
        {
            if (controller == null || day == null || data.button != PointerEventData.InputButton.Left) return;
            if (controller.IsEditing) controller.EndDaySelection(day.dateKey);
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
