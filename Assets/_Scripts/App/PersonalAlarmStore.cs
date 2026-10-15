using System;
using System.Collections.Generic;
using System.Linq;
using ShiftCal.Core;
using ShiftCal.Data;
namespace ShiftCal.App
{
    // Private EventSeries reuse the existing recurrence/native engine and IDs.
    public static class PersonalAlarmStore
    {
        public static List<PersonalAlarmDefinition> Definitions=>DeviceCalendarStore.State?.account==AppSession.Instance.Data.account?DeviceCalendarStore.State.personalAlarms.Where(a=>a.groupId==AppSession.Instance.CurrentGroup.groupId).ToList():new List<PersonalAlarmDefinition>();
        public static void Save(EventSeries edited,PersonalAlarmDefinition old,string original,int scope)
        {
            var state=DeviceCalendarStore.State;var previous=ActivityResolver.CloneList(state.personalAlarms);var items=state.personalAlarms;
            if(old!=null)old=items.Find(x=>x.schedule.id==old.schedule.id)??throw new InvalidOperationException("This alarm changed. Reopen it.");
            try{
            if(old==null){edited.id="personal-"+Guid.NewGuid().ToString("N");items.Add(new PersonalAlarmDefinition{groupId=AppSession.Instance.CurrentGroup.groupId,schedule=edited});}
            else if(old.schedule.recurrence!=RecurrenceKind.Once&&scope==0)
            {
                edited.id=Guid.NewGuid().ToString("N");edited.recurrence=RecurrenceKind.Once;edited.until=null;edited.count=0;
                old.exceptions.RemoveAll(e=>e.originalDate==original);old.exceptions.Add(new EventException{id=old.schedule.id+"_"+original,seriesId=old.schedule.id,originalDate=original,replacement=edited});
            }
            else if(old.schedule.recurrence!=RecurrenceKind.Once&&scope==1&&old.schedule.dateKey!=original)
            {
                var split=DateKeyUtility.FromDateKey(original);int used=RecurrenceEngine.Dates(old.schedule,DateKeyUtility.FromDateKey(old.schedule.dateKey),split.AddDays(-1),AppSession.Instance.Data).Count();
                if(old.schedule.count>0&&edited.count==old.schedule.count)edited.count=Math.Max(1,old.schedule.count-used);old.schedule.until=DateKeyUtility.ToDateKey(split.AddDays(-1));
                edited.id="personal-"+Guid.NewGuid().ToString("N");var next=new PersonalAlarmDefinition{groupId=old.groupId,schedule=edited};
                foreach(var ex in old.exceptions.Where(e=>string.CompareOrdinal(e.originalDate,original)>=0).ToArray()){old.exceptions.Remove(ex);ex.seriesId=edited.id;ex.id=edited.id+"_"+ex.originalDate;next.exceptions.Add(ex);}items.Add(next);
            }
            else{edited.id=old.schedule.id;old.schedule=edited;}
            DeviceCalendarStore.Write();}catch{state.personalAlarms=previous;throw;}AppSession.Instance.SaveLocal();
        }
        public static void Delete(PersonalAlarmDefinition old,string original,int scope)
        {
            var state=DeviceCalendarStore.State;var previous=ActivityResolver.CloneList(state.personalAlarms);old=state.personalAlarms.Find(x=>x.schedule.id==old.schedule.id)??throw new InvalidOperationException("This alarm changed. Reopen it.");
            try{
            if(old.schedule.recurrence!=RecurrenceKind.Once&&scope==0){old.exceptions.RemoveAll(e=>e.originalDate==original);old.exceptions.Add(new EventException{id=old.schedule.id+"_"+original,seriesId=old.schedule.id,originalDate=original,cancelled=true});}
            else if(old.schedule.recurrence!=RecurrenceKind.Once&&scope==1&&old.schedule.dateKey!=original){old.schedule.until=DateKeyUtility.ToDateKey(DateKeyUtility.FromDateKey(original).AddDays(-1));old.exceptions.RemoveAll(e=>string.CompareOrdinal(e.originalDate,original)>=0);}
            else DeviceCalendarStore.State.personalAlarms.Remove(old);
            DeviceCalendarStore.Write();}catch{state.personalAlarms=previous;throw;}AppSession.Instance.SaveLocal();
        }
    }
}
