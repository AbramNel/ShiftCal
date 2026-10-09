using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ShiftCal.App;
using ShiftCal.Core;
using ShiftCal.Data;
namespace ShiftCal.UI
{
    public class FamilyWorkbench : MonoBehaviour
    {
        public GameObject agendaPanel,detailsPanel;
        public Transform agendaContent,dayContent;
        public FamilyActivityRow rowPrefab;
        public ActivityEditor editor;
        public Text detailsTitle,detailsBody,detailsMessage;
        public Image detailsIcon;
        public Dropdown filter,agendaFilter,detailScope;
        public CalendarController calendar;
        ActivityOccurrence selected;
        List<string> filterIds=new List<string>();
        public static string Filter=>DeviceCalendarStore.State?.filter??"everyone";
        public static string Uid=>Firebase.AuthService.Instance?.IsSignedIn==true?Firebase.AuthService.Instance.UserId:AppSession.Instance?.Data?.account!="local"?AppSession.Instance?.Data?.account:null;
        void Start(){AppSession.Instance.Changed+=Refresh;ThemeManager.Changed+=Refresh;filter.onValueChanged.AddListener(SetFilter);agendaFilter.onValueChanged.AddListener(SetFilter);Refresh();}
        void OnDestroy(){if(AppSession.Instance!=null)AppSession.Instance.Changed-=Refresh;ThemeManager.Changed-=Refresh;}
        public void NewActivity()=>editor.New(DateKeyUtility.ToDateKey(DateTime.Today));
        public void ShowAgenda(){AppNavigation.Instance.Open(agendaPanel);Refresh();}
        public void Refresh()
        {
            if(AppSession.Instance?.Data==null)return;
            var s=AppSession.Instance.Data;filterIds=new List<string>{"everyone","mine","unassigned"};filterIds.AddRange(s.profiles.Where(x=>x.active).Select(x=>x.id));
            if(!filterIds.Contains(Filter)&&DeviceCalendarStore.State!=null){DeviceCalendarStore.State.filter="everyone";DeviceCalendarStore.Write();calendar.Refresh();}
            filter.ClearOptions();filter.AddOptions(new List<string>{"Everyone","Mine","Unassigned"}.Concat(s.profiles.Where(x=>x.active).Select(x=>x.name)).ToList());filter.SetValueWithoutNotify(Math.Max(0,filterIds.IndexOf(Filter)));agendaFilter.ClearOptions();agendaFilter.AddOptions(filter.options);agendaFilter.SetValueWithoutNotify(Math.Max(0,filterIds.IndexOf(Filter)));
            if(!agendaPanel.activeSelf)return;Clear(agendaContent);
            var occurrences=ActivityResolver.Resolve(s,DateTime.Today,DateTime.Today.AddDays(6),Filter,Uid);
            var alarms=RecurrenceEngine.Resolve(DeviceCalendarStore.Effective(s),DateTime.Today,DateTime.Today.AddDays(6));
            for(int i=0;i<7;i++)
            {
                var date=DateTime.Today.AddDays(i);string key=DateKeyUtility.ToDateKey(date);
                AddText(agendaContent,date.ToString("ddd, MMM d"),true);
                var day=CalendarGenerator.Generate(new DateTime(date.Year,date.Month,1),s.group,AppSession.Instance.CalendarOverrides).Find(x=>x.dateKey==key);
                var shift=AddText(agendaContent,day.shiftName+(!string.IsNullOrEmpty(day.startTime)?" · "+day.startTime+" – "+day.endTime:""));var button=shift.gameObject.AddComponent<Button>();shift.raycastTarget=true;button.onClick.AddListener(()=>OpenDay(date));
                foreach(var item in occurrences.Where(x=>x.dateKey==key))AddRow(agendaContent,item);
                foreach(var e in s.events)foreach(var item in RecurrenceEngine.EventDates(s,e,date,date)){var label=AddText(agendaContent,item.title+" · "+DayInformation.Time(item));label.raycastTarget=true;label.gameObject.AddComponent<Button>().onClick.AddListener(()=>FindFirstObjectByType<ScheduleWorkbench>(FindObjectsInactive.Include).EditEvent(e.id,key));}
                foreach(var alarm in alarms.Where(x=>(x.sourceId==null||!x.sourceId.StartsWith("activity-")||occurrences.Any(a=>"activity-"+a.seriesId==x.sourceId&&a.originalDate==x.dateKey))&&x.calendarDateKey==key&&!x.id.EndsWith(":view")&&x.state!="view"&&x.at>DateKeyUtility.UnixMsNow()&&(x.isEvent||x.audible)))AddText(agendaContent,"This device · "+alarm.title+" · "+DayInformation.Time(alarm));
                if(!occurrences.Any(x=>x.dateKey==key))AddText(agendaContent,"No activities in this filter");
            }
        }
        void SetFilter(int value)
        {
            if(value<0||value>=filterIds.Count||DeviceCalendarStore.State==null)return;DeviceCalendarStore.State.filter=filterIds[value];DeviceCalendarStore.Write();calendar.Refresh();Refresh();
        }
        public void FillDay(string key,Transform content)
        {
            Clear(content);var date=DateKeyUtility.FromDateKey(key);
            foreach(var a in ActivityResolver.Resolve(AppSession.Instance.Data,date,date,Filter,Uid))AddRow(content,a);
        }
        void AddRow(Transform content,ActivityOccurrence a){var row=Instantiate(rowPrefab,content);row.transform.SetAsLastSibling();row.Bind(a,()=>Details(a));}
        public void Details(ActivityOccurrence a)
        {
            selected=a;detailsTitle.text=a.activity.title;detailsBody.text=a.TimeLabel+"\n"+DateKeyUtility.FromDateKey(a.dateKey).ToString("ddd, MMM d, yyyy")+"\n"+ActivityResolver.People(AppSession.Instance.Data,a.activity)+"\n"+RepeatInfo(a)+"\n\n"+a.activity.notes;
            detailsIcon.sprite=ActivityIconSet.Load().Get(a.activity.icon);detailsMessage.text="";detailScope.SetValueWithoutNotify(0);detailsPanel.SetActive(true);detailsPanel.transform.SetAsLastSibling();
        }
        static string RepeatInfo(ActivityOccurrence item)
        {
            var save=AppSession.Instance.Data;var a=save.activities.Find(x=>x.id==item.seriesId)??item.activity;
            string repeat="Does not repeat";
            if(a.recurrence!=ShiftCal.Data.RecurrenceKind.Once){string unit=a.recurrence==ShiftCal.Data.RecurrenceKind.Daily?"days":a.recurrence==ShiftCal.Data.RecurrenceKind.Weekly?"weeks":"months";repeat=a.interval==1?a.recurrence.ToString():"Every "+a.interval+" "+unit;if(a.recurrence==ShiftCal.Data.RecurrenceKind.Weekly)repeat+=" · "+string.Join(", ",a.weekdays.OrderBy(x=>x).Select(x=>System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames[x]));if(!string.IsNullOrEmpty(a.until))repeat+=" · until "+DateKeyUtility.FromDateKey(a.until).ToString("MMM d, yyyy");if(a.count>0)repeat+=" · "+a.count+" occurrences";}
            if(save.activityExceptions.Exists(x=>x.seriesId==item.seriesId&&x.originalDate==item.originalDate))repeat+=" · exception for this date";
            return repeat;
        }
        public void CloseDetails()=>detailsPanel.SetActive(false);
        public void Edit(){CloseDetails();editor.Edit(selected);}
        public void Claim()
        {
            var s=AppSession.Instance.Data;var person=s.profiles.Find(x=>x.active&&x.linkedUid==Uid&&!string.IsNullOrEmpty(Uid));
            if(person==null){detailsMessage.text="Ask the family owner to link your profile to your signed-in account in Family profiles.";return;}Assign(person.id);
        }
        public void Unassign()=>Assign(null);
        void Assign(string id)
        {
            var root=AppSession.Instance.Data.activities.Find(x=>x.id==selected.seriesId);var a=ActivityResolver.Clone(detailScope.value>0?root:selected.activity);a.assigneeId=id;a.dateKey=detailScope.value==2?AppSession.Instance.Data.activities.Find(x=>x.id==selected.seriesId).dateKey:selected.dateKey;
            a.updatedAt=DateKeyUtility.UnixMsNow();ActivityResolver.SaveEdit(AppSession.Instance.Data,a,selected.seriesId,selected.originalDate,detailScope.value);AppSession.Instance.SaveLocal();CloseDetails();
        }
        public void CancelOccurrence()=>AppNavigation.Instance.Confirm("Delete / cancel the selected activity scope?",()=>{ActivityResolver.Delete(AppSession.Instance.Data,selected.seriesId,selected.originalDate,detailScope.value);AppSession.Instance.SaveLocal();CloseDetails();});
        public void OpenDay(DateTime date){AppNavigation.Instance.ShowCalendar();calendar.OpenDate(date);}
        public static void Clear(Transform parent){foreach(Transform child in parent.Cast<Transform>().ToArray()){child.gameObject.SetActive(false);if(Application.isPlaying)Destroy(child.gameObject);else DestroyImmediate(child.gameObject);}}
        public static Text AddText(Transform parent,string value,bool heading=false)
        {
            var go=new GameObject("Agenda summary",typeof(RectTransform),typeof(Text),typeof(LayoutElement),typeof(ThemeManager));go.transform.SetParent(parent,false);go.transform.SetAsLastSibling();var t=go.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=heading?38:32;t.fontStyle=heading?FontStyle.Bold:FontStyle.Normal;t.text=value;t.raycastTarget=false;var theme=go.GetComponent<ThemeManager>();theme.role=heading?ThemeManager.Role.Text:ThemeManager.Role.Muted;theme.Paint();go.GetComponent<LayoutElement>().preferredHeight=heading?72:100;return t;
        }
    }
}
