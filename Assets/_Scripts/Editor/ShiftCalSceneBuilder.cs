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

public static class ShiftCalSceneBuilder
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

        GameObject loginScreen = CreateLoginScreen(uiRoot.transform);
        GameObject calendarScreen = CreateCalendarScreen(uiRoot.transform, dayCellPrefab);
        GameObject settingsScreen = CreateSettingsScreen(uiRoot.transform, shiftRowPrefab);
        GameObject profileScreen = CreateProfileScreen(uiRoot.transform);

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
        rect.sizeDelta = new Vector2(138, 174);

        Image background = root.AddComponent<Image>();
        background.color = Card;
        background.raycastTarget = true;

        CalendarDayCell cell = root.AddComponent<CalendarDayCell>();

        Text day = CreateText("DayNumber", root.transform, "1", 34, TextDark, TextAnchor.UpperCenter, FontStyle.Bold);
        SetRect(day.rectTransform, new Vector2(0, 52), new Vector2(126, 44));

        Text shift = CreateText("ShiftName", root.transform, "Day-12", 23, TextDark, TextAnchor.UpperCenter, FontStyle.Normal);
        SetRect(shift.rectTransform, new Vector2(0, 5), new Vector2(126, 50));

        Text hours = CreateText("Hours", root.transform, "12h", 19, TextMuted, TextAnchor.UpperCenter, FontStyle.Bold);
        SetRect(hours.rectTransform, new Vector2(0, -42), new Vector2(126, 30));

        Text note = CreateText("PersonOrNote", root.transform, "", 18, TextMuted, TextAnchor.UpperCenter, FontStyle.Normal);
        SetRect(note.rectTransform, new Vector2(0, -70), new Vector2(126, 28));

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
        GameObject root = CreateUiRoot("ShiftSettingRow");
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1016, 72);

        Image background = root.AddComponent<Image>();
        background.color = Card;

        ShiftSettingRow row = root.AddComponent<ShiftSettingRow>();

        Text name = CreateText("NameLabel", root.transform, "Day-12", 20, TextMuted, TextAnchor.MiddleLeft, FontStyle.Bold);
        SetRect(name.rectTransform, new Vector2(-424, 22), new Vector2(150, 28));
        InputField nameInput = CreateInputField("Name Input", root.transform, "Shift name");
        SetRect(nameInput.GetComponent<RectTransform>(), new Vector2(-344, -10), new Vector2(266, 46));

        Image swatch = CreateImage("ColorSwatch", root.transform, Hex("#FBBF24"));
        SetRect(swatch.rectTransform, new Vector2(-170, 0), new Vector2(48, 48));
        Button colorButton = swatch.gameObject.AddComponent<Button>();
        colorButton.targetGraphic = swatch;
        AddOutline(swatch.gameObject, Border);

        InputField startInput = CreateInputField("Start Time Input", root.transform, "Start");
        SetRect(startInput.GetComponent<RectTransform>(), new Vector2(-50, 0), new Vector2(176, 46));
        InputField endInput = CreateInputField("End Time Input", root.transform, "End");
        SetRect(endInput.GetComponent<RectTransform>(), new Vector2(142, 0), new Vector2(176, 46));

        Text timeLabel = CreateText("TimeLabel", root.transform, "5:30 AM - 5:30 PM", 16, TextMuted, TextAnchor.MiddleLeft, FontStyle.Normal);
        SetRect(timeLabel.rectTransform, new Vector2(142, -36), new Vector2(360, 24));
        Text hoursLabel = CreateText("HoursLabel", root.transform, "12h", 22, TextDark, TextAnchor.MiddleCenter, FontStyle.Bold);
        SetRect(hoursLabel.rectTransform, new Vector2(288, 0), new Vector2(82, 46));

        Button save = CreateButton("Save Shift Row", root.transform, "Save", new Vector2(388, 0), new Vector2(112, 48), Primary, Hex("#04111F"), 21);
        Button delete = CreateButton("Delete Shift Row", root.transform, "Delete", new Vector2(502, 0), new Vector2(104, 48), Hex("#3A1722"), Danger, 20);
        Text saveLabel = save.GetComponentInChildren<Text>();

        UnityEventTools.AddPersistentListener(colorButton.onClick, row.CycleColor);
        UnityEventTools.AddPersistentListener(save.onClick, row.Save);
        UnityEventTools.AddPersistentListener(delete.onClick, row.Delete);
        UnityEventTools.AddPersistentListener(startInput.onValueChanged, row.UpdateComputedLabelsFromInput);
        UnityEventTools.AddPersistentListener(endInput.onValueChanged, row.UpdateComputedLabelsFromInput);

        SetObjectField(row, "uiNameLabel", name);
        SetObjectField(row, "uiColorSwatch", swatch);
        SetObjectField(row, "uiTimeLabel", timeLabel);
        SetObjectField(row, "uiHoursLabel", hoursLabel);
        SetObjectField(row, "nameInput", nameInput);
        SetObjectField(row, "startTimeInput", startInput);
        SetObjectField(row, "endTimeInput", endInput);
        SetObjectField(row, "colorButton", colorButton);
        SetObjectField(row, "saveButton", save);
        SetObjectField(row, "deleteButton", delete);
        SetObjectField(row, "saveButtonLabel", saveLabel);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/ShiftSettingRow.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateLoginScreen(Transform parent)
    {
        GameObject screen = CreateScreen("Login Screen", parent);
        CreateTopBar(screen.transform, "My Shift Calendar", false);

        Image logo = CreateImage("Logo", screen.transform, Primary);
        SetRect(logo.rectTransform, new Vector2(0, 460), new Vector2(132, 132));
        Text logoText = CreateText("Logo Text", logo.transform, "SHIFT\nCAL", 24, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        Stretch(logoText.rectTransform);

        Text title = CreateText("Headline", screen.transform, "Sign in to your calendar", 42, TextDark, TextAnchor.MiddleCenter, FontStyle.Bold);
        SetRect(title.rectTransform, new Vector2(0, 270), new Vector2(900, 64));

        Text subtitle = CreateText("Subhead", screen.transform, "Use your Google account to sync shifts, protect your data, and share calendars.", 24, TextMuted, TextAnchor.MiddleCenter, FontStyle.Normal);
        SetRect(subtitle.rectTransform, new Vector2(0, 205), new Vector2(850, 70));

        Button google = CreateButton("Google Sign In Button", screen.transform, "Continue with Google", new Vector2(0, 50), new Vector2(860, 96), Card, TextDark, 30);
        AddOutline(google.gameObject, Border);
        Text googleMark = CreateText("Google Mark", google.transform, "G", 32, Primary, TextAnchor.MiddleCenter, FontStyle.Bold);
        SetRect(googleMark.rectTransform, new Vector2(-350, 0), new Vector2(54, 54));

        Text note = CreateText("Privacy Note", screen.transform, "No account, no calendar access. Firebase rules will restrict group data to members only.", 21, TextMuted, TextAnchor.MiddleCenter, FontStyle.Normal);
        SetRect(note.rectTransform, new Vector2(0, -72), new Vector2(860, 64));

        Button options = CreateButton("Options Button", screen.transform, "Options", new Vector2(0, -190), new Vector2(260, 64), Hex("#1E293B"), Primary, 24);
        AddOutline(options.gameObject, Border);

        return screen;
    }

    private static GameObject CreateCalendarScreen(Transform parent, GameObject dayCellPrefab)
    {
        GameObject screen = CreateScreen("Calendar Screen", parent);
        CreateTopBar(screen.transform, "My Shift Calendar", true);

        RectTransform toolbar = CreatePanel("Toolbar", screen.transform, Hex("#111827")).GetComponent<RectTransform>();
        SetRect(toolbar, new Vector2(0, 707), new Vector2(1080, 90));
        CreateText("Toolbar Text", toolbar, "Goto        Edit        Stats        Share", 24, Hex("#45698F"), TextAnchor.MiddleCenter, FontStyle.Normal);

        Button prev = CreateButton("Previous Month Button", screen.transform, "<", new Vector2(-455, 610), new Vector2(82, 70), Color.clear, TextDark, 42);
        Button next = CreateButton("Next Month Button", screen.transform, ">", new Vector2(455, 610), new Vector2(82, 70), Color.clear, TextDark, 42);

        Text month = CreateText("Month Label", screen.transform, "July 2026", 40, TextDark, TextAnchor.MiddleCenter, FontStyle.Bold);
        SetRect(month.rectTransform, new Vector2(0, 610), new Vector2(480, 70));

        string[] weekdays = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        for (int i = 0; i < weekdays.Length; i++)
        {
            Text weekday = CreateText("Weekday " + weekdays[i], screen.transform, weekdays[i], 28, Hex("#374151"), TextAnchor.MiddleCenter, FontStyle.Normal);
            SetRect(weekday.rectTransform, new Vector2(-441 + i * 147, 545), new Vector2(135, 46));
        }

        RectTransform grid = CreatePanel("Permanent Calendar Grid", screen.transform, Color.clear).GetComponent<RectTransform>();
        SetRect(grid, new Vector2(0, -42), new Vector2(1030, 1120));

        CalendarController controller = screen.AddComponent<CalendarController>();
        DayDetailsPopup popup = CreateDayDetailsPopup(screen.transform);
        GameObject shiftPickerPanel = CreateShiftPickerPanel(screen.transform, controller, out List<Button> shiftPickerButtons, out List<Text> shiftPickerLabels);
        GameObject repeatPanel = CreateRepeatPanel(screen.transform, controller);
        Text selectionLabel = CreateText("Selection Label", screen.transform, "Select days", 24, TextMuted, TextAnchor.MiddleCenter, FontStyle.Bold);
        SetRect(selectionLabel.rectTransform, new Vector2(0, -858), new Vector2(500, 44));

        List<CalendarDayCell> cells = new List<CalendarDayCell>(42);
        for (int row = 0; row < 6; row++)
        {
            for (int col = 0; col < 7; col++)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(dayCellPrefab, grid);
                instance.name = "Day Cell " + (row * 7 + col + 1).ToString("00");
                SetRect(instance.GetComponent<RectTransform>(), new Vector2(-441 + col * 147, 455 - row * 182), new Vector2(138, 174));
                AddOutline(instance, Border);
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

        RectTransform content = CreateVerticalGroup("Settings Content", screen.transform, 24, 28, 28, 28, 18).GetComponent<RectTransform>();
        AnchorStretch(content, 0, 0, 1, 1, 32, 130, -32, -210);

        Text header = CreateText("Shift Setting Title", content, "Shift Setting", 42, Primary, TextAnchor.MiddleLeft, FontStyle.Bold);
        AddLayoutElement(header.gameObject, -1, 62);

        Text shifts = CreateText("Shifts Label", content, "Shifts", 32, TextDark, TextAnchor.MiddleLeft, FontStyle.Bold);
        AddLayoutElement(shifts.gameObject, -1, 44);

        ShiftSettingsController controller = screen.AddComponent<ShiftSettingsController>();
        List<ShiftSettingRow> rows = new List<ShiftSettingRow>();
        for (int i = 0; i < 12; i++)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(shiftRowPrefab, content);
            instance.name = "Shift Row " + (i + 1).ToString("00");
            AddOutline(instance, Border);
            AddLayoutElement(instance, -1, 74);
            rows.Add(instance.GetComponent<ShiftSettingRow>());
        }

        Text validation = CreateText("Settings Validation", screen.transform, "", 22, Danger, TextAnchor.MiddleCenter, FontStyle.Bold);
        AnchorStretch(validation.rectTransform, 0, 0, 1, 0, 32, 54, -32, 98);

        SetObjectList(controller, "rows", rows);
        SetObjectField(controller, "validationLabel", validation);
        return screen;
    }

    private static GameObject CreateShiftPickerPanel(Transform parent, CalendarController controller, out List<Button> buttons, out List<Text> labels)
    {
        GameObject panel = CreatePanel("Shift Picker Panel", parent, Hex("#020617F2"));
        RectTransform rect = panel.GetComponent<RectTransform>();
        AnchorStretch(rect, 0, 0, 1, 0, 24, 30, -24, 310);
        AddOutline(panel, Border);

        RectTransform header = CreateHorizontalGroup("Shift Picker Header", panel.transform, 18, 24, 24, 16, 6).GetComponent<RectTransform>();
        AnchorStretch(header, 0, 1, 1, 1, 0, -76, 0, 0);
        Text title = CreateText("Shift Picker Title", header, "Apply shift to selected days", 26, TextDark, TextAnchor.MiddleLeft, FontStyle.Bold);
        Button repeat = CreateButton("Repeat Selected Button", header, "Repeat", Vector2.zero, Vector2.zero, Accent, Hex("#03150F"), 23);
        Button close = CreateButton("Close Shift Picker", header, "Close", Vector2.zero, Vector2.zero, Hex("#1E293B"), Primary, 23);
        AddLayoutElement(title.gameObject, 0, -1, 1);
        AddLayoutElement(repeat.gameObject, 180, 54);
        AddLayoutElement(close.gameObject, 150, 54);
        UnityEventTools.AddPersistentListener(repeat.onClick, controller.ShowRepeatPanel);
        UnityEventTools.AddPersistentListener(close.onClick, controller.HideShiftPicker);

        RectTransform grid = CreateVerticalGroup("Shift Picker Button Rows", panel.transform, 12, 24, 24, 86, 18).GetComponent<RectTransform>();
        Stretch(grid);

        buttons = new List<Button>(12);
        labels = new List<Text>(12);
        for (int row = 0; row < 3; row++)
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
        Stretch(overlay.GetComponent<RectTransform>());

        RectTransform card = CreateVerticalGroup("Repeat Card", overlay.transform, 16, 32, 32, 32, 32).GetComponent<RectTransform>();
        SetRect(card, Vector2.zero, new Vector2(880, 560));
        Image cardImage = card.gameObject.AddComponent<Image>();
        cardImage.color = Card;
        AddOutline(card.gameObject, Border);

        Text title = CreateText("Repeat Title", card, "Repeat selected pattern", 34, TextDark, TextAnchor.MiddleLeft, FontStyle.Bold);
        AddLayoutElement(title.gameObject, -1, 58);
        Text note = CreateText("Repeat Note", card, "The selected date range becomes the pattern and repeats forward.", 24, TextMuted, TextAnchor.MiddleLeft, FontStyle.Normal);
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

        RectTransform content = CreateVerticalGroup("Options Content", screen.transform, 28, 40, 40, 40, 22).GetComponent<RectTransform>();
        AnchorStretch(content, 0, 0, 1, 1, 32, 150, -32, -120);

        Image avatar = CreateImage("Avatar", content, Primary);
        AddLayoutElement(avatar.gameObject, 150, 150);
        Text avatarText = CreateText("Avatar Letter", avatar.transform, "G", 48, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        Stretch(avatarText.rectTransform);

        Text account = CreateText("Account Label", content, "Google account", 36, TextDark, TextAnchor.MiddleCenter, FontStyle.Bold);
        AddLayoutElement(account.gameObject, -1, 54);

        Text sync = CreateText("Sync Label", content, "Automatic sign-in stays active until you log out.", 24, TextMuted, TextAnchor.MiddleCenter, FontStyle.Normal);
        AddLayoutElement(sync.gameObject, -1, 50);

        GameObject darkRow = CreateHorizontalGroup("Dark Mode Row", content, 20, 26, 26, 18, 18);
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
        return screen;
    }

    private static DayDetailsPopup CreateDayDetailsPopup(Transform parent)
    {
        GameObject overlay = CreatePanel("Day Details Popup", parent, Hex("#000000AA"));
        Stretch(overlay.GetComponent<RectTransform>());

        RectTransform card = CreateVerticalGroup("Day Details Card", overlay.transform, 18, 34, 34, 34, 34).GetComponent<RectTransform>();
        SetRect(card, Vector2.zero, new Vector2(880, 560));
        Image cardImage = card.gameObject.AddComponent<Image>();
        cardImage.color = Card;
        AddOutline(card.gameObject, Border);

        DayDetailsPopup popup = overlay.AddComponent<DayDetailsPopup>();

        Text title = CreateText("Day Details Title", card, "Monday, Jul 1, 2026", 34, TextDark, TextAnchor.MiddleLeft, FontStyle.Bold);
        AddLayoutElement(title.gameObject, -1, 58);

        Image swatch = CreateImage("Day Details Color", card, Hex("#FBBF24"));
        AddLayoutElement(swatch.gameObject, -1, 38);

        Text shift = CreateText("Day Details Shift", card, "Day-12", 32, TextDark, TextAnchor.MiddleLeft, FontStyle.Bold);
        AddLayoutElement(shift.gameObject, -1, 52);

        Text time = CreateText("Day Details Time", card, "5:30 AM - 5:30 PM", 28, TextMuted, TextAnchor.MiddleLeft, FontStyle.Normal);
        AddLayoutElement(time.gameObject, -1, 48);

        Text hours = CreateText("Day Details Hours", card, "12h", 28, Primary, TextAnchor.MiddleLeft, FontStyle.Bold);
        AddLayoutElement(hours.gameObject, -1, 48);

        Button close = CreateButton("Close Day Details", card, "Close", Vector2.zero, Vector2.zero, Primary, Hex("#04111F"), 28);
        AddLayoutElement(close.gameObject, -1, 78);
        UnityEventTools.AddPersistentListener(close.onClick, popup.Hide);

        SetObjectField(popup, "panel", overlay);
        SetObjectField(popup, "titleLabel", title);
        SetObjectField(popup, "shiftLabel", shift);
        SetObjectField(popup, "timeLabel", time);
        SetObjectField(popup, "hoursLabel", hours);
        SetObjectField(popup, "colorSwatch", swatch);
        overlay.SetActive(false);
        return popup;
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
        RectTransform status = CreatePanel("Status Bar", parent, Hex("#050505")).GetComponent<RectTransform>();
        SetRect(status, new Vector2(0, 914), new Vector2(1080, 92));
        CreateText("Status Text", status, "11:29      5G      90%", 24, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);

        RectTransform bar = CreatePanel("Top App Bar", parent, Header).GetComponent<RectTransform>();
        SetRect(bar, new Vector2(0, 825), new Vector2(1080, 88));
        Text label = CreateText("Top Bar Title", bar, title, 32, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal);
        SetRect(label.rectTransform, new Vector2(-325, 0), new Vector2(600, 70));

        if (includeMenu)
            CreateButton("Menu Button", bar, "Profile", new Vector2(425, 0), new Vector2(150, 62), Color.clear, Color.white, 25);
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
        text.fontSize = size;
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
        background.color = Hex("#1E293B");
        AddOutline(inputObject, Border);

        InputField input = inputObject.AddComponent<InputField>();
        input.targetGraphic = background;

        Text text = CreateText("Text", inputObject.transform, "", 26, TextDark, TextAnchor.MiddleLeft, FontStyle.Normal);
        AnchorStretch(text.rectTransform, 0, 0, 1, 1, 24, 8, -24, -8);
        text.supportRichText = false;

        Text placeholder = CreateText("Placeholder", inputObject.transform, placeholderText, 26, TextMuted, TextAnchor.MiddleLeft, FontStyle.Italic);
        AnchorStretch(placeholder.rectTransform, 0, 0, 1, 1, 24, 8, -24, -8);

        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
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

        Image background = CreateImage("Background", toggleObject.transform, Color.white);
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
        BindButton("Options Button", navigation.ShowProfile);
        BindButton("Menu Button", navigation.ShowProfile);
        BindButton("Back Button", navigation.ShowCalendar);
        BindButton("Profile Settings Button", navigation.ShowSettings);
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
