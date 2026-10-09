using UnityEngine;
namespace ShiftCal.UI
{
    public class ActivityIconChoice : MonoBehaviour
    {
        public string id;
        public ActivityEditor editor;
        public void Select()=>editor.SelectIcon(id);
    }
}
