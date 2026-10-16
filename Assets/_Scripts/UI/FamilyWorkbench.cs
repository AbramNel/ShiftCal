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
        public AgendaDayCard dayCardPrefab;
        public Sprite alarmIcon,eventIcon;
        string agendaSignature;
        float nextAgendaCheck;
        public ActivityEditor editor;
        public Text detailsTitle,detailsBody,detailsMessage;
        public Image detailsIcon;
        public SelectionField filter,agendaFilter,detailScope;
        public SelectionField assigned,participants;
        public Toggle showAllDates;
        public Button undo;
        public CalendarController calendar;
        ActivityOccurrence selected;
        ActivityAssignmentChange lastChange;
        bool binding;
        bool pendingDelete;
        string pendingAssignment;
        List<string> pendingParticipants;
        List<string> filterIds=new List<string>();
        float nextUndoCheck;
        public static string Filter=>DeviceCalendarStore.State?.filter??"everyone";
        public static string Uid=>Firebase.AuthService.Instance?.IsSignedIn==true?Firebase.AuthService.Instance.UserId:AppSession.Instance?.Data?.account!="local"?AppSession.Instance?.Data?.account:null;
        bool hooked;
        void Start(){Hook();AppSession.Instance.Changed+=Refresh;ThemeManager.Changed+=Refresh;Refresh();}
        void Hook(){if(hooked)return;hooked=true;filter.onValueChanged.AddListener(SetFilter);agendaFilter.onValueChanged.AddListener(SetFilter);showAllDates.onValueChanged.AddListener(_=>Refresh());assigned.onValueChanged.AddListener(_=>AssignmentSelected());participants.onSelectionChanged.AddListener(ParticipantsSelected);detailScope.onValueChanged.AddListener(ApplyAssignment);detailScope.onCanceled.AddListener(BindDetailsPeople);}
        void Update(){if(agendaPanel.activeInHierarchy&&Time.unscaledTime>=nextAgendaCheck){nextAgendaCheck=Time.unscaledTime+10;Refresh();}if(undo==null||!detailsPanel.activeSelf||Time.unscaledTime<nextUndoCheck)return;nextUndoCheck=Time.unscaledTime+.5f;undo.gameObject.SetActive(lastChange?.CanUndo(AppSession.Instance.Data)==true);}
        void OnDestroy(){if(AppSession.Instance!=null)AppSession.Instance.Changed-=Refresh;ThemeManager.Changed-=Refresh;}
        public void NewAgendaEvent()=>editor.New(DateKeyUtility.ToDateKey(DateTime.Today));
        public void ToggleDates()=>showAllDates.isOn=!showAllDates.isOn;
        public void NewActivity()=>BeginNew(DateKeyUtility.ToDateKey(DateTime.Today));
        public void BeginNew(string key)=>editor.templates.StartCreation(key);
        public void ShowAgenda(){AppNavigation.Instance.Open(agendaPanel);Refresh();}
        public void Refresh()
        {
            if(AppSession.Instance?.Data==null)return;
            Hook();var s=AppSession.Instance.Data;filterIds=new List<string>{"everyone","mine","unassigned"};filterIds.AddRange(s.profiles.Where(x=>x.active).Select(x=>x.id));
            if(!filterIds.Contains(Filter)&&DeviceCalendarStore.State!=null){DeviceCalendarStore.State.filter="everyone";DeviceCalendarStore.Write();calendar.Refresh();}
            var filters=new List<SelectionItem>{new SelectionItem("Everyone","everyone"),new SelectionItem("Mine","mine"),new SelectionItem("Unassigned","unassigned")};filters.AddRange(FamilySelections.People(s));filter.SetOptions(filters);filter.SetValueWithoutNotify(Math.Max(0,filterIds.IndexOf(Filter)));agendaFilter.SetOptions(filters);agendaFilter.SetValueWithoutNotify(Math.Max(0,filterIds.IndexOf(Filter)));
            if(!agendaPanel.activeSelf)return;
            string signature=JsonUtility.ToJson(s)+JsonUtility.ToJson(DeviceCalendarStore.State)+Filter+showAllDates.isOn+DateTime.Now.ToString("yyyy-MM-dd HH:mm")+AndroidBridge.Call<string>("deliveryStates");
            if(agendaSignature==signature&&agendaContent.childCount>0)return;agendaSignature=signature;Clear(agendaContent);
            var information=DayInformation.Resolve(s,DateTime.Today,DateTime.Today.AddDays(6));
            var occurrences=information.Values.SelectMany(d=>d.activities).ToList();var alarms=information.Values.SelectMany(d=>d.alarms).ToList();
            int visible=0;for(int i=0;i<7;i++)
            {
                var date=DateTime.Today.AddDays(i);string key=DateKeyUtility.ToDateKey(date);
                var day=CalendarGenerator.Generate(new DateTime(date.Year,date.Month,1),s.group,AppSession.Instance.CalendarOverrides).Find(x=>x.dateKey==key);
                var dayActivities=occurrences.Where(x=>x.dateKey==key).ToList();var dayEvents=s.events.SelectMany(e=>RecurrenceEngine.EventDates(s,e,date,date)).ToList();
                var dayAlarms=alarms.Where(x=>(x.sourceId==null||!x.sourceId.StartsWith("activity-")||occurrences.Any(a=>"activity-"+a.seriesId==x.sourceId&&a.originalDate==x.dateKey))&&x.calendarDateKey==key&&!x.id.EndsWith(":view")&&x.state!="view"&&x.at>DateKeyUtility.UnixMsNow()&&(x.isEvent||x.audible)).ToList();
                bool working=day.ResolvedShift!=(int)ShiftTypeId.Off;if(!showAllDates.isOn&&dayActivities.Count+dayEvents.Count+dayAlarms.Count==0&&!working)continue;visible++;
                var card=Instantiate(dayCardPrefab,agendaContent);card.date.text=date.ToString("ddd, MMM d");card.shift.text=day.shiftName+(!string.IsNullOrEmpty(day.startTime)?"\n"+day.startTime+" – "+day.endTime:"");card.swatch.color=ShiftStyleUtility.ToColor(day.shiftColorHex);card.button.onClick.AddListener(()=>OpenDay(date));
                foreach(var item in dayActivities)AddRow(card.entries,item);
                foreach(var item in dayEvents){var captured=item;var row=Instantiate(rowPrefab,card.entries);row.SourceId=item.sourceId;row.ThemedBorder=true;row.BindInfo(eventIcon,item.title,DateTimeOffset.FromUnixTimeMilliseconds(item.at).ToLocalTime().ToString("t")+" · Shared event",ThemeManager.Token(ThemeManager.Role.Muted),()=>FindFirstObjectByType<ScheduleWorkbench>(FindObjectsInactive.Include).EditEvent(captured.sourceId,captured.dateKey));}
                foreach(var alarm in dayAlarms){var captured=alarm;var row=Instantiate(rowPrefab,card.entries);row.SourceId=alarm.sourceId;row.IsAlarm=true;row.ThemedBorder=true;row.BindInfo(alarmIcon,alarm.title,DateTimeOffset.FromUnixTimeMilliseconds(alarm.at).ToLocalTime().ToString("t")+" · This device",ThemeManager.Token(ThemeManager.Role.Accent),()=>OpenAlarm(captured));}

            }
            if(visible==0)AddText(agendaContent,"Nothing upcoming in this filter. Turn on Show all dates to browse the week.");
        }
        void OpenAlarm(Occurrence alarm)
        {
            if(alarm.sourceId!=null&&alarm.sourceId.StartsWith("activity-")){
                var date=DateKeyUtility.FromDateKey(alarm.calendarDateKey??alarm.dateKey);var item=ActivityResolver.Resolve(AppSession.Instance.Data,date,date).Find(a=>"activity-"+a.seriesId==alarm.sourceId&&a.originalDate==alarm.dateKey);if(item!=null)Details(item);return;
            }
            var work=FindFirstObjectByType<ScheduleWorkbench>(FindObjectsInactive.Include);var rule=AppSession.Instance.Data.rules.Find(r=>r.id==alarm.sourceId);if(rule!=null)work.LoadRule(rule,alarm.dateKey);else work.EditEvent(alarm.sourceId,alarm.dateKey);
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
            Hook();
            selected=a;detailsTitle.text=a.activity.title;detailsBody.text=a.TimeLabel+"\n"+DateKeyUtility.FromDateKey(a.dateKey).ToString("ddd, MMM d, yyyy")+"\n"+ActivityResolver.People(AppSession.Instance.Data,a.activity)+"\n"+RepeatInfo(a)+"\n\n"+a.activity.notes;
            detailsIcon.sprite=ActivityIconSet.Load().Get(a.activity.icon);detailsMessage.text="";detailScope.SetValueWithoutNotify(0);detailsPanel.SetActive(true);detailsPanel.transform.SetAsLastSibling();
            BindDetailsPeople();
        }
        static string RepeatInfo(ActivityOccurrence item)
        {
            var save=AppSession.Instance.Data;var a=save.activities.Find(x=>x.id==item.seriesId)??item.activity;
            string repeat="Does not repeat";
            if(a.recurrence!=ShiftCal.Data.RecurrenceKind.Once){string unit=a.recurrence==ShiftCal.Data.RecurrenceKind.Daily?"days":a.recurrence==ShiftCal.Data.RecurrenceKind.Weekly?"weeks":"months";repeat=a.interval==1?a.recurrence.ToString():"Every "+a.interval+" "+unit;if(a.recurrence==ShiftCal.Data.RecurrenceKind.Weekly)repeat+=" · "+string.Join(", ",a.weekdays.OrderBy(x=>x).Select(x=>System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames[x]));if(!string.IsNullOrEmpty(a.until))repeat+=" · until "+DateKeyUtility.FromDateKey(a.until).ToString("MMM d, yyyy");if(a.count>0)repeat+=" · "+a.count+" occurrences";}
            if(a.hasShiftConstraint)repeat+=" · when shift is "+(save.group.shiftTypes.Find(x=>x.id==a.requiredShiftType)?.name??"Missing shift");
            if(save.activityExceptions.Exists(x=>x.seriesId==item.seriesId&&x.originalDate==item.originalDate))repeat+=" · exception for this date";
            return repeat;
        }
        public void CloseDetails()=>detailsPanel.SetActive(false);
        public void Edit(){CloseDetails();editor.Edit(selected);}
        public void Claim()
        {
            var id=FamilySelections.Me(AppSession.Instance.Data);if(id==null){FamilySelections.NeedsProfile(detailsMessage);return;}BeginAssignment(id,null);
        }
        public void Unassign()=>BeginAssignment(null,null);
        void BindDetailsPeople()
        {
            if(selected==null)return;binding=true;assigned.SetOptions(FamilySelections.People(AppSession.Instance.Data,true,new[]{selected.activity.assigneeId}));assigned.SetValueWithoutNotify(Math.Max(0,assigned.options.FindIndex(i=>i.id==selected.activity.assigneeId)));
            participants.SetOptions(FamilySelections.People(AppSession.Instance.Data,false,selected.activity.participants));participants.SetSelected(selected.activity.participants.Select(id=>participants.options.FindIndex(p=>p.id==id)));binding=false;
        }
        void AssignmentSelected(){if(binding)return;var id=FamilySelections.Assignment(assigned,AppSession.Instance.Data);if(assigned.options[assigned.value].id=="@me"&&id==null){FamilySelections.NeedsProfile(detailsMessage);BindDetailsPeople();return;}BeginAssignment(id,null);}
        void ParticipantsSelected(){if(!binding)BeginAssignment(null,participants.selectedValues.Select(i=>participants.options[i].id).ToList());}
        void BeginAssignment(string id,List<string> people){pendingDelete=false;pendingAssignment=id;pendingParticipants=people;detailScope.heading="Apply responsibility change";if(AppSession.Instance.Data.activities.Find(a=>a.id==selected.seriesId)?.recurrence!=RecurrenceKind.Once)detailScope.Open();else ApplyAssignment(0);}
        void ApplyAssignment(int scope)
        {
            if(pendingDelete){pendingDelete=false;ConfirmDelete(scope);return;}
            try{lastChange=ActivityAssignmentChange.Apply(selected,scope,pendingAssignment,pendingParticipants);var date=DateKeyUtility.FromDateKey(selected.dateKey);var updated=ActivityResolver.Resolve(AppSession.Instance.Data,date,date).Find(a=>a.originalDate==selected.originalDate&&a.seriesId==lastChange.ResultId);if(updated!=null)Details(updated);detailsMessage.text=pendingParticipants==null?selected.activity.title+" assigned. Saved on this device.":"Participants saved on this device.";Refresh();}
            catch(Exception ex){detailsMessage.text=ex.Message;BindDetailsPeople();}
        }
        public void UndoAssignment(){if(lastChange?.Undo()!=true){detailsMessage.text="This record has changed; reopen it to review the current assignment.";return;}var date=DateKeyUtility.FromDateKey(selected.dateKey);var restored=ActivityResolver.Resolve(AppSession.Instance.Data,date,date).FirstOrDefault(a=>a.seriesId==lastChange.OriginalSeriesId&&a.originalDate==selected.originalDate);if(restored!=null)Details(restored);detailsMessage.text="Assignment restored on this device.";Refresh();}
        public void Profiles()=>FindFirstObjectByType<FamilyProfileController>(FindObjectsInactive.Include).Open();
        public void CancelOccurrence(){if(!Firebase.FirestoreService.Instance.CanManageProfiles){detailsMessage.text="Ask the calendar owner to remove shared events.";return;}pendingDelete=true;detailScope.heading="Delete Activity";if(AppSession.Instance.Data.activities.Find(a=>a.id==selected.seriesId)?.recurrence!=RecurrenceKind.Once)detailScope.Open();else{pendingDelete=false;ConfirmDelete(0);}}
        void ConfirmDelete(int scope)=>AppNavigation.Instance.Confirm("Delete / cancel the selected activity scope?",()=>{if(!Firebase.FirestoreService.Instance.CanManageProfiles)return;ActivityResolver.Delete(AppSession.Instance.Data,selected.seriesId,selected.originalDate,scope);AppSession.Instance.SaveLocal();CloseDetails();});
        public void OpenDay(DateTime date){AppNavigation.Instance.ShowCalendar();calendar.OpenDate(date);}
        public static void Clear(Transform parent){foreach(Transform child in parent.Cast<Transform>().ToArray()){child.gameObject.SetActive(false);if(Application.isPlaying)Destroy(child.gameObject);else DestroyImmediate(child.gameObject);}}
        public static Text AddText(Transform parent,string value,bool heading=false)
        {
            var go=new GameObject("Agenda summary",typeof(RectTransform),typeof(Text),typeof(LayoutElement),typeof(ThemeManager));go.transform.SetParent(parent,false);go.transform.SetAsLastSibling();var t=go.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=heading?38:32;t.fontStyle=heading?FontStyle.Bold:FontStyle.Normal;t.text=value;t.raycastTarget=false;var theme=go.GetComponent<ThemeManager>();theme.role=heading?ThemeManager.Role.Text:ThemeManager.Role.Muted;theme.Paint();go.GetComponent<LayoutElement>().preferredHeight=heading?72:100;return t;
        }
    }
}
