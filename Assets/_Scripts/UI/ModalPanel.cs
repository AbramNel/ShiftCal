using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace ShiftCal.UI
{
    public class ModalPanel : MonoBehaviour, IPointerClickHandler
    {
        public Func<bool> HasChanges;
        public Action Close;
        public bool dismissOutside = true;
        private void OnEnable() => AppNavigation.Instance?.Register(this);
        private void OnDisable() => AppNavigation.Instance?.Unregister(this);
        public void RequestBack() { if (AppNavigation.Instance != null) AppNavigation.Instance.Back(); else RequestClose(); }
        public void RequestClose()
        {
            if (HasChanges?.Invoke() == true) AppNavigation.Instance?.Confirm("Discard unsaved changes?", CloseNow);
            else CloseNow();
        }
        public void CloseNow() { if (Close != null) Close(); else gameObject.SetActive(false); }
        public void OnPointerClick(PointerEventData data)
        {
            if (dismissOutside && data.pointerPressRaycast.gameObject == gameObject) RequestClose();
        }
    }
}
