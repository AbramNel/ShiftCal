using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ShiftCal.Data;

namespace ShiftCal.Core
{
    public static class RecurrenceEngine
    {
        public static TimeZoneInfo Zone(string id)
        {
            if (string.IsNullOrEmpty(id) || id == "device") return TimeZoneInfo.Local;
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException)
            {
                if (id == "America/Chicago") return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
                throw new ArgumentException("Unknown time zone: " + id);
            }
        }
        public static long Instant(DateTime date, string time, string zone)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return App.AndroidBridge.Call<long>("instant",DateKeyUtility.ToDateKey(date),time,zone);
#else
            if (!ShiftTimeUtility.TryParseTime(time, out TimeSpan clock)) throw new ArgumentException("Invalid time.");
            TimeZoneInfo tz = Zone(zone);
            DateTime wall = DateTime.SpecifyKind(date.Date + clock, DateTimeKind.Unspecified);
            // Match java.time: advance across a gap by its length; use earlier instant in a fold.
            if (tz.IsInvalidTime(wall))
            {
                var before = wall.AddHours(-3); var after = wall.AddHours(3);
                wall = wall.Add(tz.GetUtcOffset(after) - tz.GetUtcOffset(before));
            }
            var offset = tz.IsAmbiguousTime(wall) ? tz.GetAmbiguousTimeOffsets(wall).Max() : tz.GetUtcOffset(wall);
            return new DateTimeOffset(wall, offset).ToUnixTimeMilliseconds();
#endif
        }
        public static IEnumerable<DateTime> Dates(EventSeries e, DateTime from, DateTime to)
        {
            DateTime start = DateKeyUtility.FromDateKey(e.dateKey);
            DateTime limit = string.IsNullOrEmpty(e.until) ? to.Date : DateKeyUtility.FromDateKey(e.until);
            int ordinal = 0, interval = Math.Max(1, e.interval);
            for (DateTime d = start; d <= to.Date && d <= limit; d = d.AddDays(1))
            {
                int days = (d - start).Days;
                bool match = e.recurrence == RecurrenceKind.Once ? days == 0
                    : e.recurrence == RecurrenceKind.Daily ? days % interval == 0
                    : e.recurrence == RecurrenceKind.Weekly ? ((days + (int)start.DayOfWeek) / 7) % interval == 0 && e.weekdays.Contains((int)d.DayOfWeek)
                    : ((d.Year - start.Year) * 12 + d.Month - start.Month) % interval == 0 && d.Day == Math.Min(start.Day, DateTime.DaysInMonth(d.Year, d.Month));
                if (!match) continue;
                ordinal++;
                if (e.count > 0 && ordinal > e.count) yield break;
                if (d >= from.Date) yield return d;
                if (e.recurrence == RecurrenceKind.Once) yield break;
            }
        }
        public static List<Occurrence> Resolve(ScheduleSave s, DateTime from, DateTime to)
        {
            var result = new List<Occurrence>();
            foreach (var e in s.events)
            {
                foreach (DateTime d in Dates(e, from, to))
                {
                    string key = DateKeyUtility.ToDateKey(d);
                    var ex = s.exceptions.Find(x => x.seriesId == e.id && x.originalDate == key);
                    if (ex != null) continue;
                    AddEvent(result, e, e, key,e.enabled&&!s.mutedEvents.Contains(e.id));
                }
                foreach (var ex in s.exceptions.Where(x => x.seriesId == e.id && !x.cancelled && x.replacement != null))
                {
                    DateTime d = DateKeyUtility.FromDateKey(ex.replacement.dateKey);
                    if (d >= from.Date && d <= to.Date) AddEvent(result, e, ex.replacement, ex.originalDate,e.enabled&&!s.mutedEvents.Contains(e.id));
                }
            }
            if (s.group != null)
                for (DateTime d = from.Date; d <= to.Date; d = d.AddDays(1))
                {
                    string key = DateKeyUtility.ToDateKey(d);
                    int type = s.overrides.Find(o => o.dateKey == key && !o.scheduledShift)?.shiftType ?? ShiftPatternUtility.Resolve(s.group.pattern, s.group.startDateKey, d);
                    var shift = s.group.shiftTypes.Find(x => x.id == type);
                    if (shift == null || !ShiftTimeUtility.TryParseTime(shift.startTime, out _)) continue;
                    foreach (var rule in s.rules.Where(r => r.enabled && !s.mutedRules.Contains(r.id) && r.shiftType == type && (string.IsNullOrEmpty(r.groupId)||r.groupId==s.group.groupId)))
                        result.Add(new Occurrence { id = "shift:" + rule.id + ":" + key, sourceId = rule.id, dateKey = key,
                            title = shift.name + " - " + rule.label, at = Instant(d, shift.startTime, "device") - rule.beforeMinutes * 60000L,
                            audible = rule.audible, vibration = rule.vibration, sound = rule.sound, snoozeMinutes = rule.snoozeMinutes, advanceMinutes = rule.advanceMinutes });
                }
            return result.OrderBy(x => x.at).ToList();
        }
        private static void AddEvent(List<Occurrence> list, EventSeries series, EventSeries detail, string original,bool alerts)
        {
            long at = Instant(DateKeyUtility.FromDateKey(detail.dateKey), detail.startTime, detail.zone);
            // Non-exception detail has series start date; occurrences use the original date.
            if (ReferenceEquals(series, detail)) at = Instant(DateKeyUtility.FromDateKey(original), series.startTime, series.zone);
            alerts=alerts&&detail.enabled;
            if (!alerts || !detail.alarm && !detail.reminder) list.Add(Make(series, detail, original, at, false, "view"));
            if (alerts&&detail.reminder) list.Add(Make(series, detail, original, at - detail.reminderMinutes * 60000L, false, "reminder"));
            if (alerts&&detail.alarm) list.Add(Make(series, detail, original, at, true, "alarm"));
        }
        private static Occurrence Make(EventSeries e, EventSeries detail, string key, long at, bool audible, string kind)
        {
            return new Occurrence { id = "event:" + e.id + ":" + key + ":" + kind, sourceId = e.id, dateKey = key,
                calendarDateKey=ReferenceEquals(e,detail)?key:detail.dateKey,
                title = detail.title + (kind == "reminder" ? " - reminder" : ""), at = at, audible = audible, vibration = detail.vibration,
                sound = detail.sound, snoozeMinutes = detail.snoozeMinutes, advanceMinutes = audible ? detail.advanceMinutes : 0, isEvent = true };
        }
        public static List<Occurrence> EventDates(ScheduleSave save,EventSeries e,DateTime from,DateTime to)
        {
            var result=new List<Occurrence>();
            foreach(var d in Dates(e,from,to))
            {
                string key=DateKeyUtility.ToDateKey(d);
                if(!save.exceptions.Exists(x=>x.seriesId==e.id&&x.originalDate==key))result.Add(Make(e,e,key,Instant(d,e.startTime,e.zone),false,"view"));
            }
            foreach(var ex in save.exceptions.Where(x=>x.seriesId==e.id&&!x.cancelled&&x.replacement!=null))
            {
                var detail=ex.replacement;DateTime date=DateKeyUtility.FromDateKey(detail.dateKey);
                if(date>=from.Date&&date<=to.Date)result.Add(Make(e,detail,ex.originalDate,Instant(date,detail.startTime,detail.zone),false,"view"));
            }
            return result.OrderBy(x=>x.at).ToList();
        }
        public static string Summary(EventSeries e)
        {
            string cadence = e.recurrence == RecurrenceKind.Once ? "One time"
                : e.recurrence == RecurrenceKind.Daily ? "Every " + e.interval + " day(s)"
                : e.recurrence == RecurrenceKind.Weekly ? "Every " + e.interval + " week(s): " + string.Join(", ", e.weekdays.Select(d => CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedDayNames[d]))
                : "Every " + e.interval + " month(s), day " + DateKeyUtility.FromDateKey(e.dateKey).Day + " (last day in shorter months)";
            return cadence + " at " + e.startTime + " / " + (e.zone == "device" ? "Device local" : e.zone)
                + (e.count > 0 ? ", " + e.count + " occurrences" : "") + (string.IsNullOrEmpty(e.until) ? "" : ", until " + e.until);
        }
        public static bool Validate(EventSeries e, out string error)
        {
            error = "";
            try
            {
                if (string.IsNullOrWhiteSpace(e.title)) throw new ArgumentException("Enter an event title.");
                DateTime date = DateKeyUtility.FromDateKey(e.dateKey);
                if (date.Year < 2000 || date.Year > 2100) throw new ArgumentException("Use a date from 2000 to 2100.");
                Instant(date, e.startTime, e.zone);
                if (!string.IsNullOrEmpty(e.endTime) && !ShiftTimeUtility.TryParseTime(e.endTime, out _)) throw new ArgumentException("Invalid end time.");
                if (e.interval < 1 || e.interval > 365 || e.count < 0 || e.count > 10000) throw new ArgumentException("Interval 1-365; count 0-10000.");
                if (e.recurrence == RecurrenceKind.Weekly && e.weekdays.Count == 0) throw new ArgumentException("Choose at least one weekday.");
                if (!string.IsNullOrEmpty(e.until) && DateKeyUtility.FromDateKey(e.until) < date) throw new ArgumentException("End date must follow start date.");
                if (e.reminderMinutes < 0 || e.reminderMinutes > 10080 || e.advanceMinutes < 0 || e.advanceMinutes > 10080 || e.snoozeMinutes < 1 || e.snoozeMinutes > 120) throw new ArgumentException("Invalid reminder or snooze duration.");
                if(e.reminder&&e.alarm&&e.reminderMinutes==0)throw new ArgumentException("Use at least 1 minute for an advance reminder when the event also has an alarm.");
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }
    }
}
