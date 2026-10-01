using ShiftCal.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShiftCal.UI
{
    public class CalendarDayCell : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerUpHandler, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        [SerializeField] private Image uiBackground;
        [SerializeField] private Text uiDayNumberLabel;
        [SerializeField] private Text uiShiftNameLabel;
        [SerializeField] private Text uiNoteLabel;
        [SerializeField] private Text uiHoursLabel;
        [SerializeField] private Image selectedOutline;

        private CalendarController controller;
        private CalendarDayData day;
        private Color normalColor;

        public void Bind(CalendarController owner, CalendarDayData dayData)
        {
            controller = owner;
            day = dayData;

            if (day == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            Color shiftColor = ShiftStyleUtility.ToColor(day.shiftColorHex);
            if(ThemeManager.IsDark)shiftColor=Color.Lerp(new Color(.08f,.10f,.12f),shiftColor,.3f);
            normalColor = day.isCurrentMonth ? shiftColor : Fade(shiftColor, 0.32f);
            foreach(var text in new[]{uiDayNumberLabel,uiShiftNameLabel,uiHoursLabel,uiNoteLabel})if(text!=null)text.color=ThemeManager.IsDark?new Color(.94f,.95f,.96f):new Color(.04f,.07f,.09f);

            if (uiBackground != null)
                uiBackground.color = normalColor;

            if (uiDayNumberLabel != null)
                uiDayNumberLabel.text = day.date.Day.ToString();

            if (uiShiftNameLabel != null)
                uiShiftNameLabel.text = day.shiftName;

            if (uiHoursLabel != null)
                uiHoursLabel.text = ShiftTimeUtility.FormatHours(day.hours);

            if (uiNoteLabel != null)
            {
                int count=day.eventCount;
                uiNoteLabel.text = count>0?count+" event(s)":!string.IsNullOrWhiteSpace(day.personName) ? day.personName : day.note ?? "";
            }

            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (selectedOutline != null)
                selectedOutline.enabled = selected;

            if (uiBackground != null)
                uiBackground.color = selected ? Color.Lerp(normalColor, Color.white, 0.28f) : normalColor;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (controller != null && day != null)
                controller.BeginDaySelection(day.dateKey);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (controller != null && day != null)
                controller.ExtendDaySelection(day.dateKey);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (controller != null && day != null)
                controller.EndDaySelection(day.dateKey);
        }

        private static Color Fade(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
        public void OnBeginDrag(PointerEventData eventData) { }
        public void OnEndDrag(PointerEventData eventData) { }
        public void OnDrag(PointerEventData eventData)
        {
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, hits);
            foreach (var hit in hits)
            {
                var cell = hit.gameObject.GetComponentInParent<CalendarDayCell>();
                if (cell != null && cell.day != null) { controller.ExtendDaySelection(cell.day.dateKey); break; }
            }
        }
    }
}
