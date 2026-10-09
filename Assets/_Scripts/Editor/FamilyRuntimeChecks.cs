using System;
using System.Collections;
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
public static class FamilyRuntimeChecks
{
    public static IEnumerator Exercise(Action<bool,string> check)
    {
        var session=AppSession.Instance;string originalAccount=session.Data.account;
        string testAccount="runtime-family-"+Guid.NewGuid().ToString("N");session.SwitchAccount(testAccount);yield return null;
        var nav=AppNavigation.Instance;var work=Object.FindFirstObjectByType<FamilyWorkbench>();var editor=work.editor;
        var person=new FamilyProfile{name="Runtime parent",linkedUid=testAccount};session.Data.profiles.Add(person);session.SaveLocal();nav.ShowCalendar();yield return null;
        var profiles=Object.FindFirstObjectByType<FamilyProfileController>();profiles.Open();yield return null;profiles.displayName.text="Runtime child";profiles.Save();yield return null;
        check(session.Data.profiles.Count==2,"runtime profile Save persists a stable ID");nav.Back();yield return null;
        foreach(ThemeManager.Theme theme in Enum.GetValues(typeof(ThemeManager.Theme)))
        {
            ThemeManager.Apply(theme);yield return null;
            editor.New(DateKeyUtility.ToDateKey(DateTime.Today));yield return null;
            editor.title.text="Runtime karate "+theme;editor.SelectIcon("karate");editor.Claim();editor.recurrence.value=2;yield return null;
            check(editor.weekdaySection.activeSelf&&editor.assignee.value==1,"runtime conditional recurrence and UID claim "+theme);
            var footer=editor.panel.GetComponentsInChildren<RectTransform>().First(x=>x.name=="Editor actions");var scroll=editor.panel.GetComponentInChildren<ScrollRect>();
            check(scroll.viewport.rect.height>0&&footer.rect.height>100,"runtime activity editor scroll and footer "+theme);
            editor.Icons();yield return null;
            var choice=editor.iconPanel.GetComponentsInChildren<ActivityIconChoice>().Single(x=>x.id=="karate");var rect=(RectTransform)choice.transform;
            var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));var pointer=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            check(hits.Any(x=>x.gameObject.GetComponentInParent<ActivityIconChoice>()==choice),"runtime high-kick icon has a real button target "+theme);choice.GetComponent<Button>().onClick.Invoke();yield return null;
            check(!editor.iconPanel.activeSelf&&editor.preview.sprite==ActivityIconSet.Load().Get("karate"),"runtime icon picker returns to editor "+theme);
            editor.Commit();yield return null;check(!editor.panel.activeSelf&&session.Data.activities.Count>0,"runtime canonical activity Save "+theme);
            var activity=session.Data.activities.Last();var occurrence=ActivityResolver.Resolve(session.Data,DateTime.Today,DateTime.Today.AddDays(7)).First(x=>x.seriesId==activity.id);work.Details(occurrence);yield return null;
            check(work.detailsPanel.activeInHierarchy,"runtime day activity details opens "+theme);check(work.detailsBody.text.Contains("Weekly"),"runtime details display repeat schedule "+theme);nav.Back();yield return null;check(!work.detailsPanel.activeSelf,"runtime activity Back removes modal blocker "+theme);
            work.ShowAgenda();yield return null;check(work.agendaContent.GetComponentsInChildren<FamilyActivityRow>().Length>0,"runtime agenda resolves recurring rows "+theme);check(work.agendaContent.GetComponentsInChildren<FamilyActivityRow>().All(x=>x.title.color==ThemeManager.Token(ThemeManager.Role.Text)&&x.icon.color==ThemeManager.Token(ThemeManager.Role.Text)),"runtime agenda labels and icons follow theme "+theme);work.agendaFilter.value=1;yield return null;check(DeviceCalendarStore.State.filter=="mine","runtime agenda filter persists locally "+theme);nav.Back();yield return null;
            editor.Edit(occurrence);yield return null;editor.notes.text="Unsaved change";
            check(editor.panel.GetComponents<ModalPanel>().Length==1,"runtime activity editor has one modal controller "+theme);
            nav.Back();yield return null;
            if(!nav.confirmation.gameObject.activeSelf&&editor.panel.activeSelf){nav.Back();yield return null;}
            check(nav.confirmation.gameObject.activeSelf&&editor.panel.activeSelf,"runtime dirty activity confirms Back "+theme);nav.confirmation.Accept();yield return null;
            editor.New(DateKeyUtility.ToDateKey(DateTime.Today));yield return null;editor.title.text="Template draft";editor.templateName.text="Runtime template";editor.SaveTemplate();yield return null;
            check(DeviceCalendarStore.State.templates.Any(x=>x.name=="Runtime template")&&!session.Data.templates.Any(x=>x.name=="Runtime template"),"runtime local template excludes shared records "+theme);nav.Back();yield return null;nav.confirmation.Accept();yield return null;
            int count=session.Data.activities.Count;editor.templates.Open();yield return null;editor.templates.Edit();yield return null;editor.title.text="Edited template";editor.Commit();yield return null;
            check(session.Data.activities.Count==count&&DeviceCalendarStore.State.templates.Single(x=>x.name=="Runtime template").activity.title=="Edited template","runtime template editor updates template without creating activity "+theme);nav.Back();yield return null;
            var planner=Object.FindFirstObjectByType<AvailabilityPlanner>();planner.Open();yield return null;planner.Check();yield return null;planner.Search();yield return null;check(planner.results.childCount>0&&planner.message.text.Contains("90 days"),"runtime bounded availability search "+theme);nav.Back();yield return null;
            nav.ShowCalendar();yield return null;var month=work.calendar.currentMonth;var cell=work.calendar.GetComponentsInChildren<CalendarDayCell>().First();var swipe=new PointerEventData(EventSystem.current){position=new Vector2(300,300),button=PointerEventData.InputButton.Left};cell.OnPointerDown(swipe);cell.OnBeginDrag(swipe);swipe.position=new Vector2(50,310);cell.OnPointerUp(swipe);yield return null;
            check(work.calendar.currentMonth==month.AddMonths(1)&&!work.calendar.preview.gameObject.activeSelf,"runtime month swipe suppresses preview "+theme);work.calendar.PrevMonth();yield return null;
        }
        session.SwitchAccount(testAccount);yield return null;check(session.Data.activities.Count==3&&DeviceCalendarStore.State.templates.Count==1,"runtime shared and device files survive restart without duplicate templates");
        session.SwitchAccount(originalAccount);yield return null;
        foreach(string path in Directory.GetFiles(Application.persistentDataPath,Path.GetFileName(ScheduleStorage.PathFor(testAccount))+"*"))File.Delete(path);
    }
}
