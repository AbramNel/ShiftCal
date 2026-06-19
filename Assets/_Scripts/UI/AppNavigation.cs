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

            if (ShiftCal.Firebase.AuthService.Instance.IsSignedIn && loginScreen != null && loginScreen.activeSelf)
                ShowCalendar();
        }

        public void ShowLogin()
        {
            SetScreen(loginScreen, true);
            SetScreen(calendarScreen, false);
            SetScreen(settingsScreen, false);
            SetScreen(profileScreen, false);
        }

        public void ShowCalendar()
        {
            SetScreen(loginScreen, false);
            SetScreen(calendarScreen, true);
            SetScreen(settingsScreen, false);
            SetScreen(profileScreen, false);
        }

        public void ShowSettings()
        {
            SetScreen(loginScreen, false);
            SetScreen(calendarScreen, false);
            SetScreen(settingsScreen, true);
            SetScreen(profileScreen, false);
        }

        public void ShowProfile()
        {
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

            ShowLogin();
        }

        private static void SetScreen(GameObject screen, bool visible)
        {
            if (screen != null)
                screen.SetActive(visible);
        }
    }
}
