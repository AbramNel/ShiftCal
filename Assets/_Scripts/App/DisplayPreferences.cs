using System;
using UnityEngine;
namespace ShiftCal.App
{
    // Device appearance preferences deliberately live outside account/calendar saves.
    public static class DisplayPreferences
    {
        public const string LoggedHoursKey="ShiftCal.Display.ShowLoggedHoursTotal.v1";
        public static event Action Changed;
        public static bool ShowLoggedHoursTotal
        {
            get=>PlayerPrefs.GetInt(LoggedHoursKey,1)!=0;
            set { if(ShowLoggedHoursTotal==value)return;PlayerPrefs.SetInt(LoggedHoursKey,value?1:0);PlayerPrefs.Save();Changed?.Invoke(); }
        }
    }
}
