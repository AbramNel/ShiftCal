using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShiftCal.App;
using ShiftCal.Core;
using ShiftCal.Data;
using ShiftCal.Firebase;
using ShiftCal.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;
public static class DayAndHoursChecks
{
    static DateTime D(string key)=>DateKeyUtility.FromDateKey(key);
    // Keep the dense-day alarm in the future so its badge is still eligible to display.
    public static string DenseDateKey=>DateKeyUtility.ToDateKey(DateTime.Today.AddDays(1));
    public static void Models(Action<bool,string> check,FamilyWorkbench work)
    {
        var session=AppSession.Instance;var previous=session.Data;var device=DeviceCalendarStore.State;var group=session.CurrentGroup;var overrides=session.CalendarOverrides.ToDictionary(x=>x.Key,x=>x.Value);
        var s=new ScheduleSave{account="hours-check-"+Guid.NewGuid().ToString("N"),group=new GroupData{groupId="hours-test",startDateKey="2026-10-01",pattern=new List<int>{0},shiftTypes=new List<ShiftTypeDefinitionData>{new ShiftTypeDefinitionData{id=0,name="Days",startTime="05:30",endTime="17:30"},new ShiftTypeDefinitionData{id=1,name="OFF"},new ShiftTypeDefinitionData{id=2,name="Nights",startTime="17:30",endTime="05:30"}}}};
        var local=new DeviceCalendarState{account=s.account};
        try
        {
            typeof(AppSession).GetProperty("Data").SetValue(session,s);session.CurrentGroup=s.group;session.CalendarOverrides.Clear();typeof(DeviceCalendarStore).GetProperty("State").SetValue(null,local);
            var a=new CalendarActivity{id="conditional",title="Shutdown Meeting",dateKey="2026-10-01",startTime="13:00",zone="America/Chicago",recurrence=RecurrenceKind.Weekly,weekdays=new List<int>{4},hasShiftConstraint=true,requiredShiftType=0,count=3};s.activities.Add(a);
            s.overrides.Add(new DayOverrideData{dateKey="2026-10-01",shiftType=1});s.overrides.Add(new DayOverrideData{dateKey="2026-10-08",shiftType=2});
            var dates=ActivityResolver.Resolve(s,D("2026-10-01"),D("2026-11-30"));check(dates.Select(x=>x.dateKey).SequenceEqual(new[]{"2026-10-15","2026-10-22","2026-10-29"}),"weekday AND resolved shift, zero ID, count after filtering");
            check(!ActivityResolver.Resolve(s,D("2026-10-02"),D("2026-10-02")).Any(),"Days alone does not pass Thursday condition");
            s.group.shiftTypes[0].name="Renamed Days";check(ActivityResolver.Resolve(s,D("2026-10-15"),D("2026-10-15")).Count==1,"shift rename preserves stable constraint");
            s.overrides[0].scheduledShift=true;check(ActivityResolver.Resolve(s,D("2026-10-01"),D("2026-10-01")).Count==1,"restored rotation restores eligibility");s.overrides[0].scheduledShift=false;
            s.group.shiftTypes[0].retired=true;check(ActivityResolver.Resolve(s,D("2026-10-15"),D("2026-10-15")).Count==1,"retired stable shift remains resolvable");s.group.shiftTypes.RemoveAt(0);check(ActivityResolver.Resolve(s,D("2026-10-01"),D("2026-11-30")).Count==0,"missing shift is gracefully ineligible");s.group.shiftTypes.Insert(0,new ShiftTypeDefinitionData{id=0,name="Days",startTime="05:30",endTime="17:30"});
            var shiftOnly=ActivityResolver.Clone(a);shiftOnly.recurrence=RecurrenceKind.Daily;shiftOnly.count=0;check(ActivityResolver.NextDates(shiftOnly,D("2026-10-01"),3,s).SequenceEqual(new[]{D("2026-10-02"),D("2026-10-03"),D("2026-10-04")}),"daily optional shift supports every Days shift");
            var ordinary=ActivityResolver.Clone(a);ordinary.hasShiftConstraint=false;check(ActivityResolver.NextDates(ordinary,D("2026-10-01"),3,s).First()==D("2026-10-01"),"legacy weekly ignores shifts");ordinary.recurrence=RecurrenceKind.Once;check(ActivityResolver.NextDates(ordinary,D("2026-10-01"),3,s).Count()==1,"ordinary once is unchanged");
            local.subscriptions.Add(new LocalAlarmSubscription{sourceId="activity-conditional",alarm=true,alarmOffsetMinutes=30});var alarms=RecurrenceEngine.Resolve(DeviceCalendarStore.Effective(s,local),D("2026-10-01"),D("2026-11-30")).Where(x=>x.audible).ToList();check(alarms.Count==3&&alarms[0].at==DateTimeOffset.Parse("2026-10-15T17:30:00Z").ToUnixTimeMilliseconds(),"13:00 event has exactly one audible 12:30 alarm");
            check(!RecurrenceEngine.Resolve(DeviceCalendarStore.Effective(s,new DeviceCalendarState{account=s.account}),D("2026-10-01"),D("2026-11-30")).Any(x=>x.audible),"second device sees event without inheriting audible alarm");
            var moved=ActivityResolver.Clone(a);moved.dateKey="2026-10-16";ActivityResolver.SaveEdit(s,moved,a.id,"2026-10-15",0);dates=ActivityResolver.Resolve(s,D("2026-10-01"),D("2026-11-30"));check(dates.Count==3&&dates.Count(x=>x.originalDate=="2026-10-15")==1&&dates.Any(x=>x.dateKey=="2026-10-16"),"single replacement retains original identity without duplicate");s.activityExceptions.Clear();
            var future=ActivityResolver.Clone(a);future.dateKey="2026-10-22";ActivityResolver.SaveEdit(s,future,a.id,"2026-10-22",1);check(future.count==2&&ActivityResolver.Resolve(s,D("2026-10-01"),D("2026-11-30")).Count==3,"future split consumes qualifying dates only");s.activities.Remove(future);a.until=null;
            var rule=new ShiftAlarmRule{groupId=s.group.groupId,shiftType=0,timingMode=ShiftTimingMode.FixedTime,fixedTime="12:30",hasWeekdayConstraint=true,weekdays=new List<int>{4}};s.rules.Add(rule);check(RecurrenceEngine.ShiftDates(s,D("2026-10-01"),D("2026-10-31")).Count==3,"private shift rule AND weekday");rule.hasWeekdayConstraint=false;check(RecurrenceEngine.ShiftDates(s,D("2026-10-01"),D("2026-10-31")).Count==29,"legacy shift rule retains every matching day");
            var ui=work.editor;ui.New("2026-10-15");check(ui.applyTo.value==0&&ui.Draft.recurrence==RecurrenceKind.Once&&!ui.Draft.hasShiftConstraint&&ui.requiredShift.options[ui.requiredShift.value].id=="0","new event suggests stable shift but defaults to this date only");ui.title.text="Conditional UI";ui.applyTo.value=3;foreach(var t in ui.weekdays)t.isOn=false;ui.weekdays[4].isOn=true;check(ui.Draft.recurrence==RecurrenceKind.Weekly&&ui.Draft.hasShiftConstraint&&ui.repeatPreview.text.Contains("Thursday")&&ui.repeatPreview.text.Contains("Days"),"conditional editor live preview uses real weekday and shift");ui.panel.SetActive(false);
            var old=Object.FindFirstObjectByType<ScheduleWorkbench>(FindObjectsInactive.Include);int sharedEvents=s.events.Count,sharedActivities=s.activities.Count;old.NewPersonalAlarm("2026-10-16");check(old.eventDate.value=="2026-10-16"&&old.recurrence.value==0,"quick private alarm receives selected date and remains oneoff");old.SaveEvent();check(local.personalAlarms.Count==1&&s.events.Count==sharedEvents&&s.activities.Count==sharedActivities,"standalone alarm creates private definition only");check(DeviceCalendarStore.Effective(s).events.Any(x=>x.id==local.personalAlarms[0].schedule.id&&x.alarm),"private oneoff enters existing native snapshot");old.Hide();
            old.NewPersonalAlarm("2026-10-15");old.dayContextChoice.value=3;old.timingMode.value=1;old.eventTime.Set("12:30");foreach(var chip in old.weekdays)chip.isOn=false;old.weekdays[4].isOn=true;old.SaveEvent();check(s.rules.Last().hasWeekdayConstraint&&s.rules.Last().weekdays.SequenceEqual(new[]{4})&&s.rules.Last().timingMode==ShiftTimingMode.FixedTime&&s.rules.Last().fixedTime=="12:30","private alarm UI saves weekday plus stable shift rule");old.Hide();
            var weekly=new EventSeries{title="Private weekly",dateKey="2026-10-01",startTime="04:00",zone="device",alarm=true,recurrence=RecurrenceKind.Weekly,weekdays=new List<int>{4},count=4};PersonalAlarmStore.Save(weekly,null,null,0);var definition=local.personalAlarms.Last();var edit=ActivityResolver.Clone(weekly);edit.dateKey="2026-10-09";PersonalAlarmStore.Save(edit,definition,"2026-10-08",0);check(RecurrenceEngine.Resolve(DeviceCalendarStore.Effective(s),D("2026-10-01"),D("2026-10-31")).Count(o=>o.sourceId==weekly.id&&o.audible)==4,"private weekly single edit retains four deliveries without duplicates");var split=ActivityResolver.Clone(weekly);split.dateKey="2026-10-15";PersonalAlarmStore.Save(split,definition,"2026-10-15",1);check(split.count==2&&RecurrenceEngine.Resolve(DeviceCalendarStore.Effective(s),D("2026-10-01"),D("2026-10-31")).Count(o=>(o.sourceId==weekly.id||o.sourceId==split.id)&&o.audible)==4,"private future split retains remaining count and original occurrence identity");DeviceCalendarStore.Capture(s);
            foreach(var pair in new[]{("8",800),("10.5",1050),("11.75",1175),("0",0),("24",2400),("11,75",1175)})check(WorkedHoursStore.TryParse(pair.Item1,out var n)&&n==pair.Item2,"precise hours input "+pair.Item1);
            foreach(string bad in new[]{"","-1","24.01","1.001","11.750","text","1,2.3"})check(!WorkedHoursStore.TryParse(bad,out _),"invalid hours rejected "+bad);
            check(WorkedHoursStore.Get("2026-10-15")==null,"no hours are inferred from scheduled shift");WorkedHoursStore.Set("2026-10-15",0);check(WorkedHoursStore.Get("2026-10-15")?.hundredths==0,"explicit zero is distinct from absent");WorkedHoursStore.Set("2026-10-15",1175);WorkedHoursStore.Set("2026-10-01",1050);WorkedHoursStore.Set("2026-09-30",800);check(WorkedHoursStore.Total(D("2026-10-01"))==2225,"monthly total includes OFF-day actual hours and excludes adjacent month");WorkedHoursStore.Set("2026-10-15",null);check(WorkedHoursStore.Get("2026-10-15")==null&&WorkedHoursStore.Total(D("2026-10-01"))==1050,"clear removes record and updates actual monthly sum");
            var entry=WorkedHoursStore.Get("2026-10-01");session.CurrentGroup=ActivityResolver.Clone(s.group);session.CurrentGroup.groupId="different-group";check(WorkedHoursStore.Get("2026-10-01")==null&&WorkedHoursStore.Total(D("2026-10-01"))==0,"hours isolated by calendar");session.CurrentGroup=s.group;s.account+="-other";check(WorkedHoursStore.Get("2026-10-01")==null,"hours isolated by account");s.account=local.account;
            DeviceCalendarStore.Load(s,true);check(WorkedHoursStore.Get("2026-10-01")?.hundredths==1050&&DeviceCalendarStore.State.personalAlarms.Count==3,"restart reload preserves hours and private alarm");local=DeviceCalendarStore.State;
            session.CalendarOverrides["2026-10-01"]=new DayOverrideData{dateKey="2026-10-01",shiftType=1,personName="Stored person",note="Old",userId="metadata"};session.UpdateDayNotes("2026-10-01","New notes");check(session.CalendarOverrides["2026-10-01"].personName=="Stored person"&&!session.CalendarOverrides["2026-10-01"].scheduledShift,"note-only save preserves hidden person and manual shift");session.RestoreScheduledShift("2026-10-01");check(session.CalendarOverrides["2026-10-01"].note=="New notes"&&session.CalendarOverrides["2026-10-01"].personName=="Stored person"&&WorkedHoursStore.Get("2026-10-01")?.hundredths==1050,"restore scheduled shift retains notes, hidden person and hours");session.UpdateDayNotes("2026-10-02","Note alone");check(session.CalendarOverrides["2026-10-02"].scheduledShift,"new note does not create shift override");
            FirestoreService.Instance.TrackChanges(s);check(s.records.All(r=>!r.json.Contains("hundredths")&&!r.json.Contains("alarmOffsetMinutes")&&!r.json.Contains("personal-")),"shared records contain no hours, standalone alarms or local ring offset");check(JsonUtility.ToJson(SharedEventDefinition.From(ActivityResolver.AsEvent(a))).Contains("hasShiftConstraint"),"optional shift condition roundtrips through shared DTO");
            s.group.name="Remote calendar rename";DeviceCalendarStore.Hydrate(s);check(WorkedHoursStore.Get("2026-10-01")==entry||WorkedHoursStore.Get("2026-10-01")?.hundredths==1050,"remote hydration retains private actual hours");
            var popup=work.calendar.GetComponentInChildren<DayDetailsPopup>(true);check(!popup.GetComponentsInChildren<InputField>(true).Any(x=>x.name=="person")&&!popup.GetComponentsInChildren<Text>(true).Any(x=>x.name=="Hours"),"saved Day Details omits obsolete Person and scheduled hours label");
        }
        finally
        {
            work.editor.panel.SetActive(false);Object.FindFirstObjectByType<ScheduleWorkbench>(FindObjectsInactive.Include).Hide();foreach(string file in Directory.GetFiles(Application.persistentDataPath,Path.GetFileName(ScheduleStorage.PathFor(local.account))+"*"))File.Delete(file);
            typeof(AppSession).GetProperty("Data").SetValue(session,previous);session.CurrentGroup=group;session.CalendarOverrides.Clear();foreach(var pair in overrides)session.CalendarOverrides[pair.Key]=pair.Value;typeof(DeviceCalendarStore).GetProperty("State").SetValue(null,device);
        }
    }
    static Rect World(RectTransform rect){var corners=new Vector3[4];rect.GetWorldCorners(corners);return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);}
    public static void CheckDense(Action<bool,string> check,CalendarController calendar)
    {
        foreach(var cell in calendar.GetComponentsInChildren<CalendarDayCell>(true).Where(c=>c.workedBadge.activeSelf))
        {
            Rect hours=World((RectTransform)cell.workedBadge.transform);check(!hours.Overlaps(World((RectTransform)cell.transform.Find("ShiftName")))&&!hours.Overlaps(World((RectTransform)cell.transform.Find("DayNumber"))),"hours badge has separate geometry from shift/day "+cell.DateKey);
            foreach(var icon in cell.activityIcons.Where(x=>x.transform.parent.gameObject.activeSelf))check(!hours.Overlaps(World((RectTransform)icon.transform.parent)),"hours never overlaps activity icon "+cell.DateKey);
        }
        var dense=calendar.GetComponentsInChildren<CalendarDayCell>(true).Single(c=>c.DateKey==DenseDateKey);var date=D(DenseDateKey);int count=ActivityResolver.Resolve(AppSession.Instance.Data,date,date).Count;
        check(count>=6&&dense.workedBadge.activeSelf&&dense.activityIcons.Count(x=>x.transform.parent.gameObject.activeSelf)==2&&dense.activityOverflow.text=="+"+(count-2),"dense badge preserves existing icon capacity and +N");check(dense.transform.Find("Note indicator").gameObject.activeSelf&&dense.transform.Find("Alarm indicator").gameObject.activeSelf&&dense.IsSelected,"dense note/alarm/selection preserved together");
    }
    public static void SeedDense(ScheduleSave data)
    {
        var state=DeviceCalendarStore.State;for(int i=14;i<=20;i++){string key="2026-10-"+i;state.workedHours.RemoveAll(x=>x.groupId==data.group.groupId&&x.dateKey==key);state.workedHours.Add(new WorkedHoursEntry{groupId=data.group.groupId,dateKey=key,hundredths=i==14?0:i==15?800:1175});}
        state.workedHours.Add(new WorkedHoursEntry{groupId=data.group.groupId,dateKey="2026-09-30",hundredths=800});
        string dense=DenseDateKey;state.workedHours.RemoveAll(x=>x.groupId==data.group.groupId&&x.dateKey==dense);state.workedHours.Add(new WorkedHoursEntry{groupId=data.group.groupId,dateKey=dense,hundredths=1175});
        for(int i=0;i<6;i++)data.activities.Add(new CalendarActivity{id="dense-"+i,title="Dense event "+i,icon=ActivityIconSet.Ids[i],dateKey=dense,assigneeId=data.profiles[i%data.profiles.Count].id});
        for(int i=0;i<3;i++)data.activities.Add(new CalendarActivity{id="three-"+i,title="Family plan "+i,icon=ActivityIconSet.Ids[i+4],dateKey="2026-10-17",assigneeId=data.profiles[i%data.profiles.Count].id});
        AppSession.Instance.CalendarOverrides[dense]=new DayOverrideData{dateKey=dense,scheduledShift=true,note="Remember the forms"};data.overrides=AppSession.Instance.CalendarOverrides.Values.ToList();state.personalAlarms.Add(new PersonalAlarmDefinition{groupId=data.group.groupId,schedule=new EventSeries{id="personal-dense",title="Wake Up",dateKey=dense,startTime="04:00",zone="device",alarm=true}});
    }
}
