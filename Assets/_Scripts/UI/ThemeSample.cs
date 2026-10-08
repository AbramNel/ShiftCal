using UnityEngine;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    [ExecuteAlways, RequireComponent(typeof(Button))]
    public class ThemeSample : MonoBehaviour
    {
        public ThemeManager.Theme choice;
        public Image selectedBorder;
        public Text selectedLabel;
        public bool IsSelected => ThemeManager.Current == choice;
        private void OnEnable() { ThemeManager.Changed += Refresh; Refresh(); }
        private void OnDisable() => ThemeManager.Changed -= Refresh;
        public void Select() => ThemeManager.Select(choice);
        public void Refresh()
        {
            if (selectedBorder != null) { selectedBorder.enabled = IsSelected; selectedBorder.color = ThemeManager.Token(ThemeManager.Role.Selection); }
            if (selectedLabel != null) selectedLabel.text = IsSelected ? "Selected" : "Tap to apply";
        }
    }
}
