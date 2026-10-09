using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ShiftCal.App;
using ShiftCal.Core;
namespace ShiftCal.UI
{
    public class AvailabilityPlanner : MonoBehaviour
    {
        public GameObject panel;
        public Transform results;
        public Dropdown person;
        public PickerField date,time;
        public DurationChoice duration;
        public Toggle weekends;
        public Text message;
        public FamilyWorkbench workbench;
        public Transform peopleContent;
        public GameObject participantPrefab;
        readonly List<Toggle> choices=new List<Toggle>();
        List<string> ids=new List<string>();
        public void Open()
        {
            ids=new List<string>{"everyone"};ids.AddRange(AppSession.Instance.Data.profiles.Where(x=>x.active).Select(x=>x.id));person.ClearOptions();person.AddOptions(new List<string>{"All active people"}.Concat(AppSession.Instance.Data.profiles.Where(x=>x.active).Select(x=>x.name)).ToList());FamilyWorkbench.Clear(peopleContent);choices.Clear();foreach(var id in ids.Skip(1)){var row=Instantiate(participantPrefab,peopleContent);row.GetComponentInChildren<Text>().text=AppSession.Instance.Data.profiles.Find(x=>x.id==id).name;choices.Add(row.GetComponentInChildren<Toggle>());}
            string filter=FamilyWorkbench.Filter;if(filter=="mine")filter=AppSession.Instance.Data.profiles.Find(x=>x.linkedUid==FamilyWorkbench.Uid&&!string.IsNullOrEmpty(FamilyWorkbench.Uid))?.id;person.SetValueWithoutNotify(Math.Max(0,ids.IndexOf(filter)));
            date.Set(DateKeyUtility.ToDateKey(DateTime.Today));time.Set("6:00 PM");duration.Set(60);AppNavigation.Instance.Open(panel);FamilyWorkbench.Clear(results);message.text="Checks use recorded activities and the explicitly assigned work rotation. Search is limited to 90 days.";
        }
        IEnumerable<string> People=>person.value>0?new[]{ids[person.value]}:choices.Any(x=>x.isOn)?choices.Select((x,i)=>new{x,i}).Where(x=>x.x.isOn).Select(x=>ids[x.i+1]):ids.Skip(1);
        public void Check(){FamilyWorkbench.Clear(results);try{message.text=FamilyAvailability.Check(AppSession.Instance.Data,People,DateKeyUtility.FromDateKey(date.value),time.value,duration.Minutes);}catch(Exception ex){message.text=ex.Message;}}
        public void Search()
        {
            FamilyWorkbench.Clear(results);try{
                var from=DateKeyUtility.FromDateKey(date.value);int count=0;
                for(int i=0;i<90;i++) {var d=from.AddDays(i);if(weekends.isOn&&d.DayOfWeek!=DayOfWeek.Saturday)continue;string status=FamilyAvailability.Check(AppSession.Instance.Data,People,d,time.value,duration.Minutes);if(status.StartsWith("Busy"))continue;if(weekends.isOn){string sunday=FamilyAvailability.Check(AppSession.Instance.Data,People,d.AddDays(1),time.value,duration.Minutes);if(sunday.StartsWith("Busy"))continue;status+=" / Sunday: "+sunday;}Result(d,status);if(++count>=12)break;}
                message.text=count>0?"First "+count+" matching days within 90 days. Unknown entries need confirmation.":"No matching days in the next 90 days.";
            }catch(Exception ex){message.text=ex.Message;}
        }
        public void NextOff()
        {
            FamilyWorkbench.Clear(results);var s=AppSession.Instance.Data;
            if(string.IsNullOrEmpty(s.group.shiftOwnerProfileId)){message.text="Select the work rotation's owner in Family profiles first.";return;}
            foreach(var d in FamilyAvailability.NextOff(s,DateTime.Today).Take(8))Result(d,"OFF · "+(s.profiles.Find(x=>x.id==s.group.shiftOwnerProfileId)?.name??"Rotation owner"));message.text="Next OFF days, including date overrides, within 90 days.";
        }
        void Result(DateTime date,string status){var t=FamilyWorkbench.AddText(results,date.ToString("ddd, MMM d")+"\n"+status);t.GetComponent<LayoutElement>().preferredHeight=160;t.raycastTarget=true;t.gameObject.AddComponent<Button>().onClick.AddListener(()=>workbench.OpenDay(date));}
    }
}
