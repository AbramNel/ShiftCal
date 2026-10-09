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
        void PaintIcon(){if(icon!=null)icon.color=ThemeManager.Token(ThemeManager.Role.Text);if(title!=null)title.color=ThemeManager.Token(ThemeManager.Role.Text);if(detail!=null)detail.color=ThemeManager.Token(ThemeManager.Role.Muted);}
        public void Bind(ActivityOccurrence item,Action clicked)
        {
            icon.sprite=ActivityIconSet.Load().Get(item.activity.icon);icon.color=ThemeManager.Token(ThemeManager.Role.Text);
            title.text=item.activity.title;detail.text=item.TimeLabel+" · "+ActivityResolver.People(AppSession.Instance.Data,item.activity);
            Color color;if(!ColorUtility.TryParseHtmlString(AppSession.Instance.Data.profiles.Find(x=>x.id==item.activity.assigneeId)?.color??"#94A3B8",out color))color=Color.gray;
            frame.color=color;PaintIcon();button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>clicked());
        }
    }
}
