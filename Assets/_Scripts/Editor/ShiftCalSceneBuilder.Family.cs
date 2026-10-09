using System;
using System.Collections.Generic;
using ShiftCal.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
public static partial class ShiftCalSceneBuilder
{
    static void NormalizeSavedYaml()
    {
        foreach(string path in System.IO.Directory.GetFiles(PrefabFolder,"*.prefab"))TrimYaml(path);TrimYaml(ScenePath);
    }
    static void TrimYaml(string path){var lines=System.IO.File.ReadAllLines(path);for(int i=0;i<lines.Length;i++)lines[i]=lines[i].TrimEnd();System.IO.File.WriteAllLines(path,lines,new System.Text.UTF8Encoding(false));}
    private static void CreateFamilyUI(Transform root,GameObject settings,GameObject calendarScreen)
    {
        BuildActivityIcons();
        var work=root.gameObject.AddComponent<FamilyWorkbench>();work.calendar=calendarScreen.GetComponent<CalendarController>();
        var gesture=calendarScreen.AddComponent<CalendarGestureController>();gesture.calendar=work.calendar;work.calendar.gesture=gesture;
        var editor=root.gameObject.AddComponent<ActivityEditor>();work.editor=editor;editor.workbench=work;
        work.rowPrefab=BuildFamilyRow();
        work.agendaPanel=CreateScreen("Family Agenda",root);var agenda=ScreenContent(work.agendaPanel,"Next seven days");BackButton(work.agendaPanel);
        work.agendaFilter=Choice(agenda,"Show activities",new[]{"Everyone"});Command(agenda,"Add activity / note",work.NewActivity);work.agendaContent=Section(agenda,"Seven day entries").transform;work.agendaPanel.SetActive(false);

        var filterRow=CreateHorizontalGroup("Family filter",calendarScreen.transform,12,0,0,0,0);AnchorStretch(filterRow.GetComponent<RectTransform>(),0,1,1,1,24,-426,-24,-286);
        var filterContainer=Section(filterRow.transform,"Filter selection");AddLayoutElement(filterContainer,0,140,1);work.filter=Choice(filterContainer.transform,"Show activities",new[]{"Everyone"});Object.DestroyImmediate(filterContainer.transform.GetChild(0).gameObject);work.filter.transform.GetComponent<LayoutElement>().preferredHeight=132;var filterIcon=CreateImage("Filter sprite",work.filter.transform,Primary);filterIcon.sprite=NavigationIcon("filter");filterIcon.raycastTarget=false;AnchorStretch(filterIcon.rectTransform,0,.5f,0,.5f,16,-18,52,18);work.filter.captionText.rectTransform.offsetMin=new Vector2(64,8);
        var agendaButton=Command(filterRow.transform,"Agenda",work.ShowAgenda);AddLayoutElement(agendaButton.gameObject,246,132);
        AnchorStretch(FindChild(calendarScreen.transform,"Month Bar").GetComponent<RectTransform>(),0,1,1,1,24,-286,-24,-164);
        AnchorStretch(FindChild(calendarScreen.transform,"Weekday Row").GetComponent<RectTransform>(),0,1,1,1,24,-488,-24,-438);
        var grid=FindChild(calendarScreen.transform,"Permanent Calendar Grid").GetComponent<RectTransform>();grid.offsetMax=new Vector2(grid.offsetMax.x,-498);
        var dayPopup=calendarScreen.GetComponentInChildren<DayDetailsPopup>(true);dayPopup.family=work;
        var dayContent=FindChild(dayPopup.transform,"Day Details Content");dayPopup.activityContent=Section(dayContent,"Day activities").transform;dayPopup.activityContent.SetSiblingIndex(4);

        editor.panel=CreateScreen("Activity Editor",root);editor.panel.GetComponent<Image>().raycastTarget=true;var form=ScreenContent(editor.panel,"Activity / note");EditorFooter(editor.panel,editor.Commit,editor.Cancel);FindChild(editor.panel.transform,"Save Event").GetComponentInChildren<Text>().text="Save activity";
        editor.title=CompactField(form,"activityTitle","Title");editor.notes=CompactField(form,"activityNotes","Notes (optional)",true);
        var iconRow=CreateHorizontalGroup("Activity icon preview",form,20,0,0,0,0);AddLayoutElement(iconRow,-1,132);
        editor.preview=CreateImage("Activity preview",iconRow.transform,Primary);AddLayoutElement(editor.preview.gameObject,88,88);editor.preview.preserveAspect=true;
        editor.iconName=Body(iconRow.transform,"Activity",100);AddLayoutElement(editor.iconName.gameObject,0,-1,1);var pick=Command(iconRow.transform,"Choose icon",editor.Icons);AddLayoutElement(pick.gameObject,330,120);
        editor.date=Picker(form,"Activity date",true);editor.allDay=Check(form,"All day",false,90);editor.timed=Check(form,"Timed",false,90);
        editor.timeSection=Section(form,"Activity timing");editor.time=Picker(editor.timeSection.transform,"Activity time",false,true);editor.duration=Duration(editor.timeSection.transform,"Duration",new[]{0,15,30,45,60,90,120},10080);editor.duration.minimum=0;
        editor.assignee=Choice(form,"Assigned to",new[]{"Unassigned"});var assign=CreateHorizontalGroup("Assignment actions",form,16,0,0,0,0);AddLayoutElement(assign,-1,120);
        foreach(var b in new[]{Command(assign.transform,"Assign to me",editor.Claim),Command(assign.transform,"Unassign",editor.Unassign)})AddLayoutElement(b.gameObject,0,120,1);
        editor.participantContent=Expandable(form,"Participants");editor.participantPrefab=BuildParticipantRow();
        editor.recurrence=Choice(form,"Repeat",new[]{"Once","Daily","Weekly","Monthly"});
        editor.repeatSection=Section(form,"Activity repeat settings");editor.interval=CompactField(editor.repeatSection.transform,"activityInterval","Every N days / weeks / months",false,true);
        editor.weekdaySection=Section(editor.repeatSection.transform,"Activity weekdays");var weekdays=CreateHorizontalGroup("Activity weekday chips",editor.weekdaySection.transform,8,0,0,0,0);AddLayoutElement(weekdays,-1,104);editor.weekdays=new Toggle[7];
        for(int i=0;i<7;i++){var button=Command(weekdays.transform,new[]{"S","M","T","W","T","F","S"}[i],null);AddLayoutElement(button.gameObject,0,104,1);var chip=button.gameObject;Object.DestroyImmediate(button);var toggle=chip.AddComponent<Toggle>();toggle.targetGraphic=chip.GetComponent<Image>();var outline=CreateImage("Selected weekday",toggle.transform,Primary);outline.sprite=SelectionFrame;outline.type=Image.Type.Sliced;outline.raycastTarget=false;Stretch(outline.rectTransform);toggle.graphic=outline;toggle.isOn=false;editor.weekdays[i]=toggle;}
        editor.ending=Choice(editor.repeatSection.transform,"Ends",new[]{"Never","On date","After N occurrences"});editor.endingDateSection=Section(editor.repeatSection.transform,"Activity end date");editor.endingDate=Picker(editor.endingDateSection.transform,"Activity until",true);editor.countSection=Section(editor.repeatSection.transform,"Activity count");editor.count=CompactField(editor.countSection.transform,"activityCount","Occurrences",false,true);
        editor.alarmSection=Section(form,"Device-only activity alarm");Body(editor.alarmSection.transform,"Alarm settings apply only to this device.",80);editor.alarm=Check(editor.alarmSection.transform,"Alarm?",false,96);editor.activeAlarm=Check(editor.alarmSection.transform,"Enabled on this device",true,96);editor.reminder=Duration(editor.alarmSection.transform,"Remind before",new[]{0,5,10,15,30,60},10080);editor.reminder.minimum=0;
        editor.scopeSection=Section(form,"Activity edit scope");editor.scope=Choice(editor.scopeSection.transform,"Apply changes to",new[]{"This occurrence","This and future occurrences","Entire series"});editor.deleteButton=Command(form,"Delete / cancel activity",editor.Delete).gameObject;
        editor.warning=Body(form,"",180);editor.warning.fontSize=30;editor.message=Body(form,"",120);editor.message.color=Danger;
        var templateSection=Expandable(form,"Save as template");editor.templateName=CompactField(templateSection,"templateName","Template name");editor.sharedTemplate=Check(templateSection,"Share template with this calendar",false,96);Command(templateSection,"Save template",editor.SaveTemplate);
        editor.panel.SetActive(false);
        editor.iconPanel=CreateScreen("Activity Icon Picker",root);editor.iconPanel.GetComponent<Image>().raycastTarget=true;editor.iconPanel.AddComponent<ModalPanel>().dismissOutside=false;CreateTopBar(editor.iconPanel.transform,"Choose activity icon");var iconContent=CreateScrollContent("Icon picker",editor.iconPanel.transform,20,24,24,20,20,out var iconScroll);AnchorStretch(iconScroll.GetComponent<RectTransform>(),0,0,1,1,0,140,0,-174);
        Object.DestroyImmediate(iconContent.GetComponent<VerticalLayoutGroup>());var iconGrid=iconContent.gameObject.AddComponent<GridLayoutGroup>();iconGrid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;iconGrid.constraintCount=4;iconGrid.cellSize=new Vector2(238,242);iconGrid.spacing=new Vector2(12,12);
        for(int i=0;i<ActivityIconSet.Ids.Length;i++){var b=Command(iconContent,ActivityIconSet.Names[i],null);var choice=b.gameObject.AddComponent<ActivityIconChoice>();choice.id=ActivityIconSet.Ids[i];choice.editor=editor;UnityEventTools.AddPersistentListener(b.onClick,choice.Select);var image=CreateImage("Icon sprite",b.transform,Primary);image.sprite=ActivityIconSet.Load().sprites[i];image.preserveAspect=true;AnchorStretch(image.rectTransform,.5f,1,.5f,1,-50,-126,50,-26);AnchorStretch(b.GetComponentInChildren<Text>().rectTransform,0,0,1,0,4,6,-4,88);b.GetComponentInChildren<Text>().fontSize=30;}
        var close=Command(editor.iconPanel.transform,"Done",editor.CloseIcons);AnchorStretch(close.GetComponent<RectTransform>(),0,0,1,0,24,20,-24,140);editor.iconPanel.SetActive(false);

        work.detailsPanel=CreateScreen("Activity Details",root);work.detailsPanel.GetComponent<Image>().raycastTarget=true;work.detailsPanel.AddComponent<ModalPanel>().dismissOutside=false;var details=ScreenContent(work.detailsPanel,"Activity details");var detailClose=Command(work.detailsPanel.transform,"Close",work.CloseDetails);AnchorStretch(detailClose.GetComponent<RectTransform>(),1,1,1,1,-190,-146,-24,-12);
        work.detailsIcon=CreateImage("Activity detail icon",details,Primary);AddLayoutElement(work.detailsIcon.gameObject,100,100);work.detailsIcon.preserveAspect=true;work.detailsTitle=Body(details,"Activity",100);work.detailsTitle.fontStyle=FontStyle.Bold;work.detailsBody=Body(details,"Details",320);work.detailsBody.GetComponent<LayoutElement>().preferredHeight=-1;
        work.detailScope=Choice(details,"Assignment / cancellation scope",new[]{"This occurrence","This and future occurrences","Entire series"});Command(details,"Assign to me",work.Claim);Command(details,"Unassign",work.Unassign);Command(details,"Edit / change assignment / participants",work.Edit);Command(details,"Delete / cancel",work.CancelOccurrence);work.detailsMessage=Body(details,"",160);work.detailsPanel.SetActive(false);

        var settingsContent=FindChild(settings.transform,"Settings Scroll Content");var familyCard=SettingsCard(settingsContent,"Family calendar");familyCard.SetSiblingIndex(2);Disclosure(familyCard,"Upcoming seven days",work.ShowAgenda);
        BuildProfiles(root,familyCard);var templates=BuildTemplates(root,familyCard,editor);editor.templates=templates;BuildPlanner(root,familyCard,work);
        var addEvent=FindChild(root,"+ Add event").GetComponent<Button>();UnityEventTools.RemovePersistentListener(addEvent.onClick,0);UnityEventTools.AddPersistentListener(addEvent.onClick,work.NewActivity);addEvent.GetComponentInChildren<Text>().text="+ Activity";
        ReplaceNavigationGlyphs(root);BakeThemes(root);
    }
    static void BackButton(GameObject panel){var b=Command(panel.transform,"Back",null);AnchorStretch(b.GetComponent<RectTransform>(),1,1,1,1,-190,-146,-24,-12);b.gameObject.AddComponent<BackNavigationButton>();UnityEventTools.AddPersistentListener(b.onClick,b.GetComponent<BackNavigationButton>().Back);}
    static void CreateCellActivityBadges(CalendarDayCell cell,Transform root)
    {
        cell.activityIcons=new Image[3];cell.activityFrames=new Image[3];
        for(int i=0;i<3;i++){var badge=CreatePanel("Activity cell badge "+i,root,Color.clear);AnchorStretch(badge.GetComponent<RectTransform>(),0,0,0,0,8+i*38,8,42+i*38,42);badge.GetComponent<Image>().raycastTarget=false;
            var frame=CreateImage("Activity assignment border",badge.transform,TextMuted);frame.sprite=SelectionFrame;frame.type=Image.Type.Sliced;frame.raycastTarget=false;Stretch(frame.rectTransform);cell.activityFrames[i]=frame;
            var icon=CreateImage("Activity cell icon",badge.transform,TextDark);icon.raycastTarget=false;icon.preserveAspect=true;AnchorStretch(icon.rectTransform,0,0,1,1,5,5,-5,-5);cell.activityIcons[i]=icon;badge.SetActive(false);}
        cell.activityOverflow=CreateText("Activity overflow",root,"",25,TextDark,TextAnchor.MiddleRight,FontStyle.Bold);AnchorStretch(cell.activityOverflow.rectTransform,1,0,1,0,-38,7,-4,43);cell.activityOverflow.raycastTarget=false;
    }
    static GameObject BuildParticipantRow()
    {
        string path=PrefabFolder+"/FamilyParticipant.prefab";var holder=CreateUiRoot("Participant builder");var toggle=Check(holder.transform,"Person",false,100);var row=toggle.transform.parent.gameObject;row.transform.SetParent(null);BakeThemes(row.transform);var prefab=PrefabUtility.SaveAsPrefabAsset(row,path);Object.DestroyImmediate(row);Object.DestroyImmediate(holder);return prefab;
    }
    static FamilyActivityRow BuildFamilyRow()
    {
        var go=CreateHorizontalGroup("FamilyActivityRow",null,20,20,20,12,12);AddLayoutElement(go,-1,150);var bg=go.AddComponent<Image>();bg.sprite=Rounded;bg.type=Image.Type.Sliced;bg.color=CardSoft;
        var row=go.AddComponent<FamilyActivityRow>();row.button=go.AddComponent<Button>();row.button.targetGraphic=bg;
        var badge=CreatePanel("Activity badge",go.transform,Color.clear);AddLayoutElement(badge,92,92);row.frame=CreateImage("Assignment outline",badge.transform,TextMuted);row.frame.sprite=SelectionFrame;row.frame.type=Image.Type.Sliced;row.frame.raycastTarget=false;Stretch(row.frame.rectTransform);
        row.icon=CreateImage("Activity symbol",badge.transform,TextDark);row.icon.preserveAspect=true;row.icon.raycastTarget=false;AnchorStretch(row.icon.rectTransform,0,0,1,1,14,14,-14,-14);
        var text=Section(go.transform,"Activity summary");Object.DestroyImmediate(text.GetComponent<ContentSizeFitter>());AddLayoutElement(text,0,-1,1);row.title=Body(text.transform,"Activity",54);row.title.fontStyle=FontStyle.Bold;row.title.fontSize=36;row.detail=Body(text.transform,"Untimed · Unassigned",50);row.detail.fontSize=30;BakeThemes(go.transform);var iconTheme=row.icon.gameObject.AddComponent<ThemeManager>();iconTheme.role=ThemeManager.Role.Text;iconTheme.Paint();var detailTheme=row.detail.GetComponent<ThemeManager>();detailTheme.role=ThemeManager.Role.Muted;detailTheme.Paint();
        var prefab=PrefabUtility.SaveAsPrefabAsset(go,PrefabFolder+"/FamilyActivityRow.prefab");Object.DestroyImmediate(go);return prefab.GetComponent<FamilyActivityRow>();
    }
    static void BuildProfiles(Transform root,Transform settings)
    {
        var c=root.gameObject.AddComponent<FamilyProfileController>();c.panel=CreateScreen("Family Profiles",root);var form=ScreenContent(c.panel,"Family profiles");BackButton(c.panel);Disclosure(settings,"Family profiles",c.Open);
        c.person=Choice(form,"Person",new[]{"New person"});c.displayName=CompactField(form,"profileName","Display name");c.initials=CompactField(form,"profileInitials","Initials (optional)");c.color=Choice(form,"Color",new[]{"Blue","Pink","Aqua","Gold","Purple","Orange","Green","Rose"});c.active=Check(form,"Active",true,96);c.link=Choice(form,"Linked account",new[]{"Unlinked (no login required)"});Command(form,"Refresh signed-in group accounts",c.RefreshMembers);c.shiftOwner=Check(form,"Owns this work rotation",false,96);c.save=Command(form,"Save person",c.Save);c.archive=Command(form,"Archive person",c.Archive);c.message=Body(form,"",240);c.panel.SetActive(false);
    }
    static ActivityTemplateController BuildTemplates(Transform root,Transform settings,ActivityEditor editor)
    {
        var c=root.gameObject.AddComponent<ActivityTemplateController>();c.editor=editor;c.panel=CreateScreen("Activity Templates",root);var form=ScreenContent(c.panel,"Activity templates");BackButton(c.panel);Disclosure(settings,"Activity templates",c.Open);c.selection=Choice(form,"Template",new[]{"No templates yet"});c.summary=Body(form,"",300);c.name=CompactField(form,"savedTemplateName","Template name");c.shared=Check(form,"Shared with this calendar",false,96);Command(form,"Use template",c.Use);Command(form,"Edit activity fields",c.Edit);Command(form,"Save name / sharing",c.SaveMetadata);Command(form,"Delete template",c.Delete);c.message=Body(form,"",160);c.panel.SetActive(false);return c;
    }
    static void BuildPlanner(Transform root,Transform settings,FamilyWorkbench work)
    {
        var c=root.gameObject.AddComponent<AvailabilityPlanner>();c.workbench=work;c.panel=CreateScreen("Availability Planner",root);var form=ScreenContent(c.panel,"Who's free?");BackButton(c.panel);Disclosure(settings,"Who's free?",c.Open);c.person=Choice(form,"People",new[]{"All active people"});c.date=Picker(form,"Check date",true);c.time=Picker(form,"Check time",false);c.duration=Duration(form,"Needed time",new[]{15,30,60,90,120},10080);c.peopleContent=Expandable(form,"Select multiple people");c.participantPrefab=editorParticipantPrefab();c.weekends=Check(form,"Free weekends (Saturday + Sunday)",false,96);Command(form,"Check this time",c.Check);Command(form,"Find available days (90 days)",c.Search);Command(form,"Next OFF days",c.NextOff);c.message=Body(form,"",240);c.results=Section(form,"Availability results").transform;c.panel.SetActive(false);
    }
    static GameObject editorParticipantPrefab()=>AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder+"/FamilyParticipant.prefab");
    static void BuildActivityIcons()
    {
        var set=AssetDatabase.LoadAssetAtPath<ActivityIconSet>("Assets/Resources/ActivityIcons.asset");if(set==null){set=ScriptableObject.CreateInstance<ActivityIconSet>();AssetDatabase.CreateAsset(set,"Assets/Resources/ActivityIcons.asset");}
        set.sprites=new Sprite[20];for(int i=0;i<20;i++){string id=ActivityIconSet.Ids[i];set.sprites[i]=Artwork("Activity_"+id,(x,y)=>ActivityMask(id,new Vector2(x,y))?1:0,Vector4.zero);}EditorUtility.SetDirty(set);AssetDatabase.SaveAssets();
    }
    static bool Line(Vector2 p,float x,float y,float xx,float yy,float width=3){var a=new Vector2(x,y);var b=new Vector2(xx,yy);float t=Mathf.Clamp01(Vector2.Dot(p-a,b-a)/(b-a).sqrMagnitude);return Vector2.Distance(p,a+(b-a)*t)<=width;}
    static bool Circle(Vector2 p,float x,float y,float radius,bool outline=false){float d=Vector2.Distance(p,new Vector2(x,y));return outline?Mathf.Abs(d-radius)<3:d<radius;}
    static bool Box(Vector2 p,float x,float y,float xx,float yy)=>p.x>x&&p.x<xx&&p.y>y&&p.y<yy;
    static bool ActivityMask(string id,Vector2 p)
    {
        switch(id)
        {
            case "karate": return Circle(p,28,51,6)||Line(p,28,44,31,31,4)||Line(p,30,37,48,48,4)||Line(p,48,48,56,58,4)||Line(p,31,30,27,18,4)||Line(p,27,18,14,9,4)||Line(p,26,40,13,33,3)||Line(p,28,42,39,41,3);
            case "medical":return Box(p,25,10,39,54)||Box(p,10,25,54,39);
            case "dentist":return Circle(p,23,42,13)||Circle(p,41,42,13)||Box(p,14,28,50,43)||Line(p,18,29,22,9,5)||Line(p,46,29,42,9,5);
            case "note":return (Box(p,14,8,50,56)&&(!Box(p,19,13,45,51)))||Line(p,24,24,40,24,2)||Line(p,24,36,40,36,2);
            case "appointment":return (Box(p,10,10,54,52)&&!Box(p,15,15,49,42))||Line(p,22,48,22,58)||Line(p,42,48,42,58)||Line(p,24,25,31,19)||Line(p,31,19,43,34);
            case "school":return Line(p,7,43,32,56)||Line(p,32,56,57,43)||Line(p,7,43,32,30)||Line(p,32,30,57,43)||Line(p,17,29,17,19)||Line(p,17,19,47,19)||Line(p,47,19,47,29);
            case "pickup":return Circle(p,23,48,6)||Line(p,23,40,23,21,4)||Line(p,23,22,15,9)||Line(p,23,22,30,9)||Line(p,25,34,46,34)||Line(p,44,27,52,34)||Line(p,44,41,52,34);
            case "birthday":return Box(p,10,10,54,32)||Line(p,10,38,54,38)||Line(p,18,38,18,47)||Line(p,32,38,32,47)||Line(p,46,38,46,47)||Circle(p,18,53,3)||Circle(p,32,53,3)||Circle(p,46,53,3);
            case "vacation":return Line(p,34,9,38,45,4)||Line(p,12,42,36,52,4)||Line(p,36,52,57,42,4)||Line(p,19,54,36,52,4)||Line(p,36,52,48,58,4)||Line(p,9,9,53,9,2);
            case "sports":return Circle(p,32,32,24,true)||Line(p,9,32,55,32,2)||Line(p,32,9,32,55,2)||Line(p,14,49,49,14,2);
            case "exercise":return Line(p,12,32,52,32,4)||Box(p,9,17,18,47)||Box(p,46,17,55,47)||Box(p,3,24,9,40)||Box(p,55,24,61,40);
            case "work":return (Box(p,8,13,56,45)&&!Box(p,13,18,51,40))||Line(p,24,45,24,54)||Line(p,24,54,40,54)||Line(p,40,54,40,45)||Line(p,9,31,55,31,2);
            case "meeting":return Circle(p,17,47,6)||Circle(p,47,47,6)||Line(p,17,39,17,25,4)||Line(p,47,39,47,25,4)||Line(p,11,20,53,20,4)||Line(p,24,20,24,8)||Line(p,40,20,40,8);
            case "errands":return Line(p,10,48,18,17)||Line(p,18,17,49,17)||Line(p,18,27,48,27)||Line(p,48,27,55,43)||Line(p,15,43,55,43)||Circle(p,23,9,4)||Circle(p,44,9,4);
            case "vehicle":return Box(p,10,18,54,33)||Line(p,13,33,22,48,4)||Line(p,22,48,42,48,4)||Line(p,42,48,52,33,4)||Circle(p,17,17,7)||Circle(p,47,17,7);
            case "pet":return Circle(p,32,23,13)||Circle(p,13,37,6)||Circle(p,25,47,6)||Circle(p,40,47,6)||Circle(p,52,37,6);
            case "family":return Circle(p,15,47,6)||Circle(p,49,47,6)||Circle(p,32,31,5)||Line(p,15,39,15,14,6)||Line(p,49,39,49,14,6)||Line(p,32,23,32,9,5);
            case "home":return Line(p,8,34,32,55)||Line(p,32,55,56,34)||Line(p,15,33,15,10)||Line(p,49,33,49,10)||Line(p,15,10,49,10)||Box(p,27,10,37,28);
            case "reminder":return Circle(p,32,34,20,true)||Line(p,32,43,32,30)||Circle(p,32,24,3)||Line(p,18,9,46,9);
            default:return Circle(p,32,32,23,true)||Line(p,20,32,44,32)||Line(p,32,20,32,44);
        }
    }
    static Sprite NavigationIcon(string kind)=>Artwork("Navigation_"+kind,(x,y)=>{
        var p=new Vector2(x,y);if(kind=="filter")return Line(p,10,52,54,52)||Line(p,54,52,38,30)||Line(p,38,30,38,12)||Line(p,38,12,26,8)||Line(p,26,8,26,30)||Line(p,26,30,10,52)?1:0;if(kind=="close")return Line(p,16,16,48,48)||Line(p,16,48,48,16)?1:0;
        if(kind=="left")return Line(p,40,12,20,32)||Line(p,20,32,40,52)?1:0;
        if(kind=="right")return Line(p,24,12,44,32)||Line(p,44,32,24,52)?1:0;
        return Line(p,12,42,32,22)||Line(p,32,22,52,42)?1:0;
    },Vector4.zero);
    static void ReplaceNavigationGlyphs(Transform root)
    {
        foreach(var t in root.GetComponentsInChildren<Text>(true))
        {
            string text=t.text.Trim(),kind=null;bool sole=false;
            if(text=="<"||text=="‹"||text=="←"){kind="left";sole=true;}
            else if(text==">"||text=="›"||text=="→"){kind="right";sole=true;}
            else if(text=="×"||text=="X"||text=="✕"){kind="close";sole=true;}
            else if(text=="v"||text=="^"||text.EndsWith("  v")||text.EndsWith("  ^")||text.EndsWith("▾")){kind="down";sole=text.Length==1;}
            else if(text.EndsWith("  >")){kind="right";}
            if(kind==null)continue;t.text=sole?"":text.TrimEnd(' ','>','v','^','▾');
            var image=CreateImage("Navigation sprite",sole?t.transform.parent:t.transform,Primary);image.sprite=NavigationIcon(kind);image.raycastTarget=false;image.preserveAspect=true;
            if(sole)AnchorStretch(image.rectTransform,.5f,.5f,.5f,.5f,-24,-24,24,24);else AnchorStretch(image.rectTransform,1,.5f,1,.5f,-54,-24,-6,24);
        }
    }
}
