using ShiftCal.Core;
using ShiftCal.Data;
using UnityEngine;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    public class ShiftSettingRow : MonoBehaviour
    {
        [SerializeField] private Text uiNameLabel, uiTimeLabel;
        [SerializeField] private Image uiColorSwatch;
        private ShiftSettingsController controller;
        private ShiftTypeDefinitionData definition;
        public void Bind(ShiftSettingsController owner, ShiftTypeDefinitionData data, bool addRow)
        {
            controller = owner; definition = data; gameObject.SetActive(data != null || addRow); if (!gameObject.activeSelf) return;
            uiNameLabel.text = addRow ? "+ Add new shift" : data.name;
            uiColorSwatch.gameObject.SetActive(!addRow);
            if (data != null) uiColorSwatch.color = ShiftStyleUtility.ToColor(data.colorHex);
            uiTimeLabel.text = addRow ? "Choose a name, color and hours" : string.IsNullOrWhiteSpace(data.startTime) ? "No scheduled hours" : data.startTime + " – " + data.endTime + "  •  " + ShiftTimeUtility.FormatHours(data.hours);
        }
        public void Edit() => controller.editor.Show(controller, definition);
    }
}
