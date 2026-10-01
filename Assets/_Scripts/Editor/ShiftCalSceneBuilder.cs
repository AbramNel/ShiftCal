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
        GameObject root = CreateUiRoot("CalendarDayCell");
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(142, 194);

        Image background = root.AddComponent<Image>();
        background.color = Card;
        background.raycastTarget = true;
        AddOutline(root, Border);

        CalendarDayCell cell = root.AddComponent<CalendarDayCell>();

        Text day = CreateText("DayNumber", root.transform, "1", 32, Hex("#020617"), TextAnchor.UpperLeft, FontStyle.Bold);
        AnchorStretch(day.rectTransform, 0, 1, 1, 1, 12, -50, -10, -8);

        Text shift = CreateText("ShiftName", root.transform, "Day-12", 28, Hex("#020617"), TextAnchor.MiddleLeft, FontStyle.Bold);
        AnchorStretch(shift.rectTransform, 0, 0, 1, 1, 12, 62, -10, -58);

        Text hours = CreateText("Hours", root.transform, "12h", 24, Hex("#0F172A"), TextAnchor.MiddleLeft, FontStyle.Bold);
        AnchorStretch(hours.rectTransform, 0, 0, 1, 0, 12, 28, -10, 58);

        Text note = CreateText("PersonOrNote", root.transform, "", 22, Hex("#334155"), TextAnchor.MiddleLeft, FontStyle.Normal);
        AnchorStretch(note.rectTransform, 0, 0, 1, 0, 12, 8, -10, 32);

        Image selectedOutline = CreateImage("Selected Outline", root.transform, Hex("#60A5FA55"));
        Stretch(selectedOutline.rectTransform);
        selectedOutline.raycastTarget = false;
        Outline selectedBorder = selectedOutline.gameObject.AddComponent<Outline>();
        selectedBorder.effectColor = Primary;
        selectedBorder.effectDistance = new Vector2(4, -4);

        SetObjectField(cell, "uiBackground", background);
        SetObjectField(cell, "uiDayNumberLabel", day);
        SetObjectField(cell, "uiShiftNameLabel", shift);
        SetObjectField(cell, "uiHoursLabel", hours);
        SetObjectField(cell, "uiNoteLabel", note);
        SetObjectField(cell, "selectedOutline", selectedOutline);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/CalendarDayCell.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject BuildShiftRowPrefab()
    {
        GameObject root=CreateVerticalGroup("ShiftSettingRow",null,16,20,20,20,20);
        root.GetComponent<RectTransform>().sizeDelta=new Vector2(972,360);
        root.AddComponent<Image>().color=CardSoft;
        AddLayoutElement(root,-1,360);
        ShiftSettingRow row=root.AddComponent<ShiftSettingRow>();
        var first=CreateHorizontalGroup("Name and color",root.transform,16,0,0,0,0);
        AddLayoutElement(first,-1,112);
        Image swatch=CreateImage("ColorSwatch",first.transform,Hex("#FBBF24"));AddLayoutElement(swatch.gameObject,112,-1);
        Button color=swatch.gameObject.AddComponent<Button>();color.targetGraphic=swatch;
        InputField name=CreateInputField("Name Input",first.transform,"Shift name");AddLayoutElement(name.gameObject,0,-1,1);
        InputField colorInput=CreateInputField("Color",first.transform,"#FBBF24");AddLayoutElement(colorInput.gameObject,200,-1);
        var times=CreateHorizontalGroup("Time range",root.transform,16,0,0,0,0);AddLayoutElement(times,-1,96);
        InputField start=CreateInputField("Start Time Input",times.transform,"Start");
        InputField end=CreateInputField("End Time Input",times.transform,"End");
        AddLayoutElement(start.gameObject,0,-1,1);AddLayoutElement(end.gameObject,0,-1,1);
        var actions=CreateHorizontalGroup("Actions",root.transform,16,0,0,0,0);AddLayoutElement(actions,-1,96);
        Text hours=CreateText("HoursLabel",actions.transform,"12h",30,Primary,TextAnchor.MiddleLeft,FontStyle.Bold);AddLayoutElement(hours.gameObject,0,-1,1);
        Button save=CreateButton("Save Shift Row",actions.transform,"Save",Vector2.zero,Vector2.zero,Primary,Hex("#04111F"),30);AddLayoutElement(save.gameObject,180,-1);
        Button delete=CreateButton("Delete Shift Row",actions.transform,"Delete",Vector2.zero,Vector2.zero,Input,Danger,30);AddLayoutElement(delete.gameObject,180,-1);
        UnityEventTools.AddPersistentListener(color.onClick,row.CycleColor);
        UnityEventTools.AddPersistentListener(save.onClick,row.Save);UnityEventTools.AddPersistentListener(delete.onClick,row.Delete);
        UnityEventTools.AddPersistentListener(start.onValueChanged,row.UpdateComputedLabelsFromInput);UnityEventTools.AddPersistentListener(end.onValueChanged,row.UpdateComputedLabelsFromInput);
        SetObjectField(row,"uiColorSwatch",swatch);SetObjectField(row,"uiHoursLabel",hours);SetObjectField(row,"nameInput",name);SetObjectField(row,"startTimeInput",start);SetObjectField(row,"endTimeInput",end);
        SetObjectField(row,"colorButton",color);SetObjectField(row,"saveButton",save);SetObjectField(row,"deleteButton",delete);SetObjectField(row,"saveButtonLabel",save.GetComponentInChildren<Text>());
        SetObjectField(row,"colorInput",colorInput);
        BakeThemes(root.transform);
        GameObject prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabFolder+"/ShiftSettingRow.prefab");Object.DestroyImmediate(root);return prefab;
    }

    private static GameObject CreateLoginScreen(Transform parent)
    {
        GameObject screen = CreateScreen("Login Screen", parent);
        CreateTopBar(screen.transform, "ShiftCal", false);

        RectTransform content = CreateVerticalGroup("Login Content", screen.transform, 28, 64, 64, 44, 44).GetComponent<RectTransform>();
        AnchorStretch(content, 0, 0, 1, 1, 32, 120, -32, -210);
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        Image logo = CreateImage("Logo", content, Primary);
        AddLayoutElement(logo.gameObject, 144, 144);
        Text logoText = CreateText("Logo Text", logo.transform, "SHIFT\nCAL", 24, Hex("#04111F"), TextAnchor.MiddleCenter, FontStyle.Bold);
        Stretch(logoText.rectTransform);

        Text title = CreateText("Headline", content, "Sign in to your calendar", 44, TextDark, TextAnchor.MiddleCenter, FontStyle.Bold);
        AddLayoutElement(title.gameObject, -1, 70);

        Text subtitle = CreateText("Subhead", content, "Google sign-in keeps your calendar private and ready to sync.", 25, TextMuted, TextAnchor.MiddleCenter, FontStyle.Normal);
        AddLayoutElement(subtitle.gameObject, -1, 86);

        Button google = CreateButton("Google Sign In Button", content, "Continue with Google", Vector2.zero, Vector2.zero, CardSoft, TextDark, 30);
        AddOutline(google.gameObject, Border);
        Text googleMark = CreateText("Google Mark", google.transform, "G", 32, Primary, TextAnchor.MiddleCenter, FontStyle.Bold);
        AnchorStretch(googleMark.rectTransform, 0, 0, 0, 1, 28, 0, 86, 0);
        AddLayoutElement(google.gameObject, -1, 96);

        Text note = CreateText("Privacy Note", content, "No account, no calendar access.", 22, TextMuted, TextAnchor.MiddleCenter, FontStyle.Normal);
        AddLayoutElement(note.gameObject, -1, 54);

        Button options = CreateButton("Options Button", content, "Options", Vector2.zero, Vector2.zero, Input, Primary, 24);
        AddOutline(options.gameObject, Border);
        AddLayoutElement(options.gameObject, 280, 68);

        return screen;
    }

    private static GameObject CreateCalendarScreen(Transform parent, GameObject dayCellPrefab)
    {
        GameObject screen = CreateScreen("Calendar Screen", parent);
        CreateTopBar(screen.transform, "My Shift Calendar", true);

        RectTransform monthBar = CreateHorizontalGroup("Month Bar", screen.transform, 18, 28, 28, 18, 18).GetComponent<RectTransform>();
        AnchorStretch(monthBar, 0, 1, 1, 1, 26, -308, -26, -190);
        Image monthImage = monthBar.gameObject.AddComponent<Image>();
        monthImage.color = CardSoft;
        AddOutline(monthBar.gameObject, Border);

        Button prev = CreateButton("Previous Month Button", monthBar, "<", Vector2.zero, Vector2.zero, Input, Primary, 42);
        Text month = CreateText("Month Label", monthBar, "July 2026", 40, TextDark, TextAnchor.MiddleCenter, FontStyle.Bold);
        Button next = CreateButton("Next Month Button", monthBar, ">", Vector2.zero, Vector2.zero, Input, Primary, 42);
        AddLayoutElement(prev.gameObject, 92, 74);
        AddLayoutElement(month.gameObject, 0, 74, 1);
        AddLayoutElement(next.gameObject, 92, 74);

        string[] weekdays = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        RectTransform weekdayRow = CreateHorizontalGroup("Weekday Row", screen.transform, 0, 30, 30, 0, 0).GetComponent<RectTransform>();
        AnchorStretch(weekdayRow, 0, 1, 1, 1, 24, -368, -24, -318);
        for (int i = 0; i < weekdays.Length; i++)
        {
            Text weekday = CreateText("Weekday " + weekdays[i], weekdayRow, weekdays[i], 24, TextMuted, TextAnchor.MiddleCenter, FontStyle.Bold);
            AddLayoutElement(weekday.gameObject, 0, -1, 1);
        }

        RectTransform grid = CreatePanel("Permanent Calendar Grid", screen.transform, Color.clear).GetComponent<RectTransform>();
        AnchorStretch(grid, 0, 0, 1, 1, 24, 238, -24, -376);
        GridLayoutGroup gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 7;
        gridLayout.spacing = new Vector2(4, 4);
        gridLayout.cellSize = new Vector2(144, 202);
        gridLayout.childAlignment = TextAnchor.UpperCenter;
        grid.gameObject.AddComponent<ResponsiveCalendarGrid>();

        CalendarController controller = screen.AddComponent<CalendarController>();
        DayDetailsPopup popup = CreateDayDetailsPopup(screen.transform, controller);
        GameObject shiftPickerPanel = CreateShiftPickerPanel(screen.transform, controller, out List<Button> shiftPickerButtons, out List<Text> shiftPickerLabels);
        GameObject repeatPanel = CreateRepeatPanel(screen.transform, controller);
        Text selectionLabel = CreateText("Selection Label", screen.transform, "Select days", 24, TextMuted, TextAnchor.MiddleCenter, FontStyle.Bold);
        AnchorStretch(selectionLabel.rectTransform, 0, 0, 1, 0, 32, 150, -32, 196);

        RectTransform nav = CreateBottomNav(screen.transform).GetComponent<RectTransform>();
        Button settingsNav = CreateButton("Calendar Settings Button", nav, "Settings", Vector2.zero, Vector2.zero, Input, Primary, 24);
        Button profileNav = CreateButton("Calendar Profile Button", nav, "Profile", Vector2.zero, Vector2.zero, Input, Primary, 24);
        Text calLabel = CreateText("Calendar Nav Label", nav, "Calendar", 24, TextDark, TextAnchor.MiddleCenter, FontStyle.Bold);
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
        AnchorStretch(done.GetComponent<RectTransform>(), 1, 1, 1, 1, -190, -168, -28, -92);

        RectTransform content = CreateScrollContent("Settings Scroll", screen.transform, 22, 30, 30, 26, 130, out _);
        AnchorStretch(content.parent.parent.GetComponent<RectTransform>(), 0, 0, 1, 1, 24, 136, -24, -196);

        Text header = CreateText("Shift Setting Title", content, "Shift Settings", 40, Primary, TextAnchor.MiddleLeft, FontStyle.Bold);
        AddLayoutElement(header.gameObject, -1, 58);

        Text shifts = CreateText("Shifts Label", content, "Presets and custom shifts", 24, TextMuted, TextAnchor.MiddleLeft, FontStyle.Normal);
        AddLayoutElement(shifts.gameObject, -1, 42);

        ShiftSettingsController controller = screen.AddComponent<ShiftSettingsController>();
        List<ShiftSettingRow> rows = new List<ShiftSettingRow>();
        GameObject rowContainer = CreateVerticalGroup("Shift Entries", content, 20, 0, 0, 0, 0);
        rowContainer.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        for (int i = 0; i < 9; i++)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(shiftRowPrefab, rowContainer.transform);
            instance.name = "Shift Row " + (i + 1).ToString("00");
            AddLayoutElement(instance, -1, 360);
            rows.Add(instance.GetComponent<ShiftSettingRow>());
        }

        Text validation = CreateText("Settings Validation", screen.transform, "", 22, Danger, TextAnchor.MiddleCenter, FontStyle.Bold);
        AnchorStretch(validation.rectTransform, 0, 0, 1, 0, 32, 136, -32, 324);

        RectTransform nav = CreateBottomNav(screen.transform).GetComponent<RectTransform>();
        Button calendarNav = CreateButton("Settings Calendar Button", nav, "Calendar", Vector2.zero, Vector2.zero, Input, Primary, 24);
        Text settingsLabel = CreateText("Settings Nav Label", nav, "Settings", 24, TextDark, TextAnchor.MiddleCenter, FontStyle.Bold);
        Button profileNav = CreateButton("Settings Profile Button", nav, "Profile", Vector2.zero, Vector2.zero, Input, Primary, 24);
        AddLayoutElement(calendarNav.gameObject, 0, -1, 1);
        AddLayoutElement(settingsLabel.gameObject, 0, -1, 1);
        AddLayoutElement(profileNav.gameObject, 0, -1, 1);

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

        RectTransform header = CreateHorizontalGroup("Shift Picker Header", panel.transform, 18, 24, 24, 16, 6).GetComponent<RectTransform>();
        AnchorStretch(header, 0, 1, 1, 1, 0, -86, 0, 0);
        Text title = CreateText("Shift Picker Title", header, "Apply shift", 28, TextDark, TextAnchor.MiddleLeft, FontStyle.Bold);
        Button repeat = CreateButton("Repeat Selected Button", header, "Repeat", Vector2.zero, Vector2.zero, Accent, Hex("#03150F"), 23);
        Button close = CreateButton("Close Shift Picker", header, "Close", Vector2.zero, Vector2.zero, Hex("#1E293B"), Primary, 23);
        AddLayoutElement(title.gameObject, 0, -1, 1);
        AddLayoutElement(repeat.gameObject, 180, 54);
        AddLayoutElement(close.gameObject, 150, 54);
        UnityEventTools.AddPersistentListener(repeat.onClick, controller.ShowRepeatPanel);
        UnityEventTools.AddPersistentListener(close.onClick, controller.HideShiftPicker);

        RectTransform grid = CreateScrollContent("Shift choices", panel.transform, 16, 24, 24, 12, 24, out _);
        AnchorStretch(grid.parent.parent.GetComponent<RectTransform>(), 0, 0, 1, 1, 0, 0, 0, -96);

        buttons = new List<Button>(12);
        labels = new List<Text>(12);
        Button choice = CreateButton("ShiftChoice", grid, "Shift", Vector2.zero, Vector2.zero, Input, Color.black, 32);
        AddLayoutElement(choice.gameObject,-1,132);
        GameObject choiceAsset=PrefabUtility.SaveAsPrefabAsset(choice.gameObject, PrefabFolder+"/ShiftChoice.prefab");
        Object.DestroyImmediate(choice.gameObject);
        SetObjectField(controller,"shiftChoicePrefab",choiceAsset.GetComponent<Button>());
        SetObjectField(controller,"shiftChoiceContent",grid);
        for (int row = 0; row < 0; row++)
        {
            RectTransform rowGroup = CreateHorizontalGroup("Shift Picker Row " + (row + 1), grid, 12, 0, 0, 0, 0).GetComponent<RectTransform>();
            AddLayoutElement(rowGroup.gameObject, -1, 56);
            for (int col = 0; col < 4; col++)
            {
                Button button = CreateButton("Shift Picker Button " + (row * 4 + col + 1).ToString("00"), rowGroup, "Shift", Vector2.zero, Vector2.zero, Hex("#1E293B"), TextDark, 20);
                AddOutline(button.gameObject, Border);
                AddLayoutElement(button.gameObject, 0, -1, 1);
                Text label = button.GetComponentInChildren<Text>();
                buttons.Add(button);
                labels.Add(label);
            }
        }

        panel.SetActive(false);
        return panel;
    }

    private static GameObject CreateRepeatPanel(Transform parent, CalendarController controller)
    {
        GameObject overlay = CreatePanel("Repeat Panel", parent, Hex("#000000AA"));
        overlay.GetComponent<Image>().raycastTarget = true;
        Stretch(overlay.GetComponent<RectTransform>());

        RectTransform card = CreateVerticalGroup("Repeat Card", overlay.transform, 16, 32, 32, 32, 36).GetComponent<RectTransform>();
        AnchorStretch(card, 0, 0, 1, 0, 24, 24, -24, 760);
        Image cardImage = card.gameObject.AddComponent<Image>();
        cardImage.color = Card;
        AddOutline(card.gameObject, Border);

        Text title = CreateText("Repeat Title", card, "Repeat Pattern", 36, TextDark, TextAnchor.MiddleLeft, FontStyle.Bold);
        AddLayoutElement(title.gameObject, -1, 58);
        Text note = CreateText("Repeat Note", card, "Selected dates become the pattern.", 24, TextMuted, TextAnchor.MiddleLeft, FontStyle.Normal);
        AddLayoutElement(note.gameObject, -1, 58);

        Button one = CreateButton("Repeat 1 Month", card, "Repeat 1 Month", Vector2.zero, Vector2.zero, Hex("#1E293B"), TextDark, 25);
        Button three = CreateButton("Repeat 3 Months", card, "Repeat 3 Months", Vector2.zero, Vector2.zero, Hex("#1E293B"), TextDark, 25);
        Button six = CreateButton("Repeat 6 Months", card, "Repeat 6 Months", Vector2.zero, Vector2.zero, Hex("#1E293B"), TextDark, 25);
        Button twelve = CreateButton("Repeat 12 Months", card, "Repeat 12 Months", Vector2.zero, Vector2.zero, Hex("#1E293B"), TextDark, 25);
        Button twentyFour = CreateButton("Repeat 24 Months", card, "Repeat 24 Months", Vector2.zero, Vector2.zero, Primary, Hex("#04111F"), 25);
        Button close = CreateButton("Close Repeat Panel", card, "Cancel", Vector2.zero, Vector2.zero, Hex("#1E293B"), Primary, 25);

        AddLayoutElement(one.gameObject, -1, 62);
        AddLayoutElement(three.gameObject, -1, 62);
        AddLayoutElement(six.gameObject, -1, 62);
        AddLayoutElement(twelve.gameObject, -1, 62);
        AddLayoutElement(twentyFour.gameObject, -1, 62);
        AddLayoutElement(close.gameObject, -1, 62);

        UnityEventTools.AddPersistentListener(one.onClick, controller.RepeatSelectedOneMonth);
        UnityEventTools.AddPersistentListener(three.onClick, controller.RepeatSelectedThreeMonths);
        UnityEventTools.AddPersistentListener(six.onClick, controller.RepeatSelectedSixMonths);
        UnityEventTools.AddPersistentListener(twelve.onClick, controller.RepeatSelectedTwelveMonths);
        UnityEventTools.AddPersistentListener(twentyFour.onClick, controller.RepeatSelectedTwentyFourMonths);
        UnityEventTools.AddPersistentListener(close.onClick, controller.HideRepeatPanel);

        overlay.SetActive(false);
        return overlay;
    }

    private static GameObject CreateProfileScreen(Transform parent)
    {
        GameObject screen = CreateScreen("Profile Screen", parent);
        CreateTopBar(screen.transform, "Options", false);

        Button done = CreateButton("Profile Back Button", screen.transform, "Done", Vector2.zero, Vector2.zero, Color.clear, Primary, 25);
        AnchorStretch(done.GetComponent<RectTransform>(), 1, 1, 1, 1, -190, -168, -28, -92);

        RectTransform content = CreateScrollContent("Options Scroll", screen.transform, 26, 42, 42, 38, 120, out _);
        AnchorStretch(content.parent.parent.GetComponent<RectTransform>(), 0, 0, 1, 1, 24, 136, -24, -196);

        Image avatar = CreateImage("Avatar", content, Primary);
        AddLayoutElement(avatar.gameObject, 150, 150);
        Text avatarText = CreateText("Avatar Letter", avatar.transform, "G", 48, Hex("#04111F"), TextAnchor.MiddleCenter, FontStyle.Bold);
        Stretch(avatarText.rectTransform);

        Text account = CreateText("Account Label", content, "Google account", 36, TextDark, TextAnchor.MiddleCenter, FontStyle.Bold);
        AddLayoutElement(account.gameObject, -1, 54);

        Text sync = CreateText("Sync Label", content, "Automatic sign-in stays active until you log out.", 24, TextMuted, TextAnchor.MiddleCenter, FontStyle.Normal);
        AddLayoutElement(sync.gameObject, -1, 50);

        GameObject darkRow = CreateHorizontalGroup("Dark Mode Row", content, 20, 26, 26, 18, 18);
        darkRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
        Image darkCard = darkRow.AddComponent<Image>();
        darkCard.color = Card;
        AddOutline(darkRow, Border);
        AddLayoutElement(darkRow, -1, 86);
        Text darkLabel = CreateText("Dark Mode Label", darkRow.transform, "Dark mode", 28, TextDark, TextAnchor.MiddleLeft, FontStyle.Bold);
        Toggle darkToggle = CreateToggle("Dark Mode Toggle", darkRow.transform);
        darkToggle.isOn = true;
        AddLayoutElement(darkLabel.gameObject, 0, -1, 1);
        AddLayoutElement(darkToggle.gameObject, 72, 56);

        Button settings = CreateButton("Profile Settings Button", content, "Calendar settings", Vector2.zero, Vector2.zero, Primary, Hex("#04111F"), 30);
        Button logout = CreateButton("Logout Button", content, "Log out", Vector2.zero, Vector2.zero, Danger, Color.white, 30);
        Button back = CreateButton("Back Button", content, "Back to calendar", Vector2.zero, Vector2.zero, Hex("#1E293B"), Primary, 26);
        AddLayoutElement(settings.gameObject, -1, 88);
        AddLayoutElement(logout.gameObject, -1, 88);
        AddLayoutElement(back.gameObject, -1, 76);

        RectTransform nav = CreateBottomNav(screen.transform).GetComponent<RectTransform>();
        Button calendarNav = CreateButton("Profile Calendar Button", nav, "Calendar", Vector2.zero, Vector2.zero, Input, Primary, 24);
        Button settingsNav = CreateButton("Profile Settings Nav Button", nav, "Settings", Vector2.zero, Vector2.zero, Input, Primary, 24);
        Text profileLabel = CreateText("Profile Nav Label", nav, "Profile", 24, TextDark, TextAnchor.MiddleCenter, FontStyle.Bold);
        AddLayoutElement(calendarNav.gameObject, 0, -1, 1);
        AddLayoutElement(settingsNav.gameObject, 0, -1, 1);
        AddLayoutElement(profileLabel.gameObject, 0, -1, 1);
        return screen;
    }

    private static DayDetailsPopup CreateDayDetailsPopup(Transform parent,CalendarController controller)
    {
        GameObject overlay=CreatePanel("Day Details Popup",parent,Hex("#000000E8"));Stretch(overlay.GetComponent<RectTransform>());overlay.GetComponent<Image>().raycastTarget=true;
        var card=CreateScrollContent("Day Details",overlay.transform,20,32,32,24,24,out _);
        AnchorStretch(card.parent.parent.GetComponent<RectTransform>(),0,0,1,1,24,116,-24,-160);
        DayDetailsPopup popup=overlay.AddComponent<DayDetailsPopup>();
        Text title=Body(card,"Day",96);
        Image swatch=CreateImage("Day Details Color",card,Hex("#FBBF24"));AddLayoutElement(swatch.gameObject,-1,40);
        Text shift=Body(card,"Shift",64),time=Body(card,"Time",64),hours=Body(card,"Hours",60),events=Body(card,"",180);
        InputField person=Field(card,"person","Person (optional)"),note=Field(card,"note","Notes",true);
        Command(card,"Save notes and person",popup.SaveDetails);Command(card,"Change shift",controller.OpenShiftPickerForSelection);
        Command(card,"Restore scheduled shift",popup.Restore);Command(card,"Add event",popup.AddEvent);Command(card,"Close",popup.Hide);
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
        Text label = CreateText("Top Bar Title", bar, title, 34, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
        AnchorStretch(label.rectTransform, 0, 0, 1, 1, 42, 0, -230, 0);

        if (includeMenu)
        {
            Button menu = CreateButton("Menu Button", bar, "Profile", Vector2.zero, Vector2.zero, Color.clear, Primary, 25);
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
        text.fontSize = Mathf.Max(size,parent.GetComponentInParent<CalendarDayCell>()!=null?28:36);
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
        image.color = background;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        Text text = CreateText(label + " Label", buttonObject.transform, label, fontSize, textColor, TextAnchor.MiddleCenter, FontStyle.Bold);
        Stretch(text.rectTransform);
        return button;
    }

    private static InputField CreateInputField(string name, Transform parent, string placeholderText)
    {
        GameObject inputObject = CreateUiRoot(name);
        inputObject.transform.SetParent(parent, false);
        Image background = inputObject.AddComponent<Image>();
        background.color = Input;
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
        GameObject nav = CreateHorizontalGroup("Bottom Navigation", parent, 16, 24, 24, 18, 18);
        RectTransform rect = nav.GetComponent<RectTransform>();
        AnchorStretch(rect, 0, 0, 1, 0, 0, 0, 0, 112);
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
        layout.childForceExpandWidth = true;
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
        BindButton("Agenda Back Button", navigation.ShowCalendar);
        BindButton("Options Button", navigation.ShowProfile);
        BindButton("Menu Button", navigation.ShowProfile);
        BindButton("Back Button", navigation.ShowCalendar);
        BindButton("Settings Back Button", navigation.ShowCalendar);
        BindButton("Profile Back Button", navigation.ShowCalendar);
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
