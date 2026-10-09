using UnityEngine;
namespace ShiftCal.UI
{
    public class KeyboardAvoidance : MonoBehaviour
    {
        private RectTransform rect;
        private Vector2 original;
        private float nextPoll, nativeHeight;
        private void OnEnable() { rect = (RectTransform)transform; original = rect.offsetMin; }
        private void LateUpdate()
        {
            var canvas = GetComponentInParent<Canvas>();
            float height = TouchScreenKeyboard.visible ? TouchScreenKeyboard.area.height / canvas.scaleFactor : 0;
#if UNITY_ANDROID && !UNITY_EDITOR
            // Android often reports an empty TouchScreenKeyboard.area. IME window
            // insets provide the actual occupied pixels above the gesture area.
            if (Time.unscaledTime >= nextPoll) { nextPoll=Time.unscaledTime+.1f; nativeHeight=App.AndroidBridge.Call<int>("keyboardHeight"); }
            height = Mathf.Max(height, nativeHeight / canvas.scaleFactor);
#endif
            var next = original + new Vector2(0, height);
            if (rect.offsetMin != next) rect.offsetMin = next;
        }
        private void OnDisable() { if (rect != null) rect.offsetMin = original; }
    }
}
