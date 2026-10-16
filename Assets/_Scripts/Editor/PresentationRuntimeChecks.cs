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
public static class PresentationRuntimeChecks
{
    static IEnumerator Click(Button button,Action<bool,string> check,string name,RectTransform pointTarget=null)
    {
        var rect=(RectTransform)button.transform;var scroll=rect.GetComponentInParent<ScrollRect>();if(scroll!=null){var local=scroll.viewport.InverseTransformPoint(rect.TransformPoint(rect.rect.center));scroll.StopMovement();scroll.content.anchoredPosition-=new Vector2(0,local.y-scroll.viewport.rect.center.y);}yield return null;yield return null;
        var target=pointTarget??rect;var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,target.TransformPoint(target.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);bool correct=hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button;check(correct,"real presentation pointer: "+name+(correct?"":" / top="+(hits.Count>0?hits[0].gameObject.name:"none")));if(correct)ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);yield return null;yield return null;
    }
    public static IEnumerator Run(Action<bool,string> check)
    {
        var session=AppSession.Instance;string previous=session.Data.account,test="presentation-runtime-"+Guid.NewGuid().ToString("N");session.SwitchAccount(test);yield return null;
        var work=Object.FindFirstObjectByType<FamilyWorkbench>();var old=Object.FindFirstObjectByType<ScheduleWorkbench>();var calendar=work.calendar;var nav=AppNavigation.Instance;var hours=WorkedHoursEditor.Instance;var today=DateTime.Today;string key=DateKeyUtility.ToDateKey(today),tomorrow=DateKeyUtility.ToDateKey(today.AddDays(1));
        session.CurrentGroup.pattern=new List<int>{2};session.Data.profiles.AddRange(new[]{new FamilyProfile{id="parent",name="Parent",color="#60A5FA"},new FamilyProfile{id="child",name="Child",color="#F59AC8"}});
        session.Data.activities.Add(new CalendarActivity{id="class",title="Karate",icon="karate",dateKey=key,assigneeId="child"});DeviceCalendarStore.State.personalAlarms.Add(new PersonalAlarmDefinition{groupId=session.CurrentGroup.groupId,schedule=new EventSeries{id="personal-runtime",title="Wake-up",dateKey=tomorrow,startTime="04:00",zone="device",alarm=true}});session.SaveLocal();WorkedHoursStore.Set(key,1200);WorkedHoursStore.Set(tomorrow,1350);
        foreach(ThemeManager.Theme theme in Enum.GetValues(typeof(ThemeManager.Theme)))
        {
            ThemeManager.Apply(theme);nav.ShowCalendar();calendar.OpenDate(today);yield return null;yield return null;
            var preview=calendar.preview;check(preview.hoursPill.GetComponent<RectTransform>().rect.width<preview.card.rect.width*.65f,"worked-hours pill fits its content "+theme);check(preview.hoursPill.icon.sprite==hours.hoursIcon&&preview.hoursPill.edit.gameObject.activeSelf,"timesheet and pencil shown for recorded hours "+theme);
            var click=Click(preview.hoursPill.button,check,"hours pencil",preview.hoursPill.edit.rectTransform);while(click.MoveNext())yield return null;check(hours.gameObject.activeSelf&&hours.DateKey==key,"pencil opens existing date-scoped editor "+theme);hours.Cancel();preview.Hide();
            var agendaButton=calendar.GetComponentsInChildren<Button>().Single(b=>b.name=="Agenda");click=Click(agendaButton,check,"calendar Agenda shortcut");while(click.MoveNext())yield return null;check(work.agendaPanel.activeInHierarchy,"Agenda shortcut opens redesigned screen "+theme);
            var ids=work.agendaContent.GetComponentsInChildren<AgendaDayCard>().Select(c=>c.GetInstanceID()).ToArray();work.Refresh();yield return null;check(ids.SequenceEqual(work.agendaContent.GetComponentsInChildren<AgendaDayCard>().Select(c=>c.GetInstanceID())),"unchanged agenda refresh reuses existing rows "+theme);
            var row=work.agendaContent.GetComponentsInChildren<FamilyActivityRow>().Single(r=>r.title.text=="Karate");check(row.frame.color==ShiftStyleUtility.ToColor("#F59AC8"),"agenda assignment color matches saved family profile "+theme);click=Click(row.button,check,"agenda family event");while(click.MoveNext())yield return null;check(work.detailsPanel.activeSelf&&work.detailsTitle.text=="Karate","agenda event opens existing details "+theme);nav.Back();yield return null;
            var alarm=work.agendaContent.GetComponentsInChildren<FamilyActivityRow>().Single(r=>r.IsAlarm);click=Click(alarm.button,check,"agenda personal alarm");while(click.MoveNext())yield return null;check(old.eventPanel.activeSelf&&old.eventDate.value==tomorrow&&old.editorHeading.text=="Edit personal alarm","alarm row opens correct private editor and date "+theme);nav.Back();yield return null;
            var heading=work.agendaContent.GetComponentsInChildren<AgendaDayCard>().First();click=Click(heading.button,check,"agenda day heading",heading.date.rectTransform);while(click.MoveNext())yield return null;check(nav.CurrentScreen.name=="Calendar Screen"&&calendar.preview.DateKey==key,"agenda heading opens its calendar date "+theme);calendar.preview.Hide();work.ShowAgenda();yield return null;
            click=Click(work.agendaFilter.button,check,"compact Agenda filter");while(click.MoveNext())yield return null;check(SelectionSheet.Instance.Owner==work.agendaFilter,"Agenda filter uses existing selection sheet "+theme);var choice=SelectionSheet.Instance.content.GetComponentsInChildren<SelectionSheetRow>().Last();click=Click(choice.button,check,"Agenda person choice");while(click.MoveNext())yield return null;check(DeviceCalendarStore.State.filter=="child","compact filter commits stable person ID "+theme);work.agendaFilter.value=0;yield return null;
            var add=work.agendaPanel.GetComponentsInChildren<Button>().Single(b=>b.name=="Event");click=Click(add,check,"compact Agenda Add Event");while(click.MoveNext())yield return null;check(work.editor.panel.activeSelf&&work.editor.Draft.dateKey==key&&work.editor.Draft.recurrence==RecurrenceKind.Once,"Agenda Add Event opens shared single-date editor "+theme);nav.Back();yield return null;nav.Back();yield return null;
        }
        nav.ShowCalendar();session.SwitchAccount(previous);yield return null;foreach(string file in Directory.GetFiles(Application.persistentDataPath,Path.GetFileName(ScheduleStorage.PathFor(test))+"*"))File.Delete(file);
    }
}
