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
        public ContentPill hoursPill;
        public RectTransform card, scroll;
        public Transform activityContent;
        public Button detailsButton,alarmButton;
        public DayDetailsPopup editor;
        public string DateKey => selected?.dateKey;
        private CalendarDayData selected;
        private DayInformation information;
        private CalendarController calendar;
        private RectTransform closeButton, shiftSummary;
        private Rect previousBounds;
        private bool sizePending;
        string account,group;

        void RefreshSelection(){if(selected==null)return;var session=App.AppSession.Instance;if(account!=session.Data.account||group!=session.CurrentGroup.groupId){Hide();return;}var date=selected.date;var day=CalendarGenerator.Generate(new System.DateTime(date.Year,date.Month,1),session.CurrentGroup,session.CalendarOverrides).Find(d=>d.dateKey==selected.dateKey);var infos=DayInformation.Resolve(session.Data,date,date);Show(day,infos.TryGetValue(day.dateKey,out var info)?info:new DayInformation());}
        private void OnRectTransformDimensionsChange() => sizePending = true;
        private void OnEnable(){App.WorkedHoursStore.Changed+=RefreshHours;if(App.AppSession.Instance!=null)App.AppSession.Instance.Changed+=RefreshSelection;sizePending=true;}
        private void OnDisable(){App.WorkedHoursStore.Changed-=RefreshHours;if(App.AppSession.Instance!=null)App.AppSession.Instance.Changed-=RefreshSelection;}
        private void LateUpdate() => Fit();

        public void RefreshHours()
        {
            if(selected==null)return;var h=App.WorkedHoursStore.Get(selected.dateKey);
            hoursPill.SetValue(h==null?"Add hours":App.WorkedHoursStore.Format(h.hundredths)+"h worked",h!=null);sizePending=true;
        }
        public void EditHours(){if(selected!=null)WorkedHoursEditor.Instance.Open(selected.dateKey);}
        public void AddAlarm(){if(selected!=null)FindFirstObjectByType<ScheduleWorkbench>().NewPersonalAlarm(selected.dateKey);}
        public void Show(CalendarDayData day, DayInformation info)
        {
            bool changedDate=selected?.dateKey!=day.dateKey;
            selected=day;information=info;account=App.AppSession.Instance.Data.account;group=App.AppSession.Instance.CurrentGroup.groupId;
            title.text=day.date.ToString("ddd, MMM d");
            shiftTime.text=string.IsNullOrWhiteSpace(day.startTime)?"":day.startTime+" – "+day.endTime;shift.text=day.shiftName;swatch.color=ShiftStyleUtility.ToColor(day.shiftColorHex);
            shiftTime.gameObject.SetActive(!string.IsNullOrEmpty(shiftTime.text));
            details.text=string.Join("\n\n",new[]{day.note}
                .Concat(info.events.Select(DayInformation.EventLine)).Concat(info.alarms.Where(o=>o.isEvent).Select(o=>o.title+" · "+DayInformation.Time(o))).Where(x=>!string.IsNullOrWhiteSpace(x)));
            details.gameObject.SetActive(!string.IsNullOrWhiteSpace(details.text));
            editor.family?.FillDay(day.dateKey,activityContent);RefreshHours();
            if(changedDate){var scroller=scroll.GetComponent<ScrollRect>();scroller.StopMovement();scroller.verticalNormalizedPosition=1;}
            if(!gameObject.activeSelf){gameObject.SetActive(true);transform.SetAsLastSibling();}sizePending=true;
        }

        public void Fit()
        {
            if(selected==null||CanvasUpdateRegistry.IsRebuildingLayout()||CanvasUpdateRegistry.IsRebuildingGraphics())return;
            if(calendar==null)calendar=GetComponentInParent<CalendarController>();
            var parent=card.parent as RectTransform;
            if(calendar==null||parent==null||!calendar.TryGetPreviewBounds(DateKey,parent,out var bounds)){Hide();return;}
            if(!sizePending&&bounds==previousBounds)return;
            sizePending=false;previousBounds=bounds;
            card.anchorMin=card.anchorMax=new Vector2(.5f,.5f);
            card.pivot=new Vector2(.5f,.5f);
            card.sizeDelta=bounds.size;
            card.anchoredPosition=bounds.center-parent.rect.center;
            LayoutContents();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.GetComponent<ScrollRect>().content);
        }

        // Also adapts the already-saved scene; no scene regeneration is required.
        public void LayoutContents()
        {
            if(card==null||scroll==null||hoursPill==null)return;
            if(closeButton==null)closeButton=card.Find("Close preview") as RectTransform;
            Place(title.rectTransform,0,1,1,1,16,-120,-332,-12);
            title.fontSize=34;title.alignment=TextAnchor.MiddleLeft;
            title.horizontalOverflow=HorizontalWrapMode.Wrap;
            Place(closeButton,1,1,1,1,-124,-120,-16,-12);
            Place((RectTransform)detailsButton.transform,1,1,1,1,-316,-120,-132,-12);
            var detailsLabel=detailsButton.GetComponentInChildren<Text>();
            detailsLabel.fontSize=30;
            Place(detailsLabel.rectTransform,0,0,1,1,14,4,-54,-4);

            // The fixed actions stay reachable while notes and activities scroll.
            Place((RectTransform)alarmButton.transform,0,0,.5f,0,16,12,-6,120);
            alarmButton.GetComponentInChildren<Text>().fontSize=30;
            Place((RectTransform)hoursPill.transform,.5f,0,1,0,6,12,-16,120);
            var hoursRect=(RectTransform)hoursPill.transform;
            hoursRect.pivot=new Vector2(1,.5f);
            hoursPill.minimumWidth=hoursPill.maxWidth=Mathf.Max(1,card.rect.width*.5f-22);
            hoursPill.label.fontSize=30;
            hoursPill.Fit();

            Place(scroll,0,0,1,1,0,132,0,-132);
            var content=scroll.GetComponent<ScrollRect>().content;
            if(shiftSummary==null)
            {
                shiftSummary=content.Find("Preview shift summary") as RectTransform;
                if(shiftSummary==null)
                {
                    shiftSummary=new GameObject("Preview shift summary",typeof(RectTransform),typeof(LayoutElement)).GetComponent<RectTransform>();
                    shiftSummary.SetParent(content,false);
                }
                shiftSummary.SetAsFirstSibling();
                swatch.rectTransform.SetParent(shiftSummary,false);
                shift.rectTransform.SetParent(shiftSummary,false);
                shiftTime.rectTransform.SetParent(shiftSummary,false);
            }
            shiftSummary.GetComponent<LayoutElement>().preferredHeight=shiftTime.gameObject.activeSelf?112:64;
            Place(swatch.rectTransform,0,1,0,1,0,-48,36,-12);
            Place(shift.rectTransform,0,1,1,1,52,-64,0,0);
            shift.fontSize=34;
            Place(shiftTime.rectTransform,0,1,1,1,0,-110,0,-64);
            shiftTime.fontSize=28;
            details.fontSize=30;
        }

        static void Place(RectTransform rect,float x0,float y0,float x1,float y1,float left,float bottom,float right,float top)
        {
            if(rect==null)return;
            rect.anchorMin=new Vector2(x0,y0);rect.anchorMax=new Vector2(x1,y1);
            rect.offsetMin=new Vector2(left,bottom);rect.offsetMax=new Vector2(right,top);
        }
        public void OpenDetails(){if(selected==null)return;Hide();editor.Show(selected,information);}
        public void Hide()=>gameObject.SetActive(false);
    }
}
