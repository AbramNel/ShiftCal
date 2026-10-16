using ShiftCal.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
public static partial class ShiftCalSceneBuilder
{
    static SelectionField InlineChoice(Transform parent,string label,Sprite icon)
    {
        var holder=CreateVerticalGroup(label+" holder",parent,0,0,0,0,0);AddLayoutElement(holder,0,132,1);
        var field=Choice(holder.transform,label,new[]{"Everyone"});Object.DestroyImmediate(holder.transform.GetChild(0).gameObject);field.GetComponent<LayoutElement>().preferredHeight=132;
        var image=CreateImage("Filter icon",field.transform,Primary);image.sprite=icon;image.raycastTarget=false;AnchorStretch(image.rectTransform,0,.5f,0,.5f,20,-22,64,22);field.captionText.rectTransform.offsetMin=new Vector2(84,8);return field;
    }
    static void CreateAgendaPresentation(FamilyWorkbench work,Transform root)
    {
        work.agendaPanel=CreateScreen("Family Agenda",root);CreateTopBar(work.agendaPanel.transform,"Upcoming");BackButton(work.agendaPanel);
        var heading=FindChild(work.agendaPanel.transform,"Top Bar Title").GetComponent<Text>();AnchorStretch(heading.rectTransform,0,1,1,1,32,-82,-230,-18);
        var subtitle=CreateText("Agenda range",work.agendaPanel.transform,"Next 7 days",30,TextMuted,TextAnchor.MiddleLeft,FontStyle.Normal);AnchorStretch(subtitle.rectTransform,0,1,1,1,34,-140,-230,-94);
        var tools=CreateHorizontalGroup("Agenda tools",work.agendaPanel.transform,18,0,0,0,0);AnchorStretch(tools.GetComponent<RectTransform>(),0,1,1,1,24,-306,-24,-174);
        work.agendaFilter=InlineChoice(tools.transform,"Show events",NavigationIcon("filter"));var add=Command(tools.transform,"Event",work.NewAgendaEvent);AddLayoutElement(add.gameObject,270,132);add.GetComponentInChildren<Text>().rectTransform.offsetMin=new Vector2(64,4);
        var plus=CreateImage("Add event icon",add.transform,Primary);plus.sprite=NavigationIcon("plus");plus.raycastTarget=false;AnchorStretch(plus.rectTransform,0,.5f,0,.5f,22,-20,62,20);
        var options=CreatePanel("Agenda date options",work.agendaPanel.transform,Color.clear);AnchorStretch(options.GetComponent<RectTransform>(),0,1,1,1,32,-450,-32,-318);
        work.showAllDates=Check(options.transform,"Show all dates",false,132);Stretch(work.showAllDates.transform.parent.GetComponent<RectTransform>());var row=work.showAllDates.transform.parent.gameObject;var click=row.AddComponent<Button>();var hit=row.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;click.targetGraphic=hit;UnityEventTools.AddPersistentListener(click.onClick,work.ToggleDates);
        work.agendaContent=CreateScrollContent("Agenda days",work.agendaPanel.transform,18,24,24,12,24,out var scroll);AnchorStretch(scroll.GetComponent<RectTransform>(),0,0,1,1,0,12,0,-462);
        work.dayCardPrefab=BuildAgendaDayCard();work.alarmIcon=Icon("Alarm");work.eventIcon=NavigationIcon("calendar");work.agendaPanel.SetActive(false);
    }
    static AgendaDayCard BuildAgendaDayCard()
    {
        var root=CreateVerticalGroup("AgendaDayCard",null,8,16,16,12,12);root.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;var image=root.AddComponent<Image>();image.sprite=Rounded;image.type=Image.Type.Sliced;image.color=Card;image.raycastTarget=true;
        var card=root.AddComponent<AgendaDayCard>();card.button=root.AddComponent<Button>();card.button.targetGraphic=image;
        var heading=CreatePanel("Day heading",root.transform,Color.clear);AddLayoutElement(heading,-1,108);
        card.date=CreateText("Agenda summary",heading.transform,"Thu, Oct 15",36,TextDark,TextAnchor.MiddleLeft,FontStyle.Bold);AnchorStretch(card.date.rectTransform,0,0,0,1,8,0,338,0);
        card.shift=CreateText("Agenda shift",heading.transform,"Days\n5:30 AM – 5:30 PM",28,TextMuted,TextAnchor.MiddleRight,FontStyle.Normal);AnchorStretch(card.shift.rectTransform,0,0,1,1,352,0,-48,0);
        card.swatch=CreateImage("Shift swatch",heading.transform,Primary);card.swatch.sprite=Rounded;card.swatch.raycastTarget=false;AnchorStretch(card.swatch.rectTransform,1,.5f,1,.5f,-32,-11,-10,11);
        card.entries=Section(root.transform,"Day entries").transform;card.entries.GetComponent<VerticalLayoutGroup>().spacing=8;
        BakeThemes(root.transform);Object.DestroyImmediate(card.swatch.GetComponent<ThemeManager>());var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabFolder+"/AgendaDayCard.prefab").GetComponent<AgendaDayCard>();Object.DestroyImmediate(root);return prefab;
    }
    static void CreateCalendarTools(FamilyWorkbench work,GameObject calendar)
    {
        var tools=CreateHorizontalGroup("Family filter",calendar.transform,16,0,0,0,0);AnchorStretch(tools.GetComponent<RectTransform>(),0,1,1,1,24,-484,-24,-352);
        work.filter=InlineChoice(tools.transform,"Show events",NavigationIcon("filter"));var agenda=Command(tools.transform,"Agenda",work.ShowAgenda);AddLayoutElement(agenda.gameObject,270,132);agenda.GetComponentInChildren<Text>().rectTransform.offsetMin=new Vector2(72,4);
        var icon=CreateImage("Agenda icon",agenda.transform,Primary);icon.sprite=NavigationIcon("agenda");icon.raycastTarget=false;AnchorStretch(icon.rectTransform,0,.5f,0,.5f,18,-22,62,22);
        AnchorStretch(FindChild(calendar.transform,"Month Bar").GetComponent<RectTransform>(),0,1,1,1,24,-340,-24,-144);
        AnchorStretch(FindChild(calendar.transform,"Weekday Row").GetComponent<RectTransform>(),0,1,1,1,24,-536,-24,-494);
        var grid=FindChild(calendar.transform,"Permanent Calendar Grid").GetComponent<RectTransform>();grid.offsetMax=new Vector2(grid.offsetMax.x,-544);
        var top=FindChild(calendar.transform,"Top App Bar").GetComponent<RectTransform>();top.offsetMin=new Vector2(0,-132);
    }
}
