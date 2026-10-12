using System;
using System.Collections.Generic;
using System.Linq;
using ShiftCal.App;
using ShiftCal.Core;
using ShiftCal.Data;
using UnityEngine;
namespace ShiftCal.UI
{
    // Undo is deliberately short and conditional on the authoritative records
    // still matching the result. A concurrent revision/conflict disables it.
    public class ActivityAssignmentChange
    {
        string account,group,after,recordGuard;
        HashSet<string> ids;
        List<CalendarActivity> beforeActivities;
        List<ActivityException> beforeExceptions;
        List<LocalAlarmSubscription> beforeSubscriptions;
        long expires;
        public string ResultId { get; private set; }
        public string OriginalSeriesId { get; private set; }
        string State(ScheduleSave s)=>string.Join("\n",s.activities.Where(a=>ids.Contains(a.id)).OrderBy(a=>a.id).Select(a=>JsonUtility.ToJson(a)))+"\nEXCEPTIONS\n"
            +string.Join("\n",s.activityExceptions.Where(e=>ids.Contains(e.seriesId)).OrderBy(e=>e.id).Select(e=>e.id+"|"+e.seriesId+"|"+e.originalDate+"|"+(e.cancelled?"cancelled":e.replacement==null?"empty":JsonUtility.ToJson(e.replacement))));
        string Records(ScheduleSave s)=>string.Join("|",s.records.Where(r=>ids.Any(id=>r.key=="activity/"+id||r.key.StartsWith("activityException/"+id+"_"))).OrderBy(r=>r.key).Select(r=>r.key+":"+r.revision+":"+r.conflict+":"+r.json));
        public bool CanUndo(ScheduleSave s)=>s.account==account&&s.group.groupId==group&&DateKeyUtility.UnixMsNow()<expires&&State(s)==after&&Records(s)==recordGuard;
        public static ActivityAssignmentChange Apply(ActivityOccurrence selected,int scope,string person,List<string> participants)
        {
            var session=AppSession.Instance;var s=session.Data;var root=s.activities.Find(a=>a.id==selected.seriesId);if(root==null)throw new InvalidOperationException("This activity has changed. Reopen it.");
            if(s.records.Any(r=>r.conflict&&(r.key=="activity/"+root.id||r.key=="activityException/"+root.id+"_"+selected.originalDate)))throw new InvalidOperationException("Resolve this activity's sync conflict before changing responsibility.");
            var current=ActivityResolver.Resolve(s,DateKeyUtility.FromDateKey(selected.dateKey),DateKeyUtility.FromDateKey(selected.dateKey)).Find(o=>o.seriesId==selected.seriesId&&o.originalDate==selected.originalDate);
            if(current==null||JsonUtility.ToJson(current.activity)!=JsonUtility.ToJson(selected.activity))throw new InvalidOperationException("This activity has changed. Reopen it.");
            var change=new ActivityAssignmentChange{account=s.account,group=s.group.groupId,ids=new HashSet<string>{root.id},expires=DateKeyUtility.UnixMsNow()+15000,
                beforeActivities=ActivityResolver.CloneList(new[]{root}),beforeExceptions=ActivityResolver.CloneList(s.activityExceptions.Where(e=>e.seriesId==root.id)),beforeSubscriptions=ActivityResolver.CloneList(DeviceCalendarStore.State?.subscriptions.Where(x=>x.sourceId=="activity-"+root.id)??Enumerable.Empty<LocalAlarmSubscription>())};
            var a=ActivityResolver.Clone(scope>0?root:selected.activity);a.dateKey=scope==2?root.dateKey:selected.dateKey;
            if(participants==null)a.assigneeId=person;else a.participants=participants.Where(id=>id!=a.assigneeId).Distinct().ToList();a.updatedAt=DateKeyUtility.UnixMsNow();
            ActivityResolver.SaveEdit(s,a,selected.seriesId,selected.originalDate,scope);change.ids.Add(a.id);
            change.ResultId=scope==0?selected.seriesId:a.id;change.OriginalSeriesId=selected.seriesId;
            if(scope==1&&a.id!=selected.seriesId)DeviceCalendarStore.SplitSubscriptions("activity-"+selected.seriesId,"activity-"+a.id,selected.originalDate);
            session.SaveLocal();if(!string.IsNullOrEmpty(session.Error))throw new InvalidOperationException(session.Error);
            change.after=change.State(s);change.recordGuard=change.Records(s);return change;
        }
        public bool Undo()
        {
            var s=AppSession.Instance.Data;if(!CanUndo(s))return false;
            s.activities.RemoveAll(a=>ids.Contains(a.id));s.activities.AddRange(ActivityResolver.CloneList(beforeActivities));
            s.activityExceptions.RemoveAll(e=>ids.Contains(e.seriesId));s.activityExceptions.AddRange(ActivityResolver.CloneList(beforeExceptions));
            if(DeviceCalendarStore.State!=null){DeviceCalendarStore.State.subscriptions.RemoveAll(x=>ids.Any(id=>x.sourceId=="activity-"+id));DeviceCalendarStore.State.subscriptions.AddRange(ActivityResolver.CloneList(beforeSubscriptions));DeviceCalendarStore.Write();}
            AppSession.Instance.SaveLocal();expires=0;return string.IsNullOrEmpty(AppSession.Instance.Error);
        }
    }
}
