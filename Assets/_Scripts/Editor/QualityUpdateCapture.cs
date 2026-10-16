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
public static class QualityUpdateCapture
{
    static void ScrollTo(RectTransform rect)
    {
        var scroll=rect.GetComponentInParent<ScrollRect>();if(scroll==null)return;scroll.StopMovement();var local=scroll.viewport.InverseTransformPoint(rect.TransformPoint(new Vector3(rect.rect.center.x,rect.rect.yMax)));scroll.content.anchoredPosition-=new Vector2(0,local.y-scroll.viewport.rect.yMax+18);
    }
    public static void Run(Action<bool,string> check,FamilyWorkbench family)
    {
        var session=AppSession.Instance;var before=session.Data;var device=DeviceCalendarStore.State;var group=session.CurrentGroup;var overrides=session.CalendarOverrides.ToDictionary(x=>x.Key,x=>x.Value);bool preference=DisplayPreferences.ShowLoggedHoursTotal;
        var today=DateTime.Today;string key=DateKeyUtility.ToDateKey(today),tomorrow=DateKeyUtility.ToDateKey(today.AddDays(1)),past=DateKeyUtility.ToDateKey(today.AddDays(-7));
        var data=new ScheduleSave{account="quality-render-"+Guid.NewGuid().ToString("N"),group=new GroupData{groupId="local-quality-render",name="Family calendar",startDateKey=key,pattern=new List<int>{2,2,1,3},shiftOwnerProfileId="parent",shiftTypes=new List<ShiftTypeDefinitionData>{new ShiftTypeDefinitionData{id=1,name="OFF",colorHex="#9FF4F1"},new ShiftTypeDefinitionData{id=2,name="Days",colorHex="#FBBF24",startTime="05:30",endTime="17:30",hours=12},new ShiftTypeDefinitionData{id=3,name="Nights",colorHex="#6D7DF2",startTime="17:30",endTime="05:30",hours=12},new ShiftTypeDefinitionData{id=4,name="Fill Day",colorHex="#34D399"}}}};
        data.profiles.AddRange(new[]{new FamilyProfile{id="parent",name="Parent",color="#60A5FA"},new FamilyProfile{id="child",name="Child",color="#F59AC8"}});var activity=new CalendarActivity{id="class",title="Karate",dateKey=key,icon="karate",assigneeId="child",notes="Bring equipment",zone="device"};data.activities.Add(activity);
        data.events.AddRange(new[]{new EventSeries{id="old-event",title="Past appointment",dateKey=past,startTime="14:00",zone="device"},new EventSeries{id="next-event",title="Tomorrow's appointment",dateKey=tomorrow,startTime="14:00",zone="device"}});
        var local=new DeviceCalendarState{account=data.account};local.workedHours.Add(new WorkedHoursEntry{groupId=data.group.groupId,dateKey=key,hundredths=1200});local.personalAlarms.AddRange(new[]{new PersonalAlarmDefinition{groupId=data.group.groupId,schedule=new EventSeries{id="old-private",title="Past wake-up",dateKey=past,startTime="04:00",zone="device",alarm=true}},new PersonalAlarmDefinition{groupId=data.group.groupId,schedule=new EventSeries{id="future-private",title="Wake-up",dateKey=tomorrow,startTime="04:00",zone="device",alarm=true}}});local.templates.Add(new ActivityTemplate{name="Karate practice",activity=ActivityResolver.Clone(activity)});
        var canvas=Object.FindFirstObjectByType<Canvas>();var camera=Object.FindFirstObjectByType<Camera>();var root=canvas.transform.GetChild(0);var cal=family.calendar;var nav=AppNavigation.Instance;var work=Object.FindFirstObjectByType<ScheduleWorkbench>();var popup=cal.GetComponentInChildren<DayDetailsPopup>(true);var shifts=Object.FindFirstObjectByType<ShiftSettingsController>(FindObjectsInactive.Include);var hours=WorkedHoursEditor.Instance;Directory.CreateDirectory("Logs/UI/QualityUpdate");
        try
        {
            typeof(AppSession).GetProperty("Data").SetValue(session,data);typeof(DeviceCalendarStore).GetProperty("State").SetValue(null,local);session.CurrentGroup=data.group;session.CalendarOverrides.Clear();
            foreach(var size in new[]{new Vector2Int(360,640),new Vector2Int(393,851),new Vector2Int(412,915)})
            {
                var target=new RenderTexture(size.x,size.y,24);camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.scaleFactor=size.x/1080f;var safe=(RectTransform)root;safe.anchorMin=new Vector2(0,24f/size.y);safe.anchorMax=new Vector2(1,1-24f/size.y);safe.offsetMin=safe.offsetMax=Vector2.zero;
                foreach(ThemeManager.Theme theme in Enum.GetValues(typeof(ThemeManager.Theme)))foreach(string screen in new[]{"Settings","Display-hidden","Calendar-hidden","Preview-right","Day-right","Shift-timed","Shift-untimed","Sound","People-sheet","Active-events","History","Cleanup-confirm","Account","Alarm-preferences","Readiness","Event-editor","Private-alarm-editor","Manage-shifts","Activity-details","Family-profiles","Templates","Planner","Agenda","Agenda-untimed"})
                {
                    SelectionSheet.Instance.Cancel();nav.confirmation.Cancel();hours.Cancel();shifts.editor.gameObject.SetActive(false);work.Hide();family.editor.panel.SetActive(false);family.detailsPanel.SetActive(false);family.agendaPanel.SetActive(false);popup.Hide();cal.preview.Hide();data.group.pattern=new List<int>{2,2,1,3};nav.ShowLogin();nav.ShowCalendar();DisplayPreferences.ShowLoggedHoursTotal=screen!="Calendar-hidden"&&screen!="Display-hidden";cal.currentMonth=new DateTime(today.Year,today.Month,1);cal.SetEditing(false);cal.Refresh();
                    if(screen=="Settings"||screen=="Display-hidden"||screen=="Sound"||screen=="Alarm-preferences"){nav.ShowSettings();Object.FindFirstObjectByType<DisplaySettingsPanel>(FindObjectsInactive.Include).Refresh();}
                    if(screen=="Preview-right"||screen=="Day-right"){cal.TapDay(key);if(screen=="Day-right")cal.preview.OpenDetails();}
                    if(screen=="Shift-timed"||screen=="Shift-untimed"){nav.ShowManageShifts();shifts.editor.Show(shifts,data.group.shiftTypes.Find(s=>s.id==(screen=="Shift-timed"?3:4)));}
                    if(screen=="Manage-shifts")nav.ShowManageShifts();
                    if(screen=="Sound")Object.FindFirstObjectByType<AlarmPreferencesPanel>(FindObjectsInactive.Include).sound.Open();
                    if(screen=="People-sheet"){family.editor.New(key);family.editor.participants.transform.parent.gameObject.SetActive(true);family.editor.participants.Open();}
                    if(screen=="Active-events"||screen=="History"||screen=="Cleanup-confirm"||screen=="Readiness"){
                        work.ShowAgenda();if((screen=="History"||screen=="Cleanup-confirm")!=work.historySection.activeSelf)work.ToggleHistory();work.permissionDetails.SetActive(screen=="Readiness");if(screen=="Cleanup-confirm")work.CleanPersonalHistory();
                    }
                    if(screen=="Account")nav.ShowAccount();
                    if(screen=="Event-editor")family.editor.New(key);
                    if(screen=="Private-alarm-editor")work.NewPersonalAlarm(tomorrow);
                    if(screen=="Activity-details")family.Details(ActivityResolver.Resolve(data,today,today).Single());
                    if(screen=="Family-profiles")Object.FindFirstObjectByType<FamilyProfileController>(FindObjectsInactive.Include).Open();
                    if(screen=="Templates")family.editor.templates.Open();
                    if(screen=="Planner"){var planner=Object.FindFirstObjectByType<AvailabilityPlanner>(FindObjectsInactive.Include);planner.Open();planner.Search();}
                    if(screen=="Agenda")family.ShowAgenda();
                    if(screen=="Agenda-untimed"){data.group.pattern=new List<int>{4};family.showAllDates.isOn=false;family.ShowAgenda();}
                    ThemeManager.Apply(theme);camera.backgroundColor=ThemeManager.Token(ThemeManager.Role.Background);Canvas.ForceUpdateCanvases();cal.GetComponentInChildren<ResponsiveCalendarGrid>(true).Fit();foreach(var cell in cal.GetComponentsInChildren<CalendarDayCell>(true))cell.FlushPresentation();PresentationChecks.Fit();if(cal.preview.gameObject.activeSelf)cal.preview.Fit();Canvas.ForceUpdateCanvases();SelectionSheet.Instance.FitHeight();Canvas.ForceUpdateCanvases();
                    if(screen=="History")ScrollTo(work.historyHeading.rectTransform);if(screen=="Display-hidden")ScrollTo(Object.FindFirstObjectByType<DisplaySettingsPanel>(FindObjectsInactive.Include).GetComponent<RectTransform>());if(screen=="Alarm-preferences")ScrollTo(Object.FindFirstObjectByType<AlarmPreferencesPanel>(FindObjectsInactive.Include).sound.transform.parent.GetComponent<RectTransform>());Canvas.ForceUpdateCanvases();
                    string context=theme+" "+screen+" "+size;
                    if(screen=="Active-events"||screen=="History"||screen=="Readiness")check(string.IsNullOrEmpty(work.messageLabel.text),"schedule list binds hidden actions without a fallback error "+context);
                    if(screen=="Calendar-hidden")check(!cal.monthlyPill.gameObject.activeSelf&&cal.GetComponentsInChildren<CalendarDayCell>().Single(c=>c.DateKey==key).workedBadge.activeSelf,"hidden header keeps daily recorded-hours badge "+context);
                    if(screen=="Preview-right"||screen=="Day-right"){var pill=screen=="Preview-right"?cal.preview.hoursPill:popup.hoursPill;var rect=(RectTransform)pill.transform;check(rect.anchorMin.x==1&&rect.pivot.x==1&&rect.rect.width<((RectTransform)rect.parent).rect.width*.65f,"worked hours right aligned and content sized "+context);check(pill.label.preferredWidth<=pill.label.rectTransform.rect.width+1,"right hours pill label fits "+context);}
                    if(screen=="Shift-untimed")check(!shifts.editor.timeFields.activeSelf&&!shifts.editor.hours.gameObject.activeInHierarchy,"untimed editor collapses all scheduled hours controls "+context);
                    if(screen=="Sound"){var sheet=SelectionSheet.Instance;check(!sheet.actions.activeSelf&&sheet.card.rect.height<((RectTransform)sheet.transform).rect.height*.65f,"sound sheet fits three compact choices without footer "+context);check(sheet.content.GetComponentsInChildren<SelectionSheetRow>().All(r=>r.label.preferredWidth<=r.label.rectTransform.rect.width+1),"sound labels fit each row "+context);}
                    if(screen=="History")check(work.HistoryCount==2&&work.historyContent.GetComponentsInChildren<ScheduleListRow>().Length==2,"history groups completed shared event and personal alarm "+context);
                    if(screen=="Agenda-untimed")check(family.agendaContent.GetComponentsInChildren<AgendaDayCard>().Length==7&&family.agendaContent.GetComponentsInChildren<AgendaDayCard>().All(c=>c.shift.text=="Fill Day"),"untimed working shifts remain visible without being inferred OFF "+context);
                    camera.Render();RenderTexture.active=target;var texture=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,size.x,size.y),0,0);texture.Apply();File.WriteAllBytes("Logs/UI/QualityUpdate/"+theme+"-"+screen+"-"+size.x+"x"+size.y+".png",texture.EncodeToPNG());Object.DestroyImmediate(texture);
                }
                RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(target);
            }
        }
        finally
        {
            SelectionSheet.Instance.Cancel();nav.confirmation.Cancel();shifts.editor.gameObject.SetActive(false);work.Hide();family.editor.panel.SetActive(false);family.detailsPanel.SetActive(false);family.agendaPanel.SetActive(false);popup.Hide();hours.Cancel();typeof(AppSession).GetProperty("Data").SetValue(session,before);typeof(DeviceCalendarStore).GetProperty("State").SetValue(null,device);session.CurrentGroup=group;session.CalendarOverrides.Clear();foreach(var pair in overrides)session.CalendarOverrides[pair.Key]=pair.Value;DisplayPreferences.ShowLoggedHoursTotal=preference;
        }
    }
}
