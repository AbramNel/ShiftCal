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
        public string sound = "alarm";
    }
    [Serializable] public class DeviceCalendarState
    {
        public int version = 1;
        public string account, installationId, filter = "everyone";
        public List<ShiftAlarmRule> rules = new List<ShiftAlarmRule>();
        public List<LocalAlarmSubscription> subscriptions = new List<LocalAlarmSubscription>();
        public List<ActivityTemplate> templates = new List<ActivityTemplate>();
        public List<string> mutedEvents = new List<string>(), mutedRules = new List<string>();
    }
    // These DTOs deliberately have no delivery preferences, even in their JSON.
    [Serializable] public class SharedEventDefinition
    {
        public string id, title, dateKey, startTime, endTime, notes, zone, until;
        public RecurrenceKind recurrence;
        public int interval, count;
        public List<int> weekdays;
        public static SharedEventDefinition From(EventSeries e) => new SharedEventDefinition { id=e.id,title=e.title,dateKey=e.dateKey,startTime=e.startTime,endTime=e.endTime,notes=e.notes,zone=e.zone,until=e.until,recurrence=e.recurrence,interval=e.interval,count=e.count,weekdays=e.weekdays };
    }
    [Serializable] public class SharedEventException
    {
        public string id, seriesId, originalDate;
        public bool cancelled;
        public SharedEventDefinition replacement;
    }
}
