using System;
using System.Collections.Generic;
namespace ShiftCal.Data
{
    [Serializable] public class FamilyProfile
    {
        public string id = Guid.NewGuid().ToString("N"), name, initials, color = "#60A5FA", linkedUid;
        public bool active = true;
    }
    [Serializable] public class CalendarActivity
    {
        public string id = Guid.NewGuid().ToString("N"), groupId, title, icon = "activity", notes, dateKey, startTime, assigneeId, creatorUid;
        public string zone = "America/Chicago", until;
        public bool allDay;
        public bool hasShiftConstraint;
        public int requiredShiftType;
        public int durationMinutes, interval = 1, count;
        public RecurrenceKind recurrence;
        public List<int> weekdays = new List<int>();
        public List<string> participants = new List<string>();
        public long createdAt, updatedAt;
    }
    [Serializable] public class ActivityException
    {
        public string id, seriesId, originalDate;
        public bool cancelled;
        public CalendarActivity replacement;
    }
    [Serializable] public class ActivityTemplate
    {
        public string id = Guid.NewGuid().ToString("N"), name;
        public CalendarActivity activity = new CalendarActivity();
    }
    [Serializable] public class LocalAlarmSubscription
    {
        public string sourceId, originalDate;
        public bool alarm, reminder, enabled = true, vibration = true, useDefaultAlarmSettings = true;
        public int reminderMinutes = 15, snoozeMinutes = 10, advanceMinutes;
        public int alarmOffsetMinutes;
        public string sound = "alarm";
    }
    [Serializable] public class DeviceCalendarState
    {
        public int version = 1;
        public string account, installationId, filter = "everyone";
        public string localProfileId;
        public List<string> recentTemplates = new List<string>();
        public List<ShiftAlarmRule> rules = new List<ShiftAlarmRule>();
        public List<LocalAlarmSubscription> subscriptions = new List<LocalAlarmSubscription>();
        public List<ActivityTemplate> templates = new List<ActivityTemplate>();
        public List<PersonalAlarmDefinition> personalAlarms = new List<PersonalAlarmDefinition>();
        public List<WorkedHoursEntry> workedHours = new List<WorkedHoursEntry>();
        public List<string> mutedEvents = new List<string>(), mutedRules = new List<string>();
    }
    [Serializable] public class PersonalAlarmDefinition
    {
        public string groupId;
        public EventSeries schedule = new EventSeries { zone="device", alarm=true, useDefaultAlarmSettings=true };
        public List<EventException> exceptions = new List<EventException>();
    }
    [Serializable] public class WorkedHoursEntry
    {
        public string groupId, dateKey;
        public int hundredths;
        public long updatedAt;
    }
    // These DTOs deliberately have no delivery preferences, even in their JSON.
    [Serializable] public class SharedEventDefinition
    {
        public string id, title, dateKey, startTime, endTime, notes, zone, until;
        public RecurrenceKind recurrence;
        public int interval, count;
        public bool hasShiftConstraint;
        public int requiredShiftType;
        public List<int> weekdays;
        public static SharedEventDefinition From(EventSeries e) => new SharedEventDefinition { id=e.id,title=e.title,dateKey=e.dateKey,startTime=e.startTime,endTime=e.endTime,notes=e.notes,zone=e.zone,until=e.until,recurrence=e.recurrence,interval=e.interval,count=e.count,weekdays=e.weekdays,hasShiftConstraint=e.hasShiftConstraint,requiredShiftType=e.requiredShiftType };
    }
    [Serializable] public class SharedEventException
    {
        public string id, seriesId, originalDate;
        public bool cancelled;
        public SharedEventDefinition replacement;
    }
}
