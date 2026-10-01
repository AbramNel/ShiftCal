using UnityEngine;

namespace ShiftCal.UI
{
    public class AppNavigation : MonoBehaviour
    {
        [SerializeField] private GameObject loginScreen;
        [SerializeField] private GameObject calendarScreen;
        [SerializeField] private GameObject settingsScreen;
        [SerializeField] private GameObject profileScreen;

        private void Start()
        {
            ShowLogin();
        }

        private void Update()
        {
            if (ShiftCal.Firebase.AuthService.Instance == null)
                return;

            ShiftCal.Firebase.AuthService.Instance.RefreshExistingSignIn();

            if ((ShiftCal.Firebase.AuthService.Instance.IsSignedIn || ShiftCal.Firebase.AuthService.Instance.LocalMode || ShiftCal.Firebase.AuthService.Instance.HasCachedAccess) && loginScreen != null && loginScreen.activeSelf)
                ShowCalendar();
        }

        public void ShowLogin()
        {
            Object.FindFirstObjectByType<ScheduleWorkbench>(FindObjectsInactive.Include)?.Hide();
            SetScreen(loginScreen, true);
            SetScreen(calendarScreen, false);
            SetScreen(settingsScreen, false);
            SetScreen(profileScreen, false);
        }

        public void ShowCalendar()
        {
            Object.FindFirstObjectByType<ScheduleWorkbench>(FindObjectsInactive.Include)?.Hide();
            SetScreen(loginScreen, false);
            SetScreen(calendarScreen, true);
            SetScreen(settingsScreen, false);
            SetScreen(profileScreen, false);
        }

        public void ShowSettings()
        {
            Object.FindFirstObjectByType<ScheduleWorkbench>(FindObjectsInactive.Include)?.Hide();
            SetScreen(loginScreen, false);
            SetScreen(calendarScreen, false);
            SetScreen(settingsScreen, true);
            SetScreen(profileScreen, false);
        }

        public void ShowProfile()
        {
            Object.FindFirstObjectByType<ScheduleWorkbench>(FindObjectsInactive.Include)?.Hide();
            SetScreen(loginScreen, false);
            SetScreen(calendarScreen, false);
            SetScreen(settingsScreen, false);
            SetScreen(profileScreen, true);
        }

        public void OnGoogleSignInPressed()
        {
            if (ShiftCal.Firebase.AuthService.Instance == null)
            {
                Debug.LogWarning("AuthService is missing from the scene.");
                return;
            }

            ShiftCal.Firebase.AuthService.Instance.SignInWithGoogle();
        }

        public void OnLogoutPressed()
        {
            if (ShiftCal.Firebase.AuthService.Instance != null)
                ShiftCal.Firebase.AuthService.Instance.SignOut();

            ShiftCal.App.AppSession.Instance.SwitchAccount("local",false);
            ShiftCal.App.AndroidBridge.Action("deactivate");
            ShowLogin();
        }

        private static void SetScreen(GameObject screen, bool visible)
        {
            if (screen != null)
                screen.SetActive(visible);
        }
        public void OnLocalPressed() { ShiftCal.Firebase.AuthService.Instance.UseLocal(); ShowCalendar(); }
    }
}
