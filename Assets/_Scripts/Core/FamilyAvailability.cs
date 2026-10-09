using System;
using System.Collections.Generic;
using System.Linq;
using ShiftCal.Data;
namespace ShiftCal.Core
{
    public static class FamilyAvailability
    {
        public static string Check(ScheduleSave s,IEnumerable<string> people,DateTime date,string time,int minutes,string excludedId=null,string zone="device")
        {
            var ids=people.Where(x=>!string.IsNullOrEmpty(x)).Distinct().ToList();
            if(ids.Count==0)return "Choose people to check.";
            if(string.IsNullOrEmpty(time)||minutes<=0)return "Unknown · choose a time and duration to check overlaps.";
            long start=RecurrenceEngine.Instant(date,time,zone),end=start+(long)minutes*60000;
            var activities=ActivityResolver.Resolve(s,date.AddDays(-8),date.AddDays(8));var busy=new List<string>();bool unknown=false,covered=true;
            foreach(string id in ids)
            {
                string name=s.profiles.Find(x=>x.id==id)?.name??"Person";
                foreach(var a in activities.Where(x=>x.seriesId!=excludedId&&(x.activity.assigneeId==id||x.activity.participants.Contains(id))))
                {
                    if(a.activity.allDay){long until=RecurrenceEngine.Instant(DateKeyUtility.FromDateKey(a.dateKey).AddDays(1),"12:00 AM",a.activity.zone);if(a.start<end&&start<until)busy.Add(name+": all-day "+a.activity.title);continue;}
                    if(!a.HasTime||a.activity.durationMinutes<=0){if(string.CompareOrdinal(a.dateKey,DateKeyUtility.ToDateKey(date))>=0&&a.start<end)unknown=true;continue;}
                    if(a.start<end&&start<a.end)busy.Add(name+": "+a.activity.title);
                }
                if(s.group.shiftOwnerProfileId!=id){covered=false;continue;}
                // Work shifts use the device zone. Cover the whole requested interval,
                // including a previous overnight shift and work on following dates.
                var first=TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(start),TimeZoneInfo.Local).Date.AddDays(-1);
                var last=TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(end-1),TimeZoneInfo.Local).Date;
                for(var d=first;d<=last;d=d.AddDays(1))
                {
                    string key=DateKeyUtility.ToDateKey(d);var o=s.overrides.Find(x=>x.dateKey==key);
                    int shift=o!=null&&!o.scheduledShift?o.shiftType:ShiftPatternUtility.Resolve(s.group.pattern,s.group.startDateKey,d);
                    var def=s.group.shiftTypes.Find(x=>x.id==shift);
                    if(def==null){unknown=true;continue;}if(shift==(int)ShiftTypeId.Off)continue;
                    if(string.IsNullOrEmpty(def.startTime)||string.IsNullOrEmpty(def.endTime)){if(d>=first.AddDays(1))unknown=true;continue;}
                    long ss=RecurrenceEngine.Instant(d,def.startTime,"device");
                    long se=RecurrenceEngine.Instant(d,def.endTime,"device");
                    if(se<=ss)se=RecurrenceEngine.Instant(d.AddDays(1),def.endTime,"device");
                    if(ss<end&&start<se)busy.Add(name+": "+def.name);
                }
            }
            if(busy.Count>0)return "Busy · "+string.Join("; ",busy.Distinct());
            if(unknown)return "Unknown · an untimed activity, missing duration or incomplete work schedule needs checking.";
            return covered?"Known free in the recorded schedule":"No known conflicts · recorded activities only; work schedules are incomplete.";
        }
        public static List<DateTime> NextOff(ScheduleSave s,DateTime from,int days=90)
        {
            var result=new List<DateTime>();if(string.IsNullOrEmpty(s.group.shiftOwnerProfileId))return result;
            for(int i=0;i<Math.Min(days,90);i++){var d=from.Date.AddDays(i);var o=s.overrides.Find(x=>x.dateKey==DateKeyUtility.ToDateKey(d));int shift=o!=null&&!o.scheduledShift?o.shiftType:ShiftPatternUtility.Resolve(s.group.pattern,s.group.startDateKey,d);if(shift==(int)ShiftTypeId.Off)result.Add(d);}return result;
        }
    }
}
