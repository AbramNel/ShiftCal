using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ShiftCal.Data;

namespace ShiftCal.Core
{
    // Allowlisted presentation data only. No account IDs, alarm state or worked-hours store.
    public sealed class CalendarExportSnapshot
    {
        public string title, filterLabel, fileName;
        public readonly List<CalendarExportDay> days = new List<CalendarExportDay>(42);

        public static CalendarExportSnapshot Create(ScheduleSave save, DateTime month,
            Dictionary<string, DayOverrideData> overrides, string filter, string uid)
        {
            month = new DateTime(month.Year, month.Month, 1);
            var result = new CalendarExportSnapshot {
                title = month.ToString("MMMM yyyy"),
                fileName = "ShiftCal-" + month.ToString("MMMM-yyyy", CultureInfo.InvariantCulture) + ".png",
                filterLabel = filter == "mine" ? "Activities: Mine" : filter == "unassigned" ? "Activities: Unassigned" :
                    string.IsNullOrEmpty(filter) || filter == "everyone" ? "" : "Activities: " + (save.profiles.Find(p => p.id == filter)?.name ?? "Selected person")
            };
            var days = CalendarGenerator.Generate(month, save.group, overrides);
            // A shallow projection uses the current in-memory overrides, as the main calendar does.
            var shared = new ScheduleSave { account = save.account, group = save.group,
                overrides = new List<DayOverrideData>(overrides.Values), profiles = save.profiles,
                activities = save.activities, activityExceptions = save.activityExceptions };
            var activities = ActivityResolver.Resolve(shared, days[0].date, days[41].date, filter, uid);
            foreach (var day in days)
            {
                var cell = new CalendarExportDay { dateKey = day.dateKey, dayNumber = day.date.Day,
                    currentMonth = day.isCurrentMonth, shiftName = day.shiftName, shiftColor = day.shiftColorHex,
                    hasNote = !string.IsNullOrWhiteSpace(day.note) };
                foreach (var activity in activities.Where(a => a.dateKey == day.dateKey))
                    cell.activities.Add(new CalendarExportActivity { icon = activity.activity.icon,
                        assignmentColor = save.profiles.Find(p => p.id == activity.activity.assigneeId)?.color ?? "#64748B" });
                result.days.Add(cell);
            }
            return result;
        }
    }
    public sealed class CalendarExportDay
    {
        public string dateKey, shiftName, shiftColor;
        public int dayNumber;
        public bool currentMonth, hasNote;
        public readonly List<CalendarExportActivity> activities = new List<CalendarExportActivity>();
    }
    public sealed class CalendarExportActivity { public string icon, assignmentColor; }
}
