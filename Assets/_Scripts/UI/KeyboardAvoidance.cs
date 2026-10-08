using UnityEngine;
namespace ShiftCal.UI
{
    public class KeyboardAvoidance : MonoBehaviour
    {
        private RectTransform rect;
        private Vector2 original;
        private void OnEnable() { rect = (RectTransform)transform; original = rect.offsetMin; }
        private void LateUpdate()
        {
            var canvas = GetComponentInParent<Canvas>();
            float height = TouchScreenKeyboard.visible ? TouchScreenKeyboard.area.height / canvas.scaleFactor : 0;
            rect.offsetMin = original + new Vector2(0, height);
        }
        private void OnDisable() { if (rect != null) rect.offsetMin = original; }
    }
}
