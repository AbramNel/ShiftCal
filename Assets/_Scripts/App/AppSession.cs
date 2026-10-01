using UnityEngine;
using ShiftCal.Data;
using ShiftCal.Core;
using System;
using System.Collections.Generic;

namespace ShiftCal.App
{
    public class AppSession : MonoBehaviour
    {
        public static AppSession Instance;

        public GroupData CurrentGroup;
        public readonly Dictionary<string, DayOverrideData> CalendarOverrides = new Dictionary<string, DayOverrideData>();
        public ScheduleSave Data { get; private set; }
        public event Action Changed;
        public string Error { get; private set; }
        private bool loaded;
        private bool alarmsActive;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (CurrentGroup == null)
                CurrentGroup = CreateDefaultGroup();

            SwitchAccount(PlayerPrefs.GetString("ShiftCal.ActiveAccount.v3","local"),PlayerPrefs.HasKey("ShiftCal.ActiveAccount.v3"));
        }

        public void SaveLocal()
        {
            if (!loaded) return;
            try
            {
                Data.group = CurrentGroup;
                Data.overrides = new List<DayOverrideData>(CalendarOverrides.Values);
                Firebase.FirestoreService.Instance?.TrackChanges(Data);
                ScheduleStorage.Write(Data);
                if(alarmsActive)AndroidBridge.Save(Data);
                Error = "";
                Changed?.Invoke();
                Firebase.FirestoreService.Instance?.Flush();
            }
            catch (Exception ex) { Error = "Save failed; previous save retained: " + ex.Message; Debug.LogError(Error); }
        }

        public void SwitchAccount(string account, bool activate = true)
        {
            Firebase.FirestoreService.Instance?.StopListening();
            AndroidBridge.Action("deactivate");
            loaded = false;
            alarmsActive = activate;
            CalendarOverrides.Clear();
            try
            {
                Data = ScheduleStorage.Load(account, CreateDefaultGroup());
                CurrentGroup = Data.group;
                foreach (var o in Data.overrides) CalendarOverrides[o.dateKey] = o;
                loaded = true; Error = "";
                if(activate){PlayerPrefs.SetString("ShiftCal.ActiveAccount.v3",account);PlayerPrefs.Save();AndroidBridge.Save(Data);}
                Changed?.Invoke();
                Firebase.FirestoreService.Instance?.StartListening();
            }
            catch (Exception ex) { Error = ex.Message; Data = new ScheduleSave { account = account, group = CreateDefaultGroup() }; CurrentGroup = Data.group; }
        }

        public void ApplyRemote()
        {
            CurrentGroup = Data.group;
            CalendarOverrides.Clear();
            foreach (var o in Data.overrides) CalendarOverrides[o.dateKey] = o;
            ScheduleStorage.Write(Data);
            if(alarmsActive)AndroidBridge.Save(Data);
            Changed?.Invoke();
        }
        public void RestoreSnapshot(string json)
        {
            Data=JsonUtility.FromJson<ScheduleSave>(json);CurrentGroup=Data.group;CalendarOverrides.Clear();foreach(var o in Data.overrides)CalendarOverrides[o.dateKey]=o;Changed?.Invoke();
        }

        public void ImportLocal()
        {
            if (Data.account == "local" || Data.group.groupId != "local-default") return;
            var local = ScheduleStorage.Load("local", CreateDefaultGroup());
            Data.group = JsonUtility.FromJson<GroupData>(JsonUtility.ToJson(local.group));
            Data.overrides = local.overrides;
            Data.events = local.events; Data.exceptions = local.exceptions; Data.rules = local.rules;
            ApplyRemote(); SaveLocal();
        }

        private void OnApplicationPause(bool paused) { if (!paused && loaded) { AndroidBridge.Action("reconcile"); Firebase.FirestoreService.Instance?.Flush(); Changed?.Invoke(); } }
        private void OnApplicationFocus(bool focused) { if(focused&&loaded&&alarmsActive){AndroidBridge.Action("reconcile");Changed?.Invoke();} }

        public void SetShift(string dateKey, int shift)
        {
            DateKeyUtility.FromDateKey(dateKey);
            if (!CalendarOverrides.TryGetValue(dateKey, out var o)) o = new DayOverrideData { dateKey = dateKey };
            o.shiftType = shift; o.updatedAt = DateKeyUtility.UnixMsNow();
            o.scheduledShift=false;
            o.userId = Data.account;
            CalendarOverrides[dateKey] = o;
        }

