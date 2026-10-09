using UnityEngine;
namespace ShiftCal.UI
{
    public class ExpandableSection : MonoBehaviour
    {
        public GameObject content;
        public void Toggle(){content.SetActive(!content.activeSelf);var icon=transform.Find("Navigation sprite")??GetComponentInChildren<UnityEngine.UI.Text>()?.transform.Find("Navigation sprite");if(icon!=null)icon.localRotation=Quaternion.Euler(0,0,content.activeSelf?180:0);}
    }
}
