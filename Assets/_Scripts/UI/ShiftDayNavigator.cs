using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    // ScrollRect owns horizontal gestures; the picker snaps only after its drag ends.
    public class ShiftDayNavigator : ScrollRect
    {
        public ShiftPickerController picker;
        public override void OnEndDrag(PointerEventData data)
        {
            base.OnEndDrag(data);
            picker.RequestSnap();
        }
    }
}
