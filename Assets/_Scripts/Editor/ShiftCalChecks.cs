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
        var work=Object.FindFirstObjectByType<ScheduleWorkbench>(FindObjectsInactive.Include);
        Check(work!=null&&work.rowPrefab!=null&&work.eventFields.Count==12&&work.ruleFields.Count==4&&work.weekdays.Length==7,"event/alarm editor fields and prefab wired");
        work.NewEvent("2026-07-02");work.eventFields.Find(x=>x.name=="title").text="Shutdown meeting";work.recurrence.value=2;work.eventAlarm.isOn=true;work.SaveEvent();
        Check(session.Data.events.Count==1&&session.Data.events[0].weekdays.SequenceEqual(new[]{4}),"scene event editor saves Thursday meeting");
        work.NewRule();work.SaveRule();
        Check(session.Data.rules.Count==1&&session.Data.rules[0].beforeMinutes==90,"scene shift alarm editor saves 90-minute rule");
        Check(RecurrenceEngine.Resolve(session.Data,new DateTime(2026,7,2),new DateTime(2026,7,2)).Any(x=>x.isEvent),"saved scene meeting resolves independently");
        foreach(var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None))for(int i=0;i<b.onClick.GetPersistentEventCount();i++)Check(b.onClick.GetPersistentTarget(i)!=null&&!string.IsNullOrEmpty(b.onClick.GetPersistentMethodName(i)),"button wiring: "+b.name);
        Check(!Object.FindObjectsByType<Text>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(t=>t.text.Contains("11:29")),"fake system status removed");
        foreach(var size in new[]{new Vector2Int(360,640),new Vector2Int(393,851),new Vector2Int(412,915)})Capture(size,cal,work);
        Debug.Log("ShiftCal focused checks passed: "+checks);
    }
    private static void Capture(Vector2Int size,CalendarController cal,ScheduleWorkbench work)
    {
        var canvas=Object.FindFirstObjectByType<Canvas>();var camera=Object.FindFirstObjectByType<Camera>();
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.enabled=false;
        var rt=new RenderTexture(size.x,size.y,24);camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        canvas.scaleFactor=size.x/1080f;
        var root=canvas.transform.GetChild(0);root.GetComponent<SafeAreaFitter>().enabled=false;
        Directory.CreateDirectory("Logs/UI");
        foreach(var screen in new[]{"Login Screen","Calendar Screen","Settings Screen","Profile Screen","Events","Event-Editor","Alarm-Editor","Light-Settings","Light-Calendar"})
        {
            var visible=screen=="Light-Settings"?"Settings Screen":screen=="Light-Calendar"?"Calendar Screen":screen;
            foreach(Transform child in root)if(child.name.EndsWith("Screen"))child.gameObject.SetActive(child.name==visible);
            work.Hide();if(visible=="Calendar Screen"){cal.currentMonth=new DateTime(2026,7,1);cal.Refresh();}
            if(visible=="Settings Screen")Object.FindFirstObjectByType<ShiftSettingsController>(FindObjectsInactive.Include).Refresh();
            if(screen=="Events")work.ShowAgenda();
            if(screen=="Event-Editor")work.NewEvent("2026-07-02");
            if(screen=="Alarm-Editor")work.NewRule();
            Canvas.ForceUpdateCanvases();
            ThemeManager.Apply(!screen.StartsWith("Light-"));cal.GetComponentInChildren<ResponsiveCalendarGrid>(true).Fit();Canvas.ForceUpdateCanvases();
            foreach(var toggle in Object.FindObjectsByType<Toggle>(FindObjectsSortMode.None))
                if(toggle.gameObject.activeInHierarchy)Check(((RectTransform)toggle.transform).rect.width<=100,"compact toggle fits "+screen+" / "+toggle.name);
            if(screen=="Event-Editor"||screen=="Alarm-Editor")
            {
                var panel=screen=="Event-Editor"?work.eventPanel:work.rulePanel;
                var footer=panel.transform.Find("Editor actions").GetComponent<RectTransform>();
                var corners=new Vector3[4];footer.GetWorldCorners(corners);
                Check(corners.All(p=>{var point=RectTransformUtility.WorldToScreenPoint(camera,p);return point.x>=0&&point.x<=size.x&&point.y>=0&&point.y<=size.y;}),"editor actions remain visible "+screen+" / "+size);
            }
            var grid=cal.GetComponentsInChildren<GridLayoutGroup>(true).First(x=>x.constraintCount==7);
            var bounds=((RectTransform)grid.transform).rect;
            Check(grid.cellSize.x*7+grid.spacing.x*6<=bounds.width+1,"grid width fits "+size);
            Check(grid.cellSize.y*6+grid.spacing.y*5<=bounds.height+1,"grid height fits "+size);
            camera.Render();RenderTexture.active=rt;var tex=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,size.x,size.y),0,0);tex.Apply();
            File.WriteAllBytes("Logs/UI/"+screen.Replace(" ","-")+"-"+size.x+"x"+size.y+".png",tex.EncodeToPNG());Object.DestroyImmediate(tex);
        }
        RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(rt);
    }
}
