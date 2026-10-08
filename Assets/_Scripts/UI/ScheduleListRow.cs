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
        }
        private static void Wire(Button button,string label,Action action){button.gameObject.SetActive(action!=null);button.GetComponentInChildren<Text>().text=label;button.onClick.RemoveAllListeners();if(action!=null)button.onClick.AddListener(()=>action());}
    }
}
