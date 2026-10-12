using UnityEngine;
namespace ShiftCal.UI
{
    // Controllers live on the UI root; observe the actual screen's lifecycle.
    public class ProfileDraftReturn : MonoBehaviour
    {
        public FamilyProfileController controller;
        void OnDisable()=>controller?.ReturnToDraft();
    }
}
