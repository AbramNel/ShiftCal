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
        public Dropdown selection;
        public InputField name;
        public Text summary,message;
        public Toggle shared;
        public ActivityEditor editor;
        List<ActivityTemplate> items=new List<ActivityTemplate>();
        public void Open(){AppNavigation.Instance.Open(panel);Refresh();}
        void Start(){selection.onValueChanged.AddListener(_=>Select());}
        void Refresh()
        {
            items=new List<ActivityTemplate>(AppSession.Instance.Data.templates);if(DeviceCalendarStore.State!=null)items.AddRange(DeviceCalendarStore.State.templates);
            selection.ClearOptions();selection.AddOptions(items.Count>0?items.Select(x=>x.name+(AppSession.Instance.Data.templates.Contains(x)?" · shared":" · this device")).ToList():new List<string>{"No templates yet"});Select();
        }
        ActivityTemplate Selected=>selection.value<items.Count?items[selection.value]:null;
        void Select(){var t=Selected;name.text=t?.name??"";shared.isOn=t!=null&&AppSession.Instance.Data.templates.Contains(t);summary.text=t==null?"Create an activity, then use Save as template. Templates never include alarms.":t.activity.title+"\n"+ActivityIconSet.Name(t.activity.icon)+" · "+t.activity.startTime+" · "+t.activity.durationMinutes+" min\n"+ActivityResolver.People(AppSession.Instance.Data,t.activity)+"\n"+t.activity.notes;message.text="";}
        public void Use(){if(Selected!=null)editor.New(DateKeyUtility.ToDateKey(DateTime.Today),Selected.activity);}
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
