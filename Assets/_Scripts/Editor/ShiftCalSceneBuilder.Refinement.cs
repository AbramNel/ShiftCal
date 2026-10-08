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
        return circle||hand||bells||feet?1:0;
    },Vector4.zero);
    private static Sprite SelectionBorder => Artwork("SelectionBorder",(x,y)=> {
        float dx=Mathf.Max(Mathf.Abs(x-32)-18,0),dy=Mathf.Max(Mathf.Abs(y-32)-18,0);
        float distance=Mathf.Sqrt(dx*dx+dy*dy);
        return Mathf.Clamp01(12.5f-distance)-Mathf.Clamp01(9.5f-distance);
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
        AnchorStretch(preview.card,0,0,1,0,24,160,-24,760);card.GetComponent<Image>().raycastTarget=true;
        var open=card.AddComponent<Button>();open.targetGraphic=card.GetComponent<Image>();open.transition=Selectable.Transition.None;UnityEventTools.AddPersistentListener(open.onClick,preview.OpenDetails);
        preview.title=CreateText("Full date",card.transform,"Date",42,TextDark,TextAnchor.MiddleLeft,FontStyle.Bold);AnchorStretch(preview.title.rectTransform,0,1,1,1,28,-100,-140,-18);
        var close=CreateButton("Close preview",card.transform,"×",Vector2.zero,Vector2.zero,Input,Primary,48);AnchorStretch(close.GetComponent<RectTransform>(),1,1,1,1,-132,-128,-12,-8);UnityEventTools.AddPersistentListener(close.onClick,preview.Hide);
        preview.swatch=CreateImage("Preview shift color",card.transform,Hex("#FBBF24"));preview.swatch.sprite=Rounded;preview.swatch.type=Image.Type.Sliced;preview.swatch.raycastTarget=false;AnchorStretch(preview.swatch.rectTransform,0,1,0,1,28,-164,76,-116);
        preview.shift=CreateText("Shift",card.transform,"Shift",40,TextDark,TextAnchor.MiddleLeft,FontStyle.Bold);AnchorStretch(preview.shift.rectTransform,0,1,1,1,96,-176,-28,-110);
        var content=CreateScrollContent("Preview details",card.transform,0,28,28,4,4,out var scroll);preview.scroll=scroll.GetComponent<RectTransform>();AnchorStretch(preview.scroll,0,0,1,1,0,132,0,-190);
        preview.details=Body(content,"Details",100);var layout=preview.details.GetComponent<LayoutElement>();layout.preferredHeight=-1;preview.details.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
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
        var card=CreatePanel("Shift editor card",overlay.transform,Card);AnchorStretch(card.GetComponent<RectTransform>(),0,0,1,1,24,174,-24,-120);card.GetComponent<Image>().raycastTarget=true;
        var content=CreateScrollContent("Shift fields",card.transform,12,28,28,24,24,out _);AnchorStretch(content.parent.parent.GetComponent<RectTransform>(),0,0,1,1,0,164,0,0);
        Body(content,"Edit shift",70).fontStyle=FontStyle.Bold;
        editor.nameInput=Field(content,"shiftName","Name");editor.nameError=ErrorText(content);
        var palette=CreateHorizontalGroup("Color palette",content,14,0,0,0,0);AddLayoutElement(palette,-1,132);
        foreach(var hex in new[]{"#FBBF24","#9FF4F1","#6D7DF2","#F59AC8","#F4A261","#34D399"}) {
            var button=CreateButton("Color "+hex,palette.transform,"",Vector2.zero,Vector2.zero,Hex(hex),TextDark,30);AddLayoutElement(button.gameObject,0,-1,1);
            UnityEventTools.AddStringPersistentListener(button.onClick,editor.Color,hex);
        }
        editor.swatch=CreateImage("Chosen color",content,Hex("#FBBF24"));editor.swatch.sprite=Rounded;editor.swatch.type=Image.Type.Sliced;AddLayoutElement(editor.swatch.gameObject,-1,34);
        editor.startInput=Field(content,"shiftStart","Start time (5:30 AM or 17:30)");editor.endInput=Field(content,"shiftEnd","End time (leave both empty for OFF)");editor.timeError=ErrorText(content);
        editor.hours=Body(content,"Calculated hours",64);
        Command(content,"Advanced color",editor.ToggleAdvanced);
        editor.advanced=CreateVerticalGroup("Custom hex color",content,8,0,0,0,0);editor.advanced.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        editor.colorInput=Field(editor.advanced.transform,"shiftColor","Custom color (#FBBF24)");editor.colorError=ErrorText(editor.advanced.transform);
        editor.delete=Command(content,"Delete custom shift",editor.Delete);
        foreach(var input in new[]{editor.startInput,editor.endInput,editor.colorInput})UnityEventTools.AddPersistentListener(input.onValueChanged,editor.PreviewChanged);
        var footer=CreateHorizontalGroup("Editor actions",card.transform,18,24,24,12,12);AnchorStretch(footer.GetComponent<RectTransform>(),0,0,1,0,0,8,0,150);
        var cancel=Command(footer.transform,"Cancel",editor.Cancel);var save=Command(footer.transform,"Save",editor.Save);AddLayoutElement(cancel.gameObject,0,-1,1);AddLayoutElement(save.gameObject,0,-1,1);save.GetComponent<Image>().color=Primary;save.GetComponentInChildren<Text>().color=Hex("#04111F");
        overlay.SetActive(false);return editor;
    }
    private static Text ErrorText(Transform parent)
    {
        var text=CreateText("Validation",parent,"",32,Danger,TextAnchor.MiddleLeft,FontStyle.Normal);text.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;return text;
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
    private static void PopulateNavigation(Transform root,AppNavigation navigation,ScheduleWorkbench work)
    {
        foreach(string name in new[]{"Calendar Screen","Settings Screen","Profile Screen","Events and Alarms"})
        {
            var screen=FindChild(root,name);var nav=screen.Find("Bottom Navigation");
            if(nav==null)nav=CreateBottomNav(screen).transform;
            for(int i=nav.childCount-1;i>=0;i--)Object.DestroyImmediate(nav.GetChild(i).gameObject);
            string active=name=="Calendar Screen"?"Calendar":name=="Settings Screen"?"Settings":name=="Profile Screen"?"Profile":"Events";
            string[] labels={"Calendar","Events","Settings","Profile"};
            UnityEngine.Events.UnityAction[] actions={navigation.ShowCalendar,work.ShowAgenda,navigation.ShowSettings,navigation.ShowProfile};
            for(int i=0;i<labels.Length;i++)
            {
                bool selected=labels[i]==active;
                var button=CreateButton(active+" navigation "+labels[i],nav,labels[i],Vector2.zero,Vector2.zero,selected?Input:Color.clear,selected?Primary:TextMuted,36);
                AddLayoutElement(button.gameObject,0,-1,1);UnityEventTools.AddPersistentListener(button.onClick,actions[i]);
            }
        }
    }
}