        public bool CanDeleteShift(int id, out string reason)
        {
            bool used = CurrentGroup.pattern.Contains(id) || new List<DayOverrideData>(CalendarOverrides.Values).Exists(o => o.shiftType == id) || Data.rules.Exists(r => r.shiftType == id);
            reason = used ? "This shift is used by a rotation, date override or alarm rule. Replace those uses before deleting it." : "";
            return !used;
        }


        private static GroupData CreateDefaultGroup()
        {
            return new GroupData
            {
                groupId = "local-default",
                name = "Days-Mod",
                startDateKey = "2026-07-01",
                pattern = ShiftPatternUtility.ExpandBlocks(new System.Collections.Generic.List<(int shiftType, int length)>
                {
                    ((int)ShiftTypeId.Day12, 4),
                    ((int)ShiftTypeId.Off, 2),
                    ((int)ShiftTypeId.Day12, 4),
                    ((int)ShiftTypeId.Off, 3),
                    ((int)ShiftTypeId.Day12, 4),
                    ((int)ShiftTypeId.Off, 3),
                    ((int)ShiftTypeId.Day12, 4),
                    ((int)ShiftTypeId.Off, 4)
                }),
                shiftTypes = new System.Collections.Generic.List<ShiftTypeDefinitionData>
                {
                    new ShiftTypeDefinitionData { id = (int)ShiftTypeId.Empty, name = "Empty", colorHex = "#EEF2F7" },
                    new ShiftTypeDefinitionData { id = (int)ShiftTypeId.Off, name = "OFF", colorHex = "#9FF4F1" },
                    new ShiftTypeDefinitionData { id = (int)ShiftTypeId.Day12, name = "Day-12", colorHex = "#FBBF24", startTime = "5:30 AM", endTime = "5:30 PM", hours = 12f },
                    new ShiftTypeDefinitionData { id = (int)ShiftTypeId.Day, name = "Day", colorHex = "#F9D65C" },
                    new ShiftTypeDefinitionData { id = (int)ShiftTypeId.Night, name = "Night", colorHex = "#6D7DF2" },
                    new ShiftTypeDefinitionData { id = (int)ShiftTypeId.Vacation, name = "Vacation", colorHex = "#F59AC8" },
                    new ShiftTypeDefinitionData { id = (int)ShiftTypeId.FillDay, name = "Fill Day", colorHex = "#F4A261" },
                    new ShiftTypeDefinitionData { id = (int)ShiftTypeId.FillNight, name = "Fill Night", colorHex = "#7A5CFA" }
                }
            };
        }

        private static GroupData CreateLegacyExampleGroup()
        {
            return new GroupData
            {
                groupId = "local-example",
                name = "Example Shift Calendar",
                startDateKey = DateKeyUtility.ToDateKey(System.DateTime.Today),
                pattern = ShiftPatternUtility.ExpandBlocks(new System.Collections.Generic.List<(int shiftType, int length)>
                {
                    ((int)ShiftTypeId.Day, 2),
                    ((int)ShiftTypeId.Off, 2),
                    ((int)ShiftTypeId.Night, 2),
                    ((int)ShiftTypeId.Off, 2)
                }),
                shiftTypes = new System.Collections.Generic.List<ShiftTypeDefinitionData>
                {
                    new ShiftTypeDefinitionData { id = (int)ShiftTypeId.Empty, name = "Empty", colorHex = "#EEF2F7" },
                    new ShiftTypeDefinitionData { id = (int)ShiftTypeId.Day, name = "Day", colorHex = "#F9D65C" },
                    new ShiftTypeDefinitionData { id = (int)ShiftTypeId.Night, name = "Night", colorHex = "#6D7DF2" },
                    new ShiftTypeDefinitionData { id = (int)ShiftTypeId.Off, name = "Off", colorHex = "#8ED6A5" },
                    new ShiftTypeDefinitionData { id = (int)ShiftTypeId.Vacation, name = "Vacation", colorHex = "#F59AC8" },
                    new ShiftTypeDefinitionData { id = (int)ShiftTypeId.FillDay, name = "Fill Day", colorHex = "#F4A261" },
                    new ShiftTypeDefinitionData { id = (int)ShiftTypeId.FillNight, name = "Fill Night", colorHex = "#7A5CFA" }
                }
            };
        }
    }
}
