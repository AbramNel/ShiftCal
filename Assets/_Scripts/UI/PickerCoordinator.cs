using System;
using ShiftCal.App;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ShiftCal.UI
{
    public class PickerCoordinator : MonoBehaviour
    {
        [Serializable] private class Result { public string token; public bool accepted; public string value; }
        private static PickerField pending;
        private static string token;
        public static bool IsOpen => pending != null;
        public static void Open(PickerField field)
        {
            if (pending != null) return;
            EventSystem.current?.SetSelectedGameObject(null);
            pending = field; token = Guid.NewGuid().ToString("N");
#if UNITY_ANDROID && !UNITY_EDITOR
            string error = AndroidBridge.Call<string>(field.isDate ? "pickDate" : "pickTime", token, field.value);
            if (!string.IsNullOrEmpty(error)) { pending = null; Debug.LogWarning(error); }
#else
            UnityEngine.Object.FindFirstObjectByType<UnityPickerDialog>(FindObjectsInactive.Include).Show(field, token);
#endif
        }
        [UnityEngine.Scripting.Preserve]
        public void OnPickerResult(string json)
        {
            var result = JsonUtility.FromJson<Result>(json);
            Complete(result.token, result.accepted, result.value);
        }
        public static void Complete(string request, bool accepted, string value)
        {
            if (request != token || pending == null) return;
            var field = pending; pending = null; token = null;
            if (accepted && field.isActiveAndEnabled) field.Set(value, true);
        }
        public static void Cancel(PickerField field)
        {
            if (pending != field) return;
            AndroidBridge.Call<string>("cancelPicker", token);
            UnityEngine.Object.FindFirstObjectByType<UnityPickerDialog>(FindObjectsInactive.Include)?.Cancel();
            pending = null; token = null;
        }
    }
}
