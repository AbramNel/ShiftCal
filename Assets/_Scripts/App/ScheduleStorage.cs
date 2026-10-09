using System;
using System.IO;
using System.Text;
using ShiftCal.Data;
using UnityEngine;

namespace ShiftCal.App
{
    public static class ScheduleStorage
    {
        public const string LegacyKey = "ShiftCal.LocalCalendar.v1";
        public static string Status;
        public static string PathFor(string account)
        {
            using (var hash = System.Security.Cryptography.SHA256.Create())
            {
                string name = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(account))).Replace("-", "");
                return Path.Combine(Application.persistentDataPath, "schedule-" + name + ".json");
            }
        }
        public static ScheduleSave Load(string account, GroupData defaults)
        {
            string path = PathFor(account);
            foreach (string file in new[] { path, path + ".previous" })
            {
                if (!File.Exists(file)) continue;
                try
                {
                    var save = JsonUtility.FromJson<ScheduleSave>(File.ReadAllText(file));
                    Validate(save, account);
                    if (file != path) Status = "Recovered previous save. Original file retained.";
                    return save;
                }
                catch (Exception ex) { Status = "Save could not be read: " + ex.Message + ". Original file retained."; }
            }
            if (File.Exists(path)) throw new InvalidDataException(Status + " Export a backup before recovery.");
            var result = new ScheduleSave { account = account, group = JsonUtility.FromJson<GroupData>(JsonUtility.ToJson(defaults)) };
            if (account == "local" && PlayerPrefs.HasKey(LegacyKey))
            {
                string original = PlayerPrefs.GetString(LegacyKey);
                File.WriteAllText(path + ".legacy.json", original);
                try
                {
                    result = MigrateLegacy(original,defaults);
                    Status = "Existing calendar migrated; original PlayerPrefs and JSON backup retained.";
                }
                catch (Exception ex) { throw new InvalidDataException("Legacy migration stopped; original retained: " + ex.Message); }
            }
            Write(result);
            return result;
        }
        public static ScheduleSave MigrateLegacy(string original,GroupData defaults)
        {
            var legacy=JsonUtility.FromJson<ScheduleSave>(original);
            if(legacy?.group==null)throw new InvalidDataException("Legacy calendar has no group.");
            var result=new ScheduleSave{group=legacy.group,overrides=legacy.overrides??new System.Collections.Generic.List<DayOverrideData>(),events=legacy.events??new System.Collections.Generic.List<EventSeries>(),exceptions=legacy.exceptions??new System.Collections.Generic.List<EventException>(),rules=legacy.rules??new System.Collections.Generic.List<ShiftAlarmRule>(),mutedEvents=legacy.mutedEvents??new System.Collections.Generic.List<string>(),mutedRules=legacy.mutedRules??new System.Collections.Generic.List<string>(),activities=legacy.activities??new System.Collections.Generic.List<CalendarActivity>(),activityExceptions=legacy.activityExceptions??new System.Collections.Generic.List<ActivityException>(),profiles=legacy.profiles??new System.Collections.Generic.List<FamilyProfile>(),templates=legacy.templates??new System.Collections.Generic.List<ActivityTemplate>()};
            if(result.group.shiftTypes==null)result.group.shiftTypes=new System.Collections.Generic.List<ShiftTypeDefinitionData>();
            foreach(var preset in defaults.shiftTypes)if(!result.group.shiftTypes.Exists(x=>x.id==preset.id))result.group.shiftTypes.Add(JsonUtility.FromJson<ShiftTypeDefinitionData>(JsonUtility.ToJson(preset)));
            Validate(result,"local");return result;
        }
        public static void Validate(ScheduleSave s, string account)
        {
            if (s == null || s.version > 3 || s.group == null || s.account != account) throw new InvalidDataException("Invalid version or account.");
            Core.DateKeyUtility.FromDateKey(s.group.startDateKey);
            if (s.group.pattern == null || s.group.shiftTypes == null || s.overrides == null || s.events == null || s.exceptions == null || s.rules == null || s.records == null) throw new InvalidDataException("Missing calendar lists.");
            s.activities = s.activities ?? new System.Collections.Generic.List<CalendarActivity>();
            s.activityExceptions = s.activityExceptions ?? new System.Collections.Generic.List<ActivityException>();
            s.profiles = s.profiles ?? new System.Collections.Generic.List<FamilyProfile>();
            s.templates = s.templates ?? new System.Collections.Generic.List<ActivityTemplate>();
            foreach (var a in s.activities) if (!Core.ActivityResolver.Validate(a,out var issue)) throw new InvalidDataException(issue);
            foreach (var o in s.overrides) Core.DateKeyUtility.FromDateKey(o.dateKey);
            foreach (var e in s.events) if (!Core.RecurrenceEngine.Validate(e, out var error)) throw new InvalidDataException(error);
        }
        public static void Write(ScheduleSave s)
        {
            Validate(s, s.account);
            string path = PathFor(s.account), temp = path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(DeviceCalendarStore.SharedCopy(s)));
                stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
            }
            if (File.Exists(path))
            {
                bool validCurrent=true;
                try{Validate(JsonUtility.FromJson<ScheduleSave>(File.ReadAllText(path)),s.account);}catch{validCurrent=false;}
                File.Replace(temp,path,validCurrent?path+".previous":path+".corrupt-"+DateTime.UtcNow.Ticks);
            }
            else File.Move(temp, path);
        }
    }
}
