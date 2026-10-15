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
    public class ActivityTemplateController : MonoBehaviour
    {
        public GameObject panel;
        public Sprite alarmIcon,newEventIcon,templateIcon;
        public SelectionField selection;
        public SelectionField quickStart,picker;
        public InputField name;
        public Text summary,message;
        public Toggle shared;
        public ActivityEditor editor;
        List<ActivityTemplate> items=new List<ActivityTemplate>();
        string selectedDate;
        readonly List<ActivityTemplate> recent=new List<ActivityTemplate>();
        public void Open(){selectedDate=null;AppNavigation.Instance.Open(panel);Refresh();}
        bool hooked;
        void Start()=>Hook();
        void Hook(){if(hooked)return;hooked=true;selection.onValueChanged.AddListener(_=>Select());quickStart.onValueChanged.AddListener(QuickChoice);picker.onValueChanged.AddListener(UsePicked);}
        void Refresh()
        {
            items=new List<ActivityTemplate>(AppSession.Instance.Data.templates);if(DeviceCalendarStore.State!=null)items.AddRange(DeviceCalendarStore.State.templates);
            selection.ClearOptions();selection.AddOptions(items.Count>0?items.Select(x=>x.name+(AppSession.Instance.Data.templates.Contains(x)?" · shared":" · this device")).ToList():new List<string>{"No templates yet"});Select();
        }
        ActivityTemplate Selected=>selection.value<items.Count?items[selection.value]:null;
        void Select(){var t=Selected;name.text=t?.name??"";shared.isOn=t!=null&&AppSession.Instance.Data.templates.Contains(t);summary.text=t==null?"Create an event, then use Save as template. Templates never include alarms.":t.activity.title+"\n"+ActivityIconSet.Name(t.activity.icon)+" · "+t.activity.startTime+" · "+t.activity.durationMinutes+" min\n"+ActivityResolver.People(AppSession.Instance.Data,t.activity)+"\n"+t.activity.notes;message.text="";}
        public void Use(){if(Selected!=null)UseTemplate(Selected,selectedDate??DateKeyUtility.ToDateKey(DateTime.Today));}
        public void StartCreation(string key)
        {
            Hook();selectedDate=key;recent.Clear();Refresh();
            var options=new List<SelectionItem>{new SelectionItem("New Event","new","Shared calendar event"){icon=newEventIcon},new SelectionItem("Add Alarm","alarm","Private · only on this device"){icon=alarmIcon},new SelectionItem("Saved Templates","templates","Choose from your device or this calendar"){icon=templateIcon}};
            foreach(var id in DeviceCalendarStore.State?.recentTemplates??new List<string>()){var t=items.Find(x=>x.id==id);if(t==null)continue;recent.Add(t);options.Add(new SelectionItem(t.name,t.id,"Recently used"){icon=ActivityIconSet.Load().Get(t.activity.icon)});if(recent.Count>=3)break;}
            quickStart.SetOptions(options);quickStart.Open();
        }
        void QuickChoice(int index)
        {
            if(index==2){PickForDate(selectedDate);return;}if(index>=3){UseTemplate(recent[index-3],selectedDate);return;}
            if(index==1){FindFirstObjectByType<ScheduleWorkbench>().NewPersonalAlarm(selectedDate);return;}
            editor.New(selectedDate);
        }
        public void PickForDate(string key)
        {
            Hook();selectedDate=key;Refresh();picker.SetOptions(items.Select(t=>new SelectionItem(t.name,t.id,AppSession.Instance.Data.templates.Contains(t)?"Shared calendar template":"On this device"){icon=ActivityIconSet.Load().Get(t.activity.icon)}));
            if(items.Count==0){if(!editor.panel.activeSelf)editor.New(key);editor.message.text="No saved templates yet. Save an event as a template.";return;}picker.Open();
        }
        void UsePicked(int index)
        {
            if(index>=items.Count)return;var t=items[index];var key=selectedDate;
            if(editor.panel.activeSelf&&editor.HasChanges)AppNavigation.Instance.Confirm("Replace this unsaved draft with the template?",()=>UseTemplate(t,key));else UseTemplate(t,key);
        }
        void UseTemplate(ActivityTemplate t,string key)
        {
            if(DeviceCalendarStore.State!=null){var list=DeviceCalendarStore.State.recentTemplates??(DeviceCalendarStore.State.recentTemplates=new List<string>());list.Remove(t.id);list.Insert(0,t.id);if(list.Count>5)list.RemoveRange(5,list.Count-5);DeviceCalendarStore.Write();}
            editor.New(key,t.activity);
        }
        public void Edit(){if(Selected==null)return;editor.EditTemplate(Selected,shared.isOn);}
        public void UpdateDraft(string id,bool wasShared,ActivityTemplate t,bool isShared){var source=wasShared?AppSession.Instance.Data.templates:DeviceCalendarStore.State.templates;source.RemoveAll(x=>x.id==id);SaveDraft(t,isShared);if(wasShared&&!isShared)AppSession.Instance.SaveLocal();DeviceCalendarStore.Write();}
        public void SaveDraft(ActivityTemplate t,bool isShared)
        {
            var target=isShared?AppSession.Instance.Data.templates:DeviceCalendarStore.State.templates;
            var existing=target.Find(x=>x.id==t.id||x.name==t.name);if(existing!=null)t.id=existing.id;
            t.activity=ActivityResolver.Clone(t.activity);t.activity.id="template";t.activity.creatorUid=null;t.activity.createdAt=0;t.activity.updatedAt=0;
            target.RemoveAll(x=>x.id==t.id);target.Add(t);if(isShared)AppSession.Instance.SaveLocal();else DeviceCalendarStore.Write();if(panel.activeSelf)Refresh();
        }
        public void SaveMetadata()
        {
            var t=Selected;if(t==null)return;if(string.IsNullOrWhiteSpace(name.text)){message.text="Name the template.";return;}
            bool wasShared=AppSession.Instance.Data.templates.Contains(t);AppSession.Instance.Data.templates.Remove(t);DeviceCalendarStore.State.templates.Remove(t);t.name=name.text.Trim();SaveDraft(t,shared.isOn);if(wasShared||shared.isOn)AppSession.Instance.SaveLocal();else DeviceCalendarStore.Write();Refresh();
        }
        public void Delete()=>AppNavigation.Instance.Confirm("Delete this template? Existing activities stay unchanged.",()=>{var t=Selected;if(t==null)return;AppSession.Instance.Data.templates.Remove(t);DeviceCalendarStore.State.templates.Remove(t);AppSession.Instance.SaveLocal();DeviceCalendarStore.Write();Refresh();});
    }
}
