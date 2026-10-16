using System;
using UnityEngine;
using UnityEngine.UI;
using ShiftCal.App;
using ShiftCal.Core;
namespace ShiftCal.UI
{
    public class FamilyActivityRow : MonoBehaviour
    {
        public Image icon, frame;
        public Text title, detail;
        public Button button;
        void OnEnable(){ThemeManager.Changed+=PaintIcon;PaintIcon();}
        void OnDisable()=>ThemeManager.Changed-=PaintIcon;
        void PaintIcon(){if(ThemedBorder&&frame!=null)frame.color=ThemeManager.Token(IsAlarm?ThemeManager.Role.Accent:ThemeManager.Role.Muted);if(icon!=null)icon.color=ThemeManager.Token(ThemeManager.Role.Text);if(title!=null)title.color=ThemeManager.Token(ThemeManager.Role.Text);if(detail!=null)detail.color=ThemeManager.Token(ThemeManager.Role.Muted);}
        public void Bind(ActivityOccurrence item,Action clicked)
        {
            Color color;if(!ColorUtility.TryParseHtmlString(AppSession.Instance.Data.profiles.Find(x=>x.id==item.activity.assigneeId)?.color??"#94A3B8",out color))color=Color.gray;
            BindInfo(ActivityIconSet.Load().Get(item.activity.icon),item.activity.title,item.TimeLabel+" · "+ActivityResolver.People(AppSession.Instance.Data,item.activity),color,clicked);

        }
        public string SourceId {get;set;}
        public bool IsAlarm {get;set;}
        public bool ThemedBorder {get;set;}
        public void BindInfo(Sprite symbol,string text,string summary,Color border,Action clicked)
        {
            icon.sprite=symbol;title.text=text;detail.text=summary;frame.color=border;PaintIcon();button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>clicked());
        }
    }
}
