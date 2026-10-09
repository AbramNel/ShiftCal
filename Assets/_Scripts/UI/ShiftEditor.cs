using ShiftCal.Core;
using ShiftCal.Data;
using UnityEngine;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    public class ShiftEditor : MonoBehaviour
    {
        public InputField nameInput, colorInput;
        public PickerField startPicker, endPicker;
        public Text nameError, timeError, colorError, hours;
        public Image swatch;
        public Button delete;
        public GameObject advanced;
        private ShiftSettingsController owner;
        private ShiftTypeDefinitionData definition;
        private string original;
        private string Signature => nameInput.text + "|" + startPicker.value + "|" + endPicker.value + "|" + colorInput.text;
        public bool HasChanges => Signature != original;
        public void Show(ShiftSettingsController controller, ShiftTypeDefinitionData data)
        {
            owner = controller; definition = data;
            nameInput.text = data?.name ?? ""; startPicker.Set(data?.startTime); endPicker.Set(data?.endTime); colorInput.text = data?.colorHex ?? "#9FF4F1";
            nameError.text = timeError.text = colorError.text = ""; delete.gameObject.SetActive(data != null && data.id >= 100); advanced.SetActive(false);
            original = Signature; gameObject.SetActive(true); transform.SetAsLastSibling();
            GetComponent<ModalPanel>().HasChanges = () => HasChanges; UpdatePreview();
        }
        public void Color(string value) { colorInput.text = value; UpdatePreview(); }
        public void ToggleAdvanced() => advanced.SetActive(!advanced.activeSelf);
        public void ClearTimes() { startPicker.Set("", true); endPicker.Set("", true); }
        public void UpdatePreview()
        {
            if (ColorUtility.TryParseHtmlString(colorInput.text, out var color)) swatch.color = color;
            bool valid = ShiftTimeUtility.TryCalculateHours(startPicker.value.Trim(), endPicker.value.Trim(), out float value);
            hours.text = valid ? value > 0 ? ShiftTimeUtility.FormatHours(value) + " per shift" : "No scheduled hours" : "Enter both times or leave both empty";
        }
        public void PreviewChanged(string value) => UpdatePreview();
        public void Save()
        {
            nameError.text = string.IsNullOrWhiteSpace(nameInput.text) ? "Enter a shift name." : "";
            timeError.text = ShiftTimeUtility.TryCalculateHours(startPicker.value.Trim(), endPicker.value.Trim(), out _) ? "" : "Choose both times, or clear times for an untimed shift.";
            colorError.text = ColorUtility.TryParseHtmlString(colorInput.text.Trim(), out _) ? "" : "Use a color like #FBBF24.";
            if (nameError.text.Length + timeError.text.Length + colorError.text.Length > 0) { if (colorError.text.Length > 0) advanced.SetActive(true); return; }
            owner.SaveShiftRow(definition, definition == null, nameInput.text.Trim(), colorInput.text.Trim(), startPicker.value.Trim(), endPicker.value.Trim());
            gameObject.SetActive(false);
        }
        public void Cancel() => AppNavigation.Instance.Back();
        public void Delete()
        {
            if (definition == null || definition.id < 100) return;
            if (!App.AppSession.Instance.CanDeleteShift(definition.id, out var reason)) { timeError.text = reason; return; }
            AppNavigation.Instance.Confirm("Delete " + definition.name + "?", () => { owner.DeleteShift(definition); gameObject.SetActive(false); });
        }
    }
}
