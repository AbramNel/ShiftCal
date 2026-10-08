using System.Collections.Generic;
using ShiftCal.App;
using ShiftCal.Firebase;
using ShiftCal.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static partial class ShiftCalSceneBuilder
{
    private const string ScenePath = "Assets/Calendar.unity";
    private const string PrefabFolder = "Assets/_Prefabs";
    private const float DesignWidth = 1080f;
    private const float DesignHeight = 1920f;

    private static readonly Color Background = Hex("#0F172A");
    private static readonly Color Header = Hex("#020617");
    private static readonly Color Primary = Hex("#60A5FA");
    private static readonly Color Accent = Hex("#2DD4BF");
    private static readonly Color Danger = Hex("#FB7185");
    private static readonly Color TextDark = Hex("#F8FAFC");
    private static readonly Color TextMuted = Hex("#94A3B8");
    private static readonly Color Border = Hex("#334155");
    private static readonly Color Card = Hex("#111827");
    private static readonly Color CardSoft = Hex("#172033");
    private static readonly Color Input = Hex("#1E293B");

    [MenuItem("ShiftCal/Build Permanent Calendar App")]
    public static void BuildPermanentCalendarApp()
    {
        EnsureFolder("Assets", "_Prefabs");

        GameObject dayCellPrefab = BuildDayCellPrefab();
        GameObject shiftRowPrefab = BuildShiftRowPrefab();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera();
        CreateEventSystem();

        GameObject systems = new GameObject("Systems");
        systems.AddComponent<AppSession>();
        systems.AddComponent<FirebaseBootstrap>();
        systems.AddComponent<AuthService>();
        systems.AddComponent<FirestoreService>();

        Canvas canvas = CreateCanvas();
        GameObject uiRoot = CreatePanel("ShiftCal Permanent UI", canvas.transform, Background);
        Stretch(uiRoot.GetComponent<RectTransform>());
        uiRoot.AddComponent<SafeAreaFitter>();

        GameObject loginScreen = CreateLoginScreen(uiRoot.transform);
        GameObject calendarScreen = CreateCalendarScreen(uiRoot.transform, dayCellPrefab);
        GameObject settingsScreen = CreateSettingsScreen(uiRoot.transform, shiftRowPrefab);
        GameObject profileScreen = CreateProfileScreen(uiRoot.transform);
        CreateSchedulingUI(uiRoot.transform, profileScreen, loginScreen, calendarScreen);

        calendarScreen.SetActive(false);
        settingsScreen.SetActive(false);
        profileScreen.SetActive(false);

        AppNavigation navigation = uiRoot.AddComponent<AppNavigation>();
        SetObjectField(navigation, "loginScreen", loginScreen);
        SetObjectField(navigation, "calendarScreen", calendarScreen);
        SetObjectField(navigation, "settingsScreen", settingsScreen);
        SetObjectField(navigation, "profileScreen", profileScreen);
        navigation.confirmation=CreateConfirmation(uiRoot.transform);
        PopulateNavigation(uiRoot.transform,navigation,uiRoot.GetComponent<ScheduleWorkbench>());
        BakeThemes(uiRoot.transform);
        WireButtons(navigation);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true)
        };

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("ShiftCal permanent Canvas scene and prefabs built.");
    }

    private static GameObject BuildDayCellPrefab()
    {
        var root=CreateUiRoot("CalendarDayCell");root.GetComponent<RectTransform>().sizeDelta=new Vector2(142,194);
        var background=root.AddComponent<Image>();background.sprite=Rounded; background.type=Image.Type.Sliced; background.color=Card;
        var cell=root.AddComponent<CalendarDayCell>();
        var day=CreateText("DayNumber",root.transform,"1",36,TextDark,TextAnchor.UpperLeft,FontStyle.Bold);
        AnchorStretch(day.rectTransform,0,1,1,1,10,-54,-8,-8);
        var shift=CreateText("ShiftName",root.transform,"Day-12",30,TextDark,TextAnchor.MiddleLeft,FontStyle.Bold);
        AnchorStretch(shift.rectTransform,0,.43f,1,.43f,10,-24,-8,24);shift.horizontalOverflow=HorizontalWrapMode.Overflow;
        var hours=CreateText("Hours",root.transform,"12h",27,TextMuted,TextAnchor.MiddleLeft,FontStyle.Normal);
        AnchorStretch(hours.rectTransform,0,.22f,1,.22f,10,-14,-8,18);
        var note=CreateText("Note",root.transform,"",26,TextMuted,TextAnchor.MiddleLeft,FontStyle.Normal);
        AnchorStretch(note.rectTransform,0,0,1,0,10,8,-8,40);note.horizontalOverflow=HorizontalWrapMode.Overflow;
        var noteIcon=CreateImage("Note indicator",root.transform,TextDark);noteIcon.sprite=Icon("Note");noteIcon.raycastTarget=false;
        AnchorStretch(noteIcon.rectTransform,1,1,1,1,-66,-40,-38,-12);
        var alarm=CreateImage("Alarm indicator",root.transform,TextDark);alarm.sprite=Icon("Alarm");alarm.raycastTarget=false;
        AnchorStretch(alarm.rectTransform,1,1,1,1,-34,-40,-6,-12);
        var dim=CreateImage("Adjacent month shade",root.transform,Hex("#05091070"));dim.sprite=Rounded;dim.type=Image.Type.Sliced;dim.raycastTarget=false;Stretch(dim.rectTransform);
        var today=CreateImage("Today accent",root.transform,Primary);today.sprite=Rounded;today.type=Image.Type.Sliced;today.raycastTarget=false;
        AnchorStretch(today.rectTransform,0,1,0,1,10,-59,48,-54);
        var selection=CreateImage("Selected Outline",root.transform,Hex("#64B5FF"));selection.sprite=SelectionBorder;selection.type=Image.Type.Sliced;selection.raycastTarget=false;
        AnchorStretch(selection.rectTransform,0,0,1,1,0,0,0,0);
        SetObjectField(cell,"uiBackground",background);SetObjectField(cell,"uiDayNumberLabel",day);SetObjectField(cell,"uiShiftNameLabel",shift);SetObjectField(cell,"uiHoursLabel",hours);SetObjectField(cell,"uiNoteLabel",note);
        SetObjectField(cell,"selectedOutline",selection);SetObjectField(cell,"todayOutline",today);SetObjectField(cell,"dimOverlay",dim);SetObjectField(cell,"noteIcon",noteIcon);SetObjectField(cell,"alarmIcon",alarm);
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabFolder+"/CalendarDayCell.prefab");Object.DestroyImmediate(root);return prefab;
    }

    private static GameObject BuildShiftRowPrefab()
    {
        var root=CreateHorizontalGroup("ShiftSettingRow",null,24,24,24,18,18);root.GetComponent<RectTransform>().sizeDelta=new Vector2(972,176);
        var image=root.AddComponent<Image>();image.sprite=Rounded;image.type=Image.Type.Sliced;image.color=CardSoft;AddLayoutElement(root,-1,176);
        var row=root.AddComponent<ShiftSettingRow>();var button=root.AddComponent<Button>();button.targetGraphic=image;UnityEventTools.AddPersistentListener(button.onClick,row.Edit);
        var swatch=CreateImage("ColorSwatch",root.transform,Hex("#FBBF24"));swatch.sprite=Rounded;swatch.type=Image.Type.Sliced;swatch.raycastTarget=false;AddLayoutElement(swatch.gameObject,72,96);
        var labels=CreateVerticalGroup("Summary",root.transform,6,0,0,0,0);AddLayoutElement(labels,0,-1,1);
        var title=CreateText("Shift name",labels.transform,"Day-12",42,TextDark,TextAnchor.MiddleLeft,FontStyle.Bold);AddLayoutElement(title.gameObject,-1,54);
        var time=CreateText("Hours summary",labels.transform,"5:30 AM – 5:30 PM • 12h",34,TextMuted,TextAnchor.MiddleLeft,FontStyle.Normal);AddLayoutElement(time.gameObject,-1,52);
        var chevron=CreateText("Edit affordance",root.transform,">",42,Primary,TextAnchor.MiddleCenter,FontStyle.Normal);AddLayoutElement(chevron.gameObject,50,-1);
        SetObjectField(row,"uiColorSwatch",swatch);SetObjectField(row,"uiNameLabel",title);SetObjectField(row,"uiTimeLabel",time);BakeThemes(root.transform);
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabFolder+"/ShiftSettingRow.prefab");Object.DestroyImmediate(root);return prefab;
    }

    private static GameObject CreateLoginScreen(Transform parent)
    {
        GameObject screen = CreateScreen("Login Screen", parent);
        CreateTopBar(screen.transform, "ShiftCal", false);

        RectTransform content = CreateVerticalGroup("Login Content", screen.transform, 28, 64, 64, 44, 44).GetComponent<RectTransform>();
        AnchorStretch(content, 0, 0, 1, 1, 32, 120, -32, -210);
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        var brand=CreateHorizontalGroup("Brand",content,0,0,0,0,0);brand.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight=false;AddLayoutElement(brand,-1,180);
        var logo=CreateUiRoot("Logo");logo.transform.SetParent(brand.transform,false);logo.AddComponent<RawImage>().texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Branding/ShiftCalIcon.png");AddLayoutElement(logo,180,180);

        Text title = CreateText("Headline", content, "Sign in to your calendar", 44, TextDark, TextAnchor.MiddleCenter, FontStyle.Bold);
        AddLayoutElement(title.gameObject, -1, 70);

        Text subtitle = CreateText("Subhead", content, "Google sign-in keeps your calendar private and ready to sync.", 38, TextMuted, TextAnchor.MiddleCenter, FontStyle.Normal);
        AddLayoutElement(subtitle.gameObject, -1, 120);

        Button google = CreateButton("Google Sign In Button", content, "Continue with Google", Vector2.zero, Vector2.zero, CardSoft, TextDark, 30);
        AddOutline(google.gameObject, Border);
        Text googleMark = CreateText("Google Mark", google.transform, "G", 32, Primary, TextAnchor.MiddleCenter, FontStyle.Bold);
        AnchorStretch(googleMark.rectTransform, 0, 0, 0, 1, 28, 0, 86, 0);
        AddLayoutElement(google.gameObject, -1, 132);

        Text note = CreateText("Privacy Note", content, "No account, no calendar access.", 36, TextMuted, TextAnchor.MiddleCenter, FontStyle.Normal);
        AddLayoutElement(note.gameObject, -1, 74);

        Button options = CreateButton("Options Button", content, "Options", Vector2.zero, Vector2.zero, Input, Primary, 36);
        AddOutline(options.gameObject, Border);
        AddLayoutElement(options.gameObject, 280, 132);

        return screen;
    }

    private static GameObject CreateCalendarScreen(Transform parent, GameObject dayCellPrefab)
    {
        GameObject screen = CreateScreen("Calendar Screen", parent);
        CreateTopBar(screen.transform, "ShiftCal", true);

        RectTransform monthBar = CreateHorizontalGroup("Month Bar", screen.transform, 18, 12, 12, 8, 8).GetComponent<RectTransform>();
        AnchorStretch(monthBar, 0, 1, 1, 1, 24, -316, -24, -164);
        Image monthImage = monthBar.gameObject.AddComponent<Image>();
        monthImage.color = CardSoft;
        AddOutline(monthBar.gameObject, Border);

        Button prev = CreateButton("Previous Month Button", monthBar, "<", Vector2.zero, Vector2.zero, Input, Primary, 42);
        Text month = CreateText("Month Label", monthBar, "July 2026", 40, TextDark, TextAnchor.MiddleCenter, FontStyle.Bold);
        Button next = CreateButton("Next Month Button", monthBar, ">", Vector2.zero, Vector2.zero, Input, Primary, 42);
        AddLayoutElement(prev.gameObject, 132, 132);
        AddLayoutElement(month.gameObject, 0, 132, 1);
        AddLayoutElement(next.gameObject, 132, 132);

        string[] weekdays = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        RectTransform weekdayRow = CreateHorizontalGroup("Weekday Row", screen.transform, 0, 30, 30, 0, 0).GetComponent<RectTransform>();
        AnchorStretch(weekdayRow, 0, 1, 1, 1, 24, -374, -24, -324);
        for (int i = 0; i < weekdays.Length; i++)
        {
            Text weekday = CreateText("Weekday " + weekdays[i], weekdayRow, weekdays[i], 32, TextMuted, TextAnchor.MiddleCenter, FontStyle.Bold);
            AddLayoutElement(weekday.gameObject, 0, -1, 1);
        }

        RectTransform grid = CreatePanel("Permanent Calendar Grid", screen.transform, Color.clear).GetComponent<RectTransform>();
        AnchorStretch(grid, 0, 0, 1, 1, 24, 174, -24, -384);
        GridLayoutGroup gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 7;
        gridLayout.spacing = new Vector2(8, 8);
        gridLayout.cellSize = new Vector2(144, 202);
        gridLayout.childAlignment = TextAnchor.UpperCenter;
        grid.gameObject.AddComponent<ResponsiveCalendarGrid>();

        CalendarController controller = screen.AddComponent<CalendarController>();
        DayDetailsPopup popup = CreateDayDetailsPopup(screen.transform, controller);
        GameObject shiftPickerPanel = CreateShiftPickerPanel(screen.transform, controller, out List<Button> shiftPickerButtons, out List<Text> shiftPickerLabels);
        GameObject repeatPanel = CreateRepeatPanel(screen.transform, controller);
        var edit=CreateButton("Edit calendar",screen.transform,"Edit",Vector2.zero,Vector2.zero,Input,Primary,36);
        AnchorStretch(edit.GetComponent<RectTransform>(),1,1,1,1,-366,-148,-206,-16);UnityEventTools.AddPersistentListener(edit.onClick,controller.ToggleEdit);
        controller.editLabel=edit.GetComponentInChildren<Text>();
        var bar=CreateHorizontalGroup("Selection bar",screen.transform,12,8,8,8,8);AnchorStretch(bar.GetComponent<RectTransform>(),0,0,1,0,24,160,-24,300);
        var selectionLabel=CreateText("Selection Label",bar.transform,"Tap or drag dates",34,TextMuted,TextAnchor.MiddleLeft,FontStyle.Normal);AddLayoutElement(selectionLabel.gameObject,0,-1,1);
        var set=Command(bar.transform,"Set shift",controller.OpenShiftPickerForSelection);AddLayoutElement(set.gameObject,210,-1);
        var repeat=Command(bar.transform,"Repeat",controller.ShowRepeatPanel);AddLayoutElement(repeat.gameObject,180,-1);
        var clear=Command(bar.transform,"Clear",controller.ClearSelection);AddLayoutElement(clear.gameObject,160,-1);
        controller.selectionBar=bar;bar.SetActive(false);
        controller.preview=CreateDayPreview(screen.transform,popup);

        RectTransform nav = CreateBottomNav(screen.transform).GetComponent<RectTransform>();
        Button settingsNav = CreateButton("Calendar Settings Button", nav, "Settings", Vector2.zero, Vector2.zero, Input, Primary, 36);
        Button profileNav = CreateButton("Calendar Profile Button", nav, "Profile", Vector2.zero, Vector2.zero, Input, Primary, 36);
        Text calLabel = CreateText("Calendar Nav Label", nav, "Calendar", 36, TextDark, TextAnchor.MiddleCenter, FontStyle.Bold);
        AddLayoutElement(calLabel.gameObject, 0, -1, 1);
        AddLayoutElement(settingsNav.gameObject, 0, -1, 1);
        AddLayoutElement(profileNav.gameObject, 0, -1, 1);

        List<CalendarDayCell> cells = new List<CalendarDayCell>(42);
        for (int row = 0; row < 6; row++)
        {
            for (int col = 0; col < 7; col++)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(dayCellPrefab, grid);
                instance.name = "Day Cell " + (row * 7 + col + 1).ToString("00");
                cells.Add(instance.GetComponent<CalendarDayCell>());
            }
        }

        UnityEventTools.AddPersistentListener(prev.onClick, controller.PrevMonth);
        UnityEventTools.AddPersistentListener(next.onClick, controller.NextMonth);
        SetObjectField(controller, "uiMonthLabel", month);
        SetObjectField(controller, "dayDetailsPopup", popup);
        SetObjectField(controller, "shiftPickerPanel", shiftPickerPanel);
        SetObjectField(controller, "repeatPanel", repeatPanel);
        SetObjectField(controller, "selectionLabel", selectionLabel);
        SetObjectList(controller, "dayCells", cells);
        SetObjectList(controller, "shiftPickerButtons", shiftPickerButtons);
        SetObjectList(controller, "shiftPickerLabels", shiftPickerLabels);

        return screen;
    }

    private static GameObject CreateSettingsScreen(Transform parent, GameObject shiftRowPrefab)
    {
        GameObject screen = CreateScreen("Settings Screen", parent);
        CreateTopBar(screen.transform, "Settings", false);

        Button done = CreateButton("Settings Back Button", screen.transform, "Done", Vector2.zero, Vector2.zero, Color.clear, Primary, 25);
        AnchorStretch(done.GetComponent<RectTransform>(), 1, 1, 1, 1, -190, -148, -28, -16);

        RectTransform content = CreateScrollContent("Settings Scroll", screen.transform, 22, 30, 30, 26, 130, out _);
        AnchorStretch(content.parent.parent.GetComponent<RectTransform>(), 0, 0, 1, 1, 24, 136, -24, -196);

        Text header = CreateText("Shift Setting Title", content, "Shift Settings", 40, Primary, TextAnchor.MiddleLeft, FontStyle.Bold);
        AddLayoutElement(header.gameObject, -1, 58);

        Text shifts = CreateText("Shifts Label", content, "Presets and custom shifts", 34, TextMuted, TextAnchor.MiddleLeft, FontStyle.Normal);
        AddLayoutElement(shifts.gameObject, -1, 42);

        ShiftSettingsController controller = screen.AddComponent<ShiftSettingsController>();
        List<ShiftSettingRow> rows = new List<ShiftSettingRow>();
        GameObject rowContainer = CreateVerticalGroup("Shift Entries", content, 20, 0, 0, 0, 0);
        rowContainer.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        for (int i = 0; i < 9; i++)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(shiftRowPrefab, rowContainer.transform);
            instance.name = "Shift Row " + (i + 1).ToString("00");
            AddLayoutElement(instance, -1, 176);
            rows.Add(instance.GetComponent<ShiftSettingRow>());
        }

        Text validation = CreateText("Settings Validation", screen.transform, "", 22, Danger, TextAnchor.MiddleCenter, FontStyle.Bold);
        AnchorStretch(validation.rectTransform, 0, 0, 1, 0, 32, 136, -32, 324);

        RectTransform nav = CreateBottomNav(screen.transform).GetComponent<RectTransform>();
        Button calendarNav = CreateButton("Settings Calendar Button", nav, "Calendar", Vector2.zero, Vector2.zero, Input, Primary, 36);
        Text settingsLabel = CreateText("Settings Nav Label", nav, "Settings", 36, TextDark, TextAnchor.MiddleCenter, FontStyle.Bold);
        Button profileNav = CreateButton("Settings Profile Button", nav, "Profile", Vector2.zero, Vector2.zero, Input, Primary, 36);
        AddLayoutElement(calendarNav.gameObject, 0, -1, 1);
        AddLayoutElement(settingsLabel.gameObject, 0, -1, 1);
        AddLayoutElement(profileNav.gameObject, 0, -1, 1);

        controller.editor=CreateShiftEditor(screen.transform);
        SetObjectList(controller, "rows", rows);
        SetObjectField(controller, "validationLabel", validation);
        SetObjectField(controller, "rowPrefab", shiftRowPrefab.GetComponent<ShiftSettingRow>());
        SetObjectField(controller, "rowContent", rowContainer.transform);
        SetObjectField(controller, "settingsScroll",content.parent.parent.GetComponent<RectTransform>());
        return screen;
    }

    private static GameObject CreateShiftPickerPanel(Transform parent, CalendarController controller, out List<Button> buttons, out List<Text> labels)
    {
        GameObject panel = CreatePanel("Shift Picker Panel", parent, Hex("#020617F7"));
        panel.GetComponent<Image>().raycastTarget = true;
        RectTransform rect = panel.GetComponent<RectTransform>();
        AnchorStretch(rect, 0, 0, 1, 1, 20, 110, -20, -260);
        AddOutline(panel, Border);

        RectTransform header = CreateHorizontalGroup("Shift Picker Header", panel.transform, 18, 24, 24, 8, 8).GetComponent<RectTransform>();
        AnchorStretch(header, 0, 1, 1, 1, 0, -148, 0, 0);
        Text title = CreateText("Shift Picker Title", header, "Apply shift", 28, TextDark, TextAnchor.MiddleLeft, FontStyle.Bold);
        Button repeat = CreateButton("Repeat Selected Button", header, "Repeat", Vector2.zero, Vector2.zero, Input, Primary, 23);
        Button close = CreateButton("Close Shift Picker", header, "Close", Vector2.zero, Vector2.zero, Hex("#1E293B"), Primary, 23);
        AddLayoutElement(title.gameObject, 0, -1, 1);
        AddLayoutElement(repeat.gameObject, 210, 132);
        AddLayoutElement(close.gameObject, 190, 132);
        UnityEventTools.AddPersistentListener(repeat.onClick, controller.ShowRepeatPanel);
        UnityEventTools.AddPersistentListener(close.onClick, controller.HideShiftPicker);

        RectTransform grid = CreateScrollContent("Shift choices", panel.transform, 16, 24, 24, 12, 24, out _);
        AnchorStretch(grid.parent.parent.GetComponent<RectTransform>(), 0, 0, 1, 1, 0, 0, 0, -158);

        buttons = new List<Button>(12);
        labels = new List<Text>(12);
        Button choice = CreateButton("ShiftChoice", grid, "Shift", Vector2.zero, Vector2.zero, Input, Color.black, 32);
        AddLayoutElement(choice.gameObject,-1,132);
        GameObject choiceAsset=PrefabUtility.SaveAsPrefabAsset(choice.gameObject, PrefabFolder+"/ShiftChoice.prefab");
        Object.DestroyImmediate(choice.gameObject);
        SetObjectField(controller,"shiftChoicePrefab",choiceAsset.GetComponent<Button>());
        SetObjectField(controller,"shiftChoiceContent",grid);
        panel.AddComponent<ModalPanel>();
        panel.SetActive(false);
        return panel;
    }

    private static GameObject CreateRepeatPanel(Transform parent, CalendarController controller)
    {
        GameObject overlay = CreatePanel("Repeat Panel", parent, Hex("#000000AA"));
        overlay.GetComponent<Image>().raycastTarget = true;
        Stretch(overlay.GetComponent<RectTransform>());

        RectTransform card = CreateVerticalGroup("Repeat Card", overlay.transform, 16, 32, 32, 32, 36).GetComponent<RectTransform>();
        AnchorStretch(card, 0, 0, 1, 0, 24, 174, -24, 1334);
        Image cardImage = card.gameObject.AddComponent<Image>();
        cardImage.color = Card;
        AddOutline(card.gameObject, Border);

        Text title = CreateText("Repeat Title", card, "Repeat Pattern", 44, TextDark, TextAnchor.MiddleLeft, FontStyle.Bold);
        AddLayoutElement(title.gameObject, -1, 80);
        Text note = CreateText("Repeat Note", card, "Selected dates become the pattern.", 36, TextMuted, TextAnchor.MiddleLeft, FontStyle.Normal);
        AddLayoutElement(note.gameObject, -1, 80);

        Button one = CreateButton("Repeat 1 Month", card, "Repeat 1 Month", Vector2.zero, Vector2.zero, Hex("#1E293B"), TextDark, 25);
        Button three = CreateButton("Repeat 3 Months", card, "Repeat 3 Months", Vector2.zero, Vector2.zero, Hex("#1E293B"), TextDark, 25);
        Button six = CreateButton("Repeat 6 Months", card, "Repeat 6 Months", Vector2.zero, Vector2.zero, Hex("#1E293B"), TextDark, 25);
        Button twelve = CreateButton("Repeat 12 Months", card, "Repeat 12 Months", Vector2.zero, Vector2.zero, Hex("#1E293B"), TextDark, 25);
        Button twentyFour = CreateButton("Repeat 24 Months", card, "Repeat 24 Months", Vector2.zero, Vector2.zero, Primary, Hex("#04111F"), 25);
        Button close = CreateButton("Close Repeat Panel", card, "Cancel", Vector2.zero, Vector2.zero, Hex("#1E293B"), Primary, 25);

        AddLayoutElement(one.gameObject, -1, 132);
        AddLayoutElement(three.gameObject, -1, 132);
        AddLayoutElement(six.gameObject, -1, 132);
        AddLayoutElement(twelve.gameObject, -1, 132);
        AddLayoutElement(twentyFour.gameObject, -1, 132);
        AddLayoutElement(close.gameObject, -1, 132);

        UnityEventTools.AddPersistentListener(one.onClick, controller.RepeatSelectedOneMonth);
        UnityEventTools.AddPersistentListener(three.onClick, controller.RepeatSelectedThreeMonths);
        UnityEventTools.AddPersistentListener(six.onClick, controller.RepeatSelectedSixMonths);
        UnityEventTools.AddPersistentListener(twelve.onClick, controller.RepeatSelectedTwelveMonths);
        UnityEventTools.AddPersistentListener(twentyFour.onClick, controller.RepeatSelectedTwentyFourMonths);
        UnityEventTools.AddPersistentListener(close.onClick, controller.HideRepeatPanel);

        overlay.AddComponent<ModalPanel>();
        overlay.SetActive(false);
        return overlay;
    }

    private static GameObject CreateProfileScreen(Transform parent)
    {
        GameObject screen = CreateScreen("Profile Screen", parent);
        CreateTopBar(screen.transform, "Options", false);

        Button done = CreateButton("Profile Back Button", screen.transform, "Done", Vector2.zero, Vector2.zero, Color.clear, Primary, 25);
        AnchorStretch(done.GetComponent<RectTransform>(), 1, 1, 1, 1, -190, -148, -28, -16);

        RectTransform content = CreateScrollContent("Options Scroll", screen.transform, 26, 42, 42, 38, 120, out _);
        AnchorStretch(content.parent.parent.GetComponent<RectTransform>(), 0, 0, 1, 1, 24, 136, -24, -196);

        var identity=CreateHorizontalGroup("Account summary",content,24,0,0,0,0);AddLayoutElement(identity,-1,132);
        var avatar=CreateImage("Avatar",identity.transform,Primary);avatar.sprite=Rounded;avatar.type=Image.Type.Sliced;AddLayoutElement(avatar.gameObject,108,108);
        var avatarText=CreateText("Avatar Letter",avatar.transform,"S",48,Hex("#04111F"),TextAnchor.MiddleCenter,FontStyle.Bold);Stretch(avatarText.rectTransform);
        var account=CreateText("Account Label",identity.transform,"On this device",44,TextDark,TextAnchor.MiddleLeft,FontStyle.Bold);AddLayoutElement(account.gameObject,0,-1,1);
        var sync=CreateText("Sync Label",content,"Your calendar is stored on this device.",36,TextMuted,TextAnchor.MiddleLeft,FontStyle.Normal);AddLayoutElement(sync.gameObject,-1,100);

        Body(content,"Appearance",64).fontStyle=FontStyle.Bold;
        var previews=CreateHorizontalGroup("Theme previews",content,16,0,0,0,0);AddLayoutElement(previews,-1,120);
        for(int i=0;i<3;i++) {
            var preview=CreatePanel("Theme preview",previews.transform,Hex(new[]{"#171B22","#10292E","#F4F3EF"}[i]));AddLayoutElement(preview,0,-1,1);preview.GetComponent<Image>().sprite=Rounded;preview.GetComponent<Image>().type=Image.Type.Sliced;
            var sample=preview.AddComponent<ThemeSample>();sample.choice=(ThemeManager.Theme)i;var button=preview.AddComponent<Button>();button.targetGraphic=preview.GetComponent<Image>();UnityEventTools.AddPersistentListener(button.onClick,sample.Select);
            var label=CreateText("Sample",preview.transform,new[]{"Graphite","Teal","Daylight"}[i],32,i==2?Hex("#17263C"):Color.white,TextAnchor.MiddleCenter,FontStyle.Bold);Stretch(label.rectTransform);AddOutline(preview,Hex("#68778B"));
        }
        Choice(content,"Theme",ThemeManager.Names);

        var actions=CreateHorizontalGroup("Profile actions",content,18,0,0,0,0);AddLayoutElement(actions,-1,132);
        var settings=CreateButton("Profile Settings Button",actions.transform,"Settings",Vector2.zero,Vector2.zero,Input,Primary,38);
        var logout=CreateButton("Logout Button",actions.transform,"Log out",Vector2.zero,Vector2.zero,Input,Danger,38);
        AddLayoutElement(settings.gameObject,0,-1,1);AddLayoutElement(logout.gameObject,0,-1,1);

        RectTransform nav = CreateBottomNav(screen.transform).GetComponent<RectTransform>();
        Button calendarNav = CreateButton("Profile Calendar Button", nav, "Calendar", Vector2.zero, Vector2.zero, Input, Primary, 36);
        Button settingsNav = CreateButton("Profile Settings Nav Button", nav, "Settings", Vector2.zero, Vector2.zero, Input, Primary, 36);
        Text profileLabel = CreateText("Profile Nav Label", nav, "Profile", 36, TextDark, TextAnchor.MiddleCenter, FontStyle.Bold);
        AddLayoutElement(calendarNav.gameObject, 0, -1, 1);
        AddLayoutElement(settingsNav.gameObject, 0, -1, 1);
        AddLayoutElement(profileLabel.gameObject, 0, -1, 1);
        return screen;
    }

    private static DayDetailsPopup CreateDayDetailsPopup(Transform parent,CalendarController controller)
    {
        var overlay=CreatePanel("Day Details Popup",parent,Hex("#000000AA"));Stretch(overlay.GetComponent<RectTransform>());overlay.GetComponent<Image>().raycastTarget=true;
        overlay.AddComponent<ModalPanel>(); overlay.AddComponent<KeyboardAvoidance>();
        var popup=overlay.AddComponent<DayDetailsPopup>();popup.calendar=controller;
        var card=CreatePanel("Day editor card",overlay.transform,Card);AnchorStretch(card.GetComponent<RectTransform>(),0,0,1,1,24,174,-24,-120);card.GetComponent<Image>().raycastTarget=true;
        var content=CreateScrollContent("Day Details",card.transform,12,28,28,24,28,out _);AnchorStretch(content.parent.parent.GetComponent<RectTransform>(),0,0,1,1,0,158,0,0);
        var title=Body(content,"Day",100);title.fontSize=44;title.fontStyle=FontStyle.Bold;
        var header=CreateHorizontalGroup("Shift summary",content,18,0,0,0,0);AddLayoutElement(header,-1,70);
        var swatch=CreateImage("Day Details Color",header.transform,Hex("#FBBF24"));swatch.sprite=Rounded;swatch.type=Image.Type.Sliced;AddLayoutElement(swatch.gameObject,64,54);
        var shift=Body(header.transform,"Shift",70);AddLayoutElement(shift.gameObject,0,70,1);
        var time=Body(content,"Time",58);var hours=Body(content,"Hours",48);var events=Body(content,"",160);events.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var person=Field(content,"person","Person (optional)");var note=Field(content,"note","Notes",true);
        Command(content,"Change shift",popup.ChangeShift);Command(content,"Restore scheduled shift",popup.Restore);Command(content,"Add event",popup.AddEvent);
        var footer=CreateHorizontalGroup("Editor actions",card.transform,18,24,24,12,12);AnchorStretch(footer.GetComponent<RectTransform>(),0,0,1,0,0,8,0,150);
        var close=Command(footer.transform,"Cancel",popup.Cancel);var save=Command(footer.transform,"Save",popup.SaveDetails);AddLayoutElement(close.gameObject,0,-1,1);AddLayoutElement(save.gameObject,0,-1,1);save.GetComponent<Image>().color=Primary;save.GetComponentInChildren<Text>().color=Hex("#04111F");
        SetObjectField(popup,"panel",overlay);SetObjectField(popup,"titleLabel",title);SetObjectField(popup,"shiftLabel",shift);SetObjectField(popup,"timeLabel",time);SetObjectField(popup,"hoursLabel",hours);
        SetObjectField(popup,"colorSwatch",swatch);SetObjectField(popup,"noteInput",note);SetObjectField(popup,"personInput",person);SetObjectField(popup,"eventsLabel",events);
        overlay.SetActive(false);return popup;
    }

    private static Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("App Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(DesignWidth, DesignHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;
        return canvas;
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();
        cameraObject.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Hex("#202020");
        cameraObject.transform.position = new Vector3(0, 0, -10);
    }

    private static void CreateEventSystem()
    {
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        System.Type inputSystemModule = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputSystemModule != null)
            eventSystem.AddComponent(inputSystemModule);
        else
            eventSystem.AddComponent<StandaloneInputModule>();
    }

    private static void CreateTopBar(Transform parent, string title, bool includeMenu)
    {
        RectTransform bar = CreatePanel("Top App Bar", parent, Header).GetComponent<RectTransform>();
        AnchorStretch(bar, 0, 1, 1, 1, 0, -160, 0, 0);
        Text label = CreateText("Top Bar Title", bar, title, 46, TextDark, TextAnchor.MiddleLeft, FontStyle.Bold);
        AnchorStretch(label.rectTransform, 0, 0, 1, 1, 42, 0, -230, 0);

        if (includeMenu)
        {
            Button menu = CreateButton("Menu Button", bar, "Profile", Vector2.zero, Vector2.zero, Color.clear, Primary, 36);
            AnchorStretch(menu.GetComponent<RectTransform>(), 1, 0, 1, 1, -190, 18, -24, -18);
        }
    }

    private static GameObject CreateScreen(string name, Transform parent)
    {
        GameObject screen = CreatePanel(name, parent, Background);
        Stretch(screen.GetComponent<RectTransform>());
        return screen;
    }

    private static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject panel = CreateUiRoot(name);
        panel.transform.SetParent(parent, false);
        Image image = panel.AddComponent<Image>();
        image.color = color;
        if(color==Card||color==CardSoft||color==Input){image.sprite=Rounded;image.type=Image.Type.Sliced;}
        image.raycastTarget = false;
        return panel;
    }

    private static GameObject CreateUiRoot(string name)
    {
        GameObject root = new GameObject(name);
        root.AddComponent<RectTransform>();
        return root;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject imageObject = CreateUiRoot(name);
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(string name, Transform parent, string value, int size, Color color, TextAnchor anchor, FontStyle style)
    {
        GameObject textObject = CreateUiRoot(name);
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = size;
        text.raycastTarget = false;
        text.color = color;
        text.alignment = anchor;
        text.fontStyle = style;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string label, Vector2 position, Vector2 size, Color background, Color textColor, int fontSize)
    {
        GameObject buttonObject = CreateUiRoot(name);
        buttonObject.transform.SetParent(parent, false);
        SetRect(buttonObject.GetComponent<RectTransform>(), position, size);

        Image image = buttonObject.AddComponent<Image>();
        image.color = background; image.sprite=Rounded;image.type=Image.Type.Sliced;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        Text text = CreateText(label + " Label", buttonObject.transform, label, Mathf.Max(36,fontSize), textColor, TextAnchor.MiddleCenter, FontStyle.Bold);
        Stretch(text.rectTransform);
        return button;
    }

    private static InputField CreateInputField(string name, Transform parent, string placeholderText)
    {
        GameObject inputObject = CreateUiRoot(name);
        inputObject.transform.SetParent(parent, false);
        Image background = inputObject.AddComponent<Image>();
        background.color = Input; background.sprite=Rounded;background.type=Image.Type.Sliced;
        AddOutline(inputObject, Border);

        InputField input = inputObject.AddComponent<InputField>();
        input.targetGraphic = background;

        Text text = CreateText("Text", inputObject.transform, "", 42, TextDark, TextAnchor.MiddleLeft, FontStyle.Normal);
        AnchorStretch(text.rectTransform, 0, 0, 1, 1, 24, 8, -24, -8);
        text.supportRichText = false;

        Text placeholder = CreateText("Placeholder", inputObject.transform, placeholderText, 32, TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic);
        AnchorStretch(placeholder.rectTransform, 0, 0, 1, 1, 24, 8, -24, -8);

        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
    }

    private static RectTransform CreateScrollContent(string name, Transform parent, float spacing, int left, int right, int top, int bottom, out ScrollRect scrollRect)
    {
        GameObject scroll = CreateUiRoot(name);
        scroll.transform.SetParent(parent, false);
        Image scrollImage = scroll.AddComponent<Image>();
        scrollImage.color = Color.clear;
        scrollImage.raycastTarget = true;

        scrollRect = scroll.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 36f;

        GameObject viewport = CreateUiRoot(name + " Viewport");
        viewport.transform.SetParent(scroll.transform, false);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = Color.white;
        viewportImage.raycastTarget = true;
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        Stretch(viewport.GetComponent<RectTransform>());

        GameObject content = CreateVerticalGroup(name + " Content", viewport.transform, spacing, left, right, top, bottom);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = contentRect;
        return contentRect;
    }

    private static GameObject CreateBottomNav(Transform parent)
    {
        GameObject nav = CreateHorizontalGroup("Bottom Navigation", parent, 16, 24, 24, 12, 12);
        RectTransform rect = nav.GetComponent<RectTransform>();
        AnchorStretch(rect, 0, 0, 1, 0, 0, 0, 0, 156);
        Image image = nav.AddComponent<Image>();
        image.color = Header;
        AddOutline(nav, Hex("#0B1120"));
        return nav;
    }

    private static Button CreateColorButton(string name, Transform parent, string colorHex)
    {
        Button button = CreateButton(name, parent, "", Vector2.zero, Vector2.zero, ShiftCal.Core.ShiftStyleUtility.ToColor(colorHex), Color.white, 1);
        AddOutline(button.gameObject, Border);
        return button;
    }

    private static Toggle CreateToggle(string name, Transform parent)
    {
        GameObject toggleObject = CreateUiRoot(name);
        toggleObject.transform.SetParent(parent, false);
        Toggle toggle = toggleObject.AddComponent<Toggle>();

        Image background = CreateImage("Background", toggleObject.transform, Input);
        Stretch(background.rectTransform);
        AddOutline(background.gameObject, Border);

        Image check = CreateImage("Checkmark", background.transform, Primary);
        SetRect(check.rectTransform, Vector2.zero, new Vector2(30, 30));

        toggle.targetGraphic = background;
        toggle.graphic = check;
        toggle.isOn = true;
        return toggle;
    }

    private static GameObject CreateVerticalGroup(string name, Transform parent, float spacing, int left, int right, int top, int bottom)
    {
        GameObject group = CreateUiRoot(name);
        group.transform.SetParent(parent, false);
        VerticalLayoutGroup layout = group.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = new RectOffset(left, right, top, bottom);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperCenter;
        return group;
    }

    private static GameObject CreateHorizontalGroup(string name, Transform parent, float spacing, int left, int right, int top, int bottom)
    {
        GameObject group = CreateUiRoot(name);
        group.transform.SetParent(parent, false);
        HorizontalLayoutGroup layout = group.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = new RectOffset(left, right, top, bottom);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childAlignment = TextAnchor.MiddleCenter;
        return group;
    }

    private static LayoutElement AddLayoutElement(GameObject target, float preferredWidth, float preferredHeight, float flexibleWidth = 0f)
    {
        LayoutElement element = target.GetComponent<LayoutElement>();
        if (element == null)
            element = target.AddComponent<LayoutElement>();

        if (preferredWidth >= 0f)
            element.preferredWidth = preferredWidth;

        if (preferredHeight >= 0f)
            element.preferredHeight = preferredHeight;

        element.flexibleWidth = flexibleWidth;
        return element;
    }

    private static void AddOutline(GameObject target, Color color)
    {
        Outline outline = target.GetComponent<Outline>();
        if (outline == null)
            outline = target.AddComponent<Outline>();

        outline.effectColor = color;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void AnchorStretch(RectTransform rect, float anchorMinX, float anchorMinY, float anchorMaxX, float anchorMaxY, float left, float bottom, float right, float top)
    {
        rect.anchorMin = new Vector2(anchorMinX, anchorMinY);
        rect.anchorMax = new Vector2(anchorMaxX, anchorMaxY);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, top);
    }

    private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void WireButtons(AppNavigation navigation)
    {
        BindButton("Google Sign In Button", navigation.OnGoogleSignInPressed);
        BindButton("Local Mode Button", navigation.OnLocalPressed);
        BindButton("Agenda Back Button", navigation.Back);
        BindButton("Options Button", navigation.ShowProfile);
        BindButton("Menu Button", navigation.ShowProfile);
        BindButton("Back Button", navigation.Back);
        BindButton("Settings Back Button", navigation.Back);
        BindButton("Profile Back Button", navigation.Back);
        BindButton("Profile Settings Button", navigation.ShowSettings);
        BindButton("Calendar Settings Button", navigation.ShowSettings);
        BindButton("Calendar Profile Button", navigation.ShowProfile);
        BindButton("Settings Calendar Button", navigation.ShowCalendar);
        BindButton("Settings Profile Button", navigation.ShowProfile);
        BindButton("Profile Calendar Button", navigation.ShowCalendar);
        BindButton("Profile Settings Nav Button", navigation.ShowSettings);
        BindButton("Logout Button", navigation.OnLogoutPressed);
    }

    private static void BindButton(string objectName, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = FindObjectIncludingInactive(objectName);
        if (buttonObject == null)
            return;

        Button button = buttonObject.GetComponent<Button>();
        if (button != null)
            UnityEventTools.AddPersistentListener(button.onClick, action);
    }

    private static GameObject FindObjectIncludingInactive(string objectName)
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform transform in transforms)
        {
            if (transform.name == objectName)
                return transform.gameObject;
        }

        return null;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static void SetObjectField(Object target, string fieldName, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(fieldName).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObjectList<T>(Object target, string fieldName, List<T> values) where T : Object
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty list = serialized.FindProperty(fieldName);
        list.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString(value, out Color color);
        return color;
    }
}
