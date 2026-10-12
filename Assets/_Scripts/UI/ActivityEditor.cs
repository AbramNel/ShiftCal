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
        public Toggle alarm,activeAlarm,sharedTemplate;
        public Toggle[] weekdays;
        public SelectionField recurrence,ending,assignee,scope,timing,participants;
        public DurationChoice duration,reminder;
        public Image preview;
        public Text iconName,message,warning,repeatPreview,intervalLabel,heading,saveLabel;
        public FamilyWorkbench workbench;
        public ActivityTemplateController templates;
        public GameObject deleteButton;
        public Button profileRoute;
        public ExpandableSection alarmExpansion,endingExpansion,templateExpansion;
        CalendarActivity model;
        LocalAlarmSubscription originalAlarm;
        string seriesId,original,signature,templateEditingId;
        bool templateWasShared,loading,hooked;
        int initialTiming,previousAssignment;
        string draftAccount;
        ScheduleSave Save=>AppSession.Instance.Data;
        public CalendarActivity Draft=>Read();
        public bool HasChanges=>Signature!=signature;
        void Start()=>Hook();
        void Hook()
        {
            if(hooked)return;hooked=true;
            date.changed.AddListener(_=>{if(seriesId==null)for(int i=0;i<7;i++)weekdays[i].isOn=i==(int)DateKeyUtility.FromDateKey(date.value).DayOfWeek;RefreshSections();});time.changed.AddListener(_=>RefreshWarnings());
            recurrence.onValueChanged.AddListener(_=>RefreshSections());ending.onValueChanged.AddListener(_=>RefreshSections());timing.onValueChanged.AddListener(_=>RefreshSections());
            assignee.onValueChanged.AddListener(_=>AssignmentChanged());participants.onSelectionChanged.AddListener(RefreshWarnings);scope.onValueChanged.AddListener(ChangeScope);
            duration.choice.onValueChanged.AddListener(_=>RefreshWarnings());duration.custom.onValueChanged.AddListener(_=>RefreshWarnings());
            interval.onValueChanged.AddListener(_=>RefreshSections());count.onValueChanged.AddListener(_=>RefreshSections());endingDate.changed.AddListener(_=>RefreshSections());
            foreach(var chip in weekdays)chip.onValueChanged.AddListener(_=>RefreshSections());
        }
        public void New(string key,CalendarActivity preset=null)
        {
            templateEditingId=null;seriesId=null;original=key;model=preset==null?new CalendarActivity{dateKey=key,groupId=Save.group.groupId,zone="device"}:ActivityResolver.Clone(preset);model.id=Guid.NewGuid().ToString("N");model.dateKey=key;Load();
        }
        public void Edit(ActivityOccurrence item){templateEditingId=null;seriesId=item.seriesId;original=item.originalDate;model=ActivityResolver.Clone(item.activity);model.dateKey=item.dateKey;Load();}
        public void EditTemplate(ActivityTemplate t,bool shared)
        {
            New(DateKeyUtility.ToDateKey(DateTime.Today),t.activity);templateEditingId=t.id;templateWasShared=shared;templateName.text=t.name;sharedTemplate.isOn=shared;templateExpansion.content.SetActive(true);heading.text="Edit template";saveLabel.text="Save template";signature=Signature;
        }
        void Load()
        {
            Hook();draftAccount=Save.account;loading=true;heading.text=seriesId==null?"New Activity":"Edit Activity";saveLabel.text="Save Activity";title.text=model.title??"";notes.text=model.notes??"";notes.GetComponent<LayoutElement>().preferredHeight=160;
            date.Set(model.dateKey);time.Set(model.startTime??"");initialTiming=model.allDay?2:string.IsNullOrEmpty(model.startTime)?0:1;timing.SetValueWithoutNotify(initialTiming);
            duration.Set(model.durationMinutes);interval.text=Math.Max(1,model.interval).ToString();count.text=Math.Max(1,model.count).ToString();recurrence.SetValueWithoutNotify((int)model.recurrence);
            ending.SetValueWithoutNotify(!string.IsNullOrEmpty(model.until)?1:model.count>0?2:0);endingDate.Set(string.IsNullOrEmpty(model.until)?model.dateKey:model.until);
            for(int i=0;i<7;i++)weekdays[i].isOn=model.weekdays.Contains(i)||(seriesId==null&&model.weekdays.Count==0&&i==(int)DateKeyUtility.FromDateKey(model.dateKey).DayOfWeek);
            assignee.SetOptions(FamilySelections.People(Save,true,new[]{model.assigneeId}));assignee.SetValueWithoutNotify(Math.Max(0,assignee.options.FindIndex(x=>x.id==model.assigneeId)));previousAssignment=assignee.value;
            participants.SetOptions(FamilySelections.People(Save,false,model.participants));participants.SetSelected(model.participants.Select(id=>participants.options.FindIndex(x=>x.id==id)));
            scope.SetValueWithoutNotify(0);scopeSection.SetActive(seriesId!=null&&Save.activities.Find(x=>x.id==seriesId)?.recurrence!=RecurrenceKind.Once);deleteButton.SetActive(seriesId!=null);
            originalAlarm=DeviceCalendarStore.Find("activity-"+(seriesId??model.id),original);alarm.isOn=originalAlarm?.alarm==true;activeAlarm.isOn=originalAlarm?.enabled??true;reminder.Set(originalAlarm?.reminder==true?originalAlarm.reminderMinutes:0);
            SelectIcon(model.icon);message.text="";templateName.text="";sharedTemplate.isOn=false;alarmExpansion.content.SetActive(false);endingExpansion.content.SetActive(false);templateExpansion.content.SetActive(false);
            profileRoute.gameObject.SetActive(false);
            panel.SetActive(true);panel.transform.SetAsLastSibling();loading=false;RefreshSections();signature=Signature;
            var modal=panel.GetComponent<ModalPanel>();modal.HasChanges=()=>HasChanges;modal.Close=()=>panel.SetActive(false);
        }
        string Signature=>JsonUtility.ToJson(Read())+alarm.isOn+activeAlarm.isOn+SafeMinutes(reminder)+scope.value+templateName.text+sharedTemplate.isOn;
        static int SafeMinutes(DurationChoice d){try{return d.Minutes;}catch{return -1;}}
        CalendarActivity Read()
        {
            var a=ActivityResolver.Clone(model??new CalendarActivity());a.title=title.text.Trim();a.notes=notes.text;a.dateKey=date.value;
            a.allDay=timing.value==2;
            if(timing.value==1){a.startTime=time.value;a.durationMinutes=SafeMinutes(duration);}
            else if(timing.value!=initialTiming){a.startTime="";a.durationMinutes=0;}
            a.recurrence=(RecurrenceKind)recurrence.value;a.interval=int.TryParse(interval.text,out int n)?n:0;
            if(a.recurrence!=RecurrenceKind.Once){a.until=ending.value==1?endingDate.value:null;a.count=ending.value==2&&int.TryParse(count.text,out n)?n:0;}
            a.weekdays=Enumerable.Range(0,7).Where(i=>weekdays[i].isOn).ToList();a.assigneeId=assignee.options.Count>0?FamilySelections.Assignment(assignee,Save):null;
            a.participants=participants.selectedValues.Where(i=>i<participants.options.Count).Select(i=>participants.options[i].id).Where(id=>id!=a.assigneeId).ToList();return a;
        }
        void AssignmentChanged()
        {
            if(loading)return;
            if(assignee.options[assignee.value].id=="@me"&&FamilySelections.Me(Save)==null){FamilySelections.NeedsProfile(message);profileRoute.gameObject.SetActive(true);assignee.SetValueWithoutNotify(previousAssignment);return;}
            previousAssignment=assignee.value;message.text="";RefreshWarnings();
        }
        void ChangeScope(int value)
        {
            if(loading||seriesId==null)return;var root=Save.activities.Find(x=>x.id==seriesId);if(root==null)return;
            loading=true;
            if(value>0){recurrence.SetValueWithoutNotify((int)root.recurrence);interval.text=root.interval.ToString();ending.SetValueWithoutNotify(!string.IsNullOrEmpty(root.until)?1:root.count>0?2:0);endingDate.Set(string.IsNullOrEmpty(root.until)?root.dateKey:root.until);count.text=Math.Max(1,root.count).ToString();for(int i=0;i<7;i++)weekdays[i].isOn=root.weekdays.Contains(i);}
            date.Set(value==2?root.dateKey:model.dateKey);loading=false;RefreshSections();
        }
        public void SelectIcon(string id){if(model==null)return;model.icon=id;preview.sprite=ActivityIconSet.Load().Get(id);iconName.text=ActivityIconSet.Name(id);iconPanel.SetActive(false);}
        public void Icons(){iconPanel.SetActive(true);iconPanel.transform.SetAsLastSibling();}
        public void CloseIcons()=>iconPanel.SetActive(false);
        public void Cancel()=>panel.GetComponent<ModalPanel>().RequestClose();
        public void Claim(){int index=assignee.options.FindIndex(x=>x.id=="@me");if(index>=0){assignee.SetValueWithoutNotify(index);AssignmentChanged();}}
        public void Unassign()=>assignee.value=0;
        public void Profiles()=>FindFirstObjectByType<FamilyProfileController>(FindObjectsInactive.Include).OpenFromEditor(this);
        public void RestoreAfterProfiles()
        {
            if(draftAccount!=Save.account)return;
            var draft=Read();loading=true;assignee.SetOptions(FamilySelections.People(Save,true,new[]{draft.assigneeId}));assignee.SetValueWithoutNotify(Math.Max(0,assignee.options.FindIndex(p=>p.id==draft.assigneeId)));previousAssignment=assignee.value;
            participants.SetOptions(FamilySelections.People(Save,false,draft.participants));participants.SetSelected(draft.participants.Select(id=>participants.options.FindIndex(p=>p.id==id)));loading=false;panel.SetActive(true);panel.transform.SetAsLastSibling();RefreshSections();
        }
        public void ExpandNotes()=>notes.GetComponent<LayoutElement>().preferredHeight=notes.GetComponent<LayoutElement>().preferredHeight>200?160:480;
        public void ChooseTemplate()=>templates.PickForDate(date.value);
        public void RefreshSections()
        {
            if(loading)return;
            repeatSection.SetActive(recurrence.value>0);weekdaySection.SetActive(recurrence.value==2);endingExpansion.gameObject.SetActive(recurrence.value>0);
            endingDateSection.SetActive(recurrence.value>0&&ending.value==1);countSection.SetActive(recurrence.value>0&&ending.value==2);
            intervalLabel.text="Repeat every "+(recurrence.value==1?"day(s)":recurrence.value==2?"week(s)":"month(s)");
            timeSection.SetActive(timing.value==1);alarmSection.SetActive(timing.value==1);repeatPreview.gameObject.SetActive(recurrence.value>0);
            if(model!=null)repeatPreview.text=ending.value==2&&Read().count<1?"Choose at least one occurrence.":ActivityRecurrencePreview.Describe(Read());RefreshWarnings();
        }
        public void RefreshWarnings()
        {
            if(loading||model==null)return;var a=Read();warning.text="";
            var ids=a.participants.Concat(new[]{a.assigneeId}).Where(id=>!string.IsNullOrEmpty(id)).ToList();
            if(timing.value!=1||string.IsNullOrEmpty(a.startTime)||a.durationMinutes<=0||ids.Count==0){warning.gameObject.SetActive(false);return;}
            try{string status=FamilyAvailability.Check(Save,ids,DateKeyUtility.FromDateKey(a.dateKey),a.startTime,a.durationMinutes,seriesId,a.zone);if(status.StartsWith("Busy"))warning.text="Possible conflict with "+status.Substring(7)+".";}
            catch{warning.text="Choose a valid date, time and duration to check overlaps.";}warning.gameObject.SetActive(warning.text.Length>0);
        }
        public void Commit()
        {
            try{if(timing.value==1){var ignored=duration.Minutes;ignored=reminder.Minutes;}}catch(Exception ex){message.text=ex.Message;return;}
            var a=Read();if(timing.value==1&&string.IsNullOrEmpty(a.startTime)){message.text="Choose a time, or choose No time.";return;}
            if(a.recurrence!=RecurrenceKind.Once&&ending.value==2&&a.count<1){message.text="Choose at least one occurrence.";return;}
            if(!ActivityResolver.Validate(a,out string error)){message.text=error;return;}
            if(templateEditingId!=null){if(string.IsNullOrWhiteSpace(templateName.text)){message.text="Name the template.";return;}templates.UpdateDraft(templateEditingId,templateWasShared,new ActivityTemplate{id=templateEditingId,name=templateName.text.Trim(),activity=a},sharedTemplate.isOn);panel.SetActive(false);return;}
            a.groupId=Save.group.groupId;a.creatorUid=model.creatorUid??Save.account;a.createdAt=model.createdAt>0?model.createdAt:DateKeyUtility.UnixMsNow();a.updatedAt=DateKeyUtility.UnixMsNow();
            ActivityResolver.SaveEdit(Save,a,seriesId,original,scope.value);
            if(seriesId!=null&&scope.value==1&&a.id!=seriesId)DeviceCalendarStore.SplitSubscriptions("activity-"+seriesId,"activity-"+a.id,original);
            string source="activity-"+(scope.value==0&&seriesId!=null?seriesId:a.id);string occurrence=seriesId!=null&&scope.value==0&&Save.activities.Find(x=>x.id==seriesId)?.recurrence!=RecurrenceKind.Once?original:null;
            if(timing.value==1||timing.value!=initialTiming&&originalAlarm!=null)
            {
                var sub=originalAlarm==null?new LocalAlarmSubscription():ActivityResolver.Clone(originalAlarm);sub.sourceId=source;sub.originalDate=occurrence;
                sub.alarm=alarm.isOn&&timing.value==1;sub.enabled=activeAlarm.isOn;sub.reminder=SafeMinutes(reminder)>0&&timing.value==1;sub.reminderMinutes=Math.Max(0,SafeMinutes(reminder));DeviceCalendarStore.Subscribe(sub);
            }
            AppSession.Instance.SaveLocal();if(!string.IsNullOrEmpty(AppSession.Instance.Error)){message.text=AppSession.Instance.Error;return;}panel.SetActive(false);workbench.Refresh();
        }
        public void Delete()=>AppNavigation.Instance.Confirm("Delete this activity in the selected scope?",()=>{ActivityResolver.Delete(Save,seriesId,original,scope.value);AppSession.Instance.SaveLocal();panel.SetActive(false);workbench.Refresh();});
        public void SaveTemplate()
        {
            if(string.IsNullOrWhiteSpace(templateName.text)){message.text="Name the template.";return;}var a=Read();if(!ActivityResolver.Validate(a,out var error)){message.text=error;return;}
            templates.SaveDraft(new ActivityTemplate{name=templateName.text.Trim(),activity=a},sharedTemplate.isOn);message.text="Template saved "+(sharedTemplate.isOn?"for this calendar.":"on this device.");
        }
    }
}
