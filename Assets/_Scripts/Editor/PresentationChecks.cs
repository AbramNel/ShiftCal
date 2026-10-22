using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShiftCal.App;
using ShiftCal.Core;
using ShiftCal.Data;
using ShiftCal.UI;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
public static class PresentationChecks
{
    static Rect World(RectTransform r){var c=new Vector3[4];r.GetWorldCorners(c);return Rect.MinMaxRect(c[0].x,c[0].y,c[2].x,c[2].y);}
    public static void Fit(){foreach(var pill in Object.FindObjectsByType<ContentPill>(FindObjectsInactive.Include,FindObjectsSortMode.None))pill.Fit();}
    public static void Run(Action<bool,string> check,FamilyWorkbench work,CalendarController calendar)
    {
        var session=AppSession.Instance;var previous=session.Data;var oldGroup=session.CurrentGroup;var oldDevice=DeviceCalendarStore.State;var oldOverrides=session.CalendarOverrides.ToDictionary(x=>x.Key,x=>x.Value);
        var data=new ScheduleSave{account="presentation-"+Guid.NewGuid().ToString("N"),group=new GroupData{groupId="presentation",name="Test calendar",startDateKey="2026-10-01",pattern=new List<int>{2,2,1,3,2,1},shiftTypes=new List<ShiftTypeDefinitionData>{new ShiftTypeDefinitionData{id=1,name="OFF",colorHex="#9FF4F1"},new ShiftTypeDefinitionData{id=2,name="Days",colorHex="#FBBF24",startTime="5:30 AM",endTime="5:30 PM"},new ShiftTypeDefinitionData{id=3,name="Nights",colorHex="#6D7DF2",startTime="5:30 PM",endTime="5:30 AM"}}}};
        var device=new DeviceCalendarState{account=data.account};var today=DateTime.Today;string first=DateKeyUtility.ToDateKey(today),second=DateKeyUtility.ToDateKey(today.AddDays(1));
        var profiles=new[]{new FamilyProfile{id="p",name="Parent",color="#60A5FA"},new FamilyProfile{id="c",name="Child",color="#F59AC8"}};data.profiles.AddRange(profiles);
        var activities=new[]{new CalendarActivity{id="karate",dateKey=first,title="Karate",icon="karate",assigneeId="c"},new CalendarActivity{id="appointment",dateKey=second,title="Afternoon appointment",icon="appointment",startTime="14:00",durationMinutes=60,assigneeId="p"},new CalendarActivity{id="pickup",dateKey=second,title="School pickup",icon="pickup",startTime="15:30",assigneeId="c"}};
        var alarms=new[]{new PersonalAlarmDefinition{groupId=data.group.groupId,schedule=new EventSeries{id="personal-presentation",title="Wake-up",dateKey=second,startTime="04:00",zone="device",alarm=true}}};
        var canvas=Object.FindFirstObjectByType<Canvas>();var camera=Object.FindFirstObjectByType<Camera>();var root=canvas.transform.GetChild(0);var old=Object.FindFirstObjectByType<ScheduleWorkbench>(FindObjectsInactive.Include);Directory.CreateDirectory("Logs/UI/Presentation");
        var hours=WorkedHoursEditor.Instance;
        try
        {
            typeof(AppSession).GetProperty("Data").SetValue(session,data);typeof(DeviceCalendarStore).GetProperty("State").SetValue(null,device);session.CurrentGroup=data.group;
            string manifest=File.ReadAllText("Assets/Plugins/Android/ShiftCal.androidlib/src/main/AndroidManifest.xml");check(manifest.Contains("USE_FULL_SCREEN_INTENT")&&!manifest.Contains("showWhenLocked")&&!manifest.Contains("turnScreenOn"),"full-screen permission declared; only verified ringing state enables Activity presentation programmatically");
            check(Object.FindObjectsByType<Text>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(t=>t.text=="Full-screen alarms"),"saved readiness panel exposes full-screen alarm permission settings");
            check(hours.hoursIcon!=null&&File.Exists("Assets/_UI/WorkedHoursTimesheet.png"),"committed timesheet sprite and generator wired");
            foreach(var cell in calendar.GetComponentsInChildren<CalendarDayCell>(true))check(cell.workedClock.sprite==hours.hoursIcon&&cell.workedClock.sprite!=cell.transform.Find("Alarm indicator").GetComponent<Image>().sprite,"all calendar hours use distinct timesheet sprite");
            foreach(var size in new[]{new Vector2Int(360,640),new Vector2Int(393,851),new Vector2Int(412,915)})
            {
                var rt=new RenderTexture(size.x,size.y,24);camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.scaleFactor=size.x/1080f;
                var safe=(RectTransform)root;safe.anchorMin=new Vector2(0,24f/size.y);safe.anchorMax=new Vector2(1,1-24f/size.y);safe.offsetMin=safe.offsetMax=Vector2.zero;
                foreach(ThemeManager.Theme theme in Enum.GetValues(typeof(ThemeManager.Theme)))foreach(string screen in new[]{"Calendar-no-hours","Calendar-hours","Calendar-edit","Calendar-long-month","Preview-hours","Preview-no-hours","Agenda-events","Agenda-empty","Agenda-all-dates","Agenda-assignments","Agenda-personal","Hours-editor","Monthly-summary"})
                {
                    old.Hide();work.editor.panel.SetActive(false);work.detailsPanel.SetActive(false);work.agendaPanel.SetActive(false);calendar.GetComponentInChildren<DayDetailsPopup>(true).Hide();hours.Cancel();SelectionSheet.Instance.Cancel();calendar.SetEditing(false);
                    foreach(Transform child in root)if(child.name.EndsWith("Screen")||child.name=="Family Agenda"||child.name=="Family Profiles"||child.name=="Activity Templates"||child.name=="Availability Planner")child.gameObject.SetActive(child.name=="Calendar Screen");
                    data.activities=ActivityResolver.CloneList(activities);data.events.Clear();data.rules.Clear();device.personalAlarms=ActivityResolver.CloneList(alarms);device.rules.Clear();device.workedHours.Clear();device.filter="everyone";data.group.pattern=new List<int>{2,2,1,3,2,1};session.CalendarOverrides.Clear();session.CalendarOverrides[first]=new DayOverrideData{dateKey=first,scheduledShift=true,note="Bring signed forms"};data.overrides=session.CalendarOverrides.Values.ToList();
                    if(screen!="Calendar-no-hours"&&screen!="Preview-no-hours"){device.workedHours.Add(new WorkedHoursEntry{groupId=data.group.groupId,dateKey=first,hundredths=1200});device.workedHours.Add(new WorkedHoursEntry{groupId=data.group.groupId,dateKey=second,hundredths=1350});}
                    calendar.currentMonth=new DateTime(today.Year,today.Month,1);
                    if(screen=="Calendar-long-month"){calendar.currentMonth=new DateTime(2026,9,1);device.workedHours.Clear();for(int i=1;i<=30;i++)device.workedHours.Add(new WorkedHoursEntry{groupId=data.group.groupId,dateKey="2026-09-"+i.ToString("00"),hundredths=2400});}
                    calendar.Refresh();work.Refresh();
                    if(screen=="Calendar-edit"){calendar.SetEditing(true);calendar.BeginDaySelection(first);calendar.EndDaySelection(first);}
                    if(screen.StartsWith("Preview"))calendar.TapDay(first);
                    if(screen.StartsWith("Agenda")){
                        if(screen=="Agenda-empty"||screen=="Agenda-all-dates"){data.activities.Clear();device.personalAlarms.Clear();data.group.pattern=new List<int>{1};}
                        if(screen=="Agenda-personal")data.activities.Clear();work.showAllDates.isOn=screen=="Agenda-all-dates";work.agendaPanel.SetActive(true);work.ShowAgenda();
                    }
                    if(screen=="Hours-editor")hours.Open(first);if(screen=="Monthly-summary")hours.ShowMonth(calendar.currentMonth);
                    ThemeManager.Apply(theme);camera.backgroundColor=ThemeManager.Token(ThemeManager.Role.Background);Canvas.ForceUpdateCanvases();calendar.GetComponentInChildren<ResponsiveCalendarGrid>(true).Fit();Canvas.ForceUpdateCanvases();Fit();foreach(var cell in calendar.GetComponentsInChildren<CalendarDayCell>(true))cell.FlushPresentation();if(calendar.preview.gameObject.activeSelf)calendar.preview.Fit();SelectionSheet.Instance.FitHeight();Canvas.ForceUpdateCanvases();
                    string context=theme+" "+screen+" "+size;
                    foreach(var pill in Object.FindObjectsByType<ContentPill>(FindObjectsSortMode.None))check(pill.label.preferredWidth<=pill.label.rectTransform.rect.width+1&&pill.label.cachedTextGenerator.lineCount==1,"hours pill label fits on one line "+context);
                    if(screen.StartsWith("Calendar")){
                        check(calendar.monthlyPill.GetComponent<RectTransform>().rect.width<((RectTransform)calendar.monthlyPill.transform.parent).rect.width*.8f,"monthly pill fits content "+context);
                        var title=calendar.transform.Find("Month Bar/Month summary/Month Label").GetComponent<RectTransform>();check(!World(title).Overlaps(World((RectTransform)calendar.monthlyPill.transform)),"month and total have separate geometry "+context);
                        var grid=calendar.GetComponentInChildren<ResponsiveCalendarGrid>(true).GetComponent<GridLayoutGroup>();check(grid.cellSize.y*6+grid.spacing.y*5<=((RectTransform)grid.transform).rect.height+1,"all six calendar rows fit "+context);
                    }
                    if(screen.StartsWith("Preview")){var pill=calendar.preview.hoursPill;check(pill.GetComponent<RectTransform>().rect.width<calendar.preview.card.rect.width*.65f&&pill.GetComponent<RectTransform>().rect.height>=108,"preview hours compact with floating footer touch target "+context);check(pill.icon.sprite==hours.hoursIcon,"preview uses timesheet sprite "+context);}
                    if(screen.StartsWith("Agenda")){
                        var cards=work.agendaContent.GetComponentsInChildren<AgendaDayCard>();check(cards.All(c=>c.date.transform.position.y>c.entries.position.y),"agenda day headers precede entries "+context);
                        if(screen=="Agenda-empty")check(cards.Length==0,"empty agenda omits day cards "+context);if(screen=="Agenda-all-dates")check(cards.Length==7,"empty dates remain compact chronological rows "+context);
                        if(screen=="Agenda-personal")check(work.agendaContent.GetComponentsInChildren<FamilyActivityRow>().Any(r=>r.IsAlarm&&r.icon.sprite==work.alarmIcon&&r.detail.text.Contains("This device")),"private alarm has icon, time and editor action "+context);
                        foreach(var row in work.agendaContent.GetComponentsInChildren<FamilyActivityRow>().Where(r=>r.SourceId==null)){var profile=profiles.FirstOrDefault(p=>row.detail.text.Contains(p.name));if(profile!=null)check(row.frame.color==ShiftStyleUtility.ToColor(profile.color),"assignment color survives theme repaint "+context);}
                    }
                    camera.Render();RenderTexture.active=rt;var texture=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,size.x,size.y),0,0);texture.Apply();File.WriteAllBytes("Logs/UI/Presentation/"+theme+"-"+screen+"-"+size.x+"x"+size.y+".png",texture.EncodeToPNG());Object.DestroyImmediate(texture);
                }
                RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(rt);
            }
        }
        finally
        {
            hours.Cancel();SelectionSheet.Instance.Cancel();work.agendaPanel.SetActive(false);typeof(AppSession).GetProperty("Data").SetValue(session,previous);session.CurrentGroup=oldGroup;typeof(DeviceCalendarStore).GetProperty("State").SetValue(null,oldDevice);session.CalendarOverrides.Clear();foreach(var pair in oldOverrides)session.CalendarOverrides[pair.Key]=pair.Value;
        }
    }
}
