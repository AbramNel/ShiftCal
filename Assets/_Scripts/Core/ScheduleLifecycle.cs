using System;
using System.Collections.Generic;
using System.Linq;
using ShiftCal.Data;
namespace ShiftCal.Core
{
    public enum SchedulePhase { Active, Paused, Past }
    // A presentation projection; no archive flag, recurrence copies or ledger mutations.
    public static class ScheduleLifecycle
    {
        public static bool InFlight(IEnumerable<Occurrence> states,string id)=>states.Any(o=>(o.sourceId==id||o.id!=null&&o.id.StartsWith("event:"+id+":"))&&(o.state=="ringing"||o.state=="snoozed"));
        static DateTime Horizon(DateTime start,DateTime now,IEnumerable<string> replacements)
        {
            var end=now.AddYears(2);if(start>end)end=start;
            foreach(string key in replacements.Where(k=>!string.IsNullOrEmpty(k))){var date=DateKeyUtility.FromDateKey(key);if(date>end)end=date;}
            return end;
        }
        static bool Exhausted(ScheduleSave save,EventSeries e,DateTime horizon)
        {
            if(e.recurrence==RecurrenceKind.Once)return true;
            if(!string.IsNullOrEmpty(e.until)&&DateKeyUtility.FromDateKey(e.until)<=horizon)return true;
            return e.count>0&&RecurrenceEngine.Dates(e,DateKeyUtility.FromDateKey(e.dateKey),horizon,save).Take(e.count).Count()>=e.count;
        }
        public static long End(EventSeries e,string key)
        {
            var date=DateKeyUtility.FromDateKey(key);long start=RecurrenceEngine.Instant(date,e.startTime,e.zone);
            if(string.IsNullOrEmpty(e.endTime))return start;
            long end=RecurrenceEngine.Instant(date,e.endTime,e.zone);return end<=start?RecurrenceEngine.Instant(date.AddDays(1),e.endTime,e.zone):end;
        }
        public static SchedulePhase Event(ScheduleSave save,EventSeries e,IEnumerable<EventException> exceptions,IEnumerable<Occurrence> states,long now)
        {
            if(InFlight(states,e.id))return SchedulePhase.Active;
            if(!e.enabled||save.mutedEvents.Contains(e.id))return SchedulePhase.Paused;
            var edits=exceptions.Where(x=>x.seriesId==e.id).ToList();var start=DateKeyUtility.FromDateKey(e.dateKey);
            var today=TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(now),RecurrenceEngine.Zone(e.zone)).Date;
            var horizon=Horizon(start,today,edits.Where(x=>!x.cancelled).Select(x=>x.replacement?.dateKey));
            var projection=new ScheduleSave{group=save.group,overrides=save.overrides,exceptions=edits};
            foreach(var occurrence in RecurrenceEngine.EventDates(projection,e,start,horizon))
            {
                var detail=edits.Find(x=>x.originalDate==occurrence.dateKey)?.replacement??e;
                if(End(detail,occurrence.calendarDateKey)>now)return SchedulePhase.Active;
            }
            return Exhausted(save,e,horizon)?SchedulePhase.Past:SchedulePhase.Active;
        }
        public static SchedulePhase Activity(ScheduleSave save,CalendarActivity a,IEnumerable<Occurrence> states,long now)
        {
            if(InFlight(states,"activity-"+a.id))return SchedulePhase.Active;
            var start=DateKeyUtility.FromDateKey(a.dateKey);var today=TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(now),RecurrenceEngine.Zone(a.zone)).Date;
            var edits=save.activityExceptions.Where(x=>x.seriesId==a.id).ToList();var horizon=Horizon(start,today,edits.Where(x=>!x.cancelled).Select(x=>x.replacement?.dateKey));
            var projection=new ScheduleSave{group=save.group,overrides=save.overrides,activities=new List<CalendarActivity>{a},activityExceptions=edits};
            foreach(var item in ActivityResolver.Resolve(projection,start,horizon))
            {
                long end=ActivityEnd(item.activity,item.dateKey);
                if(end>now)return SchedulePhase.Active;
            }
            return Exhausted(save,ActivityResolver.AsEvent(a),horizon)?SchedulePhase.Past:SchedulePhase.Active;
        }
        public static long ActivityEnd(CalendarActivity a,string key)=>a.allDay||string.IsNullOrEmpty(a.startTime)?RecurrenceEngine.Instant(DateKeyUtility.FromDateKey(key).AddDays(1),"00:00",a.zone):RecurrenceEngine.Instant(DateKeyUtility.FromDateKey(key),a.startTime,a.zone)+(long)a.durationMinutes*60000;
        public static bool CanCleanPersonal(ScheduleSave save,PersonalAlarmDefinition alarm,IEnumerable<Occurrence> states,long now)=>alarm.schedule.recurrence==RecurrenceKind.Once&&alarm.exceptions.Count==0&&End(alarm.schedule,alarm.schedule.dateKey)<now&&Event(save,alarm.schedule,alarm.exceptions,states,now)==SchedulePhase.Past;
        public static bool CanCleanEvent(ScheduleSave save,EventSeries e,IEnumerable<Occurrence> states,long now)=>e.recurrence==RecurrenceKind.Once&&!save.exceptions.Any(x=>x.seriesId==e.id)&&End(e,e.dateKey)<now&&!save.records.Any(r=>r.key=="event/"+e.id&&(r.conflict||r.pending))&&Event(save,e,save.exceptions,states,now)==SchedulePhase.Past;
        public static bool CanCleanActivity(ScheduleSave save,CalendarActivity a,IEnumerable<Occurrence> states,long now)=>a.recurrence==RecurrenceKind.Once&&ActivityEnd(a,a.dateKey)<now&&!save.activityExceptions.Any(x=>x.seriesId==a.id)&&!save.records.Any(r=>r.key=="activity/"+a.id&&(r.conflict||r.pending))&&Activity(save,a,states,now)==SchedulePhase.Past;
    }
}
