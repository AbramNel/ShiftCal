using UnityEngine;
using UnityEngine.EventSystems;
namespace ShiftCal.UI
{
    public class PreviewOutsideDismiss : MonoBehaviour
    {
        private readonly System.Collections.Generic.List<RaycastResult> hits = new System.Collections.Generic.List<RaycastResult>();
        private void Update()
        {
            Vector2 position = Vector2.zero; bool pressed = false;
#if ENABLE_INPUT_SYSTEM
            var touch = UnityEngine.InputSystem.Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) { pressed = true; position = touch.primaryTouch.position.ReadValue(); }
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) { pressed = true; position = mouse.position.ReadValue(); }
#elif ENABLE_LEGACY_INPUT_MANAGER
            pressed = Input.GetMouseButtonDown(0);position = Input.mousePosition;
#endif
            if (!pressed) return;
            var data=new PointerEventData(EventSystem.current){position=position};hits.Clear();EventSystem.current.RaycastAll(data,hits);
            if (hits.Count > 0 && (hits[0].gameObject.GetComponentInParent<DayPreview>() != null || hits[0].gameObject.GetComponentInParent<CalendarDayCell>() != null)) return;
            GetComponent<DayPreview>().Hide();
        }
    }
}
