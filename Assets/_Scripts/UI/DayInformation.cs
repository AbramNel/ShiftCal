using System;
using System.Collections.Generic;
using System.Linq;
using ShiftCal.Core;
using ShiftCal.Data;

namespace ShiftCal.UI
{
    // One recurrence pass and one native ledger snapshot per visible calendar refresh.
    public class DayInformation
    {
        public readonly List<Occurrence> events = new List<Occurrence>();
        public readonly List<Occurrence> alarms = new List<Occurrence>();
        public bool HasExtra(CalendarDayData day) => !string.IsNullOrWhiteSpace(day.note) || !string.IsNullOrWhiteSpace(day.personName) || events.Count > 0 || alarms.Count > 0;
        public static Dictionary<string, DayInformation> Resolve(ScheduleSave save, DateTime from, DateTime to)
        {
            var result = new Dictionary<string, DayInformation>();
            Func<string, DayInformation> get = key => { if (!result.TryGetValue(key, out var value)) result[key] = value = new DayInformation(); return value; };
            foreach (var series in save.events)
                foreach (var occurrence in RecurrenceEngine.EventDates(save, series, from, to)) get(occurrence.calendarDateKey).events.Add(occurrence);
            foreach (var occurrence in RecurrenceEngine.ShiftDates(save, from, to, true)) get(occurrence.calendarDateKey).events.Add(occurrence);
            var states = App.AndroidBridge.DeliveryStates();
            long now = DateKeyUtility.UnixMsNow();
            foreach (var info in result.Values)
                foreach (var occurrence in info.events.Where(o => !o.isEvent))
                    if (states.TryGetValue(occurrence.id, out var state))
                    {
                        occurrence.state = state.state;
                        if (state.state == "ringing" || state.state == "snoozed") occurrence.at = state.at;
                    }
            foreach (var occurrence in RecurrenceEngine.Resolve(save, from, to))
            {
                if (occurrence.id.EndsWith(":view") || occurrence.state == "view" || !occurrence.isEvent && !occurrence.audible) continue;
                if (states.TryGetValue(occurrence.id, out var state))
                {
                    if (state.state != "ringing" && state.state != "snoozed") continue;
                    occurrence.state = state.state; occurrence.at = state.at;
                }
                else if (occurrence.at < now) continue;
                get(string.IsNullOrEmpty(occurrence.calendarDateKey) ? occurrence.dateKey : occurrence.calendarDateKey).alarms.Add(occurrence);
            }
            return result;
        }
        public string Describe(CalendarDayData day)
        {
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(day.startTime)) lines.Add(day.startTime + " – " + day.endTime + "  •  " + ShiftTimeUtility.FormatHours(day.hours));
            if (!string.IsNullOrWhiteSpace(day.personName)) lines.Add(day.personName);
            if (!string.IsNullOrWhiteSpace(day.note)) lines.Add(day.note);
            if (events.Count > 0) { lines.Add("EVENTS"); lines.AddRange(events.Select(EventLine)); }
            if (alarms.Any(o=>o.isEvent)) { lines.Add("ALARMS & REMINDERS"); lines.AddRange(alarms.Where(o => o.isEvent).Select(o => o.title + "  •  " + Time(o) + (string.IsNullOrEmpty(o.state) ? "" : "  (" + o.state + ")"))); }
            return string.Join("\n\n", lines);
        }
        public static string EventLine(Occurrence o) => o.title+" • "+Time(o)
            +(string.IsNullOrEmpty(o.subtitle)?"":"\n"+o.subtitle)
            +(!o.isEvent&&!string.IsNullOrEmpty(o.state)&&o.state!="view"?" ("+o.state+")":"")
            +(string.IsNullOrWhiteSpace(o.notes)?"":"\n"+o.notes);
        public static string Time(Occurrence o) => DateTimeOffset.FromUnixTimeMilliseconds(o.at).ToLocalTime().ToString("MMM d, h:mm tt");
    }
}
