using ShiftCal.Core;
using ShiftCal.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftCal.UI
{
    public class ShiftSettingRow : MonoBehaviour
    {
        [SerializeField] private Text uiNameLabel;
        [SerializeField] private Image uiColorSwatch;
        [SerializeField] private Text uiTimeLabel;
        [SerializeField] private Text uiHoursLabel;
        [SerializeField] private InputField nameInput;
        [SerializeField] private InputField startTimeInput;
        [SerializeField] private InputField endTimeInput;
        [SerializeField] private Button colorButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private Text saveButtonLabel;

        private ShiftSettingsController controller;
        private ShiftTypeDefinitionData definition;
        private bool isAddRow;
        private string colorHex = "#9FF4F1";

        public void Bind(ShiftSettingsController owner, ShiftTypeDefinitionData shiftDefinition, bool addRow)
        {
            controller = owner;
            definition = shiftDefinition;
            isAddRow = addRow;
            gameObject.SetActive(shiftDefinition != null || addRow);

            if (!gameObject.activeSelf)
                return;

            colorHex = shiftDefinition != null && !string.IsNullOrWhiteSpace(shiftDefinition.colorHex)
                ? shiftDefinition.colorHex
                : "#9FF4F1";

            SetInput(nameInput, shiftDefinition != null ? shiftDefinition.name : string.Empty);
            SetInput(startTimeInput, shiftDefinition != null ? shiftDefinition.startTime : string.Empty);
            SetInput(endTimeInput, shiftDefinition != null ? shiftDefinition.endTime : string.Empty);

            if (uiNameLabel != null)
                uiNameLabel.text = addRow ? "New shift" : shiftDefinition.name;

            if (uiColorSwatch != null)
                uiColorSwatch.color = ShiftStyleUtility.ToColor(colorHex);

            UpdateComputedLabels();

            if (saveButtonLabel != null)
                saveButtonLabel.text = addRow ? "Add" : "Save";

            if (deleteButton != null)
                deleteButton.gameObject.SetActive(!addRow && shiftDefinition != null && shiftDefinition.id >= 100);
        }

        public void CycleColor()
        {
            colorHex = colorHex == "#9FF4F1" ? "#FBBF24"
                : colorHex == "#FBBF24" ? "#FB7185"
                : colorHex == "#FB7185" ? "#34D399"
                : "#9FF4F1";

            if (uiColorSwatch != null)
                uiColorSwatch.color = ShiftStyleUtility.ToColor(colorHex);
        }

        public void Save()
        {
            string title = nameInput != null ? nameInput.text.Trim() : string.Empty;
            string start = startTimeInput != null ? startTimeInput.text.Trim() : string.Empty;
            string end = endTimeInput != null ? endTimeInput.text.Trim() : string.Empty;

            if (controller != null)
                controller.SaveShiftRow(definition, isAddRow, title, colorHex, start, end);
        }

        public void Delete()
        {
            if (controller != null && definition != null)
                controller.DeleteShift(definition);
        }

        public void UpdateComputedLabels()
        {
            string start = startTimeInput != null ? startTimeInput.text.Trim() : string.Empty;
            string end = endTimeInput != null ? endTimeInput.text.Trim() : string.Empty;

            ShiftTimeUtility.TryCalculateHours(start, end, out float hours);

            if (uiTimeLabel != null)
                uiTimeLabel.text = string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end)
                    ? "No time"
                    : start + " - " + end;

            if (uiHoursLabel != null)
                uiHoursLabel.text = ShiftTimeUtility.FormatHours(hours);
        }

        public void UpdateComputedLabelsFromInput(string _)
        {
            UpdateComputedLabels();
        }

        private static void SetInput(InputField input, string value)
        {
            if (input != null)
                input.text = value ?? string.Empty;
        }
    }
}
