using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using ShiftCal.App;
using ShiftCal.Core;
using ShiftCal.Data;
using ShiftCal.Firebase;
using ShiftCal.UI;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
public static class FamilyCalendarChecks
{
    static int checks;
    static void Check(bool ok,string text){if(!ok)throw new Exception("Family check failed: "+text);checks++;Debug.Log("FAMILY PASS: "+text);}
    public static void Run(AppSession session,CalendarController cal,ScheduleWorkbench oldWorkbench)
    {
        var previous=session.Data;var device=DeviceCalendarStore.State;var group=session.CurrentGroup;
        var data=new ScheduleSave{account="family-check-"+Guid.NewGuid().ToString("N"),group=ActivityResolver.Clone(group)};data.group.groupId="local-family-check";
        var local=new DeviceCalendarState{account=data.account,installationId="device-a"};
        try
        {
            checks=0;typeof(AppSession).GetProperty("Data").SetValue(session,data);session.CurrentGroup=data.group;SetDevice(local);
            var me=new FamilyProfile{id="me",name="Parent",linkedUid="uid-a",color="#60A5FA"};var child=new FamilyProfile{id="child",name="Child",color="#F59AC8"};data.profiles.AddRange(new[]{me,child});data.group.shiftOwnerProfileId=me.id;
            var karate=new CalendarActivity{id="karate",title="Karate",icon="karate",dateKey="2026-10-05",recurrence=RecurrenceKind.Weekly,weekdays=new List<int>{1},assigneeId=me.id,participants=new List<string>{child.id}};data.activities.Add(karate);
            var resolved=ActivityResolver.Resolve(data,new DateTime(2026,10,1),new DateTime(2026,10,31));Check(resolved.Count==4&&resolved.All(x=>x.dateKey==x.originalDate),"weekly generated dates and stable ordering");
            Check(ActivityResolver.Resolve(data,new DateTime(2026,10,1),new DateTime(2026,10,31),"mine","uid-a").Count==4,"Mine uses authenticated UID");me.name="Renamed parent";Check(karate.assigneeId=="me"&&ActivityResolver.People(data,karate).Contains(me.name),"rename preserves assignments");
            Check(ActivityResolver.Resolve(data,new DateTime(2026,10,1),new DateTime(2026,10,31),"child").Count==4,"participant person filter");Check(ActivityResolver.Resolve(data,new DateTime(2026,10,1),new DateTime(2026,10,31),"mine","uid-other").Count==0,"name alone never claims Mine");
            var reassigned=ActivityResolver.Clone(karate);reassigned.dateKey="2026-10-12";reassigned.assigneeId=child.id;ActivityResolver.SaveEdit(data,reassigned,karate.id,"2026-10-12",0);ActivityResolver.Delete(data,karate.id,"2026-10-19",0);
            resolved=ActivityResolver.Resolve(data,new DateTime(2026,10,1),new DateTime(2026,10,31));Check(resolved.Count==3&&resolved.Single(x=>x.dateKey=="2026-10-12").activity.assigneeId==child.id&&karate.assigneeId==me.id,"single reassignment and cancellation preserve series");
            var monthly=new CalendarActivity{id="monthly",title="Month end",dateKey="2026-01-31",recurrence=RecurrenceKind.Monthly,count=5};data.activities.Add(monthly);Check(ActivityResolver.Resolve(data,new DateTime(2026,1,1),new DateTime(2026,6,1)).Where(x=>x.seriesId==monthly.id).Select(x=>x.dateKey).SequenceEqual(new[]{"2026-01-31","2026-02-28","2026-03-31","2026-04-30","2026-05-31"}),"monthly clamp and count");
            var future=ActivityResolver.Clone(monthly);future.dateKey="2026-03-31";future.assigneeId=child.id;ActivityResolver.SaveEdit(data,future,monthly.id,"2026-03-31",1);Check(future.id!=monthly.id&&future.count==3&&monthly.until=="2026-03-30","future edit consumes count and splits stable identity");
            var appointment=new CalendarActivity{id="appointment",title="Doctor",icon="medical",dateKey="2026-10-10",startTime="10:00 AM",durationMinutes=60,assigneeId=me.id};data.activities.Add(appointment);
            local.subscriptions.Add(new LocalAlarmSubscription{sourceId="activity-appointment",alarm=true,reminder=true,reminderMinutes=15});local.rules.Add(new ShiftAlarmRule{id="my-wake",groupId=data.group.groupId,shiftType=2,beforeMinutes=90});
            var deviceB=new DeviceCalendarState{account=data.account,installationId="device-b"};
            var a=RecurrenceEngine.Resolve(DeviceCalendarStore.Effective(data,local),new DateTime(2026,10,1),new DateTime(2026,10,31));var b=RecurrenceEngine.Resolve(DeviceCalendarStore.Effective(data,deviceB),new DateTime(2026,10,1),new DateTime(2026,10,31));
            Check(a.Any(x=>x.audible)&&!b.Any(x=>x.audible||!x.id.EndsWith(":view")),"same shared calendar on two devices rings only for device A");Check(a.Any(x=>x.id=="event:activity-appointment:2026-10-10:alarm"),"native v3 activity identity stable");
            var legacy=new EventSeries{id="legacy",title="Old shared alarm",dateKey="2026-10-10",startTime="11:00 AM",alarm=true,reminder=true};data.events.Add(legacy);Check(!DeviceCalendarStore.Effective(data,deviceB).events.Find(x=>x.id==legacy.id).alarm,"legacy remote alarm flags cannot opt in a second device");
            var sync=FirestoreService.Instance;sync.TrackChanges(data);Check(data.activities.All(x=>x.groupId==data.group.groupId),"shared activities follow current calendar group identity");Check(!data.records.Any(x=>x.key.StartsWith("rule/")),"private rules never become SyncRecords");Check(data.records.Where(x=>x.key.StartsWith("event/")||x.key.StartsWith("activity/")).All(x=>!x.json.Contains("\"sound\"")&&!x.json.Contains("\"alarm\"")&&!x.json.Contains("snooze")),"shared event and activity JSON excludes delivery preferences");
            FirestoreService.Apply(data,"rule/remote",JsonUtility.ToJson(new ShiftAlarmRule{id="remote"}),false);Check(!data.rules.Any(x=>x.id=="remote"),"obsolete remote rule ignored");
            var shared=DeviceCalendarStore.SharedCopy(data);Check(shared.rules.Count==0&&!shared.events.Any(x=>x.alarm||x.reminder),"saved shared copy has no alarm definitions");
            var unassigned=new CalendarActivity{title="Note",icon="note",dateKey="2026-10-11"};data.activities.Add(unassigned);Check(ActivityResolver.Resolve(data,new DateTime(2026,10,11),new DateTime(2026,10,11),"unassigned").Any(x=>x.seriesId==unassigned.id),"unassigned filter includes untimed notes");
            Check(FamilyAvailability.Check(data,new[]{me.id},new DateTime(2026,10,10),"10:30 AM",30).StartsWith("Busy"),"recorded activity overlap");Check(FamilyAvailability.Check(data,new[]{child.id},new DateTime(2026,10,10),"10:30 AM",30).StartsWith("No known conflicts"),"other people never inherit shift owner schedule");
            var night=data.group.shiftTypes.Find(x=>x.id==2);night.startTime="5:30 PM";night.endTime="5:30 AM";data.overrides.Add(new DayOverrideData{dateKey="2026-10-09",shiftType=2});data.overrides.Add(new DayOverrideData{dateKey="2026-10-10",shiftType=1});Check(FamilyAvailability.Check(data,new[]{me.id},new DateTime(2026,10,10),"4:00 AM",60).StartsWith("Busy"),"previous overnight shift overlaps next morning");
            Check(FamilyAvailability.Check(data,new[]{me.id},new DateTime(2026,10,10),"7:00 AM",30).StartsWith("Known free"),"OFF override gives known free time");Check(FamilyAvailability.NextOff(data,new DateTime(2026,10,9)).Contains(new DateTime(2026,10,10)),"next OFF respects overrides");
            data.overrides.Add(new DayOverrideData{dateKey="2026-10-11",shiftType=2});
            Check(FamilyAvailability.Check(data,new[]{me.id},new DateTime(2026,10,10),"11:00 PM",1200).StartsWith("Busy"),"multi-day request checks following work shift");
            data.group.shiftOwnerProfileId=null;Check(FamilyAvailability.NextOff(data,DateTime.Today).Count==0,"planner refuses to guess shift owner");
            appointment.durationMinutes=0;Check(FamilyAvailability.Check(data,new[]{me.id},new DateTime(2026,10,10),"10:30 AM",30).StartsWith("Unknown"),"missing duration remains unknown");appointment.durationMinutes=60;
            var allDay=new CalendarActivity{title="Vacation",dateKey="2026-11-01",startTime="10:00 AM",allDay=true,assigneeId=child.id};data.activities.Add(allDay);Check(FamilyAvailability.Check(data,new[]{child.id},new DateTime(2026,10,31),"11:30 PM",90).StartsWith("Busy"),"overnight request overlaps all-day activity through DST boundary");
            Check(ActivityResolver.Validate(new CalendarActivity{title="Untimed",dateKey="2026-10-10"},out _),"untimed activity valid without invented time");Check(!ActivityResolver.Validate(new CalendarActivity{title="Bad weekly",dateKey="2026-10-10",recurrence=RecurrenceKind.Weekly},out _),"empty weekly selection rejected");
            // Exercise the wired editor and canonical storage, using an isolated test account.
            var work=Object.FindFirstObjectByType<FamilyWorkbench>(FindObjectsInactive.Include);work.Refresh();var editor=work.editor;int before=data.activities.Count;editor.New("2026-10-11");editor.title.text="Family test note";editor.SelectIcon("karate");editor.assignee.value=2;editor.Commit();Check(data.activities.Count==before+1&&data.activities.Last().assigneeId==child.id&&data.events.Count==1,"wired editor creates canonical activity, not duplicate legacy event");
            editor.New("2026-10-11");editor.title.text="Local alarm appointment";editor.timed.isOn=true;editor.time.Set("9:00 AM");editor.alarm.isOn=true;editor.duration.Set(30);editor.Commit();var created=data.activities.Last();Check(DeviceCalendarStore.Find("activity-"+created.id)?.alarm==true,"creator opts into device-local alarm");Check(!DeviceCalendarStore.Effective(data,deviceB).events.Single(x=>x.id=="activity-"+created.id).alarm,"shared creation leaves device B unsubscribed");
            var template=new ActivityTemplate{name="Weekly class",activity=karate};editor.templates.SaveDraft(template,false);editor.templates.SaveDraft(new ActivityTemplate{name=template.name,activity=karate},false);Check(DeviceCalendarStore.State.templates.Count(x=>x.name==template.name)==1&&data.templates.Count==0,"device templates update without duplicate or shared record");
            var icons=ActivityIconSet.Load();Check(icons.sprites.Length==20&&icons.sprites.All(x=>x!=null)&&icons.sprites.Distinct().Count()==20,"20 distinct committed icon sprites");
            cal.currentMonth=new DateTime(2026,10,1);cal.Refresh();cal.SetEditing(false);var month=cal.currentMonth;cal.gesture.Begin(new Vector2(250,300));Check(cal.gesture.Finish(new Vector2(50,310))&&cal.currentMonth==month.AddMonths(1),"horizontal physical-pixel swipe changes month");cal.gesture.Begin(new Vector2(250,300));Check(!cal.gesture.Finish(new Vector2(240,100)),"vertical drag never changes month");cal.SetEditing(true);month=cal.currentMonth;cal.gesture.Begin(new Vector2(250,300));Check(!cal.gesture.Finish(new Vector2(50,300))&&cal.currentMonth==month,"Edit drag never navigates month");cal.SetEditing(false);
            var pendingActivity=ActivityResolver.Clone(appointment);pendingActivity.title="Remote rename";string payload=JsonUtility.ToJson(pendingActivity);var record=data.records.Find(x=>x.key=="activity/appointment");record.pending=true;record.revision=1;sync.Merge(record.key,payload,false,2);Check(record.conflict&&data.activities.Contains(appointment),"concurrent activity edits produce explicit conflict");record.pending=false;record.conflict=false;sync.Merge(record.key,payload,false,2);Check(data.activities.Count(x=>x.id==appointment.id)==1&&data.activities.Find(x=>x.id==appointment.id).title=="Remote rename","remote activity applies by stable ID");sync.Merge(record.key,"",true,3);Check(!data.activities.Any(x=>x.id==appointment.id)&&DeviceCalendarStore.State.subscriptions.Any(x=>x.sourceId=="activity-appointment"),"remote tombstone does not erase local subscription definitions");
            work.Details(ActivityResolver.Resolve(data,new DateTime(2026,10,1),new DateTime(2026,10,31)).First(x=>x.seriesId==karate.id));Check(work.detailsBody.text.Contains("Weekly"),"activity details show readable recurrence");work.CloseDetails();
            Migration();SetDevice(local);Capture(work,cal,oldWorkbench,data);
            Debug.Log("Family calendar checks passed: "+checks);
        }
        finally
        {
            foreach(string file in Directory.GetFiles(Application.persistentDataPath,Path.GetFileName(ScheduleStorage.PathFor(data.account))+"*"))File.Delete(file);
            SetDevice(device);typeof(AppSession).GetProperty("Data").SetValue(session,previous);session.CurrentGroup=group;
        }
    }
    static void SetDevice(DeviceCalendarState state)=>typeof(DeviceCalendarStore).GetProperty("State").SetValue(null,state);
    static void Migration()
    {
        var s=new ScheduleSave{account="migration-check-"+Guid.NewGuid().ToString("N"),group=new GroupData{groupId="local-migration",name="Original",startDateKey="2026-10-01",pattern=new List<int>{1}}};s.rules.Add(new ShiftAlarmRule{id="original-wake",shiftType=1});string path=ScheduleStorage.PathFor(s.account);File.WriteAllText(path,JsonUtility.ToJson(s));
        try{DeviceCalendarStore.Load(s,true);Check(DeviceCalendarStore.State.rules.Single().id=="original-wake"&&File.Exists(path+".before-family-migration.json"),"migration keeps rule identity and untouched backup");string saved=File.ReadAllText(DeviceCalendarStore.PathFor(s.account));DeviceCalendarStore.Load(s,true);Check(File.ReadAllText(DeviceCalendarStore.PathFor(s.account))==saved&&s.rules.Count==1,"migration idempotent across restart");ScheduleStorage.Write(s);var restored=ScheduleStorage.Load(s.account,s.group);Check(restored.rules.Count==0&&restored.group.name=="Original","shared save and private definitions persist separately");DeviceCalendarStore.Hydrate(restored);Check(restored.rules.Single().id=="original-wake","device definitions hydrate after shared save reload");DeviceCalendarStore.Write();File.WriteAllText(DeviceCalendarStore.PathFor(s.account),"broken JSON");DeviceCalendarStore.Load(restored,true);Check(DeviceCalendarStore.State.rules.Single().id=="original-wake","corrupt device file recovers prior alarm definitions");DeviceCalendarStore.Write();Check(Directory.GetFiles(Application.persistentDataPath,Path.GetFileName(DeviceCalendarStore.PathFor(s.account))+".corrupt-*").Length==1,"recovery retains corrupt original and good previous file");}
        finally{foreach(string file in Directory.GetFiles(Application.persistentDataPath,Path.GetFileName(path)+"*"))File.Delete(file);}
        var interrupted=new ScheduleSave{account="interrupted-migration-"+Guid.NewGuid().ToString("N"),group=ActivityResolver.Clone(s.group)};
        path=ScheduleStorage.PathFor(interrupted.account);
        try{File.WriteAllText(path+".legacy.json",JsonUtility.ToJson(s));ScheduleStorage.Write(interrupted);DeviceCalendarStore.Load(interrupted,true);Check(DeviceCalendarStore.State.rules.Single().id=="original-wake","interrupted legacy import recovers original definitions from retained backup");}
        finally{foreach(string file in Directory.GetFiles(Application.persistentDataPath,Path.GetFileName(path)+"*"))File.Delete(file);}
    }
    static void Capture(FamilyWorkbench work,CalendarController cal,ScheduleWorkbench old,ScheduleSave data)
    {
        var canvas=Object.FindFirstObjectByType<Canvas>();var camera=Object.FindFirstObjectByType<Camera>();var root=canvas.transform.GetChild(0);canvas.GetComponent<CanvasScaler>().enabled=false;root.GetComponent<SafeAreaFitter>().enabled=false;
        var screens=new[]{"Calendar","Preview","Day","Details","Editor","Icons","Profiles","Templates","Agenda","Planner"};Directory.CreateDirectory("Logs/UI/Family");
        foreach(var size in new[]{new Vector2Int(360,640),new Vector2Int(393,851),new Vector2Int(412,915)})
        {
            var rt=new RenderTexture(size.x,size.y,24);camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.scaleFactor=size.x/1080f;var safe=(RectTransform)root;safe.anchorMin=new Vector2(0,24f/size.y);safe.anchorMax=new Vector2(1,1-24f/size.y);safe.offsetMin=safe.offsetMax=Vector2.zero;
            foreach(ThemeManager.Theme theme in Enum.GetValues(typeof(ThemeManager.Theme)))foreach(string screen in screens)
            {
                old.Hide();work.editor.panel.SetActive(false);work.editor.iconPanel.SetActive(false);work.detailsPanel.SetActive(false);cal.preview.Hide();cal.GetComponentInChildren<DayDetailsPopup>(true).Hide();
                foreach(Transform child in root)if(child.name.EndsWith("Screen")||child.name=="Family Agenda"||child.name=="Family Profiles"||child.name=="Activity Templates"||child.name=="Availability Planner")child.gameObject.SetActive(child.name=="Calendar Screen");
                cal.currentMonth=new DateTime(2026,10,1);cal.Refresh();var item=ActivityResolver.Resolve(data,new DateTime(2026,10,1),new DateTime(2026,10,31)).First(x=>x.activity.icon=="karate");
                if(screen=="Preview"||screen=="Day"){cal.TapDay(item.dateKey);if(screen=="Day")cal.preview.OpenDetails();}
                if(screen=="Details")work.Details(item);
                if(screen=="Editor"||screen=="Icons"){work.editor.Edit(item);if(screen=="Icons")work.editor.Icons();}
                if(screen=="Profiles")Object.FindFirstObjectByType<FamilyProfileController>(FindObjectsInactive.Include).Open();
                if(screen=="Templates")work.editor.templates.Open();
                if(screen=="Agenda")work.ShowAgenda();
                if(screen=="Planner"){var p=Object.FindFirstObjectByType<AvailabilityPlanner>(FindObjectsInactive.Include);p.Open();p.Search();}
                ThemeManager.Apply(theme);camera.backgroundColor=ThemeManager.Token(ThemeManager.Role.Background);Canvas.ForceUpdateCanvases();cal.GetComponentInChildren<ResponsiveCalendarGrid>(true).Fit();Canvas.ForceUpdateCanvases();foreach(var c in cal.GetComponentsInChildren<CalendarDayCell>(true))c.FlushPresentation();if(cal.preview.gameObject.activeSelf)cal.preview.Fit();Canvas.ForceUpdateCanvases();
                if(screen=="Agenda"){var headings=work.agendaContent.GetComponentsInChildren<Text>().Where(x=>x.name=="Agenda summary"&&x.fontStyle==FontStyle.Bold).ToArray();Check(headings.Select(x=>x.text).SequenceEqual(Enumerable.Range(0,7).Select(i=>DateTime.Today.AddDays(i).ToString("ddd, MMM d"))),"agenda dates stay chronological "+theme+" "+size);Check(headings.Zip(headings.Skip(1),(a,b)=>a.transform.position.y>b.transform.position.y).All(x=>x),"agenda date positions stay chronological "+theme+" "+size);}
                var editor=work.editor.panel;if(screen=="Editor"){var footer=editor.GetComponentsInChildren<RectTransform>().First(x=>x.name=="Editor actions");var corners=new Vector3[4];footer.GetWorldCorners(corners);Check(corners.All(p=>{var point=RectTransformUtility.WorldToScreenPoint(camera,p);return point.y>=0&&point.y<=size.y;}),"family editor footer fits "+theme+" "+size);}
                camera.Render();RenderTexture.active=rt;var tex=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,size.x,size.y),0,0);tex.Apply();File.WriteAllBytes("Logs/UI/Family/"+theme+"-"+screen+"-"+size.x+"x"+size.y+".png",tex.EncodeToPNG());Object.DestroyImmediate(tex);
            }
            RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(rt);
        }
    }
}
