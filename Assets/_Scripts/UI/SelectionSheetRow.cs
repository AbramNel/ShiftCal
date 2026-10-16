using System;
using UnityEngine;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    public class SelectionSheetRow : MonoBehaviour
    {
        public Button button;
        public Text label,description;
        public Image icon,swatch,selected;
        public void Bind(SelectionItem item,bool chosen,Action clicked)
        {
            label.text=item.text;description.text=item.description??"";description.gameObject.SetActive(!string.IsNullOrEmpty(item.description));
            GetComponent<LayoutElement>().minHeight=string.IsNullOrEmpty(item.description)?132:168;
            icon.sprite=item.icon;icon.gameObject.SetActive(item.icon!=null);swatch.gameObject.SetActive(ColorUtility.TryParseHtmlString(item.color??"",out var color));swatch.color=color;
            button.interactable=item.enabled;button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>clicked());Select(chosen);
        }
        public void Select(bool value)=>selected.gameObject.SetActive(value);
    }
}
