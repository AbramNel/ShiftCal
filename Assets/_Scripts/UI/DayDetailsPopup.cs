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

        private void Awake()
        {
            Hide();
        }

        public void Show(CalendarDayData day)
        {
            if (day == null)
                return;

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
    }
}
