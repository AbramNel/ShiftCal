using System.Linq;
using ShiftCal.Core;
using UnityEngine;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    public class DayPreview : MonoBehaviour
    {
        public Text title, shift, shiftTime, details, workedHours;
        public Image swatch;
        public RectTransform card, scroll;
        public Transform activityContent;
        public Button detailsButton,alarmButton;
        public DayDetailsPopup editor;
        public string DateKey => selected?.dateKey;
        private CalendarDayData selected;
        private DayInformation information;
        string account,group;
        void RefreshSelection(){if(selected==null)return;var session=App.AppSession.Instance;if(account!=session.Data.account||group!=session.CurrentGroup.groupId){Hide();return;}var date=selected.date;var day=CalendarGenerator.Generate(new System.DateTime(date.Year,date.Month,1),session.CurrentGroup,session.CalendarOverrides).Find(d=>d.dateKey==selected.dateKey);var infos=DayInformation.Resolve(session.Data,date,date);Show(day,infos.TryGetValue(day.dateKey,out var info)?info:new DayInformation());}
        private bool sizePending;
        private Vector2 previousSize;
        private void OnRectTransformDimensionsChange() => sizePending = true;
        private void OnEnable(){App.WorkedHoursStore.Changed+=RefreshHours;if(App.AppSession.Instance!=null)App.AppSession.Instance.Changed+=RefreshSelection;}
        private void OnDisable(){App.WorkedHoursStore.Changed-=RefreshHours;if(App.AppSession.Instance!=null)App.AppSession.Instance.Changed-=RefreshSelection;}
        private void LateUpdate() => Fit();
        public void RefreshHours()
        {
            if(selected==null)return;var h=App.WorkedHoursStore.Get(selected.dateKey);
            workedHours.text=h==null?"Hours worked · Add":"Worked: "+App.WorkedHoursStore.Format(h.hundredths)+"h · Edit";sizePending=true;
        }
        public void EditHours(){if(selected!=null)WorkedHoursEditor.Instance.Open(selected.dateKey);}
        public void AddAlarm(){if(selected!=null)FindFirstObjectByType<ScheduleWorkbench>().NewPersonalAlarm(selected.dateKey);}
        public void Show(CalendarDayData day, DayInformation info)
        {
            selected=day;information=info;account=App.AppSession.Instance.Data.account;group=App.AppSession.Instance.CurrentGroup.groupId;title.text=day.date.ToString("dddd, MMMM d");
            shiftTime.text=string.IsNullOrWhiteSpace(day.startTime)?"":day.startTime+" – "+day.endTime;shift.text=day.shiftName;swatch.color=ShiftStyleUtility.ToColor(day.shiftColorHex);
            details.text=string.Join("\n\n",new[]{day.note}
                .Concat(info.events.Select(DayInformation.EventLine)).Concat(info.alarms.Where(o=>o.isEvent).Select(o=>o.title+" · "+DayInformation.Time(o))).Where(x=>!string.IsNullOrWhiteSpace(x)));
            editor.family?.FillDay(day.dateKey,activityContent);RefreshHours();
            if(!gameObject.activeSelf){gameObject.SetActive(true);transform.SetAsLastSibling();}sizePending=true;
        }
        public void Fit()
        {
            if(CanvasUpdateRegistry.IsRebuildingLayout()||CanvasUpdateRegistry.IsRebuildingGraphics())return;
            var size=((RectTransform)transform).rect.size;if(!sizePending&&size==previousSize)return;
            sizePending=false;previousSize=size;
            var content=scroll.GetComponent<ScrollRect>().content;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            float body=LayoutUtility.GetPreferredHeight(content);
            card.pivot=new Vector2(.5f,0);card.sizeDelta=new Vector2(card.sizeDelta.x,Mathf.Clamp(body+590,620,size.y*.72f));card.anchoredPosition=new Vector2(0,24);
        }
        public void OpenDetails(){if(selected==null)return;Hide();editor.Show(selected,information);}
        public void Hide()=>gameObject.SetActive(false);
    }
}
