using System;
using System.Collections.Generic;
using System.Linq;
using ShiftCal.App;
using ShiftCal.Core;
using ShiftCal.Data;
using UnityEngine;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    public partial class ScheduleWorkbench
    {
        public Toggle shiftWeekdays;
        public void WeekdayRestrictionChanged(bool value)=>ConditionalSections();
        bool privateAlarm;
        PersonalAlarmDefinition personalDefinition;
        string editorAccount,editorGroup;
        public void NewPersonalAlarm(string date)
        {
            NewEvent(date,RecurrenceEngine.ResolvedShift(Save,DateKeyUtility.FromDateKey(date)));
            privateAlarm=true;editorHeading.text="New personal alarm";EditorSaveLabel("Save Alarm");Set(eventFields,"title","Wake Up");eventTime.Set("04:00");
            dayContextChoice.ClearOptions();dayContextChoice.AddOptions(new List<string>{"This date only","Selected weekdays","Every selected shift","Weekdays on a matching shift"});dayContextChoice.SetValueWithoutNotify(0);contextSection.SetActive(true);ConditionalSections();editorSnapshot=EditorSignature;
        }
        void EditPersonal(PersonalAlarmDefinition definition,string date)
        {
            editingEvent=editingRule=null;ruleTemplate=null;personalDefinition=null;privateAlarm=false;originalDate=date;
            var ex=definition.exceptions.Find(x=>x.originalDate==date);var copy=ActivityResolver.Clone(ex?.replacement??definition.schedule);copy.dateKey=date;
            LoadEvent(copy);privateAlarm=true;personalDefinition=definition;
            // Existing local EventSeries keep their identity and recurrence engine.
            if(recurrence.options.Count>4)recurrence.options.RemoveAt(4);
            editorHeading.text="Edit personal alarm";EditorSaveLabel("Save Alarm");ConditionalSections();editorSnapshot=EditorSignature;
        }
        void PersonalScope(int scope)
        {
            if(scope>0){var e=personalDefinition.schedule;recurrence.SetValueWithoutNotify((int)e.recurrence);Set(eventFields,"interval",e.interval.ToString());Set(eventFields,"count",Math.Max(1,e.count).ToString());repeatEnding.SetValueWithoutNotify(!string.IsNullOrEmpty(e.until)?1:e.count>0?2:0);endingDate.Set(string.IsNullOrEmpty(e.until)?e.dateKey:e.until);for(int i=0;i<7;i++)weekdays[i].isOn=e.weekdays.Contains(i);}
            eventDate.Set(scope==2?personalDefinition.schedule.dateKey:originalDate);ConditionalSections();
        }
        void PersonalRows(List<Occurrence> occurrences,long now)
        {
            foreach(var definition in PersonalAlarmStore.Definitions){var d=definition;var e=d.schedule;var next=occurrences.FirstOrDefault(o=>o.sourceId==e.id&&o.audible&&o.at>now);Row(e.title,"Personal · this device\n"+Cadence(e)+"\n"+(next==null?"No upcoming delivery":"Next: "+DayInformation.Time(next)),"Edit",()=>EditPersonal(d,next?.dateKey??e.dateKey),e.enabled?"Pause":"Resume",()=>{e.enabled=!e.enabled;DeviceCalendarStore.Write();AppSession.Instance.SaveLocal();},"Upcoming",()=>UpcomingFor(e.id));}
        }
        void EditorSaveLabel(string label){var button=eventPanel.GetComponentsInChildren<Button>(true).First(b=>b.name=="Save Event");button.GetComponentInChildren<Text>().text=label;}
        public void ExportPersonalBackup()
        {
            if(DeviceCalendarStore.State==null)return;
            string json=JsonUtility.ToJson(new PersonalBackup{device=DeviceCalendarStore.State,preferences=DeviceAlarmPreferences.Current},true);
#if UNITY_ANDROID && !UNITY_EDITOR
            string error=AndroidBridge.Call<string>("backupPersonal",json);Message(string.IsNullOrEmpty(error)?"Choose where to save this private personal backup.":error);
#else
            string path=System.IO.Path.Combine(Application.persistentDataPath,"ShiftCal-personal-backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".json");System.IO.File.WriteAllText(path,json);Message("Personal backup: "+path);
#endif
        }
        [Serializable] class PersonalBackup{public int personalBackupVersion=1;public DeviceCalendarState device;public AlarmPreferences preferences;}
    }
}
