using System;
using System.Globalization;
using ShiftCal.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ShiftCal.UI
{
    // A button field: it never selects an InputField or opens a keyboard.
    public class PickerField : MonoBehaviour
    {
        public bool isDate;
        public bool optional;
        public Text caption;
        public string value;
        public UnityEvent<string> changed = new UnityEvent<string>();
        public void Set(string selected, bool notify = false)
        {
            value = selected ?? "";
            string display = "Choose " + (isDate ? "date" : "time");
            if (isDate && DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) display = date.ToString("MMM d, yyyy");
            if (!isDate && ShiftTimeUtility.TryParseTime(value, out var time)) display = DateTime.Today.Add(time).ToString("t");
            caption.text = display;
            if (notify) changed.Invoke(value);
        }
        public void Open() => PickerCoordinator.Open(this);
        private void OnDisable() => PickerCoordinator.Cancel(this);
    }
}
