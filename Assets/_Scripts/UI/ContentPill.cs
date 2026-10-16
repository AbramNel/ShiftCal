using UnityEngine;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    // Sizes the visible control to its label, outside Canvas rebuild callbacks.
    [ExecuteAlways]
    public class ContentPill : MonoBehaviour
    {
        public Text label;
        public Image icon,edit;
        public Button button;
        public float minimumWidth=180,maxWidth;
        bool editing,fittedEditing;
        string fittedText;
        float fittedParent=-1;
        public void SetValue(string value,bool hasEntry=false){label.text=value;editing=hasEntry;Fit();}
        void LateUpdate()=>Fit();
        public void Fit()
        {
            if(label==null||CanvasUpdateRegistry.IsRebuildingLayout()||CanvasUpdateRegistry.IsRebuildingGraphics())return;
            var rect=(RectTransform)transform;var parent=rect.parent as RectTransform;if(parent==null||parent.rect.width<=0)return;
            float available=parent.rect.width-Mathf.Abs(rect.anchoredPosition.x)*2;
            if(fittedText==label.text&&fittedParent==available&&fittedEditing==editing)return;
            fittedText=label.text;fittedParent=available;fittedEditing=editing;
            float textWidth=label.cachedTextGeneratorForLayout.GetPreferredWidth(label.text,label.GetGenerationSettings(Vector2.zero))/label.pixelsPerUnit;
            // Leave a small glyph/rounding allowance beyond the icon and label insets.
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,Mathf.Min(Mathf.Max(minimumWidth,textWidth+120+(editing&&edit!=null?46:0)),maxWidth>0?Mathf.Min(available,maxWidth):available));
            if(edit!=null)edit.gameObject.SetActive(editing);
            label.rectTransform.offsetMax=new Vector2(editing&&edit!=null?-72:-22,label.rectTransform.offsetMax.y);
        }
    }
}
