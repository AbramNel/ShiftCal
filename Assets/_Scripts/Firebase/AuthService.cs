using UnityEngine;
using ShiftCal.App;
#if SHIFT_CAL_USE_FIREBASE
using Firebase.Auth;
using Firebase.Extensions;
#endif
namespace ShiftCal.Firebase
{
    public class AuthService : MonoBehaviour
    {
        public static AuthService Instance;
        public bool IsSignedIn { get; private set; }
        public bool LocalMode { get; private set; }
        public bool HasCachedAccess => PlayerPrefs.HasKey("ShiftCal.ActiveAccount.v3");
        public string UserId { get; private set; }
        public string DisplayName { get; private set; }
        public string Email { get; private set; }
        public string Status { get; private set; } = "";
        private bool busy;
        private int generation;
        private bool restored;
        private void Awake(){if(Instance!=null){Destroy(this);return;}Instance=this;LocalMode=PlayerPrefs.GetString("ShiftCal.ActiveAccount.v3")=="local";DisplayName=PlayerPrefs.GetString("ShiftCal.AccountLabel","On this device");Email=PlayerPrefs.GetString("ShiftCal.AccountEmail","");}
        public void SignIn()=>SignInWithGoogle();
        public void UseLocal(){SignOut();LocalMode=true;DisplayName="On this device";AppSession.Instance.SwitchAccount("local");}
        public void SignInWithGoogle()
        {
            if(busy)return;
#if SHIFT_CAL_USE_FIREBASE
            if(FirebaseBootstrap.Instance==null||!FirebaseBootstrap.Instance.Ready){Status=FirebaseBootstrap.Instance?.ConfigurationError??"Firebase is still initializing. Retry shortly.";return;}
            var config=Resources.Load<ShiftCalConfig>("ShiftCalConfig");
            if(config==null||string.IsNullOrWhiteSpace(config.webClientId)){Status="Set ShiftCal's Web/server OAuth client ID and google-services.json.";return;}
            generation++;busy=true;Status="Choose your Google account";
#if UNITY_ANDROID && !UNITY_EDITOR
            string error=AndroidBridge.Call<string>("signIn",config.webClientId);if(!string.IsNullOrEmpty(error))OnGoogleError(error);
#else
            OnGoogleError("Google account selection is available on Android. Use local mode in the Editor.");
#endif
#else
            Status="Google/Firebase is not configured for ShiftCal. Use local mode or follow FIREBASE_SETUP.md.";
#endif
        }
        [UnityEngine.Scripting.Preserve] public void OnGoogleToken(string token){if(busy)SignInWithGoogleTokens(token,null);}
        [UnityEngine.Scripting.Preserve] public void OnGoogleError(string error){busy=false;Status=error;}
        public void SignInWithGoogleTokens(string idToken,string accessToken)
        {
#if SHIFT_CAL_USE_FIREBASE
            if(!FirebaseBootstrap.Instance.Ready){OnGoogleError("Firebase is not ready.");return;}
            int request=generation;
            FirebaseBootstrap.Instance.Auth.SignInWithCredentialAsync(GoogleAuthProvider.GetCredential(idToken,accessToken)).ContinueWithOnMainThread(t=>{
                if(request!=generation)return;
                busy=false;if(t.IsCanceled||t.IsFaulted){Status="Sign-in failed. Retry: "+t.Exception?.GetBaseException().Message;return;}Apply(t.Result);
            });
#else
            OnGoogleError("Firebase configuration required.");
#endif
        }
        public void RefreshExistingSignIn()
        {
#if SHIFT_CAL_USE_FIREBASE
            if(restored||LocalMode||FirebaseBootstrap.Instance==null||!FirebaseBootstrap.Instance.Ready)return;
            restored=true;
            if(PlayerPrefs.GetInt("ShiftCal.CloudSignedOut",0)==1){FirebaseBootstrap.Instance.Auth.SignOut();return;}
            if(FirebaseBootstrap.Instance.Auth.CurrentUser!=null)Apply(FirebaseBootstrap.Instance.Auth.CurrentUser);
#endif
        }
        public void SignOut()
        {
            generation++;
            FirestoreService.Instance?.StopListening();AndroidBridge.Action("deactivate");AndroidBridge.Call<string>("signOut");
#if SHIFT_CAL_USE_FIREBASE
            FirebaseBootstrap.Instance?.Auth?.SignOut();
#endif
            PlayerPrefs.DeleteKey("ShiftCal.ActiveAccount.v3");PlayerPrefs.DeleteKey("ShiftCal.AccountLabel");PlayerPrefs.DeleteKey("ShiftCal.AccountEmail");PlayerPrefs.Save();
            PlayerPrefs.SetInt("ShiftCal.CloudSignedOut",1);PlayerPrefs.Save();
            IsSignedIn=false;LocalMode=false;UserId="";DisplayName="";Email="";busy=false;restored=true;
        }
#if SHIFT_CAL_USE_FIREBASE
        private void Apply(FirebaseUser user){
            bool google=false;foreach(var provider in user.ProviderData)if(provider.ProviderId=="google.com")google=true;
            if(!google){FirebaseBootstrap.Instance.Auth.SignOut();Status="A Google account is required for cloud sign-in.";return;}
            IsSignedIn=true;LocalMode=false;UserId=user.UserId;DisplayName=string.IsNullOrEmpty(user.DisplayName)?user.Email:user.DisplayName;
            PlayerPrefs.DeleteKey("ShiftCal.CloudSignedOut");
            Email=user.Email;PlayerPrefs.SetString("ShiftCal.AccountEmail",Email??"");PlayerPrefs.SetString("ShiftCal.AccountLabel",DisplayName);
            Status="Signed in";AppSession.Instance.SwitchAccount(UserId);
        }
#endif
    }
}
