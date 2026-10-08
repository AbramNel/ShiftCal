using UnityEngine;
namespace ShiftCal.UI
{
    public class ThemeSample : MonoBehaviour
    {
        public ThemeManager.Theme choice;
        public void Select() => ThemeManager.Select(choice);
    }
}
