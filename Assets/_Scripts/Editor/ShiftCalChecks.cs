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
    public static void BuildAndroid()
    {
        Directory.CreateDirectory("Logs/Android");
        string storePassword=PlayerSettings.Android.keystorePass,aliasPassword=PlayerSettings.Android.keyaliasPass;
        try
        {
            // The configured standard Android debug key has public, standard passwords.
            // Never substitute a key or change the certificate of the project.
            if(PlayerSettings.Android.keyaliasName=="androiddebugkey"&&PlayerSettings.Android.keystoreName.EndsWith("debug.keystore"))
            { PlayerSettings.Android.keystorePass="android";PlayerSettings.Android.keyaliasPass="android"; }
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Calendar.unity"},locationPathName="Logs/Android/ShiftCal-ui.apk",target=BuildTarget.Android,options=BuildOptions.Development});
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
        typeof(AppSession).GetProperty("Data").SetValue(session,new ScheduleSave{group=Group()});session.CurrentGroup=Group();
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
        Check(work!=null&&work.rowPrefab!=null&&work.eventFields.Count==12&&work.ruleFields.Count==4&&work.weekdays.Length==7,"event/alarm editor fields and prefab wired");
        work.NewEvent("2026-07-02");work.eventFields.Find(x=>x.name=="title").text="Shutdown meeting";work.recurrence.value=2;work.eventAlarm.isOn=true;work.SaveEvent();
        Check(session.Data.events.Count==1&&session.Data.events[0].weekdays.SequenceEqual(new[]{4}),"scene event editor saves Thursday meeting");
        work.NewRule();work.SaveRule();
        Check(session.Data.rules.Count==1&&session.Data.rules[0].beforeMinutes==90,"scene shift alarm editor saves 90-minute rule");
        Check(RecurrenceEngine.Resolve(session.Data,new DateTime(2026,7,2),new DateTime(2026,7,2)).Any(x=>x.isEvent),"saved scene meeting resolves independently");
        foreach(var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None))for(int i=0;i<b.onClick.GetPersistentEventCount();i++)Check(b.onClick.GetPersistentTarget(i)!=null&&!string.IsNullOrEmpty(b.onClick.GetPersistentMethodName(i)),"button wiring: "+b.name);
        Check(!Object.FindObjectsByType<Text>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(t=>t.text.Contains("11:29")),"fake system status removed");
        RefinementChecks(session,cal,work);
        foreach(var size in new[]{new Vector2Int(360,640),new Vector2Int(393,851),new Vector2Int(412,915)})Capture(size,cal,work);
        Debug.Log("ShiftCal focused checks passed: "+checks);
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
        settings.editor.Show(settings,custom);settings.editor.startInput.text="5:30 PM";settings.editor.endInput.text="5:30 AM";settings.editor.Save();Check(custom.hours==12,"shift editor saves overnight hours");
        session.SetShift("2026-07-05",custom.id);Check(!session.CanDeleteShift(custom.id,out _),"custom shift referenced by date cannot be deleted");session.CalendarOverrides.Remove("2026-07-05");session.CurrentGroup.shiftTypes.Remove(custom);
        foreach(int id in new[]{0,3,4,5,6,7})session.CurrentGroup.shiftTypes.Add(new ShiftTypeDefinitionData{id=id,name=ShiftStyleUtility.GetName(id,null),colorHex=ShiftStyleUtility.GetColorHex(id,null)});
        string future=DateKeyUtility.ToDateKey(DateTime.Today.AddDays(1));
        var e=session.Data.events[0];e.dateKey=future;e.alarm=true;e.reminder=true;e.weekdays=new List<int>{(int)DateTime.Today.AddDays(1).DayOfWeek};
        session.Data.overrides=new List<DayOverrideData>(session.CalendarOverrides.Values);
        var info=DayInformation.Resolve(session.Data,DateTime.Today,DateTime.Today.AddDays(14));
        Check(info[future].events.Count==1&&info[future].alarms.Count(o=>o.isEvent)==2,"event counted once while alarm and reminder remain separate");
        session.Data.mutedEvents.Add(e.id);info=DayInformation.Resolve(session.Data,DateTime.Today,DateTime.Today.AddDays(14));
        Check(info[future].events.Count==1&&info[future].alarms.Count(o=>o.isEvent)==0,"paused/view-only event has no alarm indicator");session.Data.mutedEvents.Clear();
        var replacement=JsonUtility.FromJson<EventSeries>(JsonUtility.ToJson(e));replacement.dateKey=DateKeyUtility.ToDateKey(DateTime.Today.AddDays(2));
        session.Data.exceptions.Add(new EventException{seriesId=e.id,originalDate=future,replacement=replacement});info=DayInformation.Resolve(session.Data,DateTime.Today,DateTime.Today.AddDays(14));
        Check(info[replacement.dateKey].events.Count==1&&info[replacement.dateKey].alarms.Count(o=>o.isEvent)==2,"moved exception uses intended calendar date");session.Data.exceptions.Clear();
        var nav=Object.FindFirstObjectByType<AppNavigation>();typeof(AppNavigation).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(nav,null);
        nav.ShowLogin();nav.ShowCalendar();nav.ShowSettings();nav.ShowProfile();work.ShowAgenda();
        Check(nav.CurrentScreen==work.agendaPanel,"agenda tracks opening screen");nav.Back();Check(nav.CurrentScreen.name=="Profile Screen","Back returns from Events to Profile");nav.Back();Check(nav.CurrentScreen.name=="Settings Screen","Back returns to actual previous Settings screen");nav.Back();Check(nav.CurrentScreen==cal.gameObject,"Back returns to Calendar");
        int history=nav.HistoryCount;nav.ShowCalendar();Check(nav.HistoryCount==history,"duplicate navigation suppressed");
        work.NewEvent(future);nav.Register(work.eventPanel.GetComponent<ModalPanel>());work.eventFields[0].text="Unsaved";nav.Back();
        Check(work.eventPanel.activeSelf&&nav.confirmation.gameObject.activeSelf,"Back confirms unsaved editor");nav.confirmation.Cancel();nav.Back();nav.confirmation.Accept();Check(!work.eventPanel.activeSelf,"confirm discard closes editor");
        work.ShowAgenda();work.Refresh();Check(work.agendaContent.GetComponentsInChildren<ScheduleListRow>().Length==session.Data.events.Count+session.Data.rules.Count,"one main card per series/rule");
        work.ToggleUpcoming();Check(work.upcomingContent.GetComponentsInChildren<ScheduleListRow>().Length<=4,"Upcoming starts with at most four occurrences");work.Hide();
        foreach(string screenName in new[]{"Calendar Screen","Settings Screen","Profile Screen","Events and Alarms"})
        {
            var screen=nav.GetComponentsInChildren<Transform>(true).First(t=>t.name==screenName);var bar=screen.Find("Bottom Navigation");
            Check(bar.childCount==4&&bar.GetChild(0).GetComponentInChildren<Text>().text=="Calendar"&&bar.GetChild(1).GetComponentInChildren<Text>().text=="Events"&&bar.GetChild(2).GetComponentInChildren<Text>().text=="Settings"&&bar.GetChild(3).GetComponentInChildren<Text>().text=="Profile","consistent navigation "+screenName);
        }
        bool had=PlayerPrefs.HasKey(ThemeManager.PreferenceKey);int old=PlayerPrefs.GetInt(ThemeManager.PreferenceKey);
        var flag=typeof(ThemeManager).GetField("initialized",BindingFlags.Static|BindingFlags.NonPublic);bool wasInitialized=(bool)flag.GetValue(null);
        try
        {
            PlayerPrefs.DeleteKey(ThemeManager.PreferenceKey);flag.SetValue(null,false);ThemeManager.Initialize(false);Check(ThemeManager.Current==ThemeManager.Theme.SoftDaylight,"existing light preference migrates");
            PlayerPrefs.DeleteKey(ThemeManager.PreferenceKey);flag.SetValue(null,false);ThemeManager.Initialize(true);Check(ThemeManager.Current==ThemeManager.Theme.MidnightGraphite,"dark/new user defaults to Graphite");
            foreach(var theme in new[]{ThemeManager.Theme.MidnightGraphite,ThemeManager.Theme.DeepTeal,ThemeManager.Theme.SoftDaylight}) {ThemeManager.Select(theme);Check(PlayerPrefs.GetInt(ThemeManager.PreferenceKey)==(int)theme,"theme persists "+theme);var row=Object.Instantiate(work.rowPrefab);Check(row.GetComponent<ThemeManager>().GetComponent<Image>().color==ThemeManager.Token(ThemeManager.Role.Surface),"dynamic row uses current theme "+theme);Object.DestroyImmediate(row.gameObject);}
            ThemeManager.Select(ThemeManager.Theme.DeepTeal);ThemeManager.Initialize(false);Check(ThemeManager.Current==ThemeManager.Theme.DeepTeal,"account migration never resets explicit appearance");flag.SetValue(null,false);ThemeManager.Initialize(false);Check(ThemeManager.Current==ThemeManager.Theme.DeepTeal,"explicit theme restores on next launch");
        }
        finally {if(had)PlayerPrefs.SetInt(ThemeManager.PreferenceKey,old);else PlayerPrefs.DeleteKey(ThemeManager.PreferenceKey);PlayerPrefs.Save();flag.SetValue(null,wasInitialized);ThemeManager.Apply(ThemeManager.Theme.MidnightGraphite);}

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
        foreach(var screen in new[]{"Login Screen","Calendar Screen","Edit-Calendar","Settings Screen","Profile Screen","Events","Upcoming","Event-Editor","Alarm-Editor","Shift-Editor","Day-Preview","Day-Editor","Repeat","Shift-Picker","Confirmation"})
        {
            work.Hide();settings.editor.gameObject.SetActive(false);popup.Hide();cal.preview.Hide();cal.SetEditing(false);
            var visible=screen=="Settings Screen"||screen=="Shift-Editor"?"Settings Screen":screen=="Login Screen"||screen=="Profile Screen"?screen:"Calendar Screen";
            foreach(Transform child in root)if(child.name.EndsWith("Screen"))child.gameObject.SetActive(child.name==visible);
            cal.currentMonth=new DateTime(DateTime.Today.Year,DateTime.Today.Month,1);cal.Refresh();
            if(visible=="Settings Screen")settings.Refresh();
            if(screen=="Events"||screen=="Upcoming") {work.ShowAgenda();if(screen=="Upcoming"&&!work.upcomingSection.activeSelf)work.ToggleUpcoming();if(screen=="Events"&&work.upcomingSection.activeSelf)work.ToggleUpcoming();}
            if(screen=="Event-Editor")work.NewEvent(date);
            if(screen=="Alarm-Editor")work.NewRule();
            if(screen=="Shift-Editor")settings.editor.Show(settings,AppSession.Instance.CurrentGroup.shiftTypes[1]);
            if(screen=="Day-Preview"||screen=="Day-Editor") {cal.TapDay(date);if(screen=="Day-Editor")cal.preview.OpenDetails();}
            if(screen=="Edit-Calendar"||screen=="Repeat"||screen=="Shift-Picker") {cal.SetEditing(true);cal.BeginDaySelection(date);cal.ExtendDaySelection(DateKeyUtility.ToDateKey(DateTime.Today.AddDays(4)));cal.EndDaySelection(date);if(screen=="Repeat")cal.ShowRepeatPanel();if(screen=="Shift-Picker")cal.OpenShiftPickerForSelection();}
            var confirmation=Object.FindFirstObjectByType<ConfirmationDialog>(FindObjectsInactive.Include);if(screen=="Confirmation")confirmation.Show("Discard unsaved changes?",()=>{});
            ThemeManager.Apply(theme);camera.backgroundColor=ThemeManager.Token(ThemeManager.Role.Background);Canvas.ForceUpdateCanvases();cal.GetComponentInChildren<ResponsiveCalendarGrid>(true).Fit();Canvas.ForceUpdateCanvases();
            foreach(var toggle in Object.FindObjectsByType<Toggle>(FindObjectsSortMode.None))
                if(toggle.gameObject.activeInHierarchy)Check(((RectTransform)toggle.transform).rect.width<=100,"compact toggle fits "+screen+" / "+toggle.name);
            if(screen.EndsWith("Editor"))
            {
                var panel=screen=="Event-Editor"?work.eventPanel:screen=="Alarm-Editor"?work.rulePanel:screen=="Shift-Editor"?settings.editor.gameObject:popup.gameObject;
                var footer=panel.GetComponentsInChildren<RectTransform>().First(x=>x.name=="Editor actions");var corners=new Vector3[4];footer.GetWorldCorners(corners);
                Check(corners.All(p=>{var point=RectTransformUtility.WorldToScreenPoint(camera,p);return point.x>=-1&&point.x<=size.x+1&&point.y>=-1&&point.y<=size.y+1;}),"editor footer fits "+screen+" / "+size+" / "+theme);
                var scroll=panel.GetComponentInChildren<ScrollRect>();scroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();
                Check(scroll.content.rect.height>=0&&scroll.viewport.rect.height>0,"editor scroll accessible "+screen+" / "+size);
                scroll.verticalNormalizedPosition=1;
            }
            if(screen=="Day-Preview") {var corners=new Vector3[4];cal.preview.card.GetWorldCorners(corners);Check(corners.All(p=>{var point=RectTransformUtility.WorldToScreenPoint(camera,p);return point.y>=24&&point.y<=size.y-24;}),"preview stays in safe area "+size+" / "+theme);}
            var grid=cal.GetComponentsInChildren<GridLayoutGroup>(true).First(x=>x.constraintCount==7);var bounds=((RectTransform)grid.transform).rect;
            Check(grid.cellSize.x*7+grid.spacing.x*6<=bounds.width+1&&grid.cellSize.y*6+grid.spacing.y*5<=bounds.height+1,"42 cells fit "+size+" / "+screen+" / "+theme);
            if(screen=="Calendar Screen") { var cell=cal.GetComponentsInChildren<CalendarDayCell>(true).First(c=>c.DateKey==date); var image=cell.GetComponent<Image>(); Check(image.color==ThemeManager.ShiftColor(AppSession.Instance.CurrentGroup.shiftTypes[1].colorHex)&&cell.GetComponentsInChildren<ThemeManager>(true).Length==0,"calendar retains custom shift color "+theme); }
            camera.Render();RenderTexture.active=rt;var tex=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,size.x,size.y),0,0);tex.Apply();
            File.WriteAllBytes("Logs/UI/"+theme+"-"+screen.Replace(" ","-")+"-"+size.x+"x"+size.y+".png",tex.EncodeToPNG());Object.DestroyImmediate(tex);confirmation.Cancel();
        }
        RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(rt);
    }
}
