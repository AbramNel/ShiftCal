using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using ShiftCal.App;
using ShiftCal.Core;
using ShiftCal.Data;
namespace ShiftCal.UI
{
    public class ActivityEditor : MonoBehaviour
    {
        public GameObject panel,iconPanel,repeatSection,weekdaySection,endingDateSection,countSection,timeSection,alarmSection,scopeSection;
        public InputField title,notes,interval,count,templateName;
        public PickerField date,time,endingDate;
        public Toggle timed,allDay,alarm,activeAlarm,sharedTemplate;
        public Toggle[] weekdays;
        public Transform participantContent;
        public GameObject participantPrefab;
        private List<Toggle> participants=new List<Toggle>();
        public Dropdown recurrence,ending,assignee,scope;
        public DurationChoice duration,reminder;
        public Image preview;
        public Text iconName,message,warning;
        public FamilyWorkbench workbench;
        public ActivityTemplateController templates;
        public GameObject deleteButton;
        CalendarActivity model;
        string seriesId,original,signature,templateEditingId;
        bool templateWasShared;
        List<string> profileIds=new List<string>();
        bool loading;
        ScheduleSave Save=>AppSession.Instance.Data;
        public CalendarActivity Draft=>Read();
        void Start()
        {
            date.changed.AddListener(_=>{if(seriesId==null)for(int i=0;i<7;i++)weekdays[i].isOn=i==(int)DateKeyUtility.FromDateKey(date.value).DayOfWeek;RefreshWarnings();});time.changed.AddListener(_=>RefreshWarnings());
            recurrence.onValueChanged.AddListener(_=>RefreshSections());ending.onValueChanged.AddListener(_=>RefreshSections());timed.onValueChanged.AddListener(_=>RefreshSections());allDay.onValueChanged.AddListener(_=>RefreshSections());
            assignee.onValueChanged.AddListener(_=>RefreshWarnings());scope.onValueChanged.AddListener(ChangeScope);duration.choice.onValueChanged.AddListener(_=>RefreshWarnings());
        }
        public void New(string key,CalendarActivity preset=null)
        {
            templateEditingId=null;seriesId=null;original=key;model=preset==null?new CalendarActivity{dateKey=key,groupId=Save.group.groupId}:ActivityResolver.Clone(preset);model.id=Guid.NewGuid().ToString("N");model.dateKey=key;Load();
        }
        public void Edit(ActivityOccurrence item)
        {
            templateEditingId=null;seriesId=item.seriesId;original=item.originalDate;model=ActivityResolver.Clone(item.activity);model.dateKey=item.dateKey;Load();
        }
        public void EditTemplate(ActivityTemplate t,bool shared)
        {
            New(DateKeyUtility.ToDateKey(DateTime.Today),t.activity);templateEditingId=t.id;templateWasShared=shared;templateName.text=t.name;sharedTemplate.isOn=shared;panel.GetComponentsInChildren<Text>(true).First(x=>x.name=="Save Event Label").text="Save template";signature=Signature;
        }
        void Load()
        {
            panel.GetComponentsInChildren<Text>(true).First(x=>x.name=="Save Event Label").text="Save activity";loading=true;title.text=model.title??"";notes.text=model.notes??"";date.Set(model.dateKey);time.Set(model.startTime??"");timed.isOn=!string.IsNullOrEmpty(model.startTime);allDay.isOn=model.allDay;
            duration.Set(model.durationMinutes);interval.text=Math.Max(1,model.interval).ToString();count.text=Math.Max(1,model.count).ToString();recurrence.SetValueWithoutNotify((int)model.recurrence);
            ending.SetValueWithoutNotify(!string.IsNullOrEmpty(model.until)?1:model.count>0?2:0);endingDate.Set(model.until??model.dateKey);
            for(int i=0;i<7;i++)weekdays[i].isOn=model.weekdays.Contains(i)||(seriesId==null&&model.weekdays.Count==0&&i==(int)DateKeyUtility.FromDateKey(model.dateKey).DayOfWeek);
            profileIds=Save.profiles.Select(x=>x.id).ToList();assignee.ClearOptions();assignee.AddOptions(new List<string>{"Unassigned"}.Concat(Save.profiles.Select(x=>x.name+(x.active?"":" (archived)"))).ToList());assignee.SetValueWithoutNotify(profileIds.IndexOf(model.assigneeId)+1);
            FamilyWorkbench.Clear(participantContent);participants.Clear();
            foreach(var profile in Save.profiles){var row=Instantiate(participantPrefab,participantContent);var toggle=row.GetComponentInChildren<Toggle>();row.GetComponentInChildren<Text>().text=profile.name+(profile.active?"":" (archived)");toggle.isOn=model.participants.Contains(profile.id);toggle.onValueChanged.AddListener(_=>RefreshWarnings());participants.Add(toggle);}
            scope.SetValueWithoutNotify(0);scopeSection.SetActive(seriesId!=null&&Save.activities.Find(x=>x.id==seriesId)?.recurrence!=RecurrenceKind.Once);deleteButton.SetActive(seriesId!=null);
            var sub=DeviceCalendarStore.Find("activity-"+(seriesId??model.id),original);alarm.isOn=sub?.alarm==true;activeAlarm.isOn=sub?.enabled??true;reminder.Set(sub?.reminder==true?sub.reminderMinutes:0);
            SelectIcon(model.icon);message.text="";templateName.text="";sharedTemplate.isOn=false;panel.SetActive(true);panel.transform.SetAsLastSibling();loading=false;RefreshSections();signature=Signature;
            var modal=panel.GetComponent<ModalPanel>();modal.HasChanges=()=>Signature!=signature;modal.Close=()=>panel.SetActive(false);
        }
        string Signature=>JsonUtility.ToJson(Read())+alarm.isOn+activeAlarm.isOn+SafeMinutes(reminder)+scope.value+templateName.text+sharedTemplate.isOn;
        static int SafeMinutes(DurationChoice d){try{return d.Minutes;}catch{return 0;}}
        CalendarActivity Read()
        {
            var a=ActivityResolver.Clone(model??new CalendarActivity());a.title=title.text.Trim();a.notes=notes.text;a.dateKey=date.value;a.allDay=allDay.isOn;a.startTime=timed.isOn&&!a.allDay?time.value:"";a.durationMinutes=SafeMinutes(duration);
            a.recurrence=(RecurrenceKind)recurrence.value;a.interval=int.TryParse(interval.text,out int n)?n:1;a.until=ending.value==1?endingDate.value:null;a.count=ending.value==2&&int.TryParse(count.text,out n)?n:0;
            a.weekdays=Enumerable.Range(0,7).Where(i=>weekdays[i].isOn).ToList();a.assigneeId=assignee.value>0&&assignee.value<=profileIds.Count?profileIds[assignee.value-1]:null;
            a.participants=Enumerable.Range(0,Math.Min(participants.Count,profileIds.Count)).Where(i=>participants[i].isOn&&profileIds[i]!=a.assigneeId).Select(i=>profileIds[i]).ToList();return a;
        }
        void ChangeScope(int value)
        {
            if(loading||seriesId==null)return;var root=Save.activities.Find(x=>x.id==seriesId);if(root==null)return;
            if(value>0){recurrence.SetValueWithoutNotify((int)root.recurrence);interval.text=root.interval.ToString();ending.SetValueWithoutNotify(!string.IsNullOrEmpty(root.until)?1:root.count>0?2:0);endingDate.Set(root.until??root.dateKey);count.text=Math.Max(1,root.count).ToString();for(int i=0;i<7;i++)weekdays[i].isOn=root.weekdays.Contains(i);}
            date.Set(value==2?root.dateKey:model.dateKey);RefreshSections();
        }
        public void SelectIcon(string id){if(model==null)return;model.icon=id;preview.sprite=ActivityIconSet.Load().Get(id);int i=Array.IndexOf(ActivityIconSet.Ids,id);iconName.text=ActivityIconSet.Names[i<0?19:i];iconPanel.SetActive(false);}
        public void Icons(){iconPanel.SetActive(true);iconPanel.transform.SetAsLastSibling();}
        public void CloseIcons()=>iconPanel.SetActive(false);
        public void Cancel()=>panel.GetComponent<ModalPanel>().RequestClose();
        public void Claim()
        {
            string uid=FamilyWorkbench.Uid;int i=Save.profiles.FindIndex(x=>x.active&&x.linkedUid==uid&&!string.IsNullOrEmpty(uid));
            if(i<0){message.text="Ask the family owner to link your profile to your signed-in account in Family profiles.";return;}assignee.value=i+1;
        }
        public void Unassign()=>assignee.value=0;
        public void RefreshSections()
        {
            repeatSection.SetActive(recurrence.value>0);weekdaySection.SetActive(recurrence.value==2);endingDateSection.SetActive(recurrence.value>0&&ending.value==1);countSection.SetActive(recurrence.value>0&&ending.value==2);
            timeSection.SetActive(timed.isOn&&!allDay.isOn);alarmSection.SetActive(timed.isOn&&!allDay.isOn);RefreshWarnings();
        }
        public void RefreshWarnings()
        {
            if(loading||model==null)return;var a=Read();var ids=new List<string>(a.participants){a.assigneeId};
            try{warning.text=FamilyAvailability.Check(Save,ids,DateKeyUtility.FromDateKey(a.dateKey),a.startTime,a.durationMinutes,seriesId,a.zone);}catch{warning.text="Choose a valid date and time.";}
        }
        public void Commit()
        {
            try{var ignored=duration.Minutes;ignored=reminder.Minutes;}catch(Exception ex){message.text=ex.Message;return;}
            var a=Read();if(timed.isOn&&!allDay.isOn&&string.IsNullOrEmpty(a.startTime)){message.text="Choose a time or turn off Timed.";return;}
            if(!ActivityResolver.Validate(a,out string error)){message.text=error;return;}
            if(templateEditingId!=null){if(string.IsNullOrWhiteSpace(templateName.text)){message.text="Name the template.";return;}templates.UpdateDraft(templateEditingId,templateWasShared,new ActivityTemplate{id=templateEditingId,name=templateName.text.Trim(),activity=a},sharedTemplate.isOn);panel.SetActive(false);return;}
            a.groupId=Save.group.groupId;a.creatorUid=model.creatorUid??Save.account;a.createdAt=model.createdAt>0?model.createdAt:DateKeyUtility.UnixMsNow();a.updatedAt=DateKeyUtility.UnixMsNow();
            ActivityResolver.SaveEdit(Save,a,seriesId,original,scope.value);
            if(seriesId!=null&&scope.value==1&&a.id!=seriesId)DeviceCalendarStore.SplitSubscriptions("activity-"+seriesId,"activity-"+a.id,original);
            string source="activity-"+(scope.value==0&&seriesId!=null?seriesId:a.id);string occurrence=seriesId!=null&&scope.value==0&&Save.activities.Find(x=>x.id==seriesId)?.recurrence!=RecurrenceKind.Once?original:null;
            DeviceCalendarStore.Subscribe(new LocalAlarmSubscription{sourceId=source,originalDate=occurrence,alarm=alarm.isOn&&timed.isOn&&!allDay.isOn,enabled=activeAlarm.isOn,reminder=SafeMinutes(reminder)>0&&timed.isOn&&!allDay.isOn,reminderMinutes=SafeMinutes(reminder),useDefaultAlarmSettings=true});
            AppSession.Instance.SaveLocal();panel.SetActive(false);workbench.Refresh();
        }
        public void Delete()=>AppNavigation.Instance.Confirm("Delete this activity in the selected scope?",()=>{ActivityResolver.Delete(Save,seriesId,original,scope.value);AppSession.Instance.SaveLocal();panel.SetActive(false);workbench.Refresh();});
        public void SaveTemplate()
        {
            if(string.IsNullOrWhiteSpace(templateName.text)){message.text="Name the template.";return;}
            var a=Read();if(string.IsNullOrWhiteSpace(a.title)){message.text="Add an activity title first.";return;}
            templates.SaveDraft(new ActivityTemplate{name=templateName.text.Trim(),activity=a},sharedTemplate.isOn);message.text="Template saved "+(sharedTemplate.isOn?"for this calendar.":"on this device.");
        }
    }
}
