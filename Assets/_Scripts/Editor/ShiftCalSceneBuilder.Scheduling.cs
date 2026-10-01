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
    private static void CreateSchedulingUI(Transform root,GameObject profile,GameObject login,GameObject calendar)
    {
        var work=root.gameObject.AddComponent<ScheduleWorkbench>();
        work.agendaPanel=CreateScreen("Events and Alarms",root);
        work.eventPanel=CreateScreen("Event Editor",root);
        work.rulePanel=CreateScreen("Shift Alarm Editor",root);
        var agenda=ScreenContent(work.agendaPanel,"Events & alarms");
        Command(agenda,"Back to calendar",null); // Navigation is wired below.
        var back=agenda.GetChild(0).GetComponent<Button>();
        // AppNavigation is added after this builder; WireButtons binds this name.
        back.name="Agenda Back Button";
        work.readinessLabel=Body(agenda,"Alarm readiness",240);
        Command(agenda,"Allow notifications",work.Permissions);
        Command(agenda,"Exact alarm access",work.ExactAccess);
        Command(agenda,"Full-screen alarm access",work.FullscreenAccess);
        Command(agenda,"Test alarm (15 seconds)",work.TestAlarm);
        Command(agenda,"Add event",work.NewEvent);
        Command(agenda,"Add shift alarm",work.NewRule);
        Command(agenda,"Undo skip",work.UndoSkip);
        GameObject entries=CreateVerticalGroup("Upcoming entries",agenda,20,0,0,0,0);
        entries.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        work.agendaContent=entries.transform;
        work.rowPrefab=BuildScheduleRowPrefab();

        var form=ScreenContent(work.eventPanel,"Event");
        work.eventFields.Add(Field(form,"title","Title"));
        work.eventFields.Add(Field(form,"date","Date (YYYY-MM-DD)"));
        work.eventFields.Add(Field(form,"start","Start (1:00 PM)"));
        work.eventFields.Add(Field(form,"end","End (optional)"));
        work.eventFields.Add(Field(form,"zone","Time zone (America/Chicago or device)"));
        work.recurrence=Choice(form,"Repeats",new[]{"Once","Daily","Weekly","Monthly"});
        work.eventFields.Add(Field(form,"interval","Every N days / weeks / months"));
        var dayRow=CreateHorizontalGroup("Weekdays",form,10,0,0,0,0);AddLayoutElement(dayRow,-1,144);
        work.weekdays=new Toggle[7];string[] days={"Su","Mo","Tu","We","Th","Fr","Sa"};
        for(int i=0;i<7;i++)work.weekdays[i]=Check(dayRow.transform,days[i],false,70);
        work.eventFields.Add(Field(form,"until","Through date (optional)"));
        work.eventFields.Add(Field(form,"count","Occurrence count (0 = unlimited)"));
        work.eventFields.Add(Field(form,"notes","Notes (optional)",true));
        work.eventEnabled=Check(form,"Reminders and alarms enabled");
        work.eventReminder=Check(form,"Advance reminder");
        work.eventFields.Add(Field(form,"reminder","Reminder minutes before"));
        work.eventAlarm=Check(form,"Audible alarm at event time");
        work.eventFields.Add(Field(form,"advance","Upcoming alarm notice (minutes; 0 = off)"));
        work.eventVibration=Check(form,"Vibration");
        work.eventSound=Choice(form,"Sound",new[]{"Device alarm","Device notification","Silent"});
        work.eventFields.Add(Field(form,"snooze","Snooze minutes"));
        work.editScope=Choice(form,"Apply edit / deletion to",new[]{"This occurrence","This and future","Entire series"});
        Command(form,"Delete event",work.DeleteEvent);
        EditorFooter(work.eventPanel,work.SaveEvent,work.ShowAgenda);

        var rule=ScreenContent(work.rulePanel,"Shift alarm");
        work.ruleShift=Choice(rule,"Shift",new[]{"Day-12"});
        work.ruleFields.Add(Field(rule,"label","Label"));
        work.ruleFields.Add(Field(rule,"offset","Minutes before shift start"));
        work.ruleEnabled=Check(rule,"Enabled");work.ruleAudible=Check(rule,"Audible alarm");work.ruleVibration=Check(rule,"Vibration");
        work.ruleSound=Choice(rule,"Sound",new[]{"Device alarm","Device notification","Silent"});
        work.ruleFields.Add(Field(rule,"snooze","Snooze minutes"));work.ruleFields.Add(Field(rule,"advance","Upcoming notice minutes (0 = off)"));
        Command(rule,"Delete alarm rule",work.DeleteRule);
        EditorFooter(work.rulePanel,work.SaveRule,work.ShowAgenda);

        var profileContent=FindChild(profile.transform,"Options Scroll Content");
        work.accountLabel=FindChild(profile.transform,"Account Label").GetComponent<Text>();
        work.syncLabel=FindChild(profile.transform,"Sync Label").GetComponent<Text>();
        AddLayoutElement(work.syncLabel.gameObject,-1,180);
        work.darkToggle=FindChild(profile.transform,"Dark Mode Toggle").GetComponent<Toggle>();
        Command(profileContent,"Events & alarms",work.ShowAgenda);
        Command(profileContent,"Retry sync",work.Retry);
        Command(profileContent,"Import this device's local calendar",work.ImportLocal);
        work.groupName=Field(profileContent,"groupName","New group name");Command(profileContent,"Create group",work.CreateGroup);
        work.inviteEmail=Field(profileContent,"inviteEmail","Invite Google account email");Command(profileContent,"Create invitation",work.Invite);
        work.invitation=Field(profileContent,"invitation","Invitation code");Command(profileContent,"Join invited group",work.Join);
        work.memberChoice=Choice(profileContent,"Group member",new[]{"No members loaded"});Command(profileContent,"Refresh group members",work.Members);Command(profileContent,"Remove selected member",work.RemoveMember);Command(profileContent,"Leave shared group",work.LeaveGroup);
        Command(profileContent,"Conflicts: keep my edits",work.KeepMine);Command(profileContent,"Conflicts: use remote edits",work.UseRemote);
        Command(profileContent,"Export local backup",work.ExportBackup);

        var loginContent=FindChild(login.transform,"Login Content");
        var local=Command(loginContent,"Use on this device",null);local.name="Local Mode Button";
        local.transform.SetSiblingIndex(4);
        var privacy=FindChild(login.transform,"Privacy Note").GetComponent<Text>();privacy.text="Local calendars and alarms work offline.";
        work.loginStatus=Body(loginContent,"",180);
        var nav=FindChild(calendar.transform,"Bottom Navigation");
        var agendaNav=Command(nav,"Events",work.ShowAgenda);AddLayoutElement(agendaNav.gameObject,0,-1,1);

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
        CreateTopBar(screen.transform,title,false);
        var content=CreateScrollContent(title+" Scroll",screen.transform,24,36,36,24,40,out _);
        AnchorStretch(content.parent.parent.GetComponent<RectTransform>(),0,0,1,1,20,116,-20,-174);
        return content;
    }
    private static void EditorFooter(GameObject screen,UnityEngine.Events.UnityAction save,UnityEngine.Events.UnityAction cancel)
    {
        var footer=CreateHorizontalGroup("Editor actions",screen.transform,20,0,0,0,0);
        AnchorStretch(footer.GetComponent<RectTransform>(),0,0,1,0,56,120,-56,252);
        var back=Command(footer.transform,"Cancel",cancel);AddLayoutElement(back.gameObject,0,-1,1);
        var commit=Command(footer.transform,"Save",save);AddLayoutElement(commit.gameObject,0,-1,1);
        commit.GetComponent<Image>().color=Primary;
        commit.GetComponentInChildren<Text>().color=Hex("#04111F");
        var content=screen.GetComponentInChildren<ScrollRect>(true).GetComponent<RectTransform>();
        content.offsetMin=new Vector2(content.offsetMin.x,280);
    }
    private static InputField Field(Transform content,string key,string label,bool multiline=false)
    {
        Body(content,label,56);
        var input=CreateInputField(key,content,label);AddLayoutElement(input.gameObject,-1,multiline?220:128);
        input.textComponent.fontSize=42;
        if(multiline)input.lineType=InputField.LineType.MultiLineNewline;
        return input;
    }
    private static Text Body(Transform content,string text,float height)
    {
        var label=CreateText(text.Length==0?"Status":text,content,text,40,TextDark,TextAnchor.MiddleLeft,FontStyle.Normal);
        AddLayoutElement(label.gameObject,-1,height);return label;
    }
    private static Button Command(Transform content,string text,UnityEngine.Events.UnityAction action,UnityEngine.Events.UnityAction unused=null)
    {
        var b=CreateButton(text,content,text,Vector2.zero,Vector2.zero,Input,Primary,40);AddLayoutElement(b.gameObject,-1,144);
        if(action!=null)UnityEventTools.AddPersistentListener(b.onClick,action);return b;
    }
    private static Toggle Check(Transform content,string label,bool initial=true,float height=120)
    {
        var row=CreateHorizontalGroup(label+" Row",content,20,0,0,10,10);AddLayoutElement(row,-1,height);
        row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth=false;
        var text=CreateText(label+" Label",row.transform,label,30,TextDark,TextAnchor.MiddleLeft,FontStyle.Normal);AddLayoutElement(text.gameObject,0,-1,1);
        var check=CreateToggle(label,row.transform);AddLayoutElement(check.gameObject,64,64);check.isOn=initial;return check;
    }
    private static Dropdown Choice(Transform content,string label,string[] values)
    {
        Body(content,label,56);
        var root=CreatePanel(label,content,Input);AddLayoutElement(root,-1,128);
        var drop=root.AddComponent<Dropdown>();drop.targetGraphic=root.GetComponent<Image>();
        var caption=CreateText("Value",root.transform,values[0],34,TextDark,TextAnchor.MiddleLeft,FontStyle.Normal);AnchorStretch(caption.rectTransform,0,0,1,1,24,8,-60,-8);drop.captionText=caption;
        var arrow=CreateText("Arrow",root.transform,"v",30,Primary,TextAnchor.MiddleRight,FontStyle.Bold);Stretch(arrow.rectTransform);
        var template=CreatePanel("Template",root.transform,Card);AnchorStretch(template.GetComponent<RectTransform>(),0,0,1,0,0,-600,0,0);
        var scroll=template.AddComponent<ScrollRect>();scroll.horizontal=false;
        var viewport=CreatePanel("Viewport",template.transform,Color.white);Stretch(viewport.GetComponent<RectTransform>());viewport.AddComponent<Mask>().showMaskGraphic=false;
        var inner=CreateUiRoot("Content");inner.transform.SetParent(viewport.transform,false);AnchorStretch(inner.GetComponent<RectTransform>(),0,1,1,1,0,-128,0,0);inner.GetComponent<RectTransform>().pivot=new Vector2(.5f,1);
        var item=CreatePanel("Item",inner.transform,Input);AnchorStretch(item.GetComponent<RectTransform>(),0,0,1,1,0,0,0,0);
        var toggle=item.AddComponent<Toggle>();toggle.targetGraphic=item.GetComponent<Image>();
        var text=CreateText("Item Label",item.transform,"Option",34,TextDark,TextAnchor.MiddleLeft,FontStyle.Normal);AnchorStretch(text.rectTransform,0,0,1,1,24,8,-24,-8);
        drop.itemText=text;drop.template=template.GetComponent<RectTransform>();scroll.viewport=viewport.GetComponent<RectTransform>();scroll.content=inner.GetComponent<RectTransform>();
        template.SetActive(false);drop.AddOptions(new List<string>(values));return drop;
    }
    private static ScheduleListRow BuildScheduleRowPrefab()
    {
        var obj=CreateVerticalGroup("ScheduleListRow",null,16,20,20,20,20);obj.AddComponent<Image>().color=CardSoft;AddLayoutElement(obj,-1,344);
        var row=obj.AddComponent<ScheduleListRow>();row.title=Body(obj.transform,"Title",60);row.title.fontStyle=FontStyle.Bold;row.subtitle=Body(obj.transform,"Detail",144);row.subtitle.fontSize=36;
        var buttons=CreateHorizontalGroup("Actions",obj.transform,14,0,0,0,0);AddLayoutElement(buttons,-1,116);
        row.first=Command(buttons.transform,"Edit",null);row.second=Command(buttons.transform,"Skip",null);row.third=Command(buttons.transform,"Delete",null);
        foreach(var b in new[]{row.first,row.second,row.third})AddLayoutElement(b.gameObject,0,-1,1);
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
            if(graphic.GetComponentInParent<CalendarDayCell>()!=null||graphic.GetComponentInParent<ShiftSettingRow>()!=null&&graphic.name=="ColorSwatch")continue;
            ThemeManager.Role? role=null;Color color=graphic.color;
            if(graphic is Text){if(color==TextDark||color==Color.white)role=ThemeManager.Role.Text;else if(color==TextMuted)role=ThemeManager.Role.Muted;else if(color==Primary)role=ThemeManager.Role.Accent;}
            else if(color==Background||color==Header)role=ThemeManager.Role.Background;
            else if(color==Card||color==CardSoft)role=ThemeManager.Role.Surface;
            else if(color==Input)role=ThemeManager.Role.Input;
            if(role.HasValue){var t=graphic.GetComponent<ThemeManager>()??graphic.gameObject.AddComponent<ThemeManager>();t.role=role.Value;graphic.color=color;}
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
