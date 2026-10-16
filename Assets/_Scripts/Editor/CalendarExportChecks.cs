using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShiftCal.App;
using ShiftCal.Core;
using ShiftCal.Data;
using ShiftCal.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class CalendarExportChecks
{
    static ScheduleSave Fixture()
    {
        var s=new ScheduleSave { account="export-fixture",group=new GroupData { groupId="local-export",startDateKey="2026-11-01",pattern=new List<int>{2,3,1,4},shiftTypes=new List<ShiftTypeDefinitionData>{
            new ShiftTypeDefinitionData{id=1,name="OFF",colorHex="#9FF4F1"},new ShiftTypeDefinitionData{id=2,name="Days",colorHex="#FF9B6B"},new ShiftTypeDefinitionData{id=3,name="Nights",colorHex="#82B2FF"},new ShiftTypeDefinitionData{id=4,name="Fill Day",colorHex="#D5C5FF"} } } };
        s.profiles.AddRange(new[]{new FamilyProfile{id="parent",name="Parent",linkedUid="me",color="#60A5FA"},new FamilyProfile{id="child",name="Child",color="#EC4899"}});
        s.overrides.Add(new DayOverrideData{dateKey="2026-11-07",scheduledShift=false,shiftType=4,note="Shared note indicator"});
        s.activities.Add(new CalendarActivity{id="karate",title="Karate",icon="karate",dateKey="2026-11-03",recurrence=RecurrenceKind.Weekly,weekdays=new List<int>{2,4},assigneeId="child"});
        // Stable ignored payload: Unity's JSON serializer invents a fresh ID for a null inline object.
        s.activityExceptions.Add(new ActivityException{seriesId="karate",originalDate="2026-11-10",cancelled=true,replacement=new CalendarActivity{id="cancelled-placeholder"}});
        s.activityExceptions.Add(new ActivityException{seriesId="karate",originalDate="2026-11-12",replacement=new CalendarActivity{id="moved",title="Moved class",icon="karate",dateKey="2026-11-13",assigneeId="child"}});
        for(int i=0;i<6;i++)s.activities.Add(new CalendarActivity{id="dense-"+i,title="Activity "+i,dateKey="2026-11-07",icon=ActivityIconSet.Ids[i],assigneeId=i%2==0?"parent":"child"});
        s.activities.Add(new CalendarActivity{id="unassigned",title="Family note",dateKey="2026-11-08",icon="note"});return s;
    }
    static CalendarExportSnapshot Snapshot(ScheduleSave s,string filter="everyone")=>CalendarExportSnapshot.Create(s,new DateTime(2026,11,1),s.overrides.ToDictionary(o=>o.dateKey),filter,"me");
    public static void Models(Action<bool,string> check)
    {
        var exporter=Object.FindFirstObjectByType<CalendarImageExporter>();check(exporter!=null&&exporter.calendar!=null&&exporter.font!=null&&exporter.rounded!=null&&exporter.frame!=null&&exporter.note!=null,"saved image exporter and sprites wired");
        var s=Fixture();string unchanged=JsonUtility.ToJson(s);var snapshot=Snapshot(s);
        var expected=CalendarGenerator.Generate(new DateTime(2026,11,1),s.group,s.overrides.ToDictionary(o=>o.dateKey));
        check(snapshot.title=="November 2026"&&snapshot.fileName=="ShiftCal-November-2026.png"&&snapshot.days.Count==42,"displayed month produces 42 dates and predictable PNG filename");
        check(snapshot.days.Select(d=>d.dateKey).SequenceEqual(expected.Select(d=>d.dateKey))&&snapshot.days.Select(d=>d.shiftColor).SequenceEqual(expected.Select(d=>d.shiftColorHex)),"export uses canonical rotation, colors and visible date grid");
        check(snapshot.days.Single(d=>d.dateKey=="2026-11-07").shiftName=="Fill Day"&&snapshot.days.Single(d=>d.dateKey=="2026-11-07").hasNote,"export applies current date override and shared note indicator");
        check(snapshot.days.Count(d=>!d.currentMonth)>0&&snapshot.days.Last().dateKey==expected.Last().dateKey,"adjacent-month dates retain dimming and sixth row");
        check(snapshot.days.Single(d=>d.dateKey=="2026-11-07").activities.Count==6,"dense activities retained for capacity and overflow");
        check(snapshot.days.Single(d=>d.dateKey=="2026-11-10").activities.Count==0&&snapshot.days.Single(d=>d.dateKey=="2026-11-13").activities.Single().icon=="karate","activity recurrence cancellation and moved exception respected");
        var child=Snapshot(s,"child");check(child.filterLabel=="Activities: Child"&&child.days.Single(d=>d.dateKey=="2026-11-07").activities.Count==3&&child.days.Single(d=>d.dateKey=="2026-11-07").activities.All(a=>a.assignmentColor=="#EC4899"),"person filter and assignment colors retained");
        check(Snapshot(s,"mine").days.Single(d=>d.dateKey=="2026-11-07").activities.Count==3&&Snapshot(s,"unassigned").days.Single(d=>d.dateKey=="2026-11-08").activities.Count==1,"Mine and Unassigned use existing activity filtering");
        check(child.days.Select(d=>d.shiftName).SequenceEqual(snapshot.days.Select(d=>d.shiftName)),"activity filters never filter work rotation");
        var sixWeek=CalendarExportSnapshot.Create(s,new DateTime(2026,5,1),s.overrides.ToDictionary(o=>o.dateKey),"everyone","me");
        check(sixWeek.days[35].currentMonth&&sixWeek.days[35].dayNumber==31,"month requiring a sixth week retains the final in-month date");
        check(JsonUtility.ToJson(s)==unchanged,"snapshot generation never mutates saved data or queues");
        var theme=ThemeManager.Current;var graphics=RenderTexture.active;Directory.CreateDirectory("Logs/UI/CalendarExport");
        try
        {
            foreach(var style in (ThemeManager.Theme[])Enum.GetValues(typeof(ThemeManager.Theme)))
            {
                ThemeManager.Apply(style);
                foreach(var variant in new[]{"everyone","child","six-weeks"})
                {
                    string filter=variant=="child"?"child":"everyone";var input=variant=="six-weeks"?sixWeek:Snapshot(s,filter);
                    var png=CalendarExportRenderer.Png(input,exporter.font,exporter.rounded,exporter.frame,exporter.note);
                    File.WriteAllBytes("Logs/UI/CalendarExport/"+style+"-"+(variant=="six-weeks"?"May-six-weeks":"November-"+filter)+".png",png);
                    var texture=new Texture2D(2,2);check(texture.LoadImage(png)&&texture.width==2100&&texture.height==2460,"high-resolution valid PNG "+style+" "+filter);
                    check(texture.GetPixel(0,0).a==1&&ColorDistance(texture.GetPixel(0,0),ThemeManager.Token(ThemeManager.Role.Background))<.08f,"opaque finished export background "+style+" "+filter);Object.DestroyImmediate(texture);
                    check(RenderTexture.active==graphics&&!Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(c=>c.name=="Isolated calendar export"),"export restores graphics state and releases isolated layout "+style+" "+filter);
                    if(style==ThemeManager.Theme.MidnightGraphite&&variant=="everyone")
                    {
                        s.events.Add(new EventSeries{title="PRIVATE WAKE UP",dateKey="2026-11-07",startTime="04:00",alarm=true});s.rules.Add(new ShiftAlarmRule{label="PRIVATE RULE",shiftType=2});
                        var privateState=DeviceCalendarStore.State;privateState.workedHours.Add(new WorkedHoursEntry{groupId=s.group.groupId,dateKey="2026-11-07",hundredths=1350});
                        var again=CalendarExportRenderer.Png(Snapshot(s),exporter.font,exporter.rounded,exporter.frame,exporter.note);
                        privateState.workedHours.RemoveAt(privateState.workedHours.Count-1);
                        check(png.SequenceEqual(again),"personal hours, alarms and native-state-independent data cannot affect exported pixels");
                    }
                }
            }
            bool rejected=false;try{CalendarExportRenderer.Png(new CalendarExportSnapshot(),exporter.font,exporter.rounded,exporter.frame,exporter.note);}catch(ArgumentException){rejected=true;}
            check(rejected&&RenderTexture.active==graphics,"failed export releases graphics resources");
        }
        finally{ThemeManager.Apply(theme);}
    }
    static float ColorDistance(Color a,Color b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b);
    static void Click(Button button,Action<bool,string> check,string label)
    {
        var rect=(RectTransform)button.transform;var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,label+" receives actual raycast");ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    public static IEnumerator Runtime(Action<bool,string> check)
    {
        var exporter=Object.FindFirstObjectByType<CalendarImageExporter>();var calendar=exporter.calendar;var nav=AppNavigation.Instance;nav.ShowCalendar();yield return null;
        var originalMonth=calendar.currentMonth;var theme=ThemeManager.Current;var paths=new List<string>();
        try
        {
            foreach(var style in (ThemeManager.Theme[])Enum.GetValues(typeof(ThemeManager.Theme)))
            {
                ThemeManager.Apply(style);calendar.currentMonth=new DateTime(2026,11,1);calendar.Refresh();calendar.SetEditing(true);calendar.BeginDaySelection("2026-11-07");calendar.EndDaySelection("2026-11-07");yield return null;
                string save=JsonUtility.ToJson(AppSession.Instance.Data),device=JsonUtility.ToJson(DeviceCalendarStore.State);var selection=calendar.SelectedDates.ToArray();
                Click(exporter.shareButton,check,"Share "+style);check(exporter.IsBusy,"Share starts one asynchronous export "+style);
                for(int i=0;i<10&&exporter.IsBusy;i++)yield return null;
                paths.Add(exporter.LastPath);check(!exporter.IsBusy&&File.Exists(exporter.LastPath)&&Path.GetFileName(exporter.LastPath).StartsWith("ShiftCal-November-2026"),"Share exports the displayed November month "+style);
                check(calendar.IsEditing&&calendar.SelectedDates.SequenceEqual(selection)&&calendar.currentMonth==new DateTime(2026,11,1),"export retains edit selection and visible month "+style);
                check(JsonUtility.ToJson(AppSession.Instance.Data)==save&&JsonUtility.ToJson(DeviceCalendarStore.State)==device,"export does not save calendar, filter, hours, alarms or sync records "+style);
                Click(exporter.optionsButton,check,"Image options "+style);yield return null;check(SelectionSheet.Instance.IsOpen&&SelectionSheet.Instance.Owner==exporter.options,"compact image actions sheet opens "+style);nav.Back();yield return null;check(!SelectionSheet.Instance.IsOpen,"Back cancels image options without exporting "+style);
                Click(exporter.optionsButton,check,"Image options again "+style);yield return null;yield return null;Canvas.ForceUpdateCanvases();var saveChoice=SelectionSheet.Instance.content.GetComponentsInChildren<SelectionSheetRow>().Single(r=>r.label.text=="Save image");Click(saveChoice.button,check,"Save Image choice "+style);
                for(int i=0;i<10&&exporter.IsBusy;i++)yield return null;paths.Add(exporter.LastPath);check(exporter.LastAction=="save"&&File.Exists(exporter.LastPath)&&!SelectionSheet.Instance.IsOpen,"secondary Save Image action generates PNG and dismisses options "+style);
                calendar.SetEditing(false);var first=calendar.GetComponentsInChildren<CalendarDayCell>().First();calendar.TapDay(first.DateKey);yield return null;check(calendar.preview.gameObject.activeSelf,"calendar remains interactive after export "+style);calendar.preview.Hide();
            }
        }
        finally{foreach(string path in paths.Distinct())if(File.Exists(path))File.Delete(path);exporter.statusPanel.SetActive(false);calendar.SetEditing(false);calendar.currentMonth=originalMonth;ThemeManager.Apply(theme);calendar.Refresh();}
    }
}
