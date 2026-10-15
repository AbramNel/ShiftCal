using ShiftCal.UI;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
public static partial class ShiftCalSceneBuilder
{
    static void CreateWorkedBadge(CalendarDayCell cell,Transform root,int sibling)
    {
        var badge=CreatePanel("Actual worked hours",root,Input);cell.workedBadge=badge;badge.GetComponent<Image>().sprite=Rounded;badge.GetComponent<Image>().type=Image.Type.Sliced;badge.GetComponent<Image>().raycastTarget=false;
        AnchorStretch(badge.GetComponent<RectTransform>(),0,0,1,0,8,48,-8,86);badge.transform.SetSiblingIndex(sibling);
        var clock=CreateImage("Actual hours clock",badge.transform,TextDark);clock.sprite=Icon("Clock");clock.raycastTarget=false;AnchorStretch(clock.rectTransform,0,.5f,0,.5f,4,-13,30,13);
        cell.workedLabel=CreateText("Actual hours value",badge.transform,"11.75h",28,TextDark,TextAnchor.MiddleCenter,FontStyle.Bold);cell.workedLabel.raycastTarget=false;AnchorStretch(cell.workedLabel.rectTransform,0,0,1,1,32,0,-2,0);
        cell.workedClock=clock;
        badge.SetActive(false);
    }
    static void CreateWorkedHoursUI(Transform root)
    {
        var overlay=CreatePanel("Worked Hours",root,Hex("#000000AA"));Stretch(overlay.GetComponent<RectTransform>());overlay.GetComponent<Image>().raycastTarget=true;overlay.AddComponent<ModalPanel>();overlay.AddComponent<KeyboardAvoidance>();
        var editor=overlay.AddComponent<WorkedHoursEditor>();
        var card=CreatePanel("Hours card",overlay.transform,Card);card.GetComponent<Image>().raycastTarget=true;AnchorStretch(card.GetComponent<RectTransform>(),0,.5f,1,.5f,24,-330,-24,330);
        var content=CreateVerticalGroup("Hours fields",card.transform,20,28,28,18,18);AnchorStretch(content.GetComponent<RectTransform>(),0,0,1,1,0,160,0,-130);
        editor.heading=CreateText("Hours heading",card.transform,"Hours worked",40,TextDark,TextAnchor.MiddleLeft,FontStyle.Bold);AnchorStretch(editor.heading.rectTransform,0,1,1,1,28,-126,-150,-14);
        var close=Command(card.transform,"×",editor.Cancel);AnchorStretch(close.GetComponent<RectTransform>(),1,1,1,1,-138,-124,-18,-4);
        editor.input=CompactField(content.transform,"actualHours","Actual hours (0–24)");editor.input.contentType=InputField.ContentType.DecimalNumber;
        editor.message=Body(content.transform,"Personal · only on this device",100);editor.message.fontSize=30;
        var actions=CreateHorizontalGroup("Hours actions",card.transform,16,20,20,12,12);AnchorStretch(actions.GetComponent<RectTransform>(),0,0,1,0,0,12,0,152);
        foreach(var button in new[]{Command(actions.transform,"Clear",editor.Clear),Command(actions.transform,"Cancel",editor.Cancel),Command(actions.transform,"Save",editor.Save)})AddLayoutElement(button.gameObject,0,132,1);
        editor.monthSummary=StandaloneSelection(root,"Monthly actual hours",new[]{"No entries"});overlay.SetActive(false);
    }
}
