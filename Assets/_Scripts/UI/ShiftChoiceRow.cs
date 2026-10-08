using System;
using ShiftCal.Core;
using ShiftCal.Data;
using UnityEngine;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    public class ShiftChoiceRow : MonoBehaviour
    {
        public Image swatch;
        public Text title, time;
        public Button button;
        public void Bind(ShiftTypeDefinitionData shift, Action apply)
        {
            swatch.color = ShiftStyleUtility.ToColor(shift.colorHex);
            title.text = shift.name;
            time.text = string.IsNullOrWhiteSpace(shift.startTime) ? "No scheduled hours" : shift.startTime + " – " + shift.endTime;
            button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => apply());
        }
    }
}
