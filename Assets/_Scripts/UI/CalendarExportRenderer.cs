using System;
using ShiftCal.Core;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShiftCal.UI
{
    public static class CalendarExportRenderer
    {
        public const int Width = 2100, Height = 2460;
        // Spatial isolation as well as a dedicated layer prevents the export appearing on screen.
        const int Layer = 31;
        public static byte[] Png(CalendarExportSnapshot data, Font font, Sprite rounded, Sprite frame, Sprite note)
        {
            GameObject root = null, cameraObject = null;
            RenderTexture target = null;
            Texture2D pixels = null;
            var previous = RenderTexture.active;
            try
            {
                if (data.days.Count != 42) throw new ArgumentException("A calendar export needs 42 dates.");
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                if (!target.Create()) throw new InvalidOperationException("Could not create the calendar image.");
                cameraObject = new GameObject("Calendar export camera", typeof(Camera));
                var camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false; camera.orthographic = true; camera.orthographicSize = Height / 2f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = ThemeManager.Token(ThemeManager.Role.Background);
                camera.cullingMask = 1 << Layer; camera.nearClipPlane = .1f; camera.farClipPlane = 10;
                camera.transform.position = new Vector3(100000, 100000, -2); camera.targetTexture = target;
                root = new GameObject("Isolated calendar export", typeof(RectTransform), typeof(Canvas));
                root.layer = Layer;
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera; canvas.pixelPerfect = true;
                var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(Width, Height);
                rect.position = new Vector3(100000, 100000, 0);
                Text(root.transform, "Month", data.title, 78, ThemeManager.Token(ThemeManager.Role.Text), 48, 52, Width-96, 104, font, TextAnchor.MiddleLeft);
                if (!string.IsNullOrEmpty(data.filterLabel)) Text(root.transform, "Activity filter", data.filterLabel, 34,
                    ThemeManager.Token(ThemeManager.Role.Muted), 52, 157, Width-104, 56, font, TextAnchor.MiddleLeft);
                var weekdays = new[] { "SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT" };
                const float margin = 48, gap = 12, gridTop = 300, cellHeight = 342;
                float cellWidth = (Width - margin*2 - gap*6) / 7;
                for (int i=0;i<7;i++) Text(root.transform, "Weekday", weekdays[i], 34, ThemeManager.Token(ThemeManager.Role.Muted), margin+i*(cellWidth+gap), 233, cellWidth, 48, font);
                for (int i=0;i<42;i++)
                {
                    var day = data.days[i]; float x=margin+i%7*(cellWidth+gap), y=gridTop+i/7*(cellHeight+gap);
                    Color color=ThemeManager.ShiftColor(day.shiftColor), ink=ThemeManager.Legible(color);
                    var cell=Image(root.transform, "Date "+day.dateKey, rounded, color, x,y,cellWidth,cellHeight);
                    Text(cell.transform,"Day number",day.dayNumber.ToString(),48,ink,20,16,cellWidth-40,68,font,TextAnchor.MiddleLeft);
                    var name=Text(cell.transform,"Shift",day.shiftName,37,ink,20,100,cellWidth-40,112,font,TextAnchor.UpperLeft);
                    name.resizeTextForBestFit=true; name.resizeTextMinSize=25; name.resizeTextMaxSize=37;
                    name.horizontalOverflow=HorizontalWrapMode.Wrap;
                    if(day.hasNote)Image(cell.transform,"Shared note",note,ink,cellWidth-57,33,32,32);
                    int shown=Mathf.Min(day.activities.Count,day.activities.Count>3?2:3);
                    for(int a=0;a<shown;a++)
                    {
                        var item=day.activities[a]; ColorUtility.TryParseHtmlString(item.assignmentColor,out var assignment);
                        var badge=Image(cell.transform,"Assignment",frame,assignment,18+a*72,cellHeight-88,64,64);
                        Image(badge.transform,"Shared activity",ActivityIconSet.Load().Get(item.icon),ink,10,10,44,44).preserveAspect=true;
                    }
                    if(day.activities.Count>shown)Text(cell.transform,"Overflow","+"+(day.activities.Count-shown),34,ink,cellWidth-69,cellHeight-84,57,54,font,TextAnchor.MiddleRight);
                    if(!day.currentMonth)Image(cell.transform,"Adjacent month",rounded,ThemeManager.Token(ThemeManager.Role.DimmedDate),0,0,cellWidth,cellHeight);
                }
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=target;
                pixels=new Texture2D(Width,Height,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,Width,Height),0,0);pixels.Apply();
                byte[] png=pixels.EncodeToPNG();
                if(png==null||png.Length<8)throw new InvalidOperationException("Calendar PNG encoding failed.");
                return png;
            }
            finally
            {
                RenderTexture.active=previous;
                if(cameraObject!=null)cameraObject.GetComponent<Camera>().targetTexture=null;
                if(root!=null)root.SetActive(false);
                if(target!=null)target.Release();
                Release(pixels);Release(target);Release(root);Release(cameraObject);
            }
        }
        static void Release(Object item){if(item==null)return;if(Application.isPlaying)Object.Destroy(item);else Object.DestroyImmediate(item);}
        static RectTransform Rect(Transform parent,string name,float x,float y,float width,float height)
        {
            var go=new GameObject(name,typeof(RectTransform));go.layer=Layer;go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);return rect;
        }
        static Image Image(Transform parent,string name,Sprite sprite,Color color,float x,float y,float width,float height)
        {
            var image=Rect(parent,name,x,y,width,height).gameObject.AddComponent<Image>();
            image.sprite=sprite;image.color=color;image.raycastTarget=false;if(sprite!=null)image.type=UnityEngine.UI.Image.Type.Sliced;return image;
        }
        static Text Text(Transform parent,string name,string value,int size,Color color,float x,float y,float width,float height,Font font,TextAnchor align=TextAnchor.MiddleCenter)
        {
            var text=Rect(parent,name,x,y,width,height).gameObject.AddComponent<Text>();text.font=font;text.text=value;
            text.fontSize=size;text.fontStyle=FontStyle.Bold;text.color=color;text.alignment=align;text.supportRichText=false;
            text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;return text;
        }
    }
}
