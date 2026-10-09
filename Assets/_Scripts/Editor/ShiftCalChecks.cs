using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ShiftCal.App;
using ShiftCal.Core;
using ShiftCal.Data;
using ShiftCal.Firebase;
using ShiftCal.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class ShiftCalChecks
{
    public static void RebuildAndRun(){ShiftCalSceneBuilder.BuildPermanentCalendarApp();Run();}
    public static void BuildAndroid() => BuildAndroidInternal(BuildOptions.None);
    public static void BuildDevelopmentAndroid() => BuildAndroidInternal(BuildOptions.Development);
    private static void BuildAndroidInternal(BuildOptions options)
    {
        Directory.CreateDirectory("Logs/Android");
        string storePassword=PlayerSettings.Android.keystorePass,aliasPassword=PlayerSettings.Android.keyaliasPass;
        try
        {
            // The configured standard Android debug key has public, standard passwords.
            // Never substitute a key or change the certificate of the project.
            if(PlayerSettings.Android.keyaliasName=="androiddebugkey"&&PlayerSettings.Android.keystoreName.EndsWith("debug.keystore"))
            { PlayerSettings.Android.keystorePass="android";PlayerSettings.Android.keyaliasPass="android"; }
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Calendar.unity"},locationPathName="Logs/Android/ShiftCal-ui.apk",target=BuildTarget.Android,options=options});
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Android build failed: "+report.summary.result);
            Debug.Log("ShiftCal Android build passed: "+report.summary.totalSize+" bytes");
        }
        finally { PlayerSettings.Android.keystorePass=storePassword;PlayerSettings.Android.keyaliasPass=aliasPassword; }
    }
    private static int checks;
    private static void Check(bool condition,string label){if(!condition)throw new Exception("FAILED: "+label);checks++;Debug.Log("PASS: "+label);}
    private static GroupData Group()=>new GroupData{groupId="local-check",name="Saved rotation",startDateKey="2026-07-01",pattern=new List<int>{2,2,1},shiftTypes=new List<ShiftTypeDefinitionData>{new ShiftTypeDefinitionData{id=1,name="OFF",colorHex="#9FF4F1"},new ShiftTypeDefinitionData{id=2,name="Day-12",colorHex="#FBBF24",startTime="5:30 AM",endTime="5:30 PM",hours=12}}};
    [MenuItem("ShiftCal/Run Focused Checks")]
    public static void Run()
    {
        checks=0;
        var group=Group();
        Check(ShiftPatternUtility.Resolve(group.pattern,group.startDateKey,new DateTime(2026,7,3))==1,"rotation resolves OFF");
        var overrides=new Dictionary<string,DayOverrideData>{{"2026-07-01",new DayOverrideData{dateKey="2026-07-01",shiftType=1,note="Keep me",personName="Abram"}}};
        Check(CalendarGenerator.Generate(new DateTime(2026,7,1),group,overrides).Find(d=>d.dateKey=="2026-07-01").ResolvedShift==1,"override wins rotation");
        Check(ShiftTimeUtility.TryCalculateHours("5:30 PM","5:30 AM",out float hours)&&hours==12,"overnight hours");
        Check(!ShiftTimeUtility.TryCalculateHours("5:30 AM","",out _),"partial range rejected");
        var meeting=new EventSeries{title="Shutdown meeting",dateKey="2026-07-02",startTime="1:00 PM",recurrence=RecurrenceKind.Weekly,weekdays=new List<int>{4},alarm=true,reminder=true};
        var dates=RecurrenceEngine.Dates(meeting,new DateTime(2026,7,1),new DateTime(2026,7,31)).ToArray();
        Check(dates.Length==5&&dates.All(d=>d.DayOfWeek==DayOfWeek.Thursday),"weekly Thursday meetings independent of rotation");
        Check(RecurrenceEngine.Instant(new DateTime(2026,3,8),"2:30 AM","America/Chicago")==DateTimeOffset.Parse("2026-03-08T08:30:00Z").ToUnixTimeMilliseconds(),"DST gap advances by transition length");
        Check(RecurrenceEngine.Instant(new DateTime(2026,11,1),"1:30 AM","America/Chicago")==DateTimeOffset.Parse("2026-11-01T06:30:00Z").ToUnixTimeMilliseconds(),"DST fold chooses earlier instant");
        var save=new ScheduleSave{account="check-"+Guid.NewGuid().ToString("N"),group=group,events=new List<EventSeries>{meeting},rules=new List<ShiftAlarmRule>{new ShiftAlarmRule{shiftType=2,beforeMinutes=90}}};
        var occurrence=RecurrenceEngine.Resolve(save,new DateTime(2026,7,1),new DateTime(2026,7,3));
        Check(occurrence.Count(x=>x.isEvent)==2,"advance reminder and event alarm have separate deliveries");
        var changed=JsonUtility.FromJson<EventSeries>(JsonUtility.ToJson(meeting));changed.reminderMinutes=30;changed.alarm=false;changed.sound="silent";
        save.exceptions.Add(new EventException{id=meeting.id+"_2026-07-02",seriesId=meeting.id,originalDate="2026-07-02",replacement=changed});
        var changedAlarm=RecurrenceEngine.Resolve(save,new DateTime(2026,7,2),new DateTime(2026,7,2)).Single(x=>x.isEvent);
        Check(!changedAlarm.audible&&changedAlarm.sound=="silent"&&changedAlarm.at==DateTimeOffset.Parse("2026-07-02T17:30:00Z").ToUnixTimeMilliseconds(),"occurrence edit uses its own alert settings");save.exceptions.Clear();
        save.overrides.Add(overrides.Values.First());
        Check(!RecurrenceEngine.Resolve(save,new DateTime(2026,7,1),new DateTime(2026,7,1)).Any(x=>!x.isEvent),"OFF override cancels shift alarm");
        ScheduleStorage.Write(save);
        try{
            var restored=ScheduleStorage.Load(save.account,Group());
            Check(restored.group.name=="Saved rotation"&&restored.overrides[0].note=="Keep me"&&restored.events[0].title==meeting.title,"versioned save round-trip preserves schedule and notes");
            Check(ScheduleStorage.PathFor(save.account)!=ScheduleStorage.PathFor("other-account"),"account storage separation");
            save.group.name="Second save";ScheduleStorage.Write(save);File.WriteAllText(ScheduleStorage.PathFor(save.account),"broken json");
            Check(ScheduleStorage.Load(save.account,Group()).group.name=="Saved rotation","corrupt current save recovers previous without resetting rotation");
        } finally{foreach(var suffix in new[]{"",".previous",".tmp"}){var p=ScheduleStorage.PathFor(save.account)+suffix;if(File.Exists(p))File.Delete(p);}}
        FirestoreService.Apply(save,"event/"+meeting.id,"",true);
        Check(save.events.Count==0,"record deletion removes event without replacing calendar");
        var migrated=ScheduleStorage.MigrateLegacy("{\"group\":{\"groupId\":\"legacy\",\"name\":\"My old schedule\",\"startDateKey\":\"2026-07-01\",\"pattern\":[2,1,2,1],\"shiftTypes\":[]},\"overrides\":[{\"dateKey\":\"2026-07-05\",\"shiftType\":1,\"note\":\"Holiday\",\"personName\":\"Abram\"}]}",Group());
        Check(migrated.group.name=="My old schedule"&&migrated.group.pattern.SequenceEqual(new[]{2,1,2,1})&&migrated.overrides[0].personName=="Abram","legacy migration preserves original rotation and labels even with missing presets");
        EditorSceneManager.OpenScene("Assets/Calendar.unity");
        var session=Object.FindFirstObjectByType<AppSession>();AppSession.Instance=session;
        typeof(AppSession).GetProperty("Data").SetValue(session,new ScheduleSave{account="focused-scene-"+Guid.NewGuid().ToString("N"),group=Group()});session.CurrentGroup=Group();
        session.CalendarOverrides["2026-07-01"]=new DayOverrideData{dateKey="2026-07-01",shiftType=2,note="Preserved",personName="Abram"};session.SetShift("2026-07-01",1);
        Check(session.CalendarOverrides["2026-07-01"].note=="Preserved"&&session.CalendarOverrides["2026-07-01"].personName=="Abram","shift editing preserves note/person");
        Check(!session.CanDeleteShift(2,out _),"in-use shift deletion blocked");
        var sync=Object.FindFirstObjectByType<FirestoreService>();FirestoreService.Instance=sync;
        var data=session.Data;var item=new EventSeries{title="Local meeting",dateKey="2026-07-02",startTime="1:00 PM"};data.events.Add(item);data.records.Add(new SyncRecord{key="event/"+item.id,json=JsonUtility.ToJson(item),revision=1,pending=true});
        sync.Merge("event/"+item.id,"",true,2);
        Check(data.events.Count==1&&data.records[0].conflict,"pending local edit conflicts with remote deletion instead of resurrecting silently");
        var pending=data.records[0];pending.conflict=false;pending.revision=1;pending.json="newer local edit";
        typeof(FirestoreService).GetField("uploading",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(sync,true);
        typeof(FirestoreService).GetField("uploadKey",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(sync,pending.key);
        typeof(FirestoreService).GetField("uploadJson",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(sync,"in-flight edit");
        typeof(FirestoreService).GetField("uploadRevision",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(sync,1L);
        sync.Merge(pending.key,"in-flight edit",false,2);
        Check(pending.revision==2&&pending.pending&&!pending.conflict&&pending.json=="newer local edit","own sync acknowledgement preserves newer pending edit without a false conflict");
        typeof(FirestoreService).GetField("uploading",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(sync,false);
        data.records.Clear();data.events.Clear();
        var cal=Object.FindFirstObjectByType<CalendarController>(FindObjectsInactive.Include);
        Check(cal!=null&&Object.FindObjectsByType<CalendarDayCell>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==42,"42 calendar cells wired");
        var iconCell=cal.GetComponentsInChildren<CalendarDayCell>(true)[0];
        Check(iconCell.transform.Find("Alarm indicator").GetComponent<Image>().sprite!=null&&iconCell.transform.Find("Note indicator").GetComponent<Image>().sprite!=null,"crisp alarm/note sprites assigned");
        Check(iconCell.GetComponent<Image>().sprite!=null,"rounded cell sprite assigned");
        var work=Object.FindFirstObjectByType<ScheduleWorkbench>(FindObjectsInactive.Include);
        Check(work!=null&&work.rowPrefab!=null&&work.eventFields.Count==4&&work.eventDate!=null&&work.eventTime!=null&&work.weekdays.Length==7,"event/alarm editor fields and prefab wired");
        work.NewEvent("2026-07-02");work.eventFields.Find(x=>x.name=="title").text="Shutdown meeting";work.recurrence.value=2;work.eventAlarm.isOn=true;work.SaveEvent();
        Check(session.Data.events.Count==1&&session.Data.events[0].weekdays.SequenceEqual(new[]{4}),"scene event editor saves Thursday meeting");
        work.NewRule();work.SaveEvent();
        Check(session.Data.rules.Count==1&&session.Data.rules[0].beforeMinutes==90,"scene shift alarm editor saves 90-minute rule");
        Check(RecurrenceEngine.Resolve(session.Data,new DateTime(2026,7,2),new DateTime(2026,7,2)).Any(x=>x.isEvent),"saved scene meeting resolves independently");
        EventEditorChecks(session,work);
        foreach(var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None))for(int i=0;i<b.onClick.GetPersistentEventCount();i++)Check(b.onClick.GetPersistentTarget(i)!=null&&!string.IsNullOrEmpty(b.onClick.GetPersistentMethodName(i)),"button wiring: "+b.name);
        Check(!Object.FindObjectsByType<Text>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(t=>t.text.Contains("11:29")),"fake system status removed");
        RefinementChecks(session,cal,work);
        FamilyCalendarChecks.Run(session,cal,work);
        foreach(var size in new[]{new Vector2Int(360,640),new Vector2Int(393,851),new Vector2Int(412,915)})Capture(size,cal,work);
        Debug.Log("ShiftCal focused checks passed: "+checks);
    }
    private static void DeviceSnapshot(ScheduleSave data){typeof(DeviceCalendarStore).GetProperty("State").SetValue(null,new DeviceCalendarState{account=data.account,rules=ActivityResolver.CloneList(data.rules),subscriptions=data.events.Select(DeviceCalendarStore.FromEvent).ToList(),mutedEvents=new List<string>(data.mutedEvents),mutedRules=new List<string>(data.mutedRules)});}
    private static void EventEditorChecks(AppSession session,ScheduleWorkbench work)
    {
        var previous=session.Data;
        var data=new ScheduleSave{account="editor-check-"+Guid.NewGuid().ToString("N"),group=Group()};
        typeof(AppSession).GetProperty("Data").SetValue(session,data);
        try
        {
            work.NewEvent("2026-10-08",2);
            Check(work.contextSection.activeSelf&&work.linkedShift.options[work.linkedShift.value].text=="Day-12","Day Details date and stable shift preselected");
            Check(work.eventDate.GetComponent<InputField>()==null&&work.eventTime.GetComponent<InputField>()==null,"date/time fields are buttons, never editable inputs");
            Check(!work.intervalSection.activeSelf&&!work.endingSection.activeSelf&&!work.weekdaySection.activeSelf,"Once hides all recurrence detail");
            for(int kind=1;kind<=3;kind++)
            {
                work.recurrence.value=kind;
                Check(work.intervalSection.activeSelf&&work.weekdaySection.activeSelf==(kind==2),"conditional recurrence sections "+kind);
            }
            work.repeatEnding.value=1;Check(work.endingDateSection.activeSelf&&!work.countSection.activeSelf,"repeat ends on date shows picker only");
            work.repeatEnding.value=2;Check(!work.endingDateSection.activeSelf&&work.countSection.activeSelf,"repeat count shows count only");
            var picker=Object.FindFirstObjectByType<UnityPickerDialog>(FindObjectsInactive.Include);
            work.eventDate.Open();string old=work.eventDate.value;
            Check(PickerCoordinator.IsOpen&&picker.gameObject.activeSelf&&picker.year.value==26&&picker.month.value==9&&picker.day.value==7,"Editor date picker initializes current selection");
            work.eventTime.Open();Check(picker.dateSection.activeSelf,"second picker cannot overlap");picker.Cancel();Check(work.eventDate.value==old&&!PickerCoordinator.IsOpen,"cancel preserves selected date");
            work.eventDate.Open();picker.day.value=8;picker.Accept();Check(work.eventDate.value=="2026-10-09"&&work.weekdays[5].isOn,"date OK updates date key and default weekday");
            work.eventTime.Set("04:00");work.eventTime.Open();Check(picker.hour.value==4&&picker.minute.value==0,"Editor time picker initializes hours/minutes");picker.minute.value=15;picker.Accept();Check(work.eventTime.value=="04:15","time OK serializes valid 24-hour time");
            work.dayContextChoice.value=1;work.eventFields[0].text="Wake Up";work.beforeShift.Set(90);work.SaveEvent();
            Check(data.events.Count==0&&data.rules.Count==1&&data.rules[0].shiftType==2&&data.rules[0].beforeMinutes==90,"shift event creates exactly one authoritative rule");
            var rule=data.rules[0];var date=DateKeyUtility.FromDateKey("2026-10-09");
            data.overrides.Add(new DayOverrideData{dateKey="2026-10-09",shiftType=2});
            var occurrence=RecurrenceEngine.ShiftDates(data,date,date).Single();
            Check(occurrence.at==RecurrenceEngine.Instant(date,"04:00","device")&&occurrence.calendarDateKey=="2026-10-09","relative rule anchored to resolved shift date");
            rule.timingMode=ShiftTimingMode.FixedTime;rule.fixedTime="03:45";rule.calendarOnly=true;rule.audible=false;
            DeviceSnapshot(data);var info=DayInformation.Resolve(data,date,date)["2026-10-09"];
            Check(info.events.Count==1&&info.alarms.Count==0,"Alarm OFF rule visible once without any notification indicator");
            Check(info.events[0].at==RecurrenceEngine.Instant(date,"03:45","device"),"fixed clock time matches qualifying shift date");
            work.LoadRule(rule,"2026-10-09");Check(!work.advancedButton.activeSelf&&!work.eventAlarm.interactable,"single shift occurrence exposes delivery controls only");
            data.overrides[0].shiftType=1;Check(!RecurrenceEngine.ShiftDates(data,date,date).Any(),"OFF removes linked date");
            data.overrides[0].shiftType=2;Check(RecurrenceEngine.ShiftDates(data,date,date).Count==1,"restoring shift restores qualifying date");
            data.group.shiftTypes[1].name="Renamed";Check(RecurrenceEngine.ShiftDates(data,date,date).Single().id==occurrence.id,"rename preserves occurrence identity");
            var legacy=new EventSeries{title="Legacy reminder",dateKey="2026-10-08",startTime="04:00",endTime="05:00",zone="America/Chicago",alarm=false,reminder=true,reminderMinutes=0,sound="notification",vibration=false,snoozeMinutes=7,advanceMinutes=25};data.events.Add(legacy);
            DeviceSnapshot(data);work.EditEvent(legacy.id,legacy.dateKey);work.eventFields.Find(f=>f.name=="notes").text="Saved notes";work.SaveEvent();
            var saved=data.events.Single();
            Check(saved.id==legacy.id&&saved.zone==legacy.zone&&saved.endTime==legacy.endTime&&saved.advanceMinutes==25,"saving compact form preserves hidden legacy fields and ID");
            var local=DeviceCalendarStore.Find(saved.id);Check(!saved.alarm&&!saved.reminder&&local.reminder&&local.reminderMinutes==0&&local.sound=="notification"&&!local.vibration&&local.snoozeMinutes==7&&saved.notes=="Saved notes","reminder-only and explicit alarm overrides survive saving");
            DeviceCalendarStore.Find(saved.id).sound="legacy-device-sound";saved.sound="legacy-device-sound";work.EditEvent(saved.id,saved.dateKey);work.SaveEvent();Check(DeviceCalendarStore.Find(saved.id).sound=="legacy-device-sound","unchanged legacy sound value survives compact edit");
            var restored=JsonUtility.FromJson<ShiftAlarmRule>("{\"id\":\"old\",\"shiftType\":2,\"beforeMinutes\":90}");
            Check(restored.timingMode==ShiftTimingMode.BeforeStart&&!restored.calendarOnly&&!restored.useDefaultAlarmSettings,"missing model fields preserve old shift-rule semantics");
            FirestoreService.Apply(data,"rule/"+rule.id,JsonUtility.ToJson(rule),false);
            Check(data.rules.Single().timingMode==ShiftTimingMode.FixedTime&&data.rules.Single().calendarOnly,"legacy remote rule parsing cannot alter local rule definitions");
            var monthly=new EventSeries{title="Month end",dateKey="2026-01-31",startTime="04:00",zone="UTC",endTime="05:00",recurrence=RecurrenceKind.Monthly,count=5};data.events.Add(monthly);
            var replacement=new EventSeries{title="Changed occurrence",dateKey="2026-02-28",startTime="06:00",zone="America/Chicago"};
            data.exceptions.Add(new EventException{id="changed",seriesId=monthly.id,originalDate="2026-02-28",replacement=replacement});
            data.exceptions.Add(new EventException{id="skip",seriesId=monthly.id,originalDate="2026-03-31",cancelled=true});
            work.EditEvent(monthly.id,"2026-02-28");work.editScope.value=2;work.SaveEvent();
            var edited=data.events.Find(e=>e.id==monthly.id);
            Check(edited.recurrence==RecurrenceKind.Monthly&&edited.count==5&&edited.dateKey=="2026-01-31"&&edited.zone=="UTC"&&edited.endTime=="05:00","exception-to-series edit restores cadence and preserves series metadata");
            Check(data.exceptions.Any(e=>e.id=="skip"&&e.cancelled)&&data.exceptions.Any(e=>e.id=="changed"),"entire-series edit preserves skipped and replacement exceptions");
        }
        finally {work.Hide();typeof(AppSession).GetProperty("Data").SetValue(session,previous);}
    }
    private static void RefinementChecks(AppSession session,CalendarController cal,ScheduleWorkbench work)
    {
        string key="2026-07-04";session.UpdateDayDetails(key,"Note only","Person");
        Check(session.CalendarOverrides[key].scheduledShift,"note-only save follows rotation");
        session.SetShift(key,1);session.UpdateDayDetails(key,"Updated note","Person");
        Check(!session.CalendarOverrides[key].scheduledShift&&session.CalendarOverrides[key].shiftType==1,"notes retain explicit override");
        session.RestoreScheduledShift(key);
        Check(session.CalendarOverrides[key].scheduledShift&&session.CalendarOverrides[key].note=="Updated note"&&session.CalendarOverrides[key].personName=="Person","restore keeps note/person");
        cal.currentMonth=new DateTime(2026,7,1);cal.Refresh();cal.SetEditing(false);
        string original=JsonUtility.ToJson(new ScheduleSave{overrides=session.CalendarOverrides.Values.ToList()});
        cal.BeginDaySelection(key);cal.EndDaySelection(key);Check(cal.SelectedCount==0,"normal mode ignores selection gestures");
        var cell=cal.GetComponentsInChildren<CalendarDayCell>(true).First(c=>c.DateKey==key);var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=new Vector2(100,100)};
        cell.OnPointerDown(pointer);cell.OnBeginDrag(pointer);pointer.position=new Vector2(150,150);cell.OnPointerUp(pointer);Check(cal.SelectedCount==0&&!cal.preview.gameObject.activeSelf,"normal drag never selects or opens details");
        cal.TapDay(key);Check(cal.preview.gameObject.activeSelf&&cal.SelectedCount==0,"normal note tap opens preview without selection");
        cal.TapDay(key);Check(!cal.preview.gameObject.activeSelf&&cal.GetComponentInChildren<DayDetailsPopup>(true).gameObject.activeSelf,"same day opens full details");
        cal.GetComponentInChildren<DayDetailsPopup>(true).Hide();
        cal.SetEditing(true);cal.BeginDaySelection("2026-06-30");cal.ExtendDaySelection("2026-07-03");cal.EndDaySelection("2026-07-03");
        Check(cal.SelectedCount==4,"drag range includes adjacent month");
        Check(cal.GetComponentsInChildren<CalendarDayCell>(true).Count(c=>c.IsSelected)==4,"edit selection has four visible highlights");
        cal.SetEditing(false);Check(cal.SelectedCount==0&&!cal.GetComponentsInChildren<CalendarDayCell>(true).Any(c=>c.IsSelected)&&JsonUtility.ToJson(new ScheduleSave{overrides=session.CalendarOverrides.Values.ToList()})==original,"Done clears selection without mutating data");
        var settings=Object.FindFirstObjectByType<ShiftSettingsController>(FindObjectsInactive.Include);settings.Refresh();
        var definition=session.CurrentGroup.shiftTypes[1];string before=JsonUtility.ToJson(definition);
        settings.editor.Show(settings,definition);settings.editor.nameInput.text="Uncommitted";
        Check(settings.editor.HasChanges&&JsonUtility.ToJson(definition)==before,"shift editor stages changes until Save");settings.editor.gameObject.SetActive(false);
        settings.editor.Show(settings,null);settings.editor.nameInput.text="Custom OFF";settings.editor.Save();
        var custom=session.CurrentGroup.shiftTypes.Last();Check(custom.id>=100&&custom.hours==0&&string.IsNullOrEmpty(custom.startTime),"shift editor saves intentional untimed custom shift");
        settings.editor.Show(settings,custom);settings.editor.startPicker.Set("5:30 PM");settings.editor.endPicker.Set("5:30 AM");settings.editor.Save();Check(custom.hours==12,"shift editor saves overnight hours");
        settings.editor.Show(settings,custom);settings.editor.ClearTimes();settings.editor.Save();Check(custom.hours==0&&string.IsNullOrEmpty(custom.startTime)&&string.IsNullOrEmpty(custom.endTime),"clear shift times preserves intentional untimed shifts on every platform");
        session.SetShift("2026-07-05",custom.id);Check(!session.CanDeleteShift(custom.id,out _),"custom shift referenced by date cannot be deleted");session.CalendarOverrides.Remove("2026-07-05");session.CurrentGroup.shiftTypes.Remove(custom);
        foreach(int id in new[]{0,3,4,5,6,7})session.CurrentGroup.shiftTypes.Add(new ShiftTypeDefinitionData{id=id,name=ShiftStyleUtility.GetName(id,null),colorHex=ShiftStyleUtility.GetColorHex(id,null)});
        string future=DateKeyUtility.ToDateKey(DateTime.Today.AddDays(1));
        var e=session.Data.events[0];e.dateKey=future;e.alarm=true;e.reminder=true;e.weekdays=new List<int>{(int)DateTime.Today.AddDays(1).DayOfWeek};
        session.Data.overrides=new List<DayOverrideData>(session.CalendarOverrides.Values);
        DeviceSnapshot(session.Data);var info=DayInformation.Resolve(session.Data,DateTime.Today,DateTime.Today.AddDays(14));
        Check(info[future].events.Count(o=>o.isEvent)==1&&info[future].alarms.Count(o=>o.isEvent)==2,"event counted once while alarm and reminder remain separate");
        session.Data.mutedEvents.Add(e.id);DeviceSnapshot(session.Data);info=DayInformation.Resolve(session.Data,DateTime.Today,DateTime.Today.AddDays(14));
        Check(info[future].events.Count(o=>o.isEvent)==1&&info[future].alarms.Count(o=>o.isEvent)==0,"paused/view-only event has no alarm indicator");session.Data.mutedEvents.Clear();DeviceSnapshot(session.Data);
        var replacement=JsonUtility.FromJson<EventSeries>(JsonUtility.ToJson(e));replacement.dateKey=DateKeyUtility.ToDateKey(DateTime.Today.AddDays(2));
        session.Data.exceptions.Add(new EventException{seriesId=e.id,originalDate=future,replacement=replacement});info=DayInformation.Resolve(session.Data,DateTime.Today,DateTime.Today.AddDays(14));
        Check(info[replacement.dateKey].events.Count(o=>o.isEvent)==1&&info[replacement.dateKey].alarms.Count(o=>o.isEvent)==2,"moved exception uses intended calendar date");session.Data.exceptions.Clear();
        var nav=Object.FindFirstObjectByType<AppNavigation>();typeof(AppNavigation).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(nav,null);
        nav.ShowLogin();nav.ShowCalendar();nav.ShowSettings();nav.ShowManageShifts();
        Check(nav.CurrentScreen.name=="Manage Shifts Screen","Manage Shifts opens from Settings");nav.Back();Check(nav.CurrentScreen.name=="Settings Screen","Manage Shifts Back returns to Settings");
        nav.ShowAccount();var account=nav.transform.Find("Account Popup").GetComponent<ModalPanel>();nav.Register(account);
        Check(account.gameObject.activeInHierarchy&&nav.CurrentScreen.name=="Settings Screen","account opens as modal over Settings");nav.Back();Check(!account.gameObject.activeSelf&&nav.CurrentScreen.name=="Settings Screen","account closes to Settings");
        work.ShowAgenda();Check(nav.CurrentScreen==work.agendaPanel,"agenda tracks opening Settings screen");nav.Back();Check(nav.CurrentScreen.name=="Settings Screen","Back returns from Events to Settings");nav.Back();Check(nav.CurrentScreen==cal.gameObject,"Back returns to Calendar");
        int history=nav.HistoryCount;nav.ShowCalendar();Check(nav.HistoryCount==history,"duplicate navigation suppressed");
        work.NewEvent(future);nav.Register(work.eventPanel.GetComponent<ModalPanel>());work.eventFields[0].text="Unsaved";nav.Back();
        Check(work.eventPanel.activeSelf&&nav.confirmation.gameObject.activeSelf,"Back confirms unsaved editor");nav.confirmation.Cancel();nav.Back();nav.confirmation.Accept();Check(!work.eventPanel.activeSelf,"confirm discard closes editor");
        work.ShowAgenda();work.Refresh();Check(work.agendaContent.GetComponentsInChildren<ScheduleListRow>().Length==session.Data.events.Count+session.Data.rules.Count,"one main card per series/rule");
        work.ToggleUpcoming();Check(work.upcomingContent.GetComponentsInChildren<ScheduleListRow>().Length<=4,"Upcoming starts with at most four occurrences");work.Hide();
        Check(!nav.GetComponentsInChildren<Transform>(true).Any(x=>x.name=="Bottom Navigation"),"no bottom navigation objects remain");
        Check(!nav.GetComponentsInChildren<Dropdown>(true).Any(x=>x.name=="Theme"),"redundant theme dropdown removed");
        Check(!nav.GetComponentsInChildren<Transform>(true).Any(x=>x.name=="Profile Screen"),"obsolete Profile screen removed");
        PickerChecks(session,cal,nav);
        bool had=PlayerPrefs.HasKey(ThemeManager.PreferenceKey);int old=PlayerPrefs.GetInt(ThemeManager.PreferenceKey);
        var flag=typeof(ThemeManager).GetField("initialized",BindingFlags.Static|BindingFlags.NonPublic);bool wasInitialized=(bool)flag.GetValue(null);
        try
        {
            PlayerPrefs.DeleteKey(ThemeManager.PreferenceKey);flag.SetValue(null,false);ThemeManager.Initialize(false);Check(ThemeManager.Current==ThemeManager.Theme.SoftDaylight,"existing light preference migrates");
            PlayerPrefs.DeleteKey(ThemeManager.PreferenceKey);flag.SetValue(null,false);ThemeManager.Initialize(true);Check(ThemeManager.Current==ThemeManager.Theme.MidnightGraphite,"dark/new user defaults to Graphite");
            foreach(var theme in new[]{ThemeManager.Theme.MidnightGraphite,ThemeManager.Theme.DeepTeal,ThemeManager.Theme.SoftDaylight}) {
                var sample=nav.GetComponentsInChildren<ThemeSample>(true).Single(x=>x.choice==theme);var button=sample.GetComponent<Button>();for(int i=0;i<button.onClick.GetPersistentEventCount();i++)button.onClick.SetPersistentListenerState(i,UnityEngine.Events.UnityEventCallState.EditorAndRuntime);button.onClick.Invoke();
                Check(ThemeManager.Current==theme&&sample.GetComponent<Image>().raycastTarget,"theme button invokes actual palette "+theme);
                foreach(var preview in nav.GetComponentsInChildren<ThemeSample>(true)) {preview.Refresh();Check(preview.selectedBorder.enabled==(preview.choice==theme),"selected theme marker "+theme+" / "+preview.choice);}
                Check(PlayerPrefs.GetInt(ThemeManager.PreferenceKey)==(int)theme,"theme persists "+theme);var row=Object.Instantiate(work.rowPrefab);Check(row.GetComponent<ThemeManager>().GetComponent<Image>().color==ThemeManager.Token(ThemeManager.Role.Surface),"dynamic row uses current theme "+theme);Object.DestroyImmediate(row.gameObject);}
            ThemeManager.Select(ThemeManager.Theme.DeepTeal);ThemeManager.Initialize(false);Check(ThemeManager.Current==ThemeManager.Theme.DeepTeal,"account migration never resets explicit appearance");flag.SetValue(null,false);ThemeManager.Initialize(false);Check(ThemeManager.Current==ThemeManager.Theme.DeepTeal,"explicit theme restores on next launch");
        }
        finally {if(had)PlayerPrefs.SetInt(ThemeManager.PreferenceKey,old);else PlayerPrefs.DeleteKey(ThemeManager.PreferenceKey);PlayerPrefs.Save();flag.SetValue(null,wasInitialized);ThemeManager.Apply(ThemeManager.Theme.MidnightGraphite);}

    }

    private static void PickerChecks(AppSession session,CalendarController cal,AppNavigation nav)
    {
        cal.gameObject.SetActive(true);cal.SetEditing(false);cal.currentMonth=new DateTime(2026,7,1);cal.Refresh();
        var popup=cal.GetComponentInChildren<DayDetailsPopup>(true);var day=CalendarGenerator.Generate(cal.currentMonth,session.CurrentGroup,session.CalendarOverrides).First(d=>d.dateKey=="2026-07-04");
        popup.Show(day);var note=(InputField)typeof(DayDetailsPopup).GetField("noteInput",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(popup);note.text="Uncommitted note";
        nav.Register(popup.GetComponent<ModalPanel>());popup.ChangeShift();var picker=cal.shiftPicker;nav.Register(picker.GetComponent<ModalPanel>());
        Check(picker.FocusedKey==day.dateKey&&!cal.IsEditing&&!picker.IsBulk&&popup.gameObject.activeSelf,"day Change Shift focuses original day without global edit mode");
        var originalEvents=JsonUtility.ToJson(new ScheduleSave{events=session.Data.events,rules=session.Data.rules,exceptions=session.Data.exceptions});
        string second="2026-07-05";int secondShift=ShiftPatternUtility.Resolve(session.CurrentGroup.pattern,session.CurrentGroup.startDateKey,DateKeyUtility.FromDateKey(second));
        picker.ApplyShift(2);
        Check(session.CalendarOverrides[day.dateKey].shiftType==2&&session.CalendarOverrides[day.dateKey].note=="Updated note"&&session.CalendarOverrides[day.dateKey].personName=="Person","focused shift preserves saved note/person");
        Check(!session.CalendarOverrides.ContainsKey(second)&&secondShift==ShiftPatternUtility.Resolve(session.CurrentGroup.pattern,session.CurrentGroup.startDateKey,DateKeyUtility.FromDateKey(second)),"focused shift never edits neighbor");
        Check(picker.gameObject.activeSelf&&picker.feedback.text.StartsWith("Applied"),"picker stays open with applied feedback");
        picker.Focus(new DateTime(2026,12,31));picker.dayButtons[11].onClick.Invoke();Check(picker.FocusedKey=="2027-01-01","day strip crosses year boundary with real dates");
        picker.ApplyShift(1);Check(session.CalendarOverrides["2027-01-01"].shiftType==1,"navigated shift applies only to focused new date");
        nav.Back();Check(popup.gameObject.activeSelf&&!picker.gameObject.activeSelf&&note.text=="Uncommitted note"&&popup.HasChanges,"closing picker returns to unsaved day details intact");nav.Back();Check(nav.confirmation.gameObject.activeSelf,"day close confirms unsaved notes");nav.confirmation.Accept();
        cal.SetEditing(true);cal.BeginDaySelection("2026-06-30");cal.EndDaySelection("2026-06-30");cal.BeginDaySelection("2026-07-02");cal.EndDaySelection("2026-07-02");
        var selected=cal.SelectedDates.OrderBy(k=>k).ToArray();cal.OpenShiftPickerForSelection();
        Check(picker.IsBulk&&picker.BulkCount==2&&picker.scopeLabel.text.StartsWith("Apply to 2"),"bulk picker declares explicit selected scope");
        picker.Focus(new DateTime(2027,1,3));picker.ApplyShift(1);
        Check(cal.SelectedDates.OrderBy(k=>k).SequenceEqual(selected)&&selected.All(k=>session.CalendarOverrides[k].shiftType==1)&&!session.CalendarOverrides.ContainsKey("2027-01-03"),"bulk navigation preserves set and never applies to preview date");
        Check(JsonUtility.ToJson(new ScheduleSave{events=session.Data.events,rules=session.Data.rules,exceptions=session.Data.exceptions})==originalEvents,"shift edits retain events and alarm rules");
        cal.SetEditing(false);
        var todayCell=cal.GetComponentsInChildren<CalendarDayCell>(true).First(c=>c.DateKey==day.dateKey);todayCell.UpdateToday(day.date);Check(todayCell.transform.Find("Today pill").GetComponent<Image>().enabled,"Today uses number pill");todayCell.UpdateToday(day.date.AddDays(1));Check(!todayCell.transform.Find("Today pill").GetComponent<Image>().enabled,"Today indicator changes when device date changes");
    }
    // These flushes emulate LateUpdate between forced layouts in batch-mode captures.
    private static void SettleLayout(CalendarController cal)
    {
        Canvas.ForceUpdateCanvases();cal.GetComponentInChildren<ResponsiveCalendarGrid>(true).Fit();Canvas.ForceUpdateCanvases();
        foreach(var cell in cal.GetComponentsInChildren<CalendarDayCell>(true))cell.FlushPresentation();
        if(cal.preview.gameObject.activeInHierarchy)cal.preview.Fit();
        if(cal.shiftPicker.gameObject.activeInHierarchy)cal.shiftPicker.FlushLayout();
        Canvas.ForceUpdateCanvases();
    }
    public static void CheckPickerReopen()
    {
        checks=0;EditorSceneManager.OpenScene("Assets/Calendar.unity");
        var session=Object.FindFirstObjectByType<AppSession>();AppSession.Instance=session;
        var data=new ScheduleSave{group=Group()};typeof(AppSession).GetProperty("Data").SetValue(session,data);session.CurrentGroup=data.group;
        var cal=Object.FindFirstObjectByType<CalendarController>(FindObjectsInactive.Include);cal.gameObject.SetActive(true);cal.SetEditing(false);
        var picker=cal.shiftPicker;picker.OpenSingle(new DateTime(2026,12,31));SettleLayout(cal);
        var day=(RectTransform)picker.dayButtons[10].transform;
        picker.navigator.content.anchoredPosition-=new Vector2(day.rect.width+8,0);picker.RequestSnap();picker.gameObject.SetActive(false);
        picker.OpenSingle(new DateTime(2027,1,10));SettleLayout(cal);
        Check(picker.FocusedKey=="2027-01-10","closing before pending swipe snap preserves reopened date");
        Check(!cal.IsEditing&&!picker.IsBulk,"reopened picker retains single-day scope");
        var focused=day.TransformPoint(day.rect.center);var center=picker.navigator.viewport.TransformPoint(picker.navigator.viewport.rect.center);
        Check(Mathf.Abs(focused.x-center.x)<1,"reopened picker centers exact requested date");
        Check(session.CalendarOverrides.Count==0,"cancelled swipe does not mutate dates");
        Debug.Log("ShiftCal picker reopen checks passed: "+checks);
    }
    private static void Capture(Vector2Int size,CalendarController cal,ScheduleWorkbench work)
    {
        var canvas=Object.FindFirstObjectByType<Canvas>();var camera=Object.FindFirstObjectByType<Camera>();
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.enabled=false;
        var rt=new RenderTexture(size.x,size.y,24);camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        canvas.scaleFactor=size.x/1080f;
        var root=canvas.transform.GetChild(0);root.GetComponent<SafeAreaFitter>().enabled=false;
        var safe=(RectTransform)root;safe.anchorMin=new Vector2(0,24f/size.y);safe.anchorMax=new Vector2(1,1-24f/size.y);safe.offsetMin=safe.offsetMax=Vector2.zero;
        Directory.CreateDirectory("Logs/UI");
        var popup=cal.GetComponentInChildren<DayDetailsPopup>(true);
        var settings=Object.FindFirstObjectByType<ShiftSettingsController>(FindObjectsInactive.Include);
        var date=DateKeyUtility.ToDateKey(DateTime.Today.AddDays(1));
        AppSession.Instance.UpdateDayDetails(date,"Bring the reports.\nMeet at the north entrance.\nConfirm handover before leaving.","Abram");
        foreach(var theme in new[]{ThemeManager.Theme.MidnightGraphite,ThemeManager.Theme.DeepTeal,ThemeManager.Theme.SoftDaylight})
        foreach(var screen in new[]{"Login Screen","Calendar Screen","Edit-Calendar","Settings Screen","Manage-Shifts","Account","Events","Upcoming","Event-Editor","Daily-Event-Editor","Weekly-Event-Editor","Monthly-Event-Editor","Shift-Fixed-Event-Editor","Shift-Relative-Event-Editor","Date-Picker","Time-Picker","Alarm-Preferences","Alarm-Editor","Shift-Editor","Day-Preview","Day-Editor","Repeat","Shift-Picker","Bulk-Picker","Confirmation"})
        {
            Object.FindFirstObjectByType<UnityPickerDialog>(FindObjectsInactive.Include)?.Cancel();work.Hide();settings.editor.gameObject.SetActive(false);popup.Hide();cal.preview.Hide();cal.SetEditing(false);
            var visible=screen=="Manage-Shifts"||screen=="Shift-Editor"?"Manage Shifts Screen":screen=="Settings Screen"||screen=="Account"||screen=="Alarm-Preferences"?"Settings Screen":screen=="Login Screen"?screen:"Calendar Screen";
            root.Find("Account Popup").gameObject.SetActive(false);
            foreach(Transform child in root)if(child.name.EndsWith("Screen"))child.gameObject.SetActive(child.name==visible);
            cal.currentMonth=new DateTime(DateTime.Today.Year,DateTime.Today.Month,1);cal.Refresh();
            if(visible=="Manage Shifts Screen")settings.Refresh();
            if(screen=="Account")root.Find("Account Popup").gameObject.SetActive(true);
            if(screen=="Events"||screen=="Upcoming") {work.ShowAgenda();if(screen=="Upcoming"&&!work.upcomingSection.activeSelf)work.ToggleUpcoming();if(screen=="Events"&&work.upcomingSection.activeSelf)work.ToggleUpcoming();}
            if(screen.EndsWith("Event-Editor")||screen=="Date-Picker"||screen=="Time-Picker")
            {
                work.NewEvent(date);work.eventFields[0].text="Wake Up";work.eventTime.Set("04:00");
                if(screen.StartsWith("Daily"))work.recurrence.value=1;
                if(screen.StartsWith("Weekly"))work.recurrence.value=2;
                if(screen.StartsWith("Monthly")){work.recurrence.value=3;work.repeatEnding.value=1;}
                if(screen.StartsWith("Shift")){work.NewRule();if(screen.StartsWith("Shift-Fixed"))work.timingMode.value=1;}
                if(screen=="Date-Picker")work.eventDate.Open();if(screen=="Time-Picker")work.eventTime.Open();
            }
            if(screen=="Alarm-Preferences")
            {
                var scroll=root.Find("Settings Screen").GetComponentInChildren<ScrollRect>();Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=.7f;
            }
            if(screen=="Alarm-Editor")work.NewRule();
            if(screen=="Shift-Editor")settings.editor.Show(settings,AppSession.Instance.CurrentGroup.shiftTypes[1]);
            if(screen=="Day-Preview"||screen=="Day-Editor") {cal.TapDay(date);if(screen=="Day-Editor")cal.preview.OpenDetails();}
            if(screen=="Edit-Calendar"||screen=="Repeat"||screen=="Bulk-Picker") {cal.SetEditing(true);cal.BeginDaySelection(date);cal.ExtendDaySelection(DateKeyUtility.ToDateKey(DateTime.Today.AddDays(4)));cal.EndDaySelection(date);if(screen=="Repeat")cal.ShowRepeatPanel();if(screen=="Bulk-Picker")cal.OpenShiftPickerForSelection();}
            if(screen=="Shift-Picker")cal.shiftPicker.OpenSingle(DateTime.Today.AddDays(6));
            var confirmation=Object.FindFirstObjectByType<ConfirmationDialog>(FindObjectsInactive.Include);if(screen=="Confirmation")confirmation.Show("Discard unsaved changes?",()=>{});
            ThemeManager.Apply(theme);camera.backgroundColor=ThemeManager.Token(ThemeManager.Role.Background);SettleLayout(cal);
            foreach(var toggle in Object.FindObjectsByType<Toggle>(FindObjectsSortMode.None))
                if(toggle.gameObject.activeInHierarchy)Check(((RectTransform)toggle.transform).rect.width<=(toggle.name.StartsWith("Weekday ")?150:100),"compact toggle fits "+screen+" / "+toggle.name);
            if(screen.EndsWith("Editor"))
            {
                var panel=screen=="Event-Editor"?work.eventPanel:screen=="Alarm-Editor"?work.eventPanel:screen=="Shift-Editor"?settings.editor.gameObject:popup.gameObject;
                var footer=panel.GetComponentsInChildren<RectTransform>().First(x=>x.name=="Editor actions");var corners=new Vector3[4];footer.GetWorldCorners(corners);
                Check(corners.All(p=>{var point=RectTransformUtility.WorldToScreenPoint(camera,p);return point.x>=-1&&point.x<=size.x+1&&point.y>=-1&&point.y<=size.y+1;}),"editor footer fits "+screen+" / "+size+" / "+theme);
                var scroll=panel.GetComponentInChildren<ScrollRect>();scroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();
                Check(scroll.content.rect.height>=0&&scroll.viewport.rect.height>0,"editor scroll accessible "+screen+" / "+size);
                scroll.verticalNormalizedPosition=1;
            }
            if(screen=="Weekly-Event-Editor")
            {
                var chipRects=work.weekdays.Select(w=>(RectTransform)w.transform).ToArray();
                Check(chipRects.All(r=>r.rect.width>=90&&r.rect.height>=100),"seven selectable weekday chips have full touch targets "+size);
            }
            if(screen=="Shift-Picker"||screen=="Bulk-Picker")
            {
                var picker=cal.shiftPicker;var rect=(RectTransform)picker.dayButtons[10].transform;
                var focused=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
                var center=RectTransformUtility.WorldToScreenPoint(camera,picker.navigator.viewport.TransformPoint(picker.navigator.viewport.rect.center));
                Check(Mathf.Abs(focused.x-center.x)<1,"focused date centered "+size+" / "+theme);
                picker.navigator.content.anchoredPosition-=new Vector2(rect.rect.width+8,0);picker.RequestSnap();picker.FlushLayout();SettleLayout(cal);
                Check(picker.dayButtons[10].GetComponentsInChildren<Text>()[0].text==picker.FocusedDate.Day.ToString(),"swipe snaps and recenters real date "+size+" / "+theme);
                var choiceScroll=picker.choices.GetComponentInParent<ScrollRect>();
                var firstChoice=(RectTransform)picker.choices.GetChild(0);var corners2=new Vector3[4];firstChoice.GetWorldCorners(corners2);
                var top=RectTransformUtility.WorldToScreenPoint(camera,corners2[1]);var viewTop=RectTransformUtility.WorldToScreenPoint(camera,choiceScroll.viewport.TransformPoint(new Vector3(choiceScroll.viewport.rect.xMin,choiceScroll.viewport.rect.yMax)));
                Check(top.y<=viewTop.y+1&&top.y>=viewTop.y-20,"shift list opens at top "+screen+" / "+size+" / "+theme);
                var bounds2=picker.navigator.viewport.rect;Check(rect.rect.width*7+8*6<=bounds2.width+1,"seven day navigator fits "+size);
            }
            if(screen=="Day-Preview") {var corners=new Vector3[4];cal.preview.card.GetWorldCorners(corners);Check(corners.All(p=>{var point=RectTransformUtility.WorldToScreenPoint(camera,p);return point.y>=24&&point.y<=size.y-24;}),"preview stays in safe area "+size+" / "+theme);}
            var grid=cal.GetComponentsInChildren<GridLayoutGroup>(true).First(x=>x.constraintCount==7);var bounds=((RectTransform)grid.transform).rect;
            Check(grid.cellSize.x*7+grid.spacing.x*6<=bounds.width+1&&grid.cellSize.y*6+grid.spacing.y*5<=bounds.height+1,"42 cells fit "+size+" / "+screen+" / "+theme);
            if(screen=="Calendar Screen") { var cell=cal.GetComponentsInChildren<CalendarDayCell>(true).First(c=>c.DateKey==date); var image=cell.GetComponent<Image>(); Check(image.color==ThemeManager.ShiftColor(CalendarGenerator.Generate(cal.currentMonth,AppSession.Instance.CurrentGroup,AppSession.Instance.CalendarOverrides).First(x=>x.dateKey==date).shiftColorHex)&&cell.GetComponentsInChildren<ThemeManager>(true).Length==0,"calendar retains custom shift color "+theme); }
            camera.Render();RenderTexture.active=rt;var tex=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,size.x,size.y),0,0);tex.Apply();
            File.WriteAllBytes("Logs/UI/"+theme+"-"+screen.Replace(" ","-")+"-"+size.x+"x"+size.y+".png",tex.EncodeToPNG());Object.DestroyImmediate(tex);confirmation.Cancel();
        }
        RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(rt);
    }
}
