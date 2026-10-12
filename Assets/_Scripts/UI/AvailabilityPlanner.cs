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
        public SelectionField person;
        public PickerField date,time;
        public DurationChoice duration;
        public Text message;
        public FamilyWorkbench workbench;
        public SelectionSheetRow resultPrefab;
        bool weekends;
        public void Open()
        {
            person.SetOptions(FamilySelections.People(AppSession.Instance.Data));string filter=FamilyWorkbench.Filter;if(filter=="mine")filter=FamilySelections.Me(AppSession.Instance.Data);
            person.SetSelected(filter=="everyone"?Enumerable.Range(0,person.options.Count):Enumerable.Range(0,person.options.Count).Where(i=>person.options[i].id==filter));weekends=false;
            date.Set(DateKeyUtility.ToDateKey(DateTime.Today));time.Set("6:00 PM");duration.Set(60);AppNavigation.Instance.Open(panel);FamilyWorkbench.Clear(results);message.text="Checks use recorded activities and the explicitly assigned work rotation. Search is limited to 90 days.";
        }
        IEnumerable<string> People=>person.selectedValues.Select(i=>person.options[i].id);
        public void Check(){FamilyWorkbench.Clear(results);try{if(!People.Any()){message.text="Select at least one person.";return;}var day=DateKeyUtility.FromDateKey(date.value);Result(day,FamilyAvailability.Check(AppSession.Instance.Data,People,day,time.value,duration.Minutes));message.text="Availability uses recorded information.";}catch(Exception ex){message.text=ex.Message;}}
        public void FindFamilyTime(){weekends=false;Search();}
        public void AvailableWeekends(){weekends=true;Search();}
        public void Search()
        {
            FamilyWorkbench.Clear(results);try{
                if(!People.Any()){message.text="Select at least one person.";return;}var from=DateKeyUtility.FromDateKey(date.value);int count=0;int needed=duration.Minutes;var people=People.ToArray();
                for(int i=0;i<90;i++) {var d=from.AddDays(i);if(weekends&&(d.DayOfWeek!=DayOfWeek.Saturday||i+1>=90))continue;string status=FamilyAvailability.Check(AppSession.Instance.Data,people,d,time.value,needed);if(status.StartsWith("Busy"))continue;if(weekends){string sunday=FamilyAvailability.Check(AppSession.Instance.Data,people,d.AddDays(1),time.value,needed);if(sunday.StartsWith("Busy"))continue;status+=" / Sunday: "+sunday;}Result(d,status);if(++count>=12)break;}
                message.text=count>0?"First "+count+" matching days within 90 days. Unknown entries need confirmation.":"No matching days in the next 90 days.";
            }catch(Exception ex){message.text=ex.Message;}
        }
        public void NextOff()
        {
            FamilyWorkbench.Clear(results);var s=AppSession.Instance.Data;
            if(string.IsNullOrEmpty(s.group.shiftOwnerProfileId)){message.text="Select the work rotation's owner in Family profiles first.";return;}
            foreach(var d in FamilyAvailability.NextOff(s,DateKeyUtility.FromDateKey(date.value)).Take(8))Result(d,"OFF · "+(s.profiles.Find(x=>x.id==s.group.shiftOwnerProfileId)?.name??"Rotation owner"));message.text="Next OFF days, including date overrides, within 90 days.";
        }
        void Result(DateTime date,string status){var row=Instantiate(resultPrefab,results);row.Bind(new SelectionItem(date.ToString("ddd, MMM d"),DateKeyUtility.ToDateKey(date),status),false,()=>workbench.OpenDay(date));}
    }
}
