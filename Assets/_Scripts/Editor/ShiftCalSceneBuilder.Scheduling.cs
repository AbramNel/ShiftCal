using System;
using System.Collections.Generic;
using ShiftCal.App;
using ShiftCal.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static partial class ShiftCalSceneBuilder
{
    private static void CreateSchedulingUI(Transform root,GameObject settings,GameObject account,GameObject login)
    {
        var work=root.gameObject.AddComponent<ScheduleWorkbench>();
        work.agendaPanel=CreateScreen("Events and Alarms",root);
        work.eventPanel=CreateScreen("Event Editor",root);
        var agenda=ScreenContent(work.agendaPanel,"Events & alarms");
        var back=CreateButton("Agenda Back Button",work.agendaPanel.transform,"Back",Vector2.zero,Vector2.zero,Color.clear,Primary,38);
        AnchorStretch(back.GetComponent<RectTransform>(),1,1,1,1,-192,-140,-24,-12);
        var readiness=Command(agenda,"Alarm readiness",work.TogglePermissions);work.permissionSummary=readiness.GetComponentInChildren<Text>();
        work.permissionDetails=CreateVerticalGroup("Readiness details",agenda,12,0,0,0,0);work.permissionDetails.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        work.readinessLabel=Body(work.permissionDetails.transform,"Alarm readiness",230);
        Command(work.permissionDetails.transform,"Notifications",work.Permissions);Command(work.permissionDetails.transform,"Exact alarms",work.ExactAccess);Command(work.permissionDetails.transform,"Full-screen alarms",work.FullscreenAccess);Command(work.permissionDetails.transform,"Test alarm (15 seconds)",work.TestAlarm);
        work.permissionDetails.SetActive(false);
        var add=CreateHorizontalGroup("Add actions",agenda,18,0,0,0,0);AddLayoutElement(add,-1,132);
        var addEvent=Command(add.transform,"+ Add event",work.NewEvent);var addAlarm=Command(add.transform,"+ Shift alarm",work.NewRule);AddLayoutElement(addEvent.gameObject,0,-1,1);AddLayoutElement(addAlarm.gameObject,0,-1,1);
        work.undoButton=Command(agenda,"Undo last skip",work.UndoSkip).gameObject;work.undoButton.SetActive(false);
        var entries=CreateVerticalGroup("Series and rules",agenda,20,0,0,0,0);entries.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        work.agendaContent=entries.transform;work.rowPrefab=BuildScheduleRowPrefab();
        var upcoming=Command(agenda,"Upcoming  v",work.ToggleUpcoming);work.upcomingLabel=upcoming.GetComponentInChildren<Text>();
        work.upcomingSection=CreateVerticalGroup("Upcoming section",agenda,20,0,0,0,0);work.upcomingSection.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var occurrences=CreateVerticalGroup("Individual deliveries",work.upcomingSection.transform,20,0,0,0,0);occurrences.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        work.upcomingContent=occurrences.transform;work.showMore=Command(work.upcomingSection.transform,"Show more",work.MoreUpcoming);work.upcomingSection.SetActive(false);

        var form=ScreenContent(work.eventPanel,"New event");
        work.editorHeading=FindChild(work.eventPanel.transform,"Top Bar Title").GetComponent<Text>();
        work.contextSection=Section(form,"Applies to");work.dayContextChoice=Choice(work.contextSection.transform,"Create for",new[]{"This date only","Every matching shift"});
        work.eventFields.Add(CompactField(form,"title","Title"));
        var dateTime=CreateHorizontalGroup("Date and time",form,18,0,0,0,0);AddLayoutElement(dateTime,-1,120);
        work.eventDate=Picker(dateTime.transform,"Event date",true);AddLayoutElement(work.eventDate.gameObject,0,120,1);
        work.timeSection=CreateVerticalGroup("Time section",dateTime.transform,0,0,0,0,0);AddLayoutElement(work.timeSection,0,120,1);
        work.eventTime=Picker(work.timeSection.transform,"Event time",false);
        work.recurrence=Choice(form,"Repeat",new[]{"Once","Daily","Weekly","Monthly","On shift days"});
        work.intervalSection=Section(form,"Interval section");work.intervalLabel=Body(work.intervalSection.transform,"Every N days",48);
        work.eventFields.Add(CompactField(work.intervalSection.transform,"interval","",false,true));
        work.weekdaySection=Section(form,"Weekday section");
        var dayRow=CreateHorizontalGroup("Weekday chips",work.weekdaySection.transform,8,0,0,0,0);AddLayoutElement(dayRow,-1,112);
        work.weekdays=new Toggle[7];string[] days={"S","M","T","W","T","F","S"};
        for(int i=0;i<7;i++)
        {
            var chip=CreateButton("Weekday "+i,dayRow.transform,days[i],Vector2.zero,Vector2.zero,Input,TextDark,34);AddLayoutElement(chip.gameObject,0,112,1);
            var chipObject=chip.gameObject;Object.DestroyImmediate(chip);var toggle=chipObject.AddComponent<Toggle>();toggle.targetGraphic=toggle.GetComponent<Image>();
            var selected=CreateImage("Selected weekday",toggle.transform,Primary);selected.sprite=SelectionFrame;selected.type=Image.Type.Sliced;selected.raycastTarget=false;Stretch(selected.rectTransform);toggle.graphic=selected;toggle.isOn=false;work.weekdays[i]=toggle;
        }
        work.endingSection=Section(form,"Repeat ending");work.repeatEnding=Choice(work.endingSection.transform,"Ends",new[]{"Never","On date","After N occurrences"});
        work.endingDateSection=Section(work.endingSection.transform,"End date section");work.endingDate=Picker(work.endingDateSection.transform,"Ending date",true);
        work.countSection=Section(work.endingSection.transform,"Count section");work.eventFields.Add(CompactField(work.countSection.transform,"count","Occurrences",false,true));
        work.shiftSection=Section(form,"Linked shift section");work.linkedShift=Choice(work.shiftSection.transform,"Shift",new[]{"Day-12"},true);
        work.linkedSwatch=CreateImage("Linked shift color",work.linkedShift.transform,Hex("#FBBF24"));AnchorStretch(work.linkedSwatch.rectTransform,0,.5f,0,.5f,18,-20,58,20);work.linkedSwatch.raycastTarget=false;
        AnchorStretch(work.linkedShift.captionText.rectTransform,0,0,1,1,76,8,-60,-8);
        work.timingMode=Choice(work.shiftSection.transform,"Timing",new[]{"Before shift start","Specific time"});
        work.beforeSection=Section(form,"Before shift section");work.beforeShift=Duration(work.beforeSection.transform,"Before shift",new[]{15,30,60,90,120},1440);
        work.beforeShift.minimum=0;
        work.timingPreview=Body(form,"",110);work.timingPreview.fontSize=30;
        work.eventAlarm=Check(form,"Alarm?",true,96);
        work.reminderChoice=Duration(form,"Remind before",new[]{0,5,10,15,30,60,-1},10080);
        work.eventFields.Add(CompactField(form,"notes","Notes (optional)",true));
        work.advancedButton=Command(form,"Advanced alarm settings",work.ToggleAdvanced).gameObject;
        work.advancedSection=Section(form,"Advanced alarm section");
        work.eventEnabled=Check(work.advancedSection.transform,"Enabled",true,96);
        work.defaultSettings=Check(work.advancedSection.transform,"Use device alarm preferences",true,96);
        work.overrideSection=Section(work.advancedSection.transform,"Per-event overrides");
        work.eventVibration=Check(work.overrideSection.transform,"Vibration",true,96);
        work.eventSound=Choice(work.overrideSection.transform,"Sound",new[]{"Device alarm","Device notification","Silent"});
        work.snoozeOverride=Duration(work.overrideSection.transform,"Snooze",new[]{5,10,15,20,30},120);
        work.scopeSection=Section(form,"Edit scope section");work.editScope=Choice(work.scopeSection.transform,"Apply edit / deletion to",new[]{"This occurrence","This and future occurrences","Entire series"});
        work.deleteButton=Command(form,"Delete event",work.DeleteEvent).gameObject;
        EditorFooter(work.eventPanel,work.SaveEvent,work.CancelEditor);
        // All shift rules use the same compact form; no second competing editor.
        CreatePickerDialog(root);

        var settingsContent=FindChild(settings.transform,"Settings Scroll Content");
        work.accountLabel=FindChild(settings.transform,"Account Label").GetComponent<Text>();
        work.syncLabel=FindChild(settings.transform,"Sync Label").GetComponent<Text>();
        work.accountDetailsLabel=FindChild(account.transform,"Account Details Label").GetComponent<Text>();
        work.accountStateLabel=FindChild(account.transform,"Account State Label").GetComponent<Text>();
        UnityEventTools.AddPersistentListener(FindChild(settings.transform,"Events & Alarms").GetComponent<Button>().onClick,work.ShowAgenda);
        var preferencesCard=SettingsCard(settingsContent,"Alarm Preferences");var preferences=preferencesCard.gameObject.AddComponent<AlarmPreferencesPanel>();
        preferences.upcoming=Check(preferencesCard,"Upcoming alarm notices",true,96);
        preferences.noticeSection=Section(preferencesCard,"Notice duration section");preferences.notice=Duration(preferences.noticeSection.transform,"Notice before",new[]{5,10,15,30,60},10080);
        preferences.snooze=Duration(preferencesCard,"Default snooze",new[]{5,10,15,20,30},120);
        preferences.sound=Choice(preferencesCard,"Sound",new[]{"Device alarm","Device notification","Silent"});preferences.vibration=Check(preferencesCard,"Vibration",true,96);
        Command(preferencesCard,"Save alarm preferences",preferences.Save);preferences.message=Body(preferencesCard,"",70);preferences.message.fontSize=30;
        preferences.Load();
        var sharing=SettingsCard(settingsContent,"Data & Sharing");
        var groupContent=Expandable(sharing,"Shared Groups");
        work.groupName=Field(groupContent,"groupName","New group name");Command(groupContent,"Create group",work.CreateGroup);
        work.inviteEmail=Field(groupContent,"inviteEmail","Invite Google account email");Command(groupContent,"Create invitation",work.Invite);
        work.invitation=Field(groupContent,"invitation","Invitation code");Command(groupContent,"Join invited group",work.Join);
        work.memberChoice=Choice(groupContent,"Group member",new[]{"No members loaded"});Command(groupContent,"Refresh group members",work.Members);Command(groupContent,"Remove selected member",work.RemoveMember);Command(groupContent,"Leave shared group",work.LeaveGroup);
        Command(groupContent,"Conflicts: keep my edits",work.KeepMine);Command(groupContent,"Conflicts: use remote edits",work.UseRemote);
        var backup=Expandable(sharing,"Backup / Restore");Command(backup,"Export local backup",work.ExportBackup);Command(backup,"Import this device's local calendar",work.ImportLocal);
        var advanced=SettingsCard(settingsContent,"Advanced");var utilities=Expandable(advanced,"Sync tools");Command(utilities,"Retry sync",work.Retry);

        var loginContent=FindChild(login.transform,"Login Content");
        var local=Command(loginContent,"Use on this device",null);local.name="Local Mode Button";
        local.transform.SetSiblingIndex(4);
        var privacy=FindChild(login.transform,"Privacy Note").GetComponent<Text>();privacy.text="Local calendars and alarms work offline.";
        work.loginStatus=Body(loginContent,"",180);
        // Validation stays visible above system navigation regardless of which editor is open.
        var message=CreateText("Schedule Message",root,"",30,Danger,TextAnchor.MiddleCenter,FontStyle.Bold);
        AnchorStretch(message.rectTransform,0,0,1,0,20,0,-20,104);message.raycastTarget=false;work.messageLabel=message;

        work.Hide();
        EnsureFolder("Assets","Resources");
        if(AssetDatabase.LoadAssetAtPath<ShiftCalConfig>("Assets/Resources/ShiftCalConfig.asset")==null)
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<ShiftCalConfig>(),"Assets/Resources/ShiftCalConfig.asset");
        BakeThemes(root);
        ConfigureAndroid();
    }
    private static RectTransform ScreenContent(GameObject screen,string title)
    {
        CreateTopBar(screen.transform,title);
        if(screen.name.EndsWith("Editor")){screen.AddComponent<ModalPanel>().dismissOutside=false;screen.AddComponent<KeyboardAvoidance>();}
        var content=CreateScrollContent(title+" Scroll",screen.transform,24,36,36,24,40,out _);
        AnchorStretch(content.parent.parent.GetComponent<RectTransform>(),0,0,1,1,20,24,-20,-174);
        return content;
    }
    private static void EditorFooter(GameObject screen,UnityEngine.Events.UnityAction save,UnityEngine.Events.UnityAction cancel)
    {
        var close=CreateButton("Close "+screen.name,screen.transform,"×",Vector2.zero,Vector2.zero,Input,Primary,48);
        AnchorStretch(close.GetComponent<RectTransform>(),1,1,1,1,-160,-148,-24,-12);UnityEventTools.AddPersistentListener(close.onClick,cancel);
        var footer=CreateHorizontalGroup("Editor actions",screen.transform,20,0,0,0,0);
        AnchorStretch(footer.GetComponent<RectTransform>(),0,0,1,0,32,24,-32,156);
        var discard=Command(footer.transform,"Cancel",cancel);AddLayoutElement(discard.gameObject,0,-1,1);
        var commit=Command(footer.transform,"Save Event",save);AddLayoutElement(commit.gameObject,0,-1,1);
        commit.GetComponent<Image>().color=Primary;commit.GetComponentInChildren<Text>().color=Hex("#04111F");
        var content=screen.GetComponentInChildren<ScrollRect>(true).GetComponent<RectTransform>();content.offsetMin=new Vector2(content.offsetMin.x,180);
    }
    private static GameObject Section(Transform parent,string name)
    {
        var section=CreateVerticalGroup(name,parent,12,0,0,0,0);section.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;return section;
    }
    private static InputField CompactField(Transform parent,string key,string label,bool multiline=false,bool number=false)
    {
        if(!string.IsNullOrEmpty(label))Body(parent,label,48);
        var field=CreateInputField(key,parent,label);AddLayoutElement(field.gameObject,-1,multiline?168:120);field.textComponent.fontSize=36;
        if(multiline)field.lineType=InputField.LineType.MultiLineNewline;if(number)field.contentType=InputField.ContentType.IntegerNumber;return field;
    }
    private static PickerField Picker(Transform parent,string name,bool date,bool optional=false)
    {
        var button=CreateButton(name,parent,"Choose "+(date?"date":"time"),Vector2.zero,Vector2.zero,Input,TextDark,34);AddLayoutElement(button.gameObject,-1,120);
        var field=button.gameObject.AddComponent<PickerField>();field.isDate=date;field.optional=optional;field.caption=button.GetComponentInChildren<Text>();
        field.caption.alignment=TextAnchor.MiddleLeft;AnchorStretch(field.caption.rectTransform,0,0,1,1,76,0,-12,0);
        var icon=CreateImage(date?"Calendar icon":"Clock icon",button.transform,Primary);icon.sprite=Icon(date?"Note":"Clock");AnchorStretch(icon.rectTransform,0,.5f,0,.5f,18,-23,64,23);icon.raycastTarget=false;
        UnityEventTools.AddPersistentListener(button.onClick,field.Open);return field;
    }
    private static DurationChoice Duration(Transform parent,string label,int[] values,int maximum)
    {
        var section=Section(parent,label+" duration");var duration=section.AddComponent<DurationChoice>();duration.minutes=values;duration.maximum=maximum;
        duration.choice=Choice(section.transform,label,System.Array.ConvertAll(values,v=>v==0?"Off":v<0?"At event time":v==60?"1 hour":v+" min"));duration.choice.AddOptions(new List<string>{"Custom"});
        var custom=Section(section.transform,"Custom "+label);duration.custom=CompactField(custom.transform,"minutes","Minutes",false,true);custom.SetActive(false);return duration;
    }
    private static void CreatePickerDialog(Transform root)
    {
        var overlay=CreatePanel("Date Time Picker",root,Hex("#000000AA"));Stretch(overlay.GetComponent<RectTransform>());overlay.GetComponent<Image>().raycastTarget=true;overlay.AddComponent<ModalPanel>().dismissOutside=false;
        var picker=overlay.AddComponent<UnityPickerDialog>();
        var card=CreatePanel("Picker card",overlay.transform,Card);AnchorStretch(card.GetComponent<RectTransform>(),0,.5f,1,.5f,28,-258,-28,258);card.GetComponent<Image>().raycastTarget=true;
        var body=Section(card.transform,"Picker controls");AnchorStretch(body.GetComponent<RectTransform>(),0,0,1,1,28,28,-28,-28);Object.DestroyImmediate(body.GetComponent<ContentSizeFitter>());
        picker.heading=Body(body.transform,"Choose date",70);picker.heading.fontStyle=FontStyle.Bold;
        picker.dateSection=CreateHorizontalGroup("Date selectors",body.transform,12,0,0,0,0);AddLayoutElement(picker.dateSection,-1,196);
        var year=Section(picker.dateSection.transform,"Year selector");var month=Section(picker.dateSection.transform,"Month selector");var day=Section(picker.dateSection.transform,"Day selector");
        foreach(var section in new[]{year,month,day})AddLayoutElement(section,0,196,1);
        picker.year=Choice(year.transform,"Year",new[]{"2026"});picker.month=Choice(month.transform,"Month",new[]{"Oct"});picker.day=Choice(day.transform,"Day",new[]{"8"});
        picker.timeSection=CreateHorizontalGroup("Time selectors",body.transform,16,0,0,0,0);AddLayoutElement(picker.timeSection,-1,196);
        var hour=Section(picker.timeSection.transform,"Hour selector");var minute=Section(picker.timeSection.transform,"Minute selector");foreach(var section in new[]{hour,minute})AddLayoutElement(section,0,196,1);
        picker.hour=Choice(hour.transform,"Hour",new[]{"4 AM"});picker.minute=Choice(minute.transform,"Minute",new[]{"00"});
        picker.clearButton=Command(body.transform,"Clear time",picker.Clear).gameObject;AddLayoutElement(picker.clearButton,-1,60);
        var actions=CreateHorizontalGroup("Picker actions",body.transform,18,0,0,0,0);AddLayoutElement(actions,-1,120);
        foreach(var button in new[]{Command(actions.transform,"Cancel",picker.Cancel),Command(actions.transform,"OK",picker.Accept)})AddLayoutElement(button.gameObject,0,120,1);
        overlay.SetActive(false);
    }
    private static InputField Field(Transform content,string key,string label,bool multiline=false)
    {
        Body(content,label,label.Length>42?90:56);
        var input=CreateInputField(key,content,label);AddLayoutElement(input.gameObject,-1,multiline?240:132);
        input.textComponent.fontSize=42;
        if(multiline)input.lineType=InputField.LineType.MultiLineNewline;
        return input;
    }
    private static Text Body(Transform content,string text,float height)
    {
        var label=CreateText(text.Length==0?"Status":text,content,text,38,TextDark,TextAnchor.MiddleLeft,FontStyle.Normal);
        AddLayoutElement(label.gameObject,-1,height);return label;
    }
    private static Button Command(Transform content,string text,UnityEngine.Events.UnityAction action,UnityEngine.Events.UnityAction unused=null)
    {
        var b=CreateButton(text,content,text,Vector2.zero,Vector2.zero,Input,Primary,38);AddLayoutElement(b.gameObject,-1,132);
        if(action!=null)UnityEventTools.AddPersistentListener(b.onClick,action);return b;
    }
    private static Toggle Check(Transform content,string label,bool initial=true,float height=120)
    {
        var row=CreateHorizontalGroup(label+" Row",content,20,0,0,10,10);AddLayoutElement(row,-1,height);
        row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth=false;
        var text=CreateText(label+" Label",row.transform,label,30,TextDark,TextAnchor.MiddleLeft,FontStyle.Normal);AddLayoutElement(text.gameObject,0,-1,1);
        var check=CreateToggle(label,row.transform);AddLayoutElement(check.gameObject,64,64);check.isOn=initial;return check;
    }
    private static Dropdown Choice(Transform content,string label,string[] values,bool shift=false)
    {
        Body(content,label,label.Length>42?90:56);
        var root=CreatePanel(label,content,Input);AddLayoutElement(root,-1,128);
        Dropdown drop=shift?root.AddComponent<ShiftDropdown>():root.AddComponent<Dropdown>();drop.targetGraphic=root.GetComponent<Image>();
        var caption=CreateText("Value",root.transform,values[0],34,TextDark,TextAnchor.MiddleLeft,FontStyle.Normal);AnchorStretch(caption.rectTransform,0,0,1,1,24,8,-60,-8);drop.captionText=caption;
        var arrow=CreateText("Arrow",root.transform,"v",30,Primary,TextAnchor.MiddleRight,FontStyle.Bold);Stretch(arrow.rectTransform);
        var template=CreatePanel("Template",root.transform,Card);AnchorStretch(template.GetComponent<RectTransform>(),0,0,1,0,0,-600,0,0);
        var scroll=template.AddComponent<ScrollRect>();scroll.horizontal=false;
        var viewport=CreatePanel("Viewport",template.transform,Color.white);Stretch(viewport.GetComponent<RectTransform>());viewport.AddComponent<Mask>().showMaskGraphic=false;
        var inner=CreateUiRoot("Content");inner.transform.SetParent(viewport.transform,false);AnchorStretch(inner.GetComponent<RectTransform>(),0,1,1,1,0,-128,0,0);inner.GetComponent<RectTransform>().pivot=new Vector2(.5f,1);
        var item=CreatePanel("Item",inner.transform,Input);AnchorStretch(item.GetComponent<RectTransform>(),0,0,1,1,0,0,0,0);
        var toggle=item.AddComponent<Toggle>();toggle.targetGraphic=item.GetComponent<Image>();
        var text=CreateText("Item Label",item.transform,"Option",34,TextDark,TextAnchor.MiddleLeft,FontStyle.Normal);AnchorStretch(text.rectTransform,0,0,1,1,24,8,-24,-8);
        if(shift){var swatch=CreateImage("Shift option color",item.transform,Color.white);swatch.sprite=Rounded;swatch.type=Image.Type.Sliced;swatch.raycastTarget=false;AnchorStretch(swatch.rectTransform,0,.5f,0,.5f,18,-20,58,20);AnchorStretch(text.rectTransform,0,0,1,1,76,8,-24,-8);}
        drop.itemText=text;drop.template=template.GetComponent<RectTransform>();scroll.viewport=viewport.GetComponent<RectTransform>();scroll.content=inner.GetComponent<RectTransform>();
        template.SetActive(false);drop.AddOptions(new List<string>(values));return drop;
    }
    private static ScheduleListRow BuildScheduleRowPrefab()
    {
        var obj=CreateVerticalGroup("ScheduleListRow",null,10,24,24,20,20);var image=obj.AddComponent<Image>();image.sprite=Rounded;image.type=Image.Type.Sliced;image.color=CardSoft;
        obj.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        obj.AddComponent<Button>().targetGraphic=image;
        var row=obj.AddComponent<ScheduleListRow>();row.title=Body(obj.transform,"Title",-1);row.title.fontSize=42;row.title.fontStyle=FontStyle.Bold;row.subtitle=Body(obj.transform,"Detail",-1);row.subtitle.fontSize=34;
        var buttons=CreateHorizontalGroup("Actions",obj.transform,14,0,0,0,0);AddLayoutElement(buttons,-1,104);
        row.first=Command(buttons.transform,"Edit",null);row.second=Command(buttons.transform,"Pause",null);row.third=Command(buttons.transform,"Upcoming",null);
        foreach(var button in new[]{row.first,row.second,row.third}){AddLayoutElement(button.gameObject,0,104,1);button.GetComponentInChildren<Text>().fontSize=34;}
        BakeThemes(obj.transform);
        var prefab=PrefabUtility.SaveAsPrefabAsset(obj,PrefabFolder+"/ScheduleListRow.prefab");Object.DestroyImmediate(obj);return prefab.GetComponent<ScheduleListRow>();
    }
    private static Transform FindChild(Transform parent,string name)
    {
        foreach(var t in parent.GetComponentsInChildren<Transform>(true))if(t.name==name)return t;
        throw new InvalidOperationException("Missing UI: "+name);
    }
    private static void BakeThemes(Transform root)
    {
        foreach(var graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            if(graphic.GetComponentInParent<ThemeSample>(true)!=null||graphic.name=="Logo"||graphic.name=="Chosen color"||graphic.name=="Day Details Color"||graphic.name=="Preview shift color"||graphic.name=="Choice swatch"||graphic.name=="Linked shift color"||graphic.name=="Shift option color"||graphic.GetComponentInParent<ShiftDayNavigator>(true)!=null||graphic.name.StartsWith("Color #")||graphic.name=="Theme preview"||graphic.GetComponentInParent<CalendarDayCell>(true)!=null||graphic.GetComponentInParent<ShiftSettingRow>(true)!=null&&graphic.name=="ColorSwatch")continue;
            ThemeManager.Role? role=null;Color color=graphic.color;
            if(graphic is Text){if(color==TextDark||color==Color.white)role=ThemeManager.Role.Text;else if(color==Danger)role=ThemeManager.Role.Destructive;else if(color==Hex("#04111F"))role=ThemeManager.Role.OnAccent;else if(color==TextMuted)role=ThemeManager.Role.Muted;else if(color==Primary)role=ThemeManager.Role.Accent;}
            else if(color==Background||color==Header)role=ThemeManager.Role.Background;
            else if(color==Card||color==CardSoft)role=ThemeManager.Role.Surface;
            else if(color==Input)role=ThemeManager.Role.Input;
            else if(color==Hex("#020617F7"))role=ThemeManager.Role.Elevated;
            else if(color==Primary)role=ThemeManager.Role.Accent;else if(color==Danger)role=ThemeManager.Role.Destructive;
            if(role.HasValue){var t=graphic.GetComponent<ThemeManager>()??graphic.gameObject.AddComponent<ThemeManager>();t.role=role.Value;t.Paint();}
        }
    }
    private static void ConfigureAndroid()
    {
        var target=UnityEditor.Build.NamedBuildTarget.Android;
        if(string.IsNullOrEmpty(PlayerSettings.GetApplicationIdentifier(target))||PlayerSettings.GetApplicationIdentifier(target).StartsWith("com.DefaultCompany"))PlayerSettings.SetApplicationIdentifier(target,"com.abramnel.shiftcal");
        PlayerSettings.productName="ShiftCal";PlayerSettings.bundleVersion="1.1.0";PlayerSettings.Android.bundleVersionCode=Math.Max(3,PlayerSettings.Android.bundleVersionCode);
        PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToLandscapeLeft=false;PlayerSettings.allowedAutorotateToLandscapeRight=false;PlayerSettings.allowedAutorotateToPortraitUpsideDown=false;
        PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel26;PlayerSettings.Android.targetSdkVersion=(AndroidSdkVersions)36;
        PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(target,ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.startInFullscreen=false;PlayerSettings.Android.renderOutsideSafeArea=false;
        EnsureFolder("Assets","Branding");
        const string iconPath="Assets/Branding/ShiftCalIcon.png";
        if(!System.IO.File.Exists(iconPath))
        {
            var texture=new Texture2D(256,256,TextureFormat.RGBA32,false);
            for(int y=0;y<256;y++)for(int x=0;x<256;x++)
            {
                Color pixel=Hex("#14171C");
                if(x>=24&&x<232&&y>=28&&y<224)pixel=Hex("#F0F3F6");
                if(x>=24&&x<232&&y>=178&&y<224)pixel=Hex("#22B8A7");
                if(x>=40&&x<216&&y>=44&&y<162){int col=(x-40)/60,row=(y-44)/40;if((x-40)%60<48&&(y-44)%40<28)pixel=(col+row)%3==0?Hex("#FBBF24"):Hex("#287B87");}
                texture.SetPixel(x,y,pixel);
            }
            texture.Apply();System.IO.File.WriteAllBytes(iconPath,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(iconPath);
        }
        PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown,new[]{AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath)},IconKind.Any);
    }
}
