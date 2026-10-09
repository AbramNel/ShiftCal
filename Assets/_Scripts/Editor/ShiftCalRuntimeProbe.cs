using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShiftCal.App;
using ShiftCal.Core;
using ShiftCal.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShiftCalRuntimeProbe
{
    private readonly List<string> errors = new List<string>();
    private int checks;
    public ShiftCalRuntimeProbe() => Application.logMessageReceived += Log;
    private void Log(string message,string trace,LogType type) { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert) errors.Add(message); }
    private void Check(bool ok,string message) { if(!ok)errors.Add(message);else checks++; }
    public IEnumerator Run()
    {
        for(int i=0;i<8;i++)yield return null;
        var nav=AppNavigation.Instance;var cal=UnityEngine.Object.FindFirstObjectByType<CalendarController>(FindObjectsInactive.Include);
        var work=UnityEngine.Object.FindFirstObjectByType<ScheduleWorkbench>();var settings=UnityEngine.Object.FindFirstObjectByType<ShiftSettingsController>(FindObjectsInactive.Include);
        nav.ShowCalendar();yield return null;nav.ShowSettings();yield return null;
        var preferences=nav.GetComponentInChildren<AlarmPreferencesPanel>(true);
        preferences.Load();preferences.upcoming.isOn=true;preferences.upcoming.isOn=false;yield return null;
        Check(!DeviceAlarmPreferences.Current.upcomingNotices&&!preferences.noticeSection.activeSelf,"upcoming toggle immediately persists on this device");
        preferences.upcoming.isOn=true;preferences.notice.choice.value=2;preferences.snooze.choice.value=1;yield return null;
        Check(DeviceAlarmPreferences.Current.upcomingNotices&&DeviceAlarmPreferences.Current.noticeMinutes==15&&DeviceAlarmPreferences.Current.snoozeMinutes==10,"global notice and snooze preset controls persist");
        work.NewEvent("2026-10-08");yield return null;
        var datePicker=UnityEngine.Object.FindFirstObjectByType<UnityPickerDialog>(FindObjectsInactive.Include);
        work.eventDate.GetComponent<Button>().onClick.Invoke();yield return null;
        Check(PickerCoordinator.IsOpen&&datePicker.dateSection.activeSelf&&EventSystem.current.currentSelectedGameObject?.GetComponent<InputField>()==null,"real date button opens picker without selecting input");
        nav.Back();yield return null;Check(!PickerCoordinator.IsOpen&&work.eventDate.value=="2026-10-08","Back cancels picker without editing date");
        work.recurrence.value=2;yield return null;
        var chip=work.weekdays[0];var chipRect=(RectTransform)chip.transform;
        var formScroll=work.eventPanel.GetComponentInChildren<ScrollRect>();
        var localChip=formScroll.viewport.InverseTransformPoint(chipRect.TransformPoint(chipRect.rect.center));
        formScroll.content.anchoredPosition-=new Vector2(0,localChip.y-formScroll.viewport.rect.center.y);yield return null;yield return null;
        var chipPoint=RectTransformUtility.WorldToScreenPoint(null,chipRect.TransformPoint(chipRect.rect.center));
        var chipPointer=new PointerEventData(EventSystem.current){position=chipPoint,button=PointerEventData.InputButton.Left};
        var chipHits=new List<RaycastResult>();EventSystem.current.RaycastAll(chipPointer,chipHits);
        Check(chipHits.Count>0&&chipHits[0].gameObject.GetComponentInParent<Toggle>()==chip,"weekday chip receives real pointer raycast");
        bool selected=chip.isOn;ExecuteEvents.Execute(chip.gameObject,chipPointer,ExecuteEvents.pointerClickHandler);yield return null;Check(chip.isOn!=selected,"weekday pointer click toggles selection");
        nav.Back();yield return null;nav.confirmation.Accept();yield return null;
        foreach(var theme in new[]{ThemeManager.Theme.MidnightGraphite,ThemeManager.Theme.DeepTeal,ThemeManager.Theme.SoftDaylight,ThemeManager.Theme.MidnightGraphite,ThemeManager.Theme.DeepTeal,ThemeManager.Theme.SoftDaylight})
        {
            var sample=nav.GetComponentsInChildren<ThemeSample>(true).Single(t=>t.choice==theme);var button=sample.GetComponent<Button>();
            var rect=(RectTransform)button.transform;var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            var pointer=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<ThemeSample>()==sample,"theme preview receives real raycast: "+theme);
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);yield return null;yield return null;
            Check(ThemeManager.Current==theme&&sample.selectedBorder.enabled&&PlayerPrefs.GetInt(ThemeManager.PreferenceKey)==(int)theme,"runtime theme tap applies/persists: "+theme);
            nav.ShowManageShifts();yield return null;settings.editor.Show(settings,AppSession.Instance.CurrentGroup.shiftTypes.First());yield return null;
            nav.Back();yield return null;nav.Back();yield return null;Check(nav.CurrentScreen.name=="Settings Screen","Manage Shifts returns to Settings");
            nav.ShowAccount();yield return null;nav.Back();yield return null;Check(!nav.transform.Find("Account Popup").gameObject.activeSelf,"Account modal removes blocker");
            work.ShowAgenda();yield return null;work.NewEvent();yield return null;work.SaveEvent();yield return null;
            var footer=work.eventPanel.GetComponentsInChildren<RectTransform>().First(t=>t.name=="Editor actions");var footerCorners=new Vector3[4];var messageCorners=new Vector3[4];footer.GetWorldCorners(footerCorners);work.messageLabel.rectTransform.GetWorldCorners(messageCorners);
            Check(messageCorners[0].y>=footerCorners[1].y,"editor validation stays above Save");nav.Back();yield return null;
            Check(!work.eventPanel.activeSelf,"unchanged event closes through Back");nav.Back();yield return null;nav.Back();yield return null;
            var date=DateTime.Today;var day=CalendarGenerator.Generate(new DateTime(date.Year,date.Month,1),AppSession.Instance.CurrentGroup,AppSession.Instance.CalendarOverrides).First(d=>d.date.Date==date);
            var popup=cal.GetComponentInChildren<DayDetailsPopup>(true);cal.preview.Show(day,new DayInformation());yield return null;nav.Back();yield return null;
            popup.Show(day);yield return null;popup.ChangeShift();yield return null;
            Check(!cal.IsEditing&&cal.shiftPicker.FocusedDate==date,"runtime single-day picker scope");
            var picker=cal.shiftPicker;picker.dayButtons[11].onClick.Invoke();yield return null;yield return null;
            Check(picker.FocusedDate==date.AddDays(1),"runtime neighboring date tap");
            var centered=(RectTransform)picker.dayButtons[10].transform;var neighbor=(RectTransform)picker.dayButtons[11].transform;
            var start=RectTransformUtility.WorldToScreenPoint(null,centered.TransformPoint(centered.rect.center));var adjacent=RectTransformUtility.WorldToScreenPoint(null,neighbor.TransformPoint(neighbor.rect.center));
            var swipe=new PointerEventData(EventSystem.current){position=start,pressPosition=start,button=PointerEventData.InputButton.Left};
            ExecuteEvents.Execute(picker.navigator.gameObject,swipe,ExecuteEvents.initializePotentialDrag);ExecuteEvents.Execute(picker.navigator.gameObject,swipe,ExecuteEvents.beginDragHandler);
            swipe.position=start-new Vector2(adjacent.x-start.x,0);ExecuteEvents.Execute(picker.navigator.gameObject,swipe,ExecuteEvents.dragHandler);ExecuteEvents.Execute(picker.navigator.gameObject,swipe,ExecuteEvents.endDragHandler);yield return null;yield return null;
            Check(picker.FocusedDate==date.AddDays(2),"runtime horizontal swipe focuses adjacent date");
            nav.Back();yield return null;Check(popup.gameObject.activeSelf&&!picker.gameObject.activeSelf,"picker closes back to day editor");nav.Back();yield return null;
            cal.SetEditing(true);cal.BeginDaySelection(DateKeyUtility.ToDateKey(date));cal.ExtendDaySelection(DateKeyUtility.ToDateKey(date.AddDays(3)));cal.EndDaySelection(DateKeyUtility.ToDateKey(date.AddDays(3)));yield return null;
            cal.OpenShiftPickerForSelection();yield return null;yield return null;
            var choiceScroll=picker.choices.GetComponentInParent<ScrollRect>();
            Check(choiceScroll.verticalNormalizedPosition>.99f&&picker.choices.GetComponentsInChildren<ShiftChoiceRow>().Length>0,"runtime bulk picker reopens at visible choices");nav.Back();yield return null;nav.Back();yield return null;Check(!cal.IsEditing&&cal.SelectedCount==0,"runtime Back exits edit selection");
            cal.NextMonth();yield return null;cal.PrevMonth();yield return null;nav.ShowSettings();yield return null;
        }
        var family=FamilyRuntimeChecks.Exercise(Check);while(family.MoveNext())yield return null;
        for(int i=0;i<8;i++)yield return null;
        Application.logMessageReceived -= Log;
        foreach(var error in errors)Debug.LogWarning("UI runtime failure: "+error);
        Debug.Log("UI runtime assertions: "+checks+"; errors: "+errors.Count);
        ShiftCalRuntimeChecks.Finish(errors.Count==0);
    }
}
