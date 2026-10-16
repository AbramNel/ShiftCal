using UnityEngine;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    public class DisplaySettingsPanel : MonoBehaviour
    {
        public Toggle loggedHours;
        void OnEnable(){App.DisplayPreferences.Changed+=Refresh;Refresh();}
        void OnDisable(){App.DisplayPreferences.Changed-=Refresh;}
        public void Refresh(){if(loggedHours!=null)loggedHours.SetIsOnWithoutNotify(App.DisplayPreferences.ShowLoggedHoursTotal);}
        public void SetLoggedHours(bool value)=>App.DisplayPreferences.ShowLoggedHoursTotal=value;
        public void ReviewMonthlyHours(){var calendar=FindFirstObjectByType<CalendarController>(FindObjectsInactive.Include);WorkedHoursEditor.Instance.ShowMonth(calendar.currentMonth);}
    }
}
