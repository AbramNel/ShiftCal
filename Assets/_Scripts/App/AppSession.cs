using UnityEngine;
using ShiftCal.Data;
using ShiftCal.Core;
using System;
using System.Collections.Generic;

namespace ShiftCal.App
{
    public class AppSession : MonoBehaviour
    {
        private const string LocalSaveKey = "ShiftCal.LocalCalendar.v1";

        public static AppSession Instance;

        public GroupData CurrentGroup;
        public readonly Dictionary<string, DayOverrideData> CalendarOverrides = new Dictionary<string, DayOverrideData>();

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (!HasUsableDefaults(CurrentGroup))
                CurrentGroup = CreateDefaultGroup();

            LoadLocal();
            EnsureUsableGroup();
        }

        public void SaveLocal()
        {
            LocalCalendarSaveData data = new LocalCalendarSaveData
            {
                group = CurrentGroup,
                overrides = new List<DayOverrideData>(CalendarOverrides.Values)
            };

            PlayerPrefs.SetString(LocalSaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        private void LoadLocal()
        {
            if (!PlayerPrefs.HasKey(LocalSaveKey))
                return;

            string json = PlayerPrefs.GetString(LocalSaveKey);
            if (string.IsNullOrWhiteSpace(json))
                return;

            LocalCalendarSaveData data = JsonUtility.FromJson<LocalCalendarSaveData>(json);
            if (data == null)
                return;

            if (data.group != null)
                CurrentGroup = data.group;

            CalendarOverrides.Clear();
            if (data.overrides == null)
                return;

            foreach (DayOverrideData dayOverride in data.overrides)
            {
                if (dayOverride != null && !string.IsNullOrWhiteSpace(dayOverride.dateKey))
                    CalendarOverrides[dayOverride.dateKey] = dayOverride;
            }
        }

        private void EnsureUsableGroup()
        {
            GroupData defaults = CreateDefaultGroup();
            if (!HasUsableDefaults(CurrentGroup))
            {
                CurrentGroup = defaults;
                return;
            }

            if (string.IsNullOrWhiteSpace(CurrentGroup.groupId))
                CurrentGroup.groupId = defaults.groupId;
            if (string.IsNullOrWhiteSpace(CurrentGroup.name))
                CurrentGroup.name = defaults.name;
            if (string.IsNullOrWhiteSpace(CurrentGroup.startDateKey))
                CurrentGroup.startDateKey = defaults.startDateKey;
            if (CurrentGroup.pattern == null || CurrentGroup.pattern.Count == 0)
                CurrentGroup.pattern = defaults.pattern;
            if (CurrentGroup.members == null)
                CurrentGroup.members = new List<string>();
            if (CurrentGroup.shiftTypes == null)
                CurrentGroup.shiftTypes = new List<ShiftTypeDefinitionData>();

            EnsurePreset(CurrentGroup.shiftTypes, defaults.shiftTypes, (int)ShiftTypeId.Off);
            EnsurePreset(CurrentGroup.shiftTypes, defaults.shiftTypes, (int)ShiftTypeId.Day12);
        }

        private static bool HasUsableDefaults(GroupData group)
        {
            return group != null && group.shiftTypes != null && group.shiftTypes.Count > 0;
        }

        private static void EnsurePreset(List<ShiftTypeDefinitionData> target, List<ShiftTypeDefinitionData> defaults, int presetId)
        {
            if (FindShift(target, presetId) != null)
                return;

            ShiftTypeDefinitionData preset = FindShift(defaults, presetId);
            if (preset == null)
                return;

            target.Add(new ShiftTypeDefinitionData
            {
                id = preset.id,
                name = preset.name,
                colorHex = preset.colorHex,
                startTime = preset.startTime,
                endTime = preset.endTime,
                hours = preset.hours
            });
        }

        private static ShiftTypeDefinitionData FindShift(List<ShiftTypeDefinitionData> shifts, int id)
        {
            if (shifts == null)
                return null;

            foreach (ShiftTypeDefinitionData shift in shifts)
            {
                if (shift != null && shift.id == id)
                    return shift;
            }

            return null;
        }

        [Serializable]
        private class LocalCalendarSaveData
        {
            public GroupData group;
            public List<DayOverrideData> overrides = new List<DayOverrideData>();
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
