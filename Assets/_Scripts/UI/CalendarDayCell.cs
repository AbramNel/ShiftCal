using ShiftCal.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShiftCal.UI
{
    public class CalendarDayCell : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerUpHandler
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
            normalColor = day.isCurrentMonth ? shiftColor : Fade(shiftColor, 0.32f);

            if (uiBackground != null)
                uiBackground.color = normalColor;

            if (uiDayNumberLabel != null)
                uiDayNumberLabel.text = day.date.Day.ToString();

            if (uiShiftNameLabel != null)
                uiShiftNameLabel.text = day.shiftName;

            if (uiHoursLabel != null)
                uiHoursLabel.text = ShiftTimeUtility.FormatHours(day.hours);

            if (uiNoteLabel != null)
                uiNoteLabel.text = day.hasOverride && !string.IsNullOrWhiteSpace(day.note) ? day.note : string.Empty;

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
    }
}
