using System;
using System.Globalization;
using System.Linq;
using ShiftCal.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftCal.UI
{
    public class UnityPickerDialog : MonoBehaviour
    {
        public GameObject dateSection, timeSection, clearButton;
        public Text heading;
        public Dropdown year, month, day, hour, minute;
        private string token;
        private bool isDate, hooked;
        public void Show(PickerField field, string request)
        {
            if (!hooked) { hooked = true; year.onValueChanged.AddListener(_ => Days()); month.onValueChanged.AddListener(_ => Days()); }
            token = request; isDate = field.isDate;
            gameObject.SetActive(true); transform.SetAsLastSibling();
            GetComponent<ModalPanel>().Close = Cancel;
            dateSection.SetActive(isDate); timeSection.SetActive(!isDate); clearButton.SetActive(field.optional);
            heading.text = isDate ? "Choose date" : "Choose time";
            if (isDate)
            {
                var selected = DateTime.TryParse(field.value, out var date) ? date : DateTime.Today;
                Options(year, Enumerable.Range(2000, 101).Select(x => x.ToString()).ToArray());
                Options(month, CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedMonthNames.Take(12).ToArray());
                year.SetValueWithoutNotify(Mathf.Clamp(selected.Year - 2000, 0, 100)); month.SetValueWithoutNotify(selected.Month - 1);
                Days(); day.SetValueWithoutNotify(selected.Day - 1);
            }
            else
            {
                var selected = ShiftTimeUtility.TryParseTime(field.value, out var time) ? time : new TimeSpan(13, 0, 0);
                Options(hour, Enumerable.Range(0, 24).Select(x => DateTime.Today.AddHours(x).ToString("tt").Length == 0 ? x.ToString("00") : DateTime.Today.AddHours(x).ToString("h tt")).ToArray());
                Options(minute, Enumerable.Range(0, 60).Select(x => x.ToString("00")).ToArray());
                hour.SetValueWithoutNotify(selected.Hours); minute.SetValueWithoutNotify(selected.Minutes);
            }
        }
        private void Days()
        {
            int selected = day.value;
            Options(day, Enumerable.Range(1, DateTime.DaysInMonth(2000 + year.value, 1 + month.value)).Select(x => x.ToString()).ToArray());
            day.SetValueWithoutNotify(Mathf.Min(selected, day.options.Count - 1));
        }
        private static void Options(Dropdown dropdown, string[] values) { dropdown.ClearOptions(); dropdown.AddOptions(values.ToList()); }
        public void Accept()
        {
            string value = isDate ? new DateTime(2000 + year.value, month.value + 1, day.value + 1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : hour.value.ToString("00") + ":" + minute.value.ToString("00");
            PickerCoordinator.Complete(token, true, value); gameObject.SetActive(false);
        }
        public void Clear() { PickerCoordinator.Complete(token, true, ""); gameObject.SetActive(false); }
        public void Cancel() { PickerCoordinator.Complete(token, false, ""); gameObject.SetActive(false); }
    }
}
