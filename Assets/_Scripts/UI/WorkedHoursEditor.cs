using System;
using ShiftCal.App;
using ShiftCal.Core;
using UnityEngine;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    public class WorkedHoursEditor : MonoBehaviour
    {
        public Text heading,message;
        public InputField input;
        public SelectionField monthSummary;
        string key,account,group;
        bool hooked;
        public static WorkedHoursEditor Instance=>FindFirstObjectByType<WorkedHoursEditor>(FindObjectsInactive.Include);
        public string DateKey=>key;
        public void Open(string dateKey)
        {
            key=dateKey;account=AppSession.Instance.Data.account;group=AppSession.Instance.CurrentGroup.groupId;
            heading.text="Hours worked — "+DateKeyUtility.FromDateKey(key).ToString("MMM d");var entry=WorkedHoursStore.Get(key);input.text=entry==null?"":(entry.hundredths/100m).ToString("0.##",System.Globalization.CultureInfo.InvariantCulture);message.text="Personal · only on this device";
            gameObject.SetActive(true);transform.SetAsLastSibling();GetComponent<ModalPanel>().Close=Cancel;
        }
        bool Current()=>account==AppSession.Instance.Data.account&&group==AppSession.Instance.CurrentGroup.groupId;
        public void Save()
        {
            if(!Current()){message.text="The active calendar changed. Reopen the hours entry.";return;}
            if(!WorkedHoursStore.TryParse(input.text,out var amount)){message.text="Use 0–24 hours, with up to two decimal places.";return;}
            try{WorkedHoursStore.Set(key,amount);Cancel();}catch(Exception ex){message.text=ex.Message;}
        }
        public void Clear(){if(!Current()){Cancel();return;}try{WorkedHoursStore.Set(key,null);Cancel();}catch(Exception ex){message.text=ex.Message;}}
        public void Cancel(){input.DeactivateInputField();gameObject.SetActive(false);}
        public void ShowMonth(DateTime month)
        {
            if(!hooked){hooked=true;monthSummary.onValueChanged.AddListener(i=>Open(monthSummary.options[i].id));}
            var entries=WorkedHoursStore.Month(month);monthSummary.heading=month.ToString("MMMM yyyy")+" · "+WorkedHoursStore.Format(WorkedHoursStore.Total(month))+"h logged · "+entries.Count+" days";
            if(entries.Count==0){Open(DateKeyUtility.ToDateKey(month.Year==DateTime.Today.Year&&month.Month==DateTime.Today.Month?DateTime.Today:month));return;}
            monthSummary.SetOptions(entries.ConvertAll(e=>new SelectionItem(DateKeyUtility.FromDateKey(e.dateKey).ToString("ddd, MMM d")+" · "+WorkedHoursStore.Format(e.hundredths)+"h",e.dateKey,"Tap to edit actual hours")));monthSummary.Open();
        }
    }
}
