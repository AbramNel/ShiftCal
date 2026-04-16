using System;
using System.Collections.Generic;
using UnityEngine;
using ShiftCal.Core;
using ShiftCal.Data;

namespace ShiftCal.UI
{
    public class CalendarController : MonoBehaviour
    {
        public DateTime currentMonth;

        private Dictionary<string, DayOverrideData> overrides = new Dictionary<string, DayOverrideData>();

        private void Start()
        {
            currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            Refresh();
        }

        public void NextMonth()
        {
            currentMonth = currentMonth.AddMonths(1);
            Refresh();
        }

        public void PrevMonth()
        {
            currentMonth = currentMonth.AddMonths(-1);
            Refresh();
        }

        public void Refresh()
        {
            var group = ShiftCal.App.AppSession.Instance.CurrentGroup;
            List<CalendarDayData> days = CalendarGenerator.Generate(currentMonth, group, overrides);

            Debug.Log("Generated " + days.Count + " days");
        }
    }
}
