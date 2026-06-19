using UnityEngine;
using UnityEngine.Events;

namespace ShiftCal.UI
{
    public class ClickableButton : MonoBehaviour
    {
        [SerializeField] private UnityEvent onClick = new UnityEvent();

        public UnityEvent OnClick => onClick;

        private void OnMouseUpAsButton()
        {
            onClick.Invoke();
        }
    }
}
