using System;
using System.Collections.Generic;
using ShiftCal.Data;

namespace ShiftCal.Core
{
    public static class CalendarGenerator
    {
        public static List<CalendarDayData> Generate(DateTime month, GroupData group, Dictionary<string, DayOverrideData> overrides)
        {
            List<CalendarDayData> days = new List<CalendarDayData>(42);

            DateTime start = DateKeyUtility.FirstVisibleGridDate(month);

            for (int i = 0; i < 42; i++)
            {
                DateTime date = start.AddDays(i);
                string key = DateKeyUtility.ToDateKey(date);

                int baseShift = 0;
                if (group != null)
                    baseShift = ShiftPatternUtility.Resolve(group.pattern, group.startDateKey, date);

                CalendarDayData d = new CalendarDayData
                {
                    date = date,
                    dateKey = key,
                    isCurrentMonth = date.Month == month.Month,
                    baseShift = baseShift
                };

                if (overrides != null && overrides.TryGetValue(key, out DayOverrideData o))
                {
                    d.hasOverride = true;
                    d.overrideShift = o.shiftType;
                    d.note = o.note;
                }

                days.Add(d);
            }

            return days;
        }
    }
}
