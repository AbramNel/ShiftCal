using System;
using UnityEngine;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    public class ConfirmationDialog : MonoBehaviour
    {
        public Text message;
        private Action action;
        public void Show(string text, Action accepted) { action = accepted; message.text = text; gameObject.SetActive(true); transform.SetAsLastSibling(); }
        public void Accept() { var next = action; Cancel(); next?.Invoke(); }
        public void Cancel() { action = null; gameObject.SetActive(false); }
    }
}
