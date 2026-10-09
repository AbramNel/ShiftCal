using System;
using ShiftCal.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static partial class ShiftCalSceneBuilder
{
    private static Sprite Rounded => Artwork("Rounded", (x,y) => {
        float dx=Mathf.Max(Mathf.Abs(x-32)-18,0),dy=Mathf.Max(Mathf.Abs(y-32)-18,0);
        return Mathf.Clamp01(12.5f-Mathf.Sqrt(dx*dx+dy*dy));
    },new Vector4(16,16,16,16));
    private static Sprite Icon(string name) => Artwork(name,(x,y)=> {
        if(name=="Note"){
            bool edge=(x>14&&x<50&&y>9&&y<55)&&(x<18||x>46||y<13||y>51);
            bool lines=x>23&&x<42&&((y>23&&y<27)||(y>34&&y<38));
            bool rings=(x>24&&x<28||x>36&&x<40)&&y>49&&y<60;
            return edge||lines||rings?1:0;
        }
        float d=Vector2.Distance(new Vector2(x,y),new Vector2(32,29));
        bool circle=d>18&&d<22,hand=x>30&&x<34&&y>27&&y<44||y>27&&y<31&&x>30&&x<43;
        bool bells=Vector2.Distance(new Vector2(x,y),new Vector2(17,52))<8||Vector2.Distance(new Vector2(x,y),new Vector2(47,52))<8;
        bool feet=y>4&&y<12&&(x>15&&x<20||x>44&&x<49);
        return circle||hand||(name!="Clock"&&(bells||feet))?1:0;
    },Vector4.zero);
    private static Sprite SelectionFrame => Artwork("SelectionFrame",(x,y)=> {
        float dx=Mathf.Max(Mathf.Abs(x-32)-18,0),dy=Mathf.Max(Mathf.Abs(y-32)-18,0);
        float distance=Mathf.Sqrt(dx*dx+dy*dy);
        return Mathf.Clamp01(12.5f-distance)-Mathf.Clamp01(3.5f-distance);
    },new Vector4(16,16,16,16));
    private static Sprite Artwork(string name,Func<float,float,float> alpha,Vector4 border)
    {
        string path="Assets/_UI/"+name+".png";
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(sprite!=null)return sprite;
        var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);
        for(int y=0;y<64;y++)for(int x=0;x<64;x++) { float a=0; for(int sy=0;sy<2;sy++)for(int sx=0;sx<2;sx++)a+=alpha(x+sx*.5f,y+sy*.5f)/4; texture.SetPixel(x,y,new Color(1,1,1,a)); }
        texture.Apply();System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spriteBorder=border;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    private static DayPreview CreateDayPreview(Transform parent,DayDetailsPopup editor)
    {
        // A low backdrop keeps the calendar available for directly choosing another day.
        var overlay=CreatePanel("Compact Day Preview",parent,Color.clear);Stretch(overlay.GetComponent<RectTransform>());overlay.GetComponent<Image>().raycastTarget=true;
        overlay.AddComponent<ModalPanel>();var preview=overlay.AddComponent<DayPreview>();preview.editor=editor;
        var card=CreatePanel("Preview card",overlay.transform,Card);preview.card=card.GetComponent<RectTransform>();
        AnchorStretch(preview.card,0,0,1,0,24,24,-24,624);card.GetComponent<Image>().raycastTarget=true;
        var open=card.AddComponent<Button>();open.targetGraphic=card.GetComponent<Image>();open.transition=Selectable.Transition.None;UnityEventTools.AddPersistentListener(open.onClick,preview.OpenDetails);
        preview.title=CreateText("Full date",card.transform,"Date",42,TextDark,TextAnchor.MiddleLeft,FontStyle.Bold);AnchorStretch(preview.title.rectTransform,0,1,1,1,28,-100,-140,-18);
        var close=CreateButton("Close preview",card.transform,"×",Vector2.zero,Vector2.zero,Input,Primary,48);AnchorStretch(close.GetComponent<RectTransform>(),1,1,1,1,-132,-128,-12,-8);UnityEventTools.AddPersistentListener(close.onClick,preview.Hide);
        preview.swatch=CreateImage("Preview shift color",card.transform,Hex("#FBBF24"));preview.swatch.sprite=Rounded;preview.swatch.type=Image.Type.Sliced;preview.swatch.raycastTarget=false;AnchorStretch(preview.swatch.rectTransform,0,1,0,1,28,-164,76,-116);
        preview.shift=CreateText("Shift",card.transform,"Shift",40,TextDark,TextAnchor.MiddleLeft,FontStyle.Bold);AnchorStretch(preview.shift.rectTransform,0,1,1,1,96,-176,-28,-110);
        var content=CreateScrollContent("Preview details",card.transform,0,28,28,4,4,out var scroll);preview.scroll=scroll.GetComponent<RectTransform>();AnchorStretch(preview.scroll,0,0,1,1,0,132,0,-190);
        preview.details=Body(content,"Details",100);var layout=preview.details.GetComponent<LayoutElement>();layout.preferredHeight=-1;
        var full=CreateButton("Open full details",card.transform,"Open full details  >",Vector2.zero,Vector2.zero,Input,Primary,38);AnchorStretch(full.GetComponent<RectTransform>(),0,0,1,0,24,12,-24,120);UnityEventTools.AddPersistentListener(full.onClick,preview.OpenDetails);
        // Don't block date taps in the upper calendar; blank space is dismissed by the parent callback.
        overlay.GetComponent<Image>().raycastTarget=false;
        overlay.AddComponent<PreviewOutsideDismiss>();
        overlay.SetActive(false);return preview;
    }
    private static ShiftEditor CreateShiftEditor(Transform parent)
    {
        var overlay=CreatePanel("Shift Editor",parent,Hex("#000000AA"));Stretch(overlay.GetComponent<RectTransform>());overlay.GetComponent<Image>().raycastTarget=true;
        overlay.AddComponent<ModalPanel>();overlay.AddComponent<KeyboardAvoidance>();var editor=overlay.AddComponent<ShiftEditor>();
        var card=CreatePanel("Shift editor card",overlay.transform,Card);AnchorStretch(card.GetComponent<RectTransform>(),0,0,1,1,24,24,-24,-24);card.GetComponent<Image>().raycastTarget=true;
        var content=CreateScrollContent("Shift fields",card.transform,12,28,28,24,24,out _);AnchorStretch(content.parent.parent.GetComponent<RectTransform>(),0,0,1,1,0,164,0,-160);
        ModalHeader(card.transform,"Edit shift",overlay.GetComponent<ModalPanel>());
        editor.nameInput=Field(content,"shiftName","Name");editor.nameError=ErrorText(content);
        var palette=CreateHorizontalGroup("Color palette",content,14,0,0,0,0);AddLayoutElement(palette,-1,132);
        foreach(var hex in new[]{"#FBBF24","#9FF4F1","#6D7DF2","#F59AC8","#F4A261","#34D399"}) {
            var button=CreateButton("Color "+hex,palette.transform,"",Vector2.zero,Vector2.zero,Hex(hex),TextDark,30);AddLayoutElement(button.gameObject,0,-1,1);
            UnityEventTools.AddStringPersistentListener(button.onClick,editor.Color,hex);
        }
        editor.swatch=CreateImage("Chosen color",content,Hex("#FBBF24"));editor.swatch.sprite=Rounded;editor.swatch.type=Image.Type.Sliced;AddLayoutElement(editor.swatch.gameObject,-1,34);
        Body(content,"Shift times (optional)",48);var times=CreateHorizontalGroup("Shift time pickers",content,18,0,0,0,0);AddLayoutElement(times,-1,120);editor.startPicker=Picker(times.transform,"Shift start",false,true);editor.endPicker=Picker(times.transform,"Shift end",false,true);AddLayoutElement(editor.startPicker.gameObject,0,120,1);AddLayoutElement(editor.endPicker.gameObject,0,120,1);editor.timeError=ErrorText(content);
        editor.hours=Body(content,"Calculated hours",64);
        Command(content,"Clear shift times",editor.ClearTimes);
        Command(content,"Advanced color",editor.ToggleAdvanced);
        editor.advanced=CreateVerticalGroup("Custom hex color",content,8,0,0,0,0);editor.advanced.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        editor.colorInput=Field(editor.advanced.transform,"shiftColor","Custom color (#FBBF24)");editor.colorError=ErrorText(editor.advanced.transform);
        editor.delete=Command(content,"Delete custom shift",editor.Delete);
        UnityEventTools.AddPersistentListener(editor.colorInput.onValueChanged,editor.PreviewChanged);UnityEventTools.AddPersistentListener(editor.startPicker.changed,editor.PreviewChanged);UnityEventTools.AddPersistentListener(editor.endPicker.changed,editor.PreviewChanged);
        var footer=CreateHorizontalGroup("Editor actions",card.transform,18,24,24,12,12);AnchorStretch(footer.GetComponent<RectTransform>(),0,0,1,0,0,8,0,150);
        var save=Command(footer.transform,"Save",editor.Save);AddLayoutElement(save.gameObject,0,-1,1);save.GetComponent<Image>().color=Primary;save.GetComponentInChildren<Text>().color=Hex("#04111F");
        overlay.SetActive(false);return editor;
    }
    private static Text ErrorText(Transform parent)
    {
        var text=CreateText("Validation",parent,"",32,Danger,TextAnchor.MiddleLeft,FontStyle.Normal);return text;
    }
    private static ConfirmationDialog CreateConfirmation(Transform parent)
    {
        var overlay=CreatePanel("Confirmation",parent,Hex("#000000AA"));Stretch(overlay.GetComponent<RectTransform>());overlay.GetComponent<Image>().raycastTarget=true;overlay.AddComponent<ModalPanel>();
        var dialog=overlay.AddComponent<ConfirmationDialog>();var card=CreateVerticalGroup("Confirmation card",overlay.transform,24,32,32,32,32);var image=card.AddComponent<Image>();image.sprite=Rounded;image.type=Image.Type.Sliced;image.color=Card;image.raycastTarget=true;
        AnchorStretch(card.GetComponent<RectTransform>(),0,.5f,1,.5f,48,-180,-48,180);
        dialog.message=Body(card.transform,"Discard unsaved changes?",120);
        var actions=CreateHorizontalGroup("Confirm actions",card.transform,18,0,0,0,0);AddLayoutElement(actions,-1,132);
        var cancel=Command(actions.transform,"Cancel",dialog.Cancel);var accept=Command(actions.transform,"Confirm",dialog.Accept);AddLayoutElement(cancel.gameObject,0,-1,1);AddLayoutElement(accept.gameObject,0,-1,1);
        overlay.SetActive(false);return dialog;
    }
    private static Transform Expandable(Transform parent,string title)
    {
        var button=Command(parent,title+"  ▾",null);var section=button.gameObject.AddComponent<ExpandableSection>();
        var content=CreateVerticalGroup(title+" content",parent,16,0,0,0,0);content.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        section.content=content;UnityEventTools.AddPersistentListener(button.onClick,section.Toggle);content.SetActive(false);return content.transform;
    }
    private static GameObject CreateMainSettingsScreen(Transform parent)
    {
        var screen=CreateScreen("Settings Screen",parent);CreateTopBar(screen.transform,"Settings");
        var back=CreateButton("Settings Back Button",screen.transform,"Back",Vector2.zero,Vector2.zero,Color.clear,Primary,38);
        AnchorStretch(back.GetComponent<RectTransform>(),1,1,1,1,-192,-148,-24,-16);
        var content=CreateScrollContent("Settings Scroll",screen.transform,24,24,24,24,24,out var scroll);
        AnchorStretch(scroll.GetComponent<RectTransform>(),0,0,1,1,0,24,0,-174);
        var appearance=SettingsCard(content,"Appearance");
        var previews=CreateHorizontalGroup("Theme previews",appearance,16,0,0,0,0);AddLayoutElement(previews,-1,174);
        for(int i=0;i<3;i++)
        {
            var preview=CreatePanel("Theme preview",previews.transform,Hex(new[]{"#171B22","#10292E","#F4F3EF"}[i]));
            AddLayoutElement(preview,0,-1,1);preview.GetComponent<Image>().raycastTarget=true;
            preview.GetComponent<Image>().sprite=Rounded;preview.GetComponent<Image>().type=Image.Type.Sliced;
            var button=preview.AddComponent<Button>();button.targetGraphic=preview.GetComponent<Image>();button.transition=Selectable.Transition.None;
            var sample=preview.AddComponent<ThemeSample>();sample.choice=(ThemeManager.Theme)i;UnityEventTools.AddPersistentListener(button.onClick,sample.Select);
            var label=CreateText("Sample",preview.transform,new[]{"Midnight\nGraphite","Deep\nTeal","Soft\nDaylight"}[i],34,i==2?Hex("#17263C"):Color.white,TextAnchor.MiddleCenter,FontStyle.Bold);
            AnchorStretch(label.rectTransform,0,0,1,1,8,54,-8,-8);
            sample.selectedLabel=CreateText("Theme state",preview.transform,"Tap to apply",27,i==2?Hex("#57667B"):Hex("#ADB9CA"),TextAnchor.MiddleCenter,FontStyle.Normal);
            AnchorStretch(sample.selectedLabel.rectTransform,0,0,1,0,4,12,-4,48);
            sample.selectedBorder=CreateImage("Selected theme",preview.transform,Primary);sample.selectedBorder.sprite=SelectionFrame;sample.selectedBorder.type=Image.Type.Sliced;sample.selectedBorder.raycastTarget=false;Stretch(sample.selectedBorder.rectTransform);
            sample.Refresh();
        }
        var calendar=SettingsCard(content,"Calendar");
        Disclosure(calendar,"Manage Shifts",null);Disclosure(calendar,"Events & Alarms",null);
        var account=SettingsCard(content,"Account");
        var summary=Disclosure(account,"Account summary",null);summary.GetComponentInChildren<Text>().name="Account Label";summary.GetComponentInChildren<Text>().text="On this device  >";
        var sync=CreateText("Sync Label",account,"Stored on this device",34,TextMuted,TextAnchor.MiddleLeft,FontStyle.Normal);
        AddLayoutElement(sync.gameObject,-1,-1);
        return screen;
    }
    private static Transform SettingsCard(Transform parent,string title)
    {
        var card=CreateVerticalGroup(title+" section",parent,12,24,24,20,20);
        var image=card.AddComponent<Image>();image.sprite=Rounded;image.type=Image.Type.Sliced;image.color=Card;
        card.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var heading=Body(card.transform,title,54);heading.fontSize=34;heading.fontStyle=FontStyle.Bold;heading.color=TextMuted;
        return card.transform;
    }
    private static Button Disclosure(Transform parent,string title,UnityEngine.Events.UnityAction action)
    {
        var button=Command(parent,title+"  >",action);button.name=title;
        button.GetComponentInChildren<Text>().alignment=TextAnchor.MiddleLeft;
        AnchorStretch(button.GetComponentInChildren<Text>().rectTransform,0,0,1,1,24,0,-18,0);
        AddLayoutElement(button.gameObject,-1,144);return button;
    }
    private static void ModalHeader(Transform card,string title,ModalPanel modal)
    {
        var heading=CreateText("Modal title",card,title,44,TextDark,TextAnchor.MiddleLeft,FontStyle.Bold);
        AnchorStretch(heading.rectTransform,0,1,1,1,28,-150,-180,-12);
        var close=CreateButton("Close "+title,card,"×",Vector2.zero,Vector2.zero,Input,Primary,48);
        AnchorStretch(close.GetComponent<RectTransform>(),1,1,1,1,-160,-148,-24,-12);
        UnityEventTools.AddPersistentListener(close.onClick,modal.RequestBack);
    }
    private static GameObject CreateAccountPopup(Transform parent)
    {
        var overlay=CreatePanel("Account Popup",parent,Hex("#000000AA"));Stretch(overlay.GetComponent<RectTransform>());overlay.GetComponent<Image>().raycastTarget=true;
        var modal=overlay.AddComponent<ModalPanel>();
        var card=CreatePanel("Account card",overlay.transform,Card);AnchorStretch(card.GetComponent<RectTransform>(),0,.5f,1,.5f,24,-410,-24,410);card.GetComponent<Image>().raycastTarget=true;
        ModalHeader(card.transform,"Account / Profile",modal);
        var content=CreateScrollContent("Account details",card.transform,24,28,28,12,24,out var scroll);
        AnchorStretch(scroll.GetComponent<RectTransform>(),0,0,1,1,0,12,0,-160);
        var name=CreateText("Account Details Label",content,"On this device",44,TextDark,TextAnchor.MiddleLeft,FontStyle.Bold);AddLayoutElement(name.gameObject,-1,-1);
        var state=CreateText("Account State Label",content,"Local calendar on this device",36,TextMuted,TextAnchor.MiddleLeft,FontStyle.Normal);AddLayoutElement(state.gameObject,-1,-1);
        Command(content,"Connect Google account",null);
        var logout=Command(content,"Sign out",null);logout.name="Logout Button";logout.GetComponentInChildren<Text>().color=Danger;
        overlay.SetActive(false);return overlay;
    }
    private static ShiftPickerController CreateShiftPicker(Transform parent,CalendarController calendar)
    {
        var panel=CreateScreen("Change Shift",parent);panel.GetComponent<Image>().raycastTarget=true;
        var modal=panel.AddComponent<ModalPanel>();modal.dismissOutside=false;
        var picker=panel.AddComponent<ShiftPickerController>();picker.calendar=calendar;
        ModalHeader(panel.transform,"Change Shift",modal);
        picker.scopeLabel=CreateText("Shift scope",panel.transform,"Apply to the focused day",32,Primary,TextAnchor.MiddleLeft,FontStyle.Bold);
        AnchorStretch(picker.scopeLabel.rectTransform,0,1,1,1,32,-232,-32,-164);
        var strip=CreateUiRoot("Day navigator");strip.transform.SetParent(panel.transform,false);
        AnchorStretch(strip.GetComponent<RectTransform>(),0,1,1,1,24,-392,-24,-244);
        strip.AddComponent<Image>().color=Color.clear;
        var navigator=strip.AddComponent<ShiftDayNavigator>();navigator.horizontal=true;navigator.vertical=false;navigator.inertia=false;navigator.movementType=ScrollRect.MovementType.Clamped;navigator.picker=picker;picker.navigator=navigator;
        var viewport=CreatePanel("Day viewport",strip.transform,Color.clear);Stretch(viewport.GetComponent<RectTransform>());viewport.GetComponent<Image>().raycastTarget=true;viewport.AddComponent<RectMask2D>();navigator.viewport=viewport.GetComponent<RectTransform>();
        var days=CreateHorizontalGroup("Adjacent dates",viewport.transform,8,0,0,0,0);var rect=days.GetComponent<RectTransform>();rect.anchorMin=new Vector2(0,0);rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,.5f);rect.sizeDelta=new Vector2(3100,0);navigator.content=rect;
        for(int i=0;i<21;i++)
        {
            var day=CreateButton("Adjacent date "+i,days.transform,"14",Vector2.zero,Vector2.zero,Input,TextDark,40);day.transition=Selectable.Transition.None;AddLayoutElement(day.gameObject,136,140);
            AnchorStretch(day.GetComponentInChildren<Text>().rectTransform,0,0,1,1,4,46,-4,-8);
            var month=CreateText("Month",day.transform,"Oct",26,TextDark,TextAnchor.MiddleCenter,FontStyle.Normal);AnchorStretch(month.rectTransform,0,0,1,0,4,10,-4,42);
            var focus=CreateImage("Focused date",day.transform,Primary);focus.sprite=SelectionFrame;focus.type=Image.Type.Sliced;focus.raycastTarget=false;Stretch(focus.rectTransform);AddOutline(focus.gameObject,Background);
            picker.dayButtons.Add(day);
        }
        picker.dateLabel=CreateText("Focused date label",panel.transform,"Wednesday, October 14",40,TextDark,TextAnchor.MiddleLeft,FontStyle.Bold);AnchorStretch(picker.dateLabel.rectTransform,0,1,1,1,32,-468,-32,-406);
        picker.currentShift=CreateText("Current shift",panel.transform,"Current shift",34,TextMuted,TextAnchor.MiddleLeft,FontStyle.Normal);AnchorStretch(picker.currentShift.rectTransform,0,1,1,1,32,-550,-32,-468);
        picker.choices=CreateScrollContent("Available shifts",panel.transform,14,24,24,12,24,out var scroll);AnchorStretch(scroll.GetComponent<RectTransform>(),0,0,1,1,0,150,0,-566);
        picker.feedback=CreateText("Applied feedback",panel.transform,"",34,Primary,TextAnchor.MiddleLeft,FontStyle.Normal);AnchorStretch(picker.feedback.rectTransform,0,0,1,0,32,24,-32,140);
        picker.repeat=CreateButton("Repeat selected pattern",panel.transform,"Repeat",Vector2.zero,Vector2.zero,Input,Primary,34);AnchorStretch(picker.repeat.GetComponent<RectTransform>(),1,0,1,0,-242,24,-24,148);UnityEventTools.AddPersistentListener(picker.repeat.onClick,picker.RepeatPattern);
        // The feedback reserves width for Repeat only in bulk scope.
        picker.choicePrefab=BuildShiftChoicePrefab();panel.SetActive(false);return picker;
    }
    private static ShiftChoiceRow BuildShiftChoicePrefab()
    {
        var row=CreateHorizontalGroup("ShiftChoice",null,24,24,24,16,16);AddLayoutElement(row,-1,164);
        var image=row.AddComponent<Image>();image.sprite=Rounded;image.type=Image.Type.Sliced;image.color=Card;image.raycastTarget=true;
        var choice=row.AddComponent<ShiftChoiceRow>();choice.button=row.AddComponent<Button>();choice.button.targetGraphic=image;
        choice.swatch=CreateImage("Choice swatch",row.transform,Hex("#FBBF24"));choice.swatch.sprite=Rounded;choice.swatch.type=Image.Type.Sliced;choice.swatch.raycastTarget=false;AddLayoutElement(choice.swatch.gameObject,72,92);
        var labels=CreateVerticalGroup("Shift labels",row.transform,4,0,0,0,0);AddLayoutElement(labels,0,-1,1);
        choice.title=CreateText("Choice name",labels.transform,"Day-12",42,TextDark,TextAnchor.MiddleLeft,FontStyle.Bold);AddLayoutElement(choice.title.gameObject,-1,56);
        choice.time=CreateText("Choice time",labels.transform,"5:30 AM – 5:30 PM",34,TextMuted,TextAnchor.MiddleLeft,FontStyle.Normal);AddLayoutElement(choice.time.gameObject,-1,50);
        BakeThemes(row.transform);var prefab=PrefabUtility.SaveAsPrefabAsset(row,PrefabFolder+"/ShiftChoice.prefab");Object.DestroyImmediate(row);return prefab.GetComponent<ShiftChoiceRow>();
    }

}
