using System;
using System.Collections.Generic;

namespace ShiftCal.Data
{
    public enum RecurrenceKind { Once, Daily, Weekly, Monthly }
    [Serializable] public class EventSeries
    {
        public string id = Guid.NewGuid().ToString("N");
        public string title;
        public string dateKey;
        public string startTime;
        public string endTime;
        public string notes;
        public string zone = "America/Chicago";
        public RecurrenceKind recurrence;
        public int interval = 1;
        public List<int> weekdays = new List<int>(); // Sunday = 0
        public string until;
        public int count;
        public bool reminder;
        public int reminderMinutes = 15;
        public bool alarm;
        public bool enabled = true;
        public bool vibration = true;
        public string sound = "alarm";
        public int snoozeMinutes = 10;
        public int advanceMinutes;
        public bool useDefaultAlarmSettings;
    }
    [Serializable] public class EventException
    {
        public string id;
        public string seriesId;
        public string originalDate;
        public bool cancelled;
        public EventSeries replacement;
    }
    [Serializable] public class ShiftAlarmRule
    {
        public string id = Guid.NewGuid().ToString("N");
        public string groupId;
        public int shiftType;
        public int beforeMinutes = 90;
        public string label = "Wake-up alarm";
        public bool enabled = true;
        public bool audible = true;
        public bool vibration = true;
        public string sound = "alarm";
        public int snoozeMinutes = 10;
        public int advanceMinutes = 60;
        // Zero preserves the relative timing of version-3 rules already installed.
        public ShiftTimingMode timingMode;
        public string fixedTime;
        public string notes;
        public string fromDate;
        public bool calendarOnly;
        public bool useDefaultAlarmSettings;
    }
    public enum ShiftTimingMode { BeforeStart, FixedTime }
    [Serializable] public class AlarmPreferences
    {
        public bool upcomingNotices = true;
        public int noticeMinutes = 15;
        public int snoozeMinutes = 10;
        public string sound = "alarm";
        public bool vibration = true;
    }
    [Serializable] public class ShiftDeliveryChange { public string id; public long at; }
    [Serializable] public class ShiftDeliveryChanges { public List<ShiftDeliveryChange> items = new List<ShiftDeliveryChange>(); }
    [Serializable] public class SyncRecord
    {
        public string key;
        public string json;
        public bool deleted;
        public long revision;
        public bool pending;
        public bool conflict;
        public string remoteJson;
        public bool remoteDeleted;
        public long remoteRevision;
    }
    [Serializable] public class ScheduleSave
    {
        public int version = 3;
        public string account = "local";
        public bool dark = true;
        public GroupData group;
        public List<DayOverrideData> overrides = new List<DayOverrideData>();
        public List<EventSeries> events = new List<EventSeries>();
        public List<EventException> exceptions = new List<EventException>();
        public List<ShiftAlarmRule> rules = new List<ShiftAlarmRule>();
        public List<SyncRecord> records = new List<SyncRecord>();
        public List<string> mutedEvents = new List<string>();
        public List<string> mutedRules = new List<string>();
    }
    [Serializable] public class Occurrence
    {
        public string id;
        public string sourceId;
        public string dateKey;
        public string title;
        public string calendarDateKey;
        public long at;
        public bool audible;
        public bool vibration;
        public string sound;
        public int snoozeMinutes;
        public int advanceMinutes;
        public bool isEvent;
        public string state;
        public string subtitle;
        public string notes;
    }
    [Serializable] public class OccurrenceList { public List<Occurrence> items = new List<Occurrence>(); }
}
