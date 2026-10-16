using System;
using ShiftCal.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
public static partial class ShiftCalSceneBuilder
{
    static SelectionField StandaloneSelection(Transform parent,string title,string[] options)
    {
        var go=CreateUiRoot(title+" action");go.transform.SetParent(parent,false);var field=go.AddComponent<SelectionField>();field.button=go.AddComponent<Button>();field.heading=title;field.AddOptions(new System.Collections.Generic.List<string>(options));go.SetActive(false);return field;
    }
    static Transform BuildSaveStatus(Transform parent)
    {
        var row=CreateHorizontalGroup("Save and sync status",parent,12,12,12,8,8);AddLayoutElement(row,-1,110);var status=row.AddComponent<SaveSyncStatus>();status.label=Body(row.transform,"Saved on this device",-1);status.label.fontSize=30;status.label.color=TextMuted;AddLayoutElement(status.label.gameObject,0,-1,1);status.retry=Command(row.transform,"Retry",status.Retry);AddLayoutElement(status.retry.gameObject,180,100);status.retry.GetComponentInChildren<Text>().fontSize=30;status.retry.gameObject.SetActive(false);return row.transform;
    }
    static void BuildSelectionSheet(Transform parent)
    {
        var overlay=CreatePanel("Selection Sheet",parent,Hex("#00000099"));Stretch(overlay.GetComponent<RectTransform>());overlay.GetComponent<Image>().raycastTarget=true;
        var modal=overlay.AddComponent<ModalPanel>();var sheet=overlay.AddComponent<SelectionSheet>();
        var card=CreatePanel("Selection card",overlay.transform,Card);card.GetComponent<Image>().sprite=Rounded;card.GetComponent<Image>().type=Image.Type.Sliced;card.GetComponent<Image>().raycastTarget=true;
        sheet.card=card.GetComponent<RectTransform>();sheet.card.pivot=new Vector2(.5f,0);AnchorStretch(sheet.card,0,0,1,0,18,18,-18,1200);
        sheet.heading=CreateText("Selection heading",card.transform,"Choose",42,TextDark,TextAnchor.MiddleLeft,FontStyle.Bold);AnchorStretch(sheet.heading.rectTransform,0,1,1,1,30,-130,-150,-20);
        var close=Command(card.transform,"",sheet.Cancel);AnchorStretch(close.GetComponent<RectTransform>(),1,1,1,1,-138,-124,-22,-16);var x=CreateImage("Close icon",close.transform,Primary);x.sprite=NavigationIcon("close");x.raycastTarget=false;AnchorStretch(x.rectTransform,.5f,.5f,.5f,.5f,-23,-23,23,23);
        sheet.content=CreateScrollContent("Selection choices",card.transform,12,20,20,12,12,out var scroll);sheet.scroll=scroll;AnchorStretch(scroll.GetComponent<RectTransform>(),0,0,1,1,8,162,-8,-148);
        var footer=CreateHorizontalGroup("Selection actions",card.transform,18,24,24,12,12);AnchorStretch(footer.GetComponent<RectTransform>(),0,0,1,0,0,12,0,150);
        sheet.actions=footer;
        var cancel=Command(footer.transform,"Cancel",sheet.Cancel);AddLayoutElement(cancel.gameObject,0,120,1);sheet.done=Command(footer.transform,"Done",sheet.Accept);AddLayoutElement(sheet.done.gameObject,0,120,1);sheet.done.GetComponent<Image>().color=Primary;sheet.done.GetComponentInChildren<Text>().color=Hex("#04111F");
        var row=CreateHorizontalGroup("SelectionSheetRow",null,16,22,22,16,16);row.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;var bg=row.AddComponent<Image>();bg.sprite=Rounded;bg.type=Image.Type.Sliced;bg.color=Input;
        var view=row.AddComponent<SelectionSheetRow>();view.button=row.AddComponent<Button>();view.button.targetGraphic=bg;
        AddLayoutElement(row,-1,-1);row.GetComponent<LayoutElement>().minHeight=132;
        view.swatch=CreateImage("Selection color",row.transform,Color.white);view.swatch.sprite=Rounded;AddLayoutElement(view.swatch.gameObject,44,44);view.swatch.raycastTarget=false;
        view.icon=CreateImage("Choice icon",row.transform,Primary);AddLayoutElement(view.icon.gameObject,66,66);view.icon.preserveAspect=true;view.icon.raycastTarget=false;
        var labels=CreateVerticalGroup("Choice labels",row.transform,6,0,0,0,0);AddLayoutElement(labels,0,-1,1);view.label=Body(labels.transform,"Choice",-1);view.label.fontSize=38;view.label.raycastTarget=false;view.description=Body(labels.transform,"Description",-1);view.description.fontSize=30;view.description.color=TextMuted;view.description.raycastTarget=false;
        view.selected=CreateImage("Choice selected",row.transform,Primary);view.selected.sprite=NavigationIcon("check");view.selected.raycastTarget=false;view.selected.preserveAspect=true;AddLayoutElement(view.selected.gameObject,44,44);
        BakeThemes(row.transform);Object.DestroyImmediate(view.swatch.GetComponent<ThemeManager>());sheet.rowPrefab=PrefabUtility.SaveAsPrefabAsset(row,PrefabFolder+"/SelectionSheetRow.prefab").GetComponent<SelectionSheetRow>();Object.DestroyImmediate(row);overlay.SetActive(false);
    }
    static InputField Stepper(Transform parent,string key,string label,int max=365)
    {
        Body(parent,label,56);var row=CreateHorizontalGroup(key+" stepper",parent,16,0,0,0,0);AddLayoutElement(row,-1,120);var step=row.AddComponent<NumberStepper>();step.maximum=max;
        var less=Command(row.transform,"−",step.Less);AddLayoutElement(less.gameObject,120,120);
        step.input=CreateInputField(key,row.transform,"1");step.input.contentType=InputField.ContentType.IntegerNumber;AddLayoutElement(step.input.gameObject,0,120,1);
        var more=Command(row.transform,"+",step.More);AddLayoutElement(more.gameObject,120,120);return step.input;
    }
    static void AttachSelectionSwatch(SelectionField field)
    {
        field.swatch=CreateImage("Selection color",field.transform,Color.white);field.swatch.raycastTarget=false;field.swatch.sprite=Rounded;AnchorStretch(field.swatch.rectTransform,0,.5f,0,.5f,20,-20,60,20);field.captionText.rectTransform.offsetMin=new Vector2(76,8);
    }
}
