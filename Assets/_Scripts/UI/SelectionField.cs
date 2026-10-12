using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ShiftCal.UI
{
    [Serializable] public class SelectionItem
    {
        public string id, text, description;
        public Sprite icon;
        public string color;
        public bool enabled = true;
        public SelectionItem(string label, string key = null, string detail = null) { text=label; id=key; description=detail; }
    }
    // A real Button opens a shared sheet; no Dropdown templates or extra Canvas.
    public class SelectionField : MonoBehaviour
    {
        public string heading;
        public Button button;
        public Text captionText;
        public Image swatch;
        public bool multiple;
        public string emptyLabel = "Select people";
        public List<SelectionItem> options = new List<SelectionItem>();
        [SerializeField] private int selected;
        public List<int> selectedValues = new List<int>();
        public UnityEvent<int> onValueChanged = new UnityEvent<int>();
        public UnityEvent onSelectionChanged = new UnityEvent();
        public UnityEvent onCanceled = new UnityEvent();
        public bool interactable { get => button.interactable; set => button.interactable=value; }
        public int value { get => selected; set { int previous=selected; SetValueWithoutNotify(value); if(previous!=selected)onValueChanged.Invoke(selected); } }
        public void SetValueWithoutNotify(int index) { selected=Mathf.Clamp(index,0,Math.Max(0,options.Count-1)); RefreshCaption(); }
        public void SetSelected(IEnumerable<int> values, bool notify=false)
        {
            selectedValues=values.Distinct().Where(i=>i>=0&&i<options.Count).OrderBy(i=>i).ToList();RefreshCaption();if(notify)onSelectionChanged.Invoke();
        }
        public void ClearOptions() { options.Clear(); selected=0; selectedValues.Clear(); RefreshCaption(); }
        public void AddOptions(List<string> values) { options.AddRange(values.Select(s=>new SelectionItem(s)));RefreshCaption(); }
        public void AddOptions(List<SelectionItem> values) { options.AddRange(values);RefreshCaption(); }
        public void SetOptions(IEnumerable<SelectionItem> values) { options=values.ToList(); selected=Mathf.Clamp(selected,0,Math.Max(0,options.Count-1));RefreshCaption(); }
        public virtual List<SelectionItem> Items => options;
        public void RefreshCaption()
        {
            if(captionText==null)return;
            captionText.text=multiple ? selectedValues.Count==0?emptyLabel:selectedValues.Count<=2?string.Join(", ",selectedValues.Where(i=>i<options.Count).Select(i=>options[i].text)):selectedValues.Count+" people"
                :options.Count>selected?options[selected].text:emptyLabel;
            if(swatch!=null){string hex=options.Count>selected?options[selected].color:null;swatch.gameObject.SetActive(ColorUtility.TryParseHtmlString(hex??"",out var color));swatch.color=color;}
        }
        public void Open() { if(interactable&&options.Count>0)SelectionSheet.Instance?.Show(this); }
        public void Hide() { if(SelectionSheet.Instance?.Owner==this)SelectionSheet.Instance.Cancel(); }
        private void OnDisable()=>Hide();
    }
}
