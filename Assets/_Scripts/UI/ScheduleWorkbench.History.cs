using System;
using System.Collections.Generic;
using System.Linq;
using ShiftCal.App;
using ShiftCal.Core;
using ShiftCal.Data;
using ShiftCal.Firebase;
using UnityEngine;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    public partial class ScheduleWorkbench
    {
        public Transform historyContent;
        public GameObject historySection,sharedCleanup;
        public Text historyHeading,cleanupPersonalLabel,cleanupSharedLabel;
        public int HistoryCount {get;private set;}
        bool historyOpen;
        string listMinute;
        public void ToggleHistory(){historyOpen=!historyOpen;Refresh();}
        Transform Destination(SchedulePhase phase){if(phase==SchedulePhase.Past){HistoryCount++;return historyContent;}return agendaContent;}
        void ActivityRows(IEnumerable<Occurrence> states,long now)
        {
            var family=GetComponent<FamilyWorkbench>();
            foreach(var item in Save.activities.ToArray())
            {
                var a=item;var phase=ScheduleLifecycle.Activity(Save,a,states,now);
                RowTo(Destination(phase),a.title,ActivityResolver.People(Save,a)+" · "+(phase==SchedulePhase.Past?"Past · ":"")+DateKeyUtility.FromDateKey(a.dateKey).ToString("MMM d, yyyy"),"Details",()=>family.Details(new ActivityOccurrence{activity=a,seriesId=a.id,originalDate=a.dateKey,dateKey=a.dateKey}),"",null,"",null);
            }
        }
        void BindHistory(IEnumerable<Occurrence> states,long now)
        {
            historySection.SetActive(historyOpen);historyHeading.text="Past / History · "+HistoryCount;
            int personal=PersonalAlarmStore.Definitions.Count(d=>ScheduleLifecycle.CanCleanPersonal(Save,d,states,now));
            int shared=Save.events.Count(e=>ScheduleLifecycle.CanCleanEvent(Save,e,states,now))+Save.activities.Count(a=>ScheduleLifecycle.CanCleanActivity(Save,a,states,now));
            cleanupPersonalLabel.text="Clean up past personal alarms · "+personal;cleanupPersonalLabel.GetComponentInParent<Button>(true).interactable=personal>0;
            sharedCleanup.SetActive(FirestoreService.Instance.CanManageProfiles);cleanupSharedLabel.text="Delete past calendar history · "+shared;sharedCleanup.GetComponent<Button>().interactable=shared>0;
            if(HistoryCount==0)FamilyWorkbench.AddText(historyContent,"No completed entries.");
        }
        public void CleanPersonalHistory()
        {
            var save=Save;string account=save.account,group=save.group.groupId;var states=AndroidBridge.DeliveryStates().Values;long now=DateKeyUtility.UnixMsNow();
            var ids=PersonalAlarmStore.Definitions.Where(d=>ScheduleLifecycle.CanCleanPersonal(save,d,states,now)).Select(d=>d.schedule.id).ToHashSet();if(ids.Count==0)return;
            AppNavigation.Instance.Confirm("Clean up "+ids.Count+" completed personal alarm(s) on this device? Shared calendar history and other phones are unaffected.",()=>{
                if(Save.account!=account||Save.group.groupId!=group)return;
                var current=AndroidBridge.DeliveryStates().Values;long at=DateKeyUtility.UnixMsNow();var state=DeviceCalendarStore.State;var backup=state.personalAlarms.ToList();
                try{state.personalAlarms.RemoveAll(d=>d.groupId==group&&ids.Contains(d.schedule.id)&&ScheduleLifecycle.CanCleanPersonal(Save,d,current,at));DeviceCalendarStore.Write();AppSession.Instance.SaveLocal();Refresh();}
                catch(Exception ex){state.personalAlarms=backup;Message("Cleanup could not be saved. Your alarms were retained. "+ex.Message);}
            });
        }
        public void CleanSharedHistory()
        {
            if(!FirestoreService.Instance.CanManageProfiles)return;
            string account=Save.account,group=Save.group.groupId;var states=AndroidBridge.DeliveryStates().Values;long now=DateKeyUtility.UnixMsNow();
            var events=Save.events.Where(e=>ScheduleLifecycle.CanCleanEvent(Save,e,states,now)).Select(e=>e.id).ToHashSet();var activities=Save.activities.Where(a=>ScheduleLifecycle.CanCleanActivity(Save,a,states,now)).Select(a=>a.id).ToHashSet();int count=events.Count+activities.Count;if(count==0)return;
            bool shared=!group.StartsWith("local-");AppNavigation.Instance.Confirm("Permanently delete "+count+" completed one-time calendar event(s)"+(shared?" for everyone in this group?":" from your calendar?")+" Recurring definitions, alarms and worked hours are retained.",()=>{
                if(Save.account!=account||Save.group.groupId!=group||!FirestoreService.Instance.CanManageProfiles)return;
                var current=AndroidBridge.DeliveryStates().Values;long at=DateKeyUtility.UnixMsNow();string backup=JsonUtility.ToJson(Save);
                Save.events.RemoveAll(e=>events.Contains(e.id)&&ScheduleLifecycle.CanCleanEvent(Save,e,current,at));Save.activities.RemoveAll(a=>activities.Contains(a.id)&&ScheduleLifecycle.CanCleanActivity(Save,a,current,at));
                AppSession.Instance.SaveLocal();if(!string.IsNullOrEmpty(AppSession.Instance.Error))AppSession.Instance.RestoreSnapshot(backup);Refresh();
            });
        }
    }
}
