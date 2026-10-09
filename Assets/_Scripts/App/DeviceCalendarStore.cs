using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ShiftCal.Data;
using ShiftCal.Core;
namespace ShiftCal.App
{
    public static class DeviceCalendarStore
    {
        public static DeviceCalendarState State { get; private set; }
        public static string Status { get; private set; }
        public static string PathFor(string account) => ScheduleStorage.PathFor(account)+".device.json";
        public static void Load(ScheduleSave s, bool existed)
        {
            string path=PathFor(s.account);
            if(File.Exists(path))
            {
                Exception failure=null;bool recovered=false;
                foreach(string file in new[]{path,path+".previous"}){if(!File.Exists(file))continue;try{var loaded=JsonUtility.FromJson<DeviceCalendarState>(File.ReadAllText(file));Validate(loaded,s.account);State=loaded;recovered=true;if(file!=path)Status="Recovered previous device alarm definitions; original retained.";break;}catch(Exception ex){failure=ex;}}
                if(!recovered)throw new InvalidDataException("Device alarm file is invalid; originals retained.",failure);
            }
            else
            {
                State=new DeviceCalendarState{account=s.account,installationId=Guid.NewGuid().ToString("N")};
                // Cached v3 rules already belonged to this installation. Remote listeners never run this migration.
                if(existed)
                {
                    string source=ScheduleStorage.PathFor(s.account);
                    if(File.Exists(source))if(!File.Exists(source+".before-family-migration.json"))File.Copy(source,source+".before-family-migration.json",false);
                    // A legacy import can be interrupted after its shared file was written,
                    // before this private file existed. Recover definitions from its retained backup.
                    var migration=s;
                    if(s.rules.Count==0&&File.Exists(source+".legacy.json"))migration=ScheduleStorage.MigrateLegacy(File.ReadAllText(source+".legacy.json"),s.group);
                    State.rules=ActivityResolver.CloneList(migration.rules);foreach(var r in State.rules)if(string.IsNullOrEmpty(r.groupId))r.groupId=s.group.groupId;State.mutedEvents=new List<string>(migration.mutedEvents);State.mutedRules=new List<string>(migration.mutedRules);
                    string evidence=AndroidBridge.Call<string>("savedDefinitions",s.account);
                    var native=string.IsNullOrEmpty(evidence)?null:JsonUtility.FromJson<ScheduleSave>(evidence);
                    if(native!=null){foreach(var rule in native.rules)if(!State.rules.Exists(x=>x.id==rule.id)){var copy=ActivityResolver.Clone(rule);if(string.IsNullOrEmpty(copy.groupId))copy.groupId=native.group?.groupId??s.group.groupId;State.rules.Add(copy);}}
                    var provenRules=new HashSet<string>(native?.rules.Select(x=>x.id)??Enumerable.Empty<string>());
                    if(s.account!="local")foreach(var rule in State.rules)if(!provenRules.Contains(rule.id))rule.enabled=false;
                    var proven=new HashSet<string>(native?.events.Where(e=>e.alarm||e.reminder).Select(e=>e.id)??Enumerable.Empty<string>());
                    foreach(var e in migration.events)
                        if((e.alarm||e.reminder)&&(s.account=="local"||proven.Contains(e.id)))State.subscriptions.Add(FromEvent(e));
                    foreach(var ex in migration.exceptions)if(ex.replacement!=null&&(s.account=="local"||proven.Contains(ex.seriesId))){var sub=FromEvent(ex.replacement);sub.sourceId=ex.seriesId;sub.originalDate=ex.originalDate;State.subscriptions.Add(sub);}
                    if(native!=null){foreach(var e in native.events)if((e.alarm||e.reminder)&&!State.subscriptions.Exists(x=>x.sourceId==e.id&&string.IsNullOrEmpty(x.originalDate)))State.subscriptions.Add(FromEvent(e));foreach(var ex in native.exceptions)if(ex.replacement!=null&&!State.subscriptions.Exists(x=>x.sourceId==ex.seriesId&&x.originalDate==ex.originalDate)){var sub=FromEvent(ex.replacement);sub.sourceId=ex.seriesId;sub.originalDate=ex.originalDate;State.subscriptions.Add(sub);}foreach(var id in native.mutedEvents)if(!State.mutedEvents.Contains(id))State.mutedEvents.Add(id);foreach(var id in native.mutedRules)if(!State.mutedRules.Contains(id))State.mutedRules.Add(id);}
                    Status=s.account=="local"?"Existing device alarms retained.":"Alarm definitions retained. Legacy cloud alarms without native evidence remain off until you enable them on this device.";
                }
                Write();
            }
            // Old queued private records must never be uploaded, applied, or turned into tombstones.
            s.records.RemoveAll(x=>x.key.StartsWith("rule/"));
            Hydrate(s);
        }
        public static void RemapGroup(string oldId,string newId){if(State==null)return;foreach(var r in State.rules)if(r.groupId==oldId)r.groupId=newId;Write();if(AppSession.Instance?.Data!=null)Hydrate(AppSession.Instance.Data);}
        public static void Hydrate(ScheduleSave s)
        {
            if(State==null||State.account!=s.account)return;
            s.rules=ActivityResolver.CloneList(State.rules);s.mutedEvents=new List<string>(State.mutedEvents);s.mutedRules=new List<string>(State.mutedRules);
        }
        public static void Capture(ScheduleSave s)
        {
            if(State==null||State.account!=s.account)return;
            State.rules=ActivityResolver.CloneList(s.rules);State.mutedEvents=new List<string>(s.mutedEvents);State.mutedRules=new List<string>(s.mutedRules);Write();
        }
        public static LocalAlarmSubscription FromEvent(EventSeries e) => new LocalAlarmSubscription{sourceId=e.id,alarm=e.alarm,reminder=e.reminder,enabled=e.enabled,reminderMinutes=e.reminderMinutes,vibration=e.vibration,sound=e.sound,snoozeMinutes=e.snoozeMinutes,advanceMinutes=e.advanceMinutes,useDefaultAlarmSettings=e.useDefaultAlarmSettings};
        public static LocalAlarmSubscription Find(string source,string date=null) => State?.subscriptions.Find(x=>x.sourceId==source&&x.originalDate==date)??State?.subscriptions.Find(x=>x.sourceId==source&&string.IsNullOrEmpty(x.originalDate));
        public static void Subscribe(LocalAlarmSubscription sub)
        {
            if(State==null)return;State.subscriptions.RemoveAll(x=>x.sourceId==sub.sourceId&&x.originalDate==sub.originalDate);State.subscriptions.Add(sub);Write();
        }
        public static void SplitSubscriptions(string oldId,string newId,string from)
        {
            if(State==null)return;foreach(var x in State.subscriptions.Where(x=>x.sourceId==oldId&&(string.IsNullOrEmpty(x.originalDate)||string.CompareOrdinal(x.originalDate,from)>=0)).ToArray()){var clone=ActivityResolver.Clone(x);clone.sourceId=newId;State.subscriptions.Add(clone);}Write();
        }
        public static void Apply(EventSeries e,LocalAlarmSubscription sub)
        {
            e.alarm=sub?.alarm==true;e.reminder=sub?.reminder==true;e.enabled=sub?.enabled??true;
            if(sub==null)return;e.reminderMinutes=sub.reminderMinutes;e.vibration=sub.vibration;e.sound=sub.sound;e.snoozeMinutes=sub.snoozeMinutes;e.advanceMinutes=sub.advanceMinutes;e.useDefaultAlarmSettings=sub.useDefaultAlarmSettings;
        }
        public static ScheduleSave Effective(ScheduleSave shared,DeviceCalendarState device=null)
        {
            var result=ActivityResolver.Clone(shared);device=device??(State?.account==shared.account?State:null);
            result.rules=device==null?new List<ShiftAlarmRule>():ActivityResolver.CloneList(device.rules);
            result.mutedEvents=device?.mutedEvents??new List<string>();result.mutedRules=device?.mutedRules??new List<string>();
            Func<string,string,LocalAlarmSubscription> sub=(id,date)=>device?.subscriptions.Find(x=>x.sourceId==id&&x.originalDate==date)??device?.subscriptions.Find(x=>x.sourceId==id&&string.IsNullOrEmpty(x.originalDate));
            foreach(var e in result.events)Apply(e,sub(e.id,null));
            foreach(var ex in result.exceptions)if(ex.replacement!=null)Apply(ex.replacement,sub(ex.seriesId,ex.originalDate));
            foreach(var a in shared.activities){var e=ActivityResolver.AsEvent(a);Apply(e,sub(e.id,null));if(a.allDay||string.IsNullOrEmpty(a.startTime)){e.alarm=false;e.reminder=false;}result.events.Add(e);}
            foreach(var ex in shared.activityExceptions)
            {
                EventSeries replacement=null;if(ex.replacement!=null){replacement=ActivityResolver.AsEvent(ex.replacement);Apply(replacement,sub("activity-"+ex.seriesId,ex.originalDate));if(ex.replacement.allDay||string.IsNullOrEmpty(ex.replacement.startTime)){replacement.alarm=false;replacement.reminder=false;}}
                result.exceptions.Add(new EventException{id=ex.id,seriesId="activity-"+ex.seriesId,originalDate=ex.originalDate,cancelled=ex.cancelled,replacement=replacement});
            }
            return result;
        }
        public static ScheduleSave SharedCopy(ScheduleSave s)
        {
            var copy=ActivityResolver.Clone(s);copy.rules.Clear();copy.mutedEvents.Clear();copy.mutedRules.Clear();copy.records.RemoveAll(x=>x.key.StartsWith("rule/"));
            // Local disk also excludes device delivery fields from the shared event definitions.
            copy.events=copy.events.Select(x=>JsonUtility.FromJson<EventSeries>(JsonUtility.ToJson(SharedEventDefinition.From(x)))).ToList();
            foreach(var e in copy.events){e.alarm=false;e.reminder=false;}
            foreach(var ex in copy.exceptions)if(ex.replacement!=null){ex.replacement=JsonUtility.FromJson<EventSeries>(JsonUtility.ToJson(SharedEventDefinition.From(ex.replacement)));ex.replacement.alarm=false;ex.replacement.reminder=false;}
            return copy;
        }
        static void Validate(DeviceCalendarState state,string account)
        {
            if(state==null||state.account!=account||state.version!=1||state.rules==null||state.subscriptions==null||state.templates==null||state.mutedEvents==null||state.mutedRules==null)throw new InvalidDataException("Invalid device state.");
        }
        public static void Write()
        {
            if(State==null)return;string path=PathFor(State.account),temp=path+".tmp";Validate(State,State.account);
            using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){byte[] bytes=System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(State));stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            if(File.Exists(path)){bool valid=true;try{Validate(JsonUtility.FromJson<DeviceCalendarState>(File.ReadAllText(path)),State.account);}catch{valid=false;}File.Replace(temp,path,valid?path+".previous":path+".corrupt-"+DateTime.UtcNow.Ticks);}else File.Move(temp,path);
        }
    }
}
