using UnityEngine;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    public class ToggleRow : MonoBehaviour
    {
        public Toggle toggle;
        public void Flip(){if(toggle.interactable)toggle.isOn=!toggle.isOn;}
    }
}
