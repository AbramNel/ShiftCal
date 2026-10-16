using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShiftCal.UI
{
    public class SelectionSheet : MonoBehaviour
    {
        public static SelectionSheet Instance => UnityEngine.Object.FindFirstObjectByType<SelectionSheet>(FindObjectsInactive.Include);
        public Text heading;
        public Transform content;
        public ScrollRect scroll;
        public RectTransform card;
        public SelectionSheetRow rowPrefab;
        public Button done;
        public GameObject actions;
        public SelectionField Owner { get; private set; }
        public bool IsOpen=>gameObject.activeSelf;
        private HashSet<int> staged=new HashSet<int>();
        private List<SelectionItem> shown=new List<SelectionItem>();
        private int fitFrames;
        public void Show(SelectionField field)
        {
            if(IsOpen)return;
            EventSystem.current?.SetSelectedGameObject(null);
            Owner=field;heading.text=field.heading;staged=field.multiple?new HashSet<int>(field.selectedValues):new HashSet<int>{field.value};
            FamilyWorkbench.Clear(content);gameObject.SetActive(true);transform.SetAsLastSibling();
            GetComponent<ModalPanel>().Close=Cancel;actions.SetActive(field.multiple);done.gameObject.SetActive(field.multiple);
            var viewport=(RectTransform)scroll.transform;viewport.offsetMin=new Vector2(viewport.offsetMin.x,field.multiple?162:18);
            var items=field.Items;shown=items.ToList();
            float desired=(field.multiple?310:166)+items.Sum(i=>string.IsNullOrEmpty(i.description)?144:180);card.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Min(desired,((RectTransform)transform).rect.height*.87f));
            for(int i=0;i<items.Count;i++) { int index=i;var row=Instantiate(rowPrefab,content);row.Bind(items[i],staged.Contains(i),()=>Choose(index)); }
            scroll.verticalNormalizedPosition=1;
            fitFrames=2;
        }
        void LateUpdate(){if(IsOpen&&fitFrames>0&&--fitFrames==0)FitHeight();}
        public void FitHeight()
        {
            if(!IsOpen||CanvasUpdateRegistry.IsRebuildingLayout()||CanvasUpdateRegistry.IsRebuildingGraphics())return;float height=(Owner.multiple?310:166)+LayoutUtility.GetPreferredHeight((RectTransform)content);
            card.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Min(height,((RectTransform)transform).rect.height*.87f));
        }
        public void Choose(int index)
        {
            if(Owner==null||index<0||index>=shown.Count)return;int current=CurrentIndex(shown[index]);
            if(current<0||!Owner.interactable||!Owner.Items[current].enabled){Cancel();return;}
            if(!Owner.multiple){var field=Owner;Close();field.SetValueWithoutNotify(current);field.onValueChanged.Invoke(current);return;}
            if(!staged.Add(index))staged.Remove(index);
            var rows=content.GetComponentsInChildren<SelectionSheetRow>();for(int i=0;i<rows.Length;i++)rows[i].Select(staged.Contains(i));
        }
        int CurrentIndex(SelectionItem item)=>Owner.Items.FindIndex(i=>string.IsNullOrEmpty(item.id)?string.IsNullOrEmpty(i.id)&&i.text==item.text:i.id==item.id);
        public void Accept(){if(Owner==null)return;var field=Owner;var values=staged.Select(i=>CurrentIndex(shown[i])).Where(i=>i>=0).ToArray();Close();field.SetSelected(values,true);}
        public void Cancel(){var field=Owner;Close();field?.onCanceled.Invoke();}
        void Close(){Owner=null;gameObject.SetActive(false);}
        private void OnDisable(){Owner=null;}
    }
}
