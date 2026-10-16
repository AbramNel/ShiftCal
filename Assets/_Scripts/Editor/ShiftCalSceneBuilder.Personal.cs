using ShiftCal.UI;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
public static partial class ShiftCalSceneBuilder
{
    static Sprite TimesheetSprite=>Artwork("WorkedHoursTimesheet",(x,y)=>{
        var p=new Vector2(x,y);return Line(p,13,10,51,10,4)||Line(p,13,10,13,52,4)||Line(p,51,10,51,52,4)||Line(p,13,52,22,52,4)||Line(p,42,52,51,52,4)||Line(p,23,51,23,58,4)||Line(p,23,58,41,58,4)||Line(p,41,58,41,51,4)||Line(p,22,39,41,39,4)||Line(p,22,29,33,29,4)||Line(p,25,20,31,15,4)||Line(p,31,15,43,27,4)?1:0;
    },Vector4.zero);
    static ContentPill HoursPill(Transform parent,string name,UnityEngine.Events.UnityAction action,bool edit=false)
    {
        var button=CreateButton(name,parent,"Add hours",Vector2.zero,Vector2.zero,Input,Primary,34);var pill=button.gameObject.AddComponent<ContentPill>();pill.button=button;pill.label=button.GetComponentInChildren<Text>();pill.label.alignment=TextAnchor.MiddleLeft;pill.label.horizontalOverflow=HorizontalWrapMode.Overflow;AnchorStretch(pill.label.rectTransform,0,0,1,1,80,4,-22,-4);
        pill.icon=CreateImage("Timesheet icon",button.transform,Primary);pill.icon.sprite=TimesheetSprite;pill.icon.raycastTarget=false;AnchorStretch(pill.icon.rectTransform,0,.5f,0,.5f,20,-24,64,24);
        if(edit){pill.edit=CreateImage("Edit hours pencil",button.transform,Primary);pill.edit.sprite=NavigationIcon("pencil");pill.edit.raycastTarget=false;AnchorStretch(pill.edit.rectTransform,1,.5f,1,.5f,-58,-18,-22,18);}
        UnityEventTools.AddPersistentListener(button.onClick,action);return pill;
    }
    static void CreateWorkedBadge(CalendarDayCell cell,Transform root,int sibling)
    {
        var badge=CreatePanel("Actual worked hours",root,Input);cell.workedBadge=badge;badge.GetComponent<Image>().sprite=Rounded;badge.GetComponent<Image>().type=Image.Type.Sliced;badge.GetComponent<Image>().raycastTarget=false;
        AnchorStretch(badge.GetComponent<RectTransform>(),0,0,1,0,8,44,-8,78);badge.transform.SetSiblingIndex(sibling);
        var clock=CreateImage("Actual hours timesheet",badge.transform,TextDark);clock.sprite=TimesheetSprite;clock.raycastTarget=false;AnchorStretch(clock.rectTransform,0,.5f,0,.5f,3,-15,33,15);
        cell.workedLabel=CreateText("Actual hours value",badge.transform,"11.75h",28,TextDark,TextAnchor.MiddleCenter,FontStyle.Bold);cell.workedLabel.raycastTarget=false;AnchorStretch(cell.workedLabel.rectTransform,0,0,1,1,34,0,-2,0);
        cell.workedClock=clock;
        badge.SetActive(false);
    }
    static void CreateWorkedHoursUI(Transform root)
    {
        var overlay=CreatePanel("Worked Hours",root,Hex("#000000AA"));Stretch(overlay.GetComponent<RectTransform>());overlay.GetComponent<Image>().raycastTarget=true;overlay.AddComponent<ModalPanel>();overlay.AddComponent<KeyboardAvoidance>();
        var editor=overlay.AddComponent<WorkedHoursEditor>();
        var card=CreatePanel("Hours card",overlay.transform,Card);card.GetComponent<Image>().raycastTarget=true;AnchorStretch(card.GetComponent<RectTransform>(),0,.5f,1,.5f,24,-330,-24,330);
        var content=CreateVerticalGroup("Hours fields",card.transform,20,28,28,18,18);AnchorStretch(content.GetComponent<RectTransform>(),0,0,1,1,0,160,0,-130);
        editor.heading=CreateText("Hours heading",card.transform,"Hours worked",40,TextDark,TextAnchor.MiddleLeft,FontStyle.Bold);AnchorStretch(editor.heading.rectTransform,0,1,1,1,88,-126,-150,-14);
        var icon=CreateImage("Hours editor timesheet",card.transform,Primary);icon.sprite=TimesheetSprite;icon.raycastTarget=false;AnchorStretch(icon.rectTransform,0,1,0,1,28,-88,72,-44);editor.hoursIcon=TimesheetSprite;
        var close=Command(card.transform,"×",editor.Cancel);AnchorStretch(close.GetComponent<RectTransform>(),1,1,1,1,-138,-124,-18,-4);
        editor.input=CompactField(content.transform,"actualHours","Actual hours (0–24)");editor.input.contentType=InputField.ContentType.DecimalNumber;
        editor.message=Body(content.transform,"Personal · only on this device",100);editor.message.fontSize=30;
        var actions=CreateHorizontalGroup("Hours actions",card.transform,16,20,20,12,12);AnchorStretch(actions.GetComponent<RectTransform>(),0,0,1,0,0,12,0,152);
        foreach(var button in new[]{Command(actions.transform,"Clear",editor.Clear),Command(actions.transform,"Cancel",editor.Cancel),Command(actions.transform,"Save",editor.Save)})AddLayoutElement(button.gameObject,0,132,1);
        editor.monthSummary=StandaloneSelection(root,"Monthly actual hours",new[]{"No entries"});overlay.SetActive(false);
    }
}
