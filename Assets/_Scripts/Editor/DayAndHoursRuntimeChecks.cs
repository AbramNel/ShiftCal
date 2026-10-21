using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShiftCal.App;
using ShiftCal.Core;
using ShiftCal.Data;
using ShiftCal.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;
public static class DayAndHoursRuntimeChecks
{
    static void Tap(RectTransform rect,Action<bool,string> check,string label)
    {
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        bool target=hits.Count>0&&(hits[0].gameObject.transform==rect||hits[0].gameObject.transform.IsChildOf(rect));check(target,"real pointer receives "+label+(target?"":" / top="+(hits.Count>0?hits[0].gameObject.name:"none")));if(target)ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static IEnumerator Click(Button button,Action<bool,string> check,string label){var rect=(RectTransform)button.transform;var scroll=rect.GetComponentInParent<ScrollRect>();if(scroll!=null){var point=scroll.viewport.InverseTransformPoint(rect.TransformPoint(rect.rect.center));scroll.StopMovement();scroll.content.anchoredPosition-=new Vector2(0,point.y-scroll.viewport.rect.center.y);}yield return null;yield return null;Tap((RectTransform)button.transform,check,label);yield return null;yield return null;}
    static Rect BoundsIn(RectTransform target,RectTransform parent)
    {
        var corners=new Vector3[4];target.GetWorldCorners(corners);
        Vector2 min=parent.InverseTransformPoint(corners[0]),max=min;
        foreach(var corner in corners){Vector2 point=parent.InverseTransformPoint(corner);min=Vector2.Min(min,point);max=Vector2.Max(max,point);}
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }
    static IEnumerator CheckFloatingPreview(CalendarController calendar,Action<bool,string> check)
    {
        var cells=calendar.GetComponentsInChildren<CalendarDayCell>().Where(c=>c.DateKey!=null).OrderBy(c=>c.DateKey,StringComparer.Ordinal).ToArray();
        check(cells.Length==42,"floating preview has six visible week rows");
        if(cells.Length!=42)yield break;
        var preview=calendar.preview;var parent=(RectTransform)preview.card.parent;
        for(int row=0;row<6;row++)
        {
            var selected=cells[row*7+(row%2==0?0:6)];
            preview.Hide();calendar.TapDay(selected.DateKey);yield return null;yield return null;preview.Fit();
            var panel=BoundsIn(preview.card,parent);var clicked=BoundsIn((RectTransform)selected.transform,parent);
            int firstRow=row<2?row+1:row-2;
            var first=BoundsIn((RectTransform)cells[firstRow*7+1].transform,parent);
            var last=BoundsIn((RectTransform)cells[(firstRow+1)*7+5].transform,parent);
            check(Mathf.Abs(panel.xMin-first.xMin)<.5f&&Mathf.Abs(panel.xMax-last.xMax)<.5f&&Mathf.Abs(panel.yMin-last.yMin)<.5f&&Mathf.Abs(panel.yMax-first.yMax)<.5f,"preview spans the middle five columns and two rows for week "+row);
            check(row<2?panel.yMax<clicked.yMin:panel.yMin>clicked.yMax,"preview leaves the selected week visible for week "+row);
            check(parent.rect.Contains(panel.min)&&parent.rect.Contains(panel.max),"preview stays inside the safe calendar area for week "+row);
            check(preview.scroll.rect.height>0,"floating preview retains a scrollable body for week "+row);
            foreach(var button in new[]{preview.detailsButton,preview.alarmButton,preview.hoursPill.button})
            {
                var rect=BoundsIn((RectTransform)button.transform,preview.card);
                check(preview.card.rect.Contains(rect.min)&&preview.card.rect.Contains(rect.max)&&rect.height>=108,"floating action stays inside the card with its touch target: "+button.name);
            }
        }
        preview.Hide();
    }
    public static IEnumerator Run(Action<bool,string> check)
    {
        var session=AppSession.Instance;string previous=session.Data.account,test="day-hours-runtime-"+Guid.NewGuid().ToString("N");session.SwitchAccount(test);yield return null;
        var nav=AppNavigation.Instance;var cal=Object.FindFirstObjectByType<CalendarController>(FindObjectsInactive.Include);var old=Object.FindFirstObjectByType<ScheduleWorkbench>();var hours=WorkedHoursEditor.Instance;var popup=cal.GetComponentInChildren<DayDetailsPopup>(true);var family=Object.FindFirstObjectByType<FamilyWorkbench>();
        string key=DateKeyUtility.ToDateKey(DateTime.Today.AddDays(1));session.CurrentGroup.pattern=new List<int>{2};session.UpdateDayDetails(key,"Shared note","Hidden person");session.SaveLocal();
        bool placementChecked=false;
        foreach(ThemeManager.Theme theme in Enum.GetValues(typeof(ThemeManager.Theme)))
        {
            session.UpdateDayDetails(key,"Shared note","Hidden person");session.SaveLocal();ThemeManager.Apply(theme);nav.ShowCalendar();cal.OpenDate(DateKeyUtility.FromDateKey(key));yield return null;yield return null;
            if(!placementChecked)
            {
                placementChecked=true;
                var placement=CheckFloatingPreview(cal,check);while(placement.MoveNext())yield return placement.Current;
                cal.OpenDate(DateKeyUtility.FromDateKey(key));yield return null;yield return null;
            }
            var preview=cal.preview;check(preview.gameObject.activeSelf,"ordinary date opens compact preview "+theme);
            var detailsRect=(RectTransform)preview.detailsButton.transform;check(detailsRect.rect.width<preview.card.rect.width*.5f&&detailsRect.rect.height>=108,"Details compact width and full touch target "+theme);
            var alarmRect=(RectTransform)preview.alarmButton.transform;check(alarmRect.rect.height>=108&&alarmRect.anchorMin.x==0,"Add Alarm remains in the floating footer "+theme);
            var openHours=preview.GetComponentsInChildren<Button>().First(b=>b.name=="Preview hours");var click=Click(openHours,check,"preview hours");while(click.MoveNext())yield return null;
            check(hours.gameObject.activeSelf&&hours.DateKey==key,"hours receives selected calendar date "+theme);hours.input.text="11.5";var save=hours.GetComponentsInChildren<Button>().First(b=>b.name=="Save");click=Click(save,check,"hours Save");while(click.MoveNext())yield return null;
            check(!hours.gameObject.activeSelf&&!preview.gameObject.activeSelf&&!nav.BlocksCalendarGesture&&WorkedHoursStore.Get(key)?.hundredths==1150,"saving hours persists and returns to the calendar without a modal blocker "+theme);
            var cell=cal.GetComponentsInChildren<CalendarDayCell>(true).Single(c=>c.DateKey==key);check(cell.workedBadge.activeSelf&&cell.workedLabel.text.Contains("11.5")&&cal.monthlyHours.text.Contains("11.5"),"badge and displayed monthly total update immediately "+theme);
            check(session.CalendarOverrides[key].personName=="Hidden person"&&session.CalendarOverrides[key].note=="Shared note","hours save leaves shared details unchanged "+theme);
            preview.Hide();var monthPill=cal.GetComponentsInChildren<Button>().Single(b=>b.name=="Monthly actual hours");click=Click(monthPill,check,"monthly actual hours pill");while(click.MoveNext())yield return null;check(SelectionSheet.Instance.IsOpen&&SelectionSheet.Instance.Owner==hours.monthSummary,"monthly total opens personal entry summary "+theme);var entry=SelectionSheet.Instance.content.GetComponentsInChildren<SelectionSheetRow>().First();click=Click(entry.button,check,"monthly entry edit");while(click.MoveNext())yield return null;check(hours.DateKey==key&&hours.gameObject.activeSelf,"monthly entry reopens correct date "+theme);hours.Cancel();cal.TapDay(key);yield return null;
            click=Click(preview.alarmButton,check,"preview Add Alarm");while(click.MoveNext())yield return null;check(old.eventPanel.activeSelf&&old.eventDate.value==key&&old.recurrence.value==0,"Add Alarm opens private selected-date oneoff editor "+theme);nav.Back();yield return null;
            click=Click(preview.detailsButton,check,"compact Details");while(click.MoveNext())yield return null;check(popup.gameObject.activeSelf&&!preview.gameObject.activeSelf,"Details opens full day without blocker "+theme);
            var fullAlarm=popup.GetComponentsInChildren<Button>().Single(b=>b.name=="Add Alarm");click=Click(fullAlarm,check,"Day Details Add Alarm");while(click.MoveNext())yield return null;check(old.eventPanel.activeSelf&&old.eventDate.value==key,"full day alarm retains selected date "+theme);nav.Back();yield return null;
            var fullHours=popup.GetComponentsInChildren<Button>().Single(b=>b.name=="Hours worked · Add");click=Click(fullHours,check,"Day Details hours");while(click.MoveNext())yield return null;check(hours.gameObject.activeSelf&&hours.input.text=="11.5","full day reopens saved actual hours "+theme);
            hours.input.text="0";save=hours.GetComponentsInChildren<Button>().First(b=>b.name=="Save");click=Click(save,check,"save explicit zero");while(click.MoveNext())yield return null;check(cell.workedBadge.activeSelf&&cell.workedLabel.text=="0h","saved zero keeps visible badge "+theme);
            click=Click(fullHours,check,"reopen hours to clear");while(click.MoveNext())yield return null;var clear=hours.GetComponentsInChildren<Button>().First(b=>b.name=="Clear");click=Click(clear,check,"clear hours");while(click.MoveNext())yield return null;check(WorkedHoursStore.Get(key)==null&&!cell.workedBadge.activeSelf,"Clear deletes hours and removes badge "+theme);
            var notes=popup.GetComponentsInChildren<InputField>().Single();notes.text="Updated shared notes";popup.SaveDetails();yield return null;check(session.CalendarOverrides[key].personName=="Hidden person"&&session.CalendarOverrides[key].note=="Updated shared notes","notes UI preserves hidden day person "+theme);
            cal.TapDay(key);yield return null;cal.TapDay(key);yield return null;check(popup.gameObject.activeSelf&&!preview.gameObject.activeSelf,"second date tap opens full details "+theme);nav.Back();yield return null;
            cal.TapDay(key);yield return null;var close=preview.GetComponentsInChildren<Button>().First(b=>b.name=="Close preview");click=Click(close,check,"preview X");while(click.MoveNext())yield return null;check(!preview.gameObject.activeSelf,"X removes preview input surface "+theme);
            cal.TapDay(key);yield return null;nav.Back();yield return null;check(!preview.gameObject.activeSelf,"Back dismisses preview "+theme);
            cal.TapDay(key);yield return null;string other=DateKeyUtility.ToDateKey(DateKeyUtility.FromDateKey(key).AddDays(1));cal.TapDay(other);yield return null;check(preview.DateKey==other&&preview.gameObject.activeSelf,"another date updates preview selection "+theme);preview.Hide();
            var grid=cal.GetComponentsInChildren<ResponsiveCalendarGrid>(true).First();grid.Fit();yield return null;foreach(var c in cal.GetComponentsInChildren<CalendarDayCell>(true))c.FlushPresentation();
            var editor=family.editor;editor.New(key);editor.title.text="Shutdown Meeting";editor.timing.value=1;editor.time.Set("13:00");editor.applyTo.value=3;foreach(var chip in editor.weekdays)chip.isOn=false;editor.weekdays[(int)DateKeyUtility.FromDateKey(key).DayOfWeek].isOn=true;editor.alarm.isOn=true;editor.ringBefore.Set(30);editor.Commit();yield return null;var created=session.Data.activities.Last();check(created.hasShiftConstraint&&created.recurrence==RecurrenceKind.Weekly&&DeviceCalendarStore.Find("activity-"+created.id)?.alarmOffsetMinutes==30,"editor commits one constrained event with private audible offset "+theme);
            var delivery=RecurrenceEngine.Resolve(DeviceCalendarStore.Effective(session.Data),DateKeyUtility.FromDateKey(key),DateKeyUtility.FromDateKey(key)).Single(o=>o.sourceId=="activity-"+created.id&&o.audible);check(delivery.at==RecurrenceEngine.Instant(DateKeyUtility.FromDateKey(key),"12:30","device"),"saved event rings early once "+theme);
            WorkedHoursStore.Set(key,null);yield return null;
        }
        session.SwitchAccount(previous);yield return null;foreach(string file in Directory.GetFiles(Application.persistentDataPath,Path.GetFileName(ScheduleStorage.PathFor(test))+"*"))File.Delete(file);
    }
}
