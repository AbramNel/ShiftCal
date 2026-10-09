using System;
using System.Collections.Generic;
using System.Linq;
using ShiftCal.Data;
using UnityEngine;
namespace ShiftCal.Core
{
    public sealed class ActivityOccurrence
    {
        public CalendarActivity activity;
        public string seriesId, originalDate, dateKey;
        public long start, end;
        public bool HasTime => !activity.allDay && !string.IsNullOrEmpty(activity.startTime);
        public string TimeLabel => activity.allDay ? "All day" : HasTime ? activity.startTime + (activity.durationMinutes>0 ? " · " + activity.durationMinutes + " min" : "") : "Untimed";
    }
    public static class ActivityResolver
    {
        public static List<T> CloneList<T>(IEnumerable<T> values)=>values.Select(x=>Clone(x)).ToList();
        public static T Clone<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        public static EventSeries AsEvent(CalendarActivity a, string id = null) => new EventSeries { id=id??"activity-"+a.id,title=a.title,dateKey=a.dateKey,startTime=string.IsNullOrEmpty(a.startTime)?"12:00 AM":a.startTime,notes=a.notes,zone=a.zone,recurrence=a.recurrence,interval=a.interval,weekdays=a.weekdays,until=a.until,count=a.count };
        public static List<ActivityOccurrence> Resolve(ScheduleSave s, DateTime from, DateTime to, string filter="everyone", string uid=null)
        {
            var result=new List<ActivityOccurrence>();
            foreach(var a in s.activities)
                foreach(var date in RecurrenceEngine.Dates(AsEvent(a),from,to))
                {
                    string key=DateKeyUtility.ToDateKey(date);
                    if(s.activityExceptions.Exists(x=>x.seriesId==a.id&&x.originalDate==key))continue;
                    Add(result,s,a,a.id,key,key,filter,uid);
                }
            foreach(var ex in s.activityExceptions)
                if(!ex.cancelled&&ex.replacement!=null) { var d=DateKeyUtility.FromDateKey(ex.replacement.dateKey); if(d>=from.Date&&d<=to.Date)Add(result,s,ex.replacement,ex.seriesId,ex.originalDate,ex.replacement.dateKey,filter,uid); }
            return result.OrderBy(x=>x.dateKey,StringComparer.Ordinal).ThenBy(x=>x.HasTime?x.start:long.MinValue).ThenBy(x=>x.activity.title,StringComparer.Ordinal).ThenBy(x=>x.seriesId,StringComparer.Ordinal).ToList();
        }
        static void Add(List<ActivityOccurrence> list, ScheduleSave s, CalendarActivity a,string id,string original,string actual,string filter,string uid)
        {
            if(!Matches(s,a,filter,uid))return;
            long at=RecurrenceEngine.Instant(DateKeyUtility.FromDateKey(actual),a.allDay||string.IsNullOrEmpty(a.startTime)?"12:00 AM":a.startTime,a.zone);
            list.Add(new ActivityOccurrence{activity=a,seriesId=id,originalDate=original,dateKey=actual,start=at,end=at+(long)a.durationMinutes*60000});
        }
        public static bool Matches(ScheduleSave s,CalendarActivity a,string filter,string uid)
        {
            if(string.IsNullOrEmpty(filter)||filter=="everyone")return true;
            if(filter=="unassigned")return string.IsNullOrEmpty(a.assigneeId);
            string person=filter=="mine"?s.profiles.Find(x=>x.active&&!string.IsNullOrEmpty(uid)&&x.linkedUid==uid)?.id:filter;
            return !string.IsNullOrEmpty(person)&&(a.assigneeId==person||a.participants.Contains(person));
        }
        public static bool Validate(CalendarActivity a,out string error)
        {
            error="";
            if(a==null||string.IsNullOrWhiteSpace(a.title)){error="Add a title.";return false;}
            try{DateKeyUtility.FromDateKey(a.dateKey);if(a.interval<1||a.interval>365||a.count<0||a.durationMinutes<0||a.durationMinutes>10080)throw new ArgumentException();
                if(!string.IsNullOrEmpty(a.startTime)&&!a.allDay)RecurrenceEngine.Instant(DateKeyUtility.FromDateKey(a.dateKey),a.startTime,a.zone);
                var e=AsEvent(a);if(!RecurrenceEngine.Validate(e,out error))return false;
            }catch{error="Check the date, time, duration and repeat interval.";return false;}
            return true;
        }
        public static string People(ScheduleSave s,CalendarActivity a)
        {
            var ids=new List<string>();if(!string.IsNullOrEmpty(a.assigneeId))ids.Add(a.assigneeId);ids.AddRange(a.participants);
            return ids.Count==0?"Unassigned":string.Join(", ",ids.Distinct().Select(id=>s.profiles.Find(p=>p.id==id)?.name??"Archived person"));
        }
        public static void SaveEdit(ScheduleSave s,CalendarActivity edited,string seriesId,string original,int scope)
        {
            var old=s.activities.Find(x=>x.id==seriesId);
            if(old==null){s.activities.Add(edited);return;}
            if(scope==0&&old.recurrence!=RecurrenceKind.Once)
            {
                edited.id=Guid.NewGuid().ToString("N");edited.recurrence=RecurrenceKind.Once;edited.count=0;edited.until=null;
                s.activityExceptions.RemoveAll(x=>x.seriesId==seriesId&&x.originalDate==original);
                s.activityExceptions.Add(new ActivityException{id=seriesId+"_"+original,seriesId=seriesId,originalDate=original,replacement=edited});
            }
            else if(scope==1&&old.recurrence!=RecurrenceKind.Once&&original!=old.dateKey)
            {
                var split=DateKeyUtility.FromDateKey(original);
                int used=RecurrenceEngine.Dates(AsEvent(old),DateKeyUtility.FromDateKey(old.dateKey),split.AddDays(-1)).Count();
                if(old.count>0&&edited.count==old.count)edited.count=Math.Max(1,old.count-used);
                old.until=DateKeyUtility.ToDateKey(split.AddDays(-1));edited.id=Guid.NewGuid().ToString("N");s.activities.Add(edited);
                foreach(var ex in s.activityExceptions.Where(x=>x.seriesId==seriesId&&string.CompareOrdinal(x.originalDate,original)>=0)){ex.seriesId=edited.id;ex.id=edited.id+"_"+ex.originalDate;}
            }
            else{edited.id=old.id;s.activities[s.activities.IndexOf(old)]=edited;}
        }
        public static void Delete(ScheduleSave s,string id,string original,int scope)
        {
            var old=s.activities.Find(x=>x.id==id);if(old==null)return;
            if(scope==0&&old.recurrence!=RecurrenceKind.Once){s.activityExceptions.RemoveAll(x=>x.seriesId==id&&x.originalDate==original);s.activityExceptions.Add(new ActivityException{id=id+"_"+original,seriesId=id,originalDate=original,cancelled=true});}
            else if(scope==1&&old.recurrence!=RecurrenceKind.Once&&old.dateKey!=original){old.until=DateKeyUtility.ToDateKey(DateKeyUtility.FromDateKey(original).AddDays(-1));s.activityExceptions.RemoveAll(x=>x.seriesId==id&&string.CompareOrdinal(x.originalDate,original)>=0);}
            else{s.activities.Remove(old);s.activityExceptions.RemoveAll(x=>x.seriesId==id);}
        }
    }
}
