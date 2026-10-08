using UnityEngine;
namespace ShiftCal.UI
{
    public class ExpandableSection : MonoBehaviour
    {
        public GameObject content;
        public void Toggle() => content.SetActive(!content.activeSelf);
    }
}
