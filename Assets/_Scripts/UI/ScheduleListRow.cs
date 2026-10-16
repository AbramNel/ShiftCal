using System;
using UnityEngine;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    public class ScheduleListRow : MonoBehaviour
    {
        public Text title, subtitle;
        public Button first, second, third;
        public void Bind(string name,string detail,string a,Action one,string b,Action two,string c,Action three)
        {
            title.text=name;subtitle.text=detail;var open=GetComponent<Button>();open.onClick.RemoveAllListeners();if(one!=null)open.onClick.AddListener(()=>one());Wire(first,a,one);Wire(second,b,two);Wire(third,c,three);
            int count=(one!=null?1:0)+(two!=null?1:0)+(three!=null?1:0);var layout=first.transform.parent.GetComponent<HorizontalLayoutGroup>();layout.childForceExpandWidth=false;layout.childAlignment=TextAnchor.MiddleLeft;
            foreach(var action in new[]{first,second,third}){var size=action.GetComponent<LayoutElement>();size.preferredWidth=count>1?0:240;size.flexibleWidth=count>1?1:0;}
        }
        private static void Wire(Button button,string label,Action action){button.GetComponentInChildren<Text>(true).text=label;button.gameObject.SetActive(action!=null);button.onClick.RemoveAllListeners();if(action!=null)button.onClick.AddListener(()=>action());}
    }
}
