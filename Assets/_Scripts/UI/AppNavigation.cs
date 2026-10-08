using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    public class AppNavigation : MonoBehaviour
    {
        public static AppNavigation Instance { get; private set; }
        [SerializeField] private GameObject loginScreen, calendarScreen, settingsScreen, profileScreen;
        public ConfirmationDialog confirmation;
        private readonly List<GameObject> history = new List<GameObject>();
        private readonly List<ModalPanel> modals = new List<ModalPanel>();
        public GameObject CurrentScreen => history.Count == 0 ? null : history[history.Count - 1];
        public int HistoryCount => history.Count;
        private void Awake()
        {
            Instance = this;
            Input.backButtonLeavesApp = false;
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        private void Start() => ShowLogin();
        private void Update()
        {
            bool back = false;
#if ENABLE_INPUT_SYSTEM
            back = UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true;
#elif ENABLE_LEGACY_INPUT_MANAGER
            back = Input.GetKeyDown(KeyCode.Escape);
#endif
            if (back) Back();
            var auth = Firebase.AuthService.Instance;
            if (auth == null) return;
            auth.RefreshExistingSignIn();
            if ((auth.IsSignedIn || auth.LocalMode || auth.HasCachedAccess) && CurrentScreen == loginScreen) ShowCalendar();
        }
        public void Register(ModalPanel panel) { modals.Remove(panel); modals.Add(panel); }
        public void Unregister(ModalPanel panel) => modals.Remove(panel);
        public void Confirm(string text, Action accepted) => confirmation.Show(text, accepted);
        public void Back()
        {
            var focused = EventSystem.current?.currentSelectedGameObject?.GetComponent<InputField>();
            if (TouchScreenKeyboard.visible || focused != null && focused.isFocused)
            {
                if (focused?.touchScreenKeyboard != null) focused.touchScreenKeyboard.active = false;
                focused?.DeactivateInputField(); EventSystem.current?.SetSelectedGameObject(null); return;
            }
            foreach (var dropdown in GetComponentsInChildren<Dropdown>(true))
                if (dropdown.transform.Find("Dropdown List") != null) { dropdown.Hide(); return; }
            modals.RemoveAll(m => m == null || !m.gameObject.activeInHierarchy);
            if (modals.Count > 0) { modals[modals.Count - 1].RequestClose(); return; }
            var cal = calendarScreen.GetComponent<CalendarController>();
            if (CurrentScreen == calendarScreen && cal.IsEditing) { cal.SetEditing(false); return; }
            if (history.Count > 1) { CurrentScreen.SetActive(false); history.RemoveAt(history.Count - 1); CurrentScreen.SetActive(true); return; }
            App.AndroidBridge.Action("background");
        }
        public void Open(GameObject screen)
        {
            if (screen == null || screen == CurrentScreen) return;
            CloseModals(); CurrentScreen?.SetActive(false);
            int index = history.IndexOf(screen);
            if (index >= 0) history.RemoveRange(index + 1, history.Count - index - 1);
            else history.Add(screen);
            screen.SetActive(true); screen.transform.SetAsLastSibling();
        }
        private void CloseModals() { foreach (var modal in modals.ToArray()) if (modal != null) modal.CloseNow(); modals.Clear(); }
        public void ShowLogin()
        {
            CloseModals(); foreach (var screen in history) if (screen != null) screen.SetActive(false);
            UnityEngine.Object.FindFirstObjectByType<ScheduleWorkbench>(FindObjectsInactive.Include)?.Hide();
            calendarScreen.SetActive(false); settingsScreen.SetActive(false); profileScreen.SetActive(false);
            history.Clear(); history.Add(loginScreen); loginScreen.SetActive(true);
        }
        public void ShowCalendar()
        {
            if (CurrentScreen == loginScreen) { loginScreen.SetActive(false); history.Clear(); }
            Open(calendarScreen);
        }
        public void ShowSettings() => Open(settingsScreen);
        public void ShowProfile() => Open(profileScreen);
        public void OnGoogleSignInPressed() => Firebase.AuthService.Instance?.SignInWithGoogle();
        public void OnLogoutPressed()
        {
            Firebase.AuthService.Instance?.SignOut(); App.AppSession.Instance.SwitchAccount("local", false);
            App.AndroidBridge.Action("deactivate"); ShowLogin();
        }
        public void OnLocalPressed() { Firebase.AuthService.Instance.UseLocal(); ShowCalendar(); }
    }
}
