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
public static class QualityOfLifeChecks
{
    public static void Models(Action<bool,string> check,FamilyWorkbench work)
    {
        var s=AppSession.Instance.Data;var e=work.editor;
        check(!work.GetComponentsInChildren<Dropdown>(true).Any(),"saved UI has no legacy Dropdown components or templates");
        e.New("2026-10-19");e.title.text="Untimed note";e.RefreshWarnings();check(string.IsNullOrEmpty(e.warning.text)&&!e.alarmSection.activeSelf&&!e.repeatSection.activeSelf,"untimed unassigned note has no irrelevant conflicts or recurrence controls");
        e.timing.value=1;e.time.Set("09:00");e.duration.Set(60);e.RefreshWarnings();check(e.warning.text=="","unassigned timed draft has no person-specific conflict");
        e.assignee.value=2;e.duration.Set(0);e.RefreshWarnings();check(e.warning.text=="","missing duration does not claim a definite conflict");
        e.recurrence.value=2;foreach(var chip in e.weekdays)chip.isOn=false;e.RefreshSections();check(e.repeatPreview.text.Contains("weekday"),"empty weekly chips show validation before save");
        e.weekdays[1].isOn=e.weekdays[3].isOn=true;e.RefreshSections();check(e.repeatPreview.text.Contains("Monday and Wednesday")&&e.repeatPreview.text.Contains("Next:"),"weekly preview uses actual next dates and readable days");
        var legacy=new CalendarActivity{title="All-day legacy",dateKey="2026-10-19",allDay=true,startTime="08:15",durationMinutes=35,zone="UTC"};
        e.New(legacy.dateKey,legacy);e.notes.text="Unrelated change";check(e.Draft.startTime=="08:15"&&e.Draft.durationMinutes==35&&e.Draft.zone=="UTC","unrelated all-day edit preserves stored timing metadata");e.timing.value=0;check(e.Draft.startTime==""&&e.Draft.durationMinutes==0,"explicit timing mode change clears incompatible values");
        var template=new ActivityTemplate{name="QoL karate",activity=new CalendarActivity{title="Karate template",icon="karate",dateKey="2026-10-01",recurrence=RecurrenceKind.Weekly,weekdays=new List<int>{1,3}}};
        e.templates.SaveDraft(template,false);e.panel.SetActive(false);e.templates.StartCreation("2026-10-19");var sheet=SelectionSheet.Instance;sheet.Choose(3);check(sheet.IsOpen&&sheet.Owner==e.templates.picker,"quick-start opens actual saved-template sheet");sheet.Choose(e.templates.picker.options.FindIndex(o=>o.id==template.id));
        var first=e.Draft.id;check(e.Draft.dateKey=="2026-10-19"&&first!="template"&&e.Draft.icon=="karate","template retains chosen date and creates fresh activity identity");e.panel.SetActive(false);e.templates.PickForDate("2026-10-20");sheet.Choose(e.templates.picker.options.FindIndex(o=>o.id==template.id));check(e.Draft.id!=first&&e.Draft.dateKey=="2026-10-20","each template use creates a distinct ID at the requested date");
        check(SaveSyncStatus.Describe(new ScheduleSave{account="local"},"","",false,false,false)=="Saved on this device","local status reports local save only");
        check(DeviceCalendarStore.Find("activity-"+e.Draft.id)==null,"template use never creates a personal alarm subscription");
        var item=ActivityResolver.Resolve(s,new DateTime(2026,10,1),new DateTime(2026,10,31)).First(a=>a.activity.assigneeId!=null);
        var change=ActivityAssignmentChange.Apply(item,0,null,null);check(change.CanUndo(s),"assignment Undo starts available while authoritative state matches");var changed=s.activityExceptions.Find(a=>a.seriesId==item.seriesId&&a.originalDate==item.originalDate);if(changed?.replacement!=null)changed.replacement.notes="Other device edit";else s.activities.Find(a=>a.id==item.seriesId).notes="Other device edit";
        check(!change.CanUndo(s)&&!change.Undo(),"concurrent authoritative change prevents stale Undo overwrite");
        var localState=DeviceCalendarStore.State;string previousAccount=s.account;s.account="local";localState.localProfileId=s.profiles[0].id;check(FamilySelections.Me(s)==s.profiles[0].id,"local Me uses explicitly selected device profile");localState.localProfileId=null;check(FamilySelections.Me(s)==null,"unlinked local device cannot silently claim a profile");s.account=previousAccount;
        var rare=new CalendarActivity{title="Rare monthly",dateKey="2026-10-19",recurrence=RecurrenceKind.Monthly,interval=60};check(ActivityResolver.NextDates(rare,new DateTime(2026,10,19)).Count()==3,"next-date preview supports long intervals through actual recurrence resolver");
        var pending=new ScheduleSave{account="cloud",records={new SyncRecord{pending=true}}};check(SaveSyncStatus.Describe(pending,"","Synced",false,true,false)!="Synced","pending records cannot display Synced");check(SaveSyncStatus.Describe(pending,"","Offline",false,true,true).Contains("pending"),"offline pending changes are labeled honestly");
        e.panel.SetActive(false);sheet.Cancel();
    }
    // Scroll first, then use the topmost real GraphicRaycaster hit to send the
    // pointer click. Every sheet workflow goes through its serialized Button.
    static void Center(RectTransform rect)
    {
        var scroll=rect.GetComponentInParent<ScrollRect>();if(scroll==null||rect==scroll.transform)return;
        var local=scroll.viewport.InverseTransformPoint(rect.TransformPoint(rect.rect.center));scroll.StopMovement();scroll.content.anchoredPosition-=new Vector2(0,local.y-scroll.viewport.rect.center.y);
    }
    static void Tap(RectTransform rect,Action<bool,string> check,string name)
    {
        var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));var pointer=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);bool hit=hits.Count>0&&(hits[0].gameObject.transform==rect||hits[0].gameObject.transform.IsChildOf(rect));
        check(hit,"pointer target receives input: "+name+(hit?"":" / top="+(hits.Count>0?hits[0].gameObject.name:"none")+" point="+point+" active="+rect.gameObject.activeInHierarchy));if(hit)ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static IEnumerator Click(RectTransform rect,Action<bool,string> check,string name){Center(rect);yield return null;yield return null;Tap(rect,check,name);yield return null;yield return null;}
    static IEnumerator Choice(SelectionField field,int option,Action<bool,string> check,string name)
    {
        var open=Click((RectTransform)field.transform,check,name+" field");while(open.MoveNext())yield return null;
        var sheet=SelectionSheet.Instance;check(sheet.IsOpen&&sheet.Owner==field,"sheet opens in front: "+name);
        if(!sheet.IsOpen)yield break;
        var rows=sheet.content.GetComponentsInChildren<SelectionSheetRow>();var click=Click((RectTransform)rows[option].transform,check,name+" choice");while(click.MoveNext())yield return null;
    }
    public static IEnumerator Runtime(Action<bool,string> check)
    {
        var session=AppSession.Instance;string account=session.Data.account;string test="qol-runtime-"+Guid.NewGuid().ToString("N");session.SwitchAccount(test);yield return null;
        var nav=AppNavigation.Instance;var work=Object.FindFirstObjectByType<FamilyWorkbench>();var e=work.editor;var sheet=SelectionSheet.Instance;
        session.Data.profiles.Add(new FamilyProfile{id="qol-me",name="Parent",linkedUid=test,color="#60A5FA"});session.Data.profiles.Add(new FamilyProfile{id="qol-child",name="Child",color="#F59AC8"});session.SaveLocal();
        foreach(ThemeManager.Theme theme in Enum.GetValues(typeof(ThemeManager.Theme)))
        {
            ThemeManager.Apply(theme);nav.ShowCalendar();e.New("2026-10-19");e.title.text="Karate "+theme;e.SelectIcon("karate");yield return null;
            var choose=Choice(e.recurrence,2,check,"Repeat Weekly "+theme);while(choose.MoveNext())yield return null;
            check(e.recurrence.value==2&&e.weekdaySection.activeInHierarchy,"Weekly selected by actual pointer and conditional fields appear");
            foreach(int i in new[]{1,3})if(!e.weekdays[i].isOn){var click=Click((RectTransform)e.weekdays[i].transform,check,"weekday "+i);while(click.MoveNext())yield return null;}
            check(e.weekdays[1].isOn&&e.weekdays[3].isOn&&e.repeatPreview.text.Contains("Monday and Wednesday"),"Monday/Wednesday chips update preview");
            choose=Choice(e.assignee,1,check,"Assign Me "+theme);while(choose.MoveNext())yield return null;check(e.Draft.assigneeId=="qol-me","Me resolves authenticated profile ID");
            var peopleExpansion=e.participants.transform.parent.GetComponentInParent<ScrollRect>();
            e.participants.transform.parent.gameObject.SetActive(true);yield return null;
            choose=Choice(e.participants,1,check,"Participants "+theme);while(choose.MoveNext())yield return null;
            var done=Click((RectTransform)sheet.done.transform,check,"participant Done");while(done.MoveNext())yield return null;check(e.Draft.participants.SequenceEqual(new[]{"qol-child"}),"participant sheet commits multiple selection independently of assignment");
            var saveButton=e.saveLabel.GetComponentInParent<Button>();var save=Click((RectTransform)saveButton.transform,check,"Save Activity "+theme);while(save.MoveNext())yield return null;
            var created=session.Data.activities.LastOrDefault();check(created!=null&&created.weekdays.SequenceEqual(new[]{1,3}),"weekly selection persists after real Save tap");if(created==null)continue;
            var resolved=ActivityResolver.Resolve(session.Data,new DateTime(2026,10,19),new DateTime(2026,10,25)).Where(a=>a.seriesId==created.id).ToArray();check(resolved.Select(a=>a.dateKey).SequenceEqual(new[]{"2026-10-19","2026-10-21"}),"saved weekly activity resolves on correct future dates");
            e.Edit(resolved[0]);yield return null;check(e.recurrence.value==2&&e.weekdays[1].isOn&&e.weekdays[3].isOn,"reopened Weekly and chips retain saved state");
            string notes=e.notes.text;e.notes.text="Draft retained";var open=Click((RectTransform)e.recurrence.transform,check,"reopen Repeat");while(open.MoveNext())yield return null;nav.Back();yield return null;
            check(!sheet.IsOpen&&e.panel.activeSelf&&e.notes.text=="Draft retained","Back cancels sheet before parent and preserves draft");nav.Back();yield return null;nav.confirmation.Accept();yield return null;
            work.Details(resolved[0]);yield return null;choose=Choice(work.assigned,3,check,"quick assignment");while(choose.MoveNext())yield return null;check(sheet.IsOpen&&sheet.Owner==work.detailScope,"recurring quick assignment asks scope");
            var scopeRows=sheet.content.GetComponentsInChildren<SelectionSheetRow>();if(scopeRows.Length==0){check(false,"scope rows missing: "+work.detailsMessage.text+" / owner="+sheet.Owner);continue;}var scopeClick=Click((RectTransform)scopeRows[0].transform,check,"This occurrence");while(scopeClick.MoveNext())yield return null;
            check(session.Data.activities.Find(a=>a.id==created.id).assigneeId=="qol-me"&&session.Data.activityExceptions.Find(a=>a.seriesId==created.id)?.replacement.assigneeId=="qol-child","single quick reassignment preserves series assignment");
            work.UndoAssignment();yield return null;check(!session.Data.activityExceptions.Any(a=>a.seriesId==created.id),"Undo restores authoritative assignment without duplicates");nav.Back();yield return null;
            var profiles=Object.FindFirstObjectByType<FamilyProfileController>();profiles.Open();yield return null;choose=Choice(profiles.person,1,check,"profile person");while(choose.MoveNext())yield return null;choose=Choice(profiles.color,3,check,"profile color swatch");while(choose.MoveNext())yield return null;check(profiles.color.options[profiles.color.value].id=="#FBBF24","color swatch sheet selects stable profile color");nav.Back();yield return null;
            nav.ShowSettings();yield return null;var prefs=nav.GetComponentInChildren<AlarmPreferencesPanel>(true);choose=Choice(prefs.snooze.choice,2,check,"snooze duration");while(choose.MoveNext())yield return null;check(DeviceAlarmPreferences.Current.snoozeMinutes==15,"duration sheet persists effective snooze choice");choose=Choice(prefs.sound,1,check,"sound choice");while(choose.MoveNext())yield return null;check(DeviceAlarmPreferences.Current.sound=="notification","sound sheet persists effective preference");
            var planner=Object.FindFirstObjectByType<AvailabilityPlanner>();planner.Open();yield return null;choose=Choice(planner.person,0,check,"planner people");while(choose.MoveNext())yield return null;nav.Back();yield return null;check(!sheet.IsOpen&&planner.panel.activeSelf&&planner.person.selectedValues.Count==2,"planner sheet Cancel preserves original people");nav.Back();yield return null;
            e.templates.SaveDraft(new ActivityTemplate{name="From chosen date",activity=new CalendarActivity{title="Karate template",icon="karate",zone="device"}},false);
            nav.ShowCalendar();var day=CalendarGenerator.Generate(new DateTime(2026,10,1),session.CurrentGroup,session.CalendarOverrides).Find(d=>d.dateKey=="2026-10-19");var popup=work.calendar.GetComponentInChildren<DayDetailsPopup>(true);popup.Show(day);yield return null;
            var add=popup.GetComponentsInChildren<Button>(true).First(b=>Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Any(i=>b.onClick.GetPersistentMethodName(i)=="AddEvent"));var addClick=Click((RectTransform)add.transform,check,"Day Details Add Activity");while(addClick.MoveNext())yield return null;
            check(sheet.IsOpen&&sheet.Owner==e.templates.quickStart,"real Add Activity button opens quick-start sheet");
            var quickRows=sheet.content.GetComponentsInChildren<SelectionSheetRow>();var templateChoice=Click((RectTransform)quickRows[3].transform,check,"Saved templates quick start");while(templateChoice.MoveNext())yield return null;
            check(sheet.IsOpen&&sheet.Owner==e.templates.picker,"quick-start opens saved template picker");var templateRows=sheet.content.GetComponentsInChildren<SelectionSheetRow>();var use=Click((RectTransform)templateRows[0].transform,check,"template Use from calendar date");while(use.MoveNext())yield return null;
            check(e.Draft.dateKey=="2026-10-19"&&e.Draft.id!="template"&&e.Draft.icon=="karate"&&!e.alarm.isOn,"template keeps date context and icon without opting into an alarm");nav.Back();yield return null;
        }
        nav.ShowCalendar();yield return null;var filterChoice=Choice(work.filter,4,check,"Calendar person filter");while(filterChoice.MoveNext())yield return null;check(DeviceCalendarStore.State.filter=="qol-child","calendar filter sheet saves selected stable person ID");
        var me=session.Data.profiles.Find(p=>p.id=="qol-me");me.linkedUid=null;e.New("2026-10-19");e.title.text="Keep this draft";e.Claim();yield return null;
        check(e.profileRoute.gameObject.activeSelf&&e.message.text.Contains("owner"),"unlinked Me offers correct profile configuration route");e.Profiles();yield return null;nav.Back();yield return null;check(e.panel.activeSelf&&e.title.text=="Keep this draft","profile configuration Back restores unsaved activity draft");nav.Back();yield return null;nav.confirmation.Accept();yield return null;
        var pattern=session.CurrentGroup.pattern;session.CurrentGroup.pattern=new List<int>{1};work.showAllDates.isOn=false;work.ShowAgenda();yield return null;
        check(!work.agendaContent.GetComponentsInChildren<Text>().Any(t=>t.fontStyle==FontStyle.Bold),"fully empty OFF agenda collapses empty date headings");work.showAllDates.isOn=true;yield return null;check(work.agendaContent.GetComponentsInChildren<Text>().Count(t=>t.fontStyle==FontStyle.Bold)==7,"Show all dates restores seven chronological headings");session.CurrentGroup.pattern=pattern;
        session.SwitchAccount(account);yield return null;foreach(string file in Directory.GetFiles(Application.persistentDataPath,Path.GetFileName(ScheduleStorage.PathFor(test))+"*"))File.Delete(file);
    }
}
