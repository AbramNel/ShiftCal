using System;
using System.Collections;
using System.IO;
using ShiftCal.App;
using ShiftCal.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftCal.UI
{
    public class CalendarImageExporter : MonoBehaviour
    {
        public CalendarController calendar;
        public Font font;
        public Sprite rounded,frame,note;
        public SelectionField options;
        public Button shareButton,optionsButton;
        public GameObject statusPanel;
        public Text statusLabel;
        public bool IsBusy {get;private set;}
        public string LastPath {get;private set;}
        public string LastAction {get;private set;}
        string request;
        float hideStatusAt;
        [Serializable] class Result { public string token,status,message; }
        void Update(){if(statusPanel.activeSelf&&!IsBusy&&Time.unscaledTime>hideStatusAt)statusPanel.SetActive(false);}
        public void Share()=>Begin(false);
        public void SaveImage()=>Begin(true);
        public void OpenOptions(){if(!IsBusy)options.Open();}
        public void Choose(int choice){if(choice==1)SaveImage();else Share();}
        void Begin(bool save)
        {
            if(IsBusy||AppSession.Instance?.Data==null)return;
            IsBusy=true;shareButton.interactable=optionsButton.interactable=false;
            request=Guid.NewGuid().ToString("N");LastAction=save?"save":"share";Message("Preparing calendar image…");
            // Snapshot the requested month/filter before yielding. Export never edits the live calendar.
            CalendarExportSnapshot snapshot;
            try{snapshot=CalendarExportSnapshot.Create(AppSession.Instance.Data,calendar.currentMonth,AppSession.Instance.CalendarOverrides,FamilyWorkbench.Filter,FamilyWorkbench.Uid);}
            catch(Exception ex){Failed(ex);return;}
            StartCoroutine(Export(snapshot,save));
        }
        IEnumerator Export(CalendarExportSnapshot snapshot,bool save)
        {
            yield return null;
            try
            {
                byte[] png=CalendarExportRenderer.Png(snapshot,font,rounded,frame,note);
#if UNITY_ANDROID && !UNITY_EDITOR
                string folder=AndroidBridge.Call<string>("calendarImageDirectory");
                if(string.IsNullOrEmpty(folder))throw new IOException("Image cache unavailable.");
#else
                string folder=Path.Combine(Application.temporaryCachePath,"shiftcal-calendar-images");
#endif
                Directory.CreateDirectory(folder);
                LastPath=Path.Combine(folder,Path.GetFileNameWithoutExtension(snapshot.fileName)+"-"+request+".png");
                string temporary=LastPath+".tmp";
                try{File.WriteAllBytes(temporary,png);File.Move(temporary,LastPath);}finally{if(File.Exists(temporary))File.Delete(temporary);}
#if UNITY_ANDROID && !UNITY_EDITOR
                string error=AndroidBridge.Call<string>(save?"saveCalendarImage":"shareCalendarImage",LastPath,snapshot.fileName,request);
                if(!string.IsNullOrEmpty(error))Complete(error);
#else
                Complete("Calendar PNG ready in the local cache. Share and Save Image use Android on a phone.");
#endif
            }
            catch(Exception ex){Failed(ex);}
        }
        void Failed(Exception ex){Debug.LogWarning("Calendar image export failed: "+ex.GetType().Name);Complete("Couldn't create the calendar image. Please try again.");}
        void Complete(string text){IsBusy=false;shareButton.interactable=optionsButton.interactable=true;request=null;if(string.IsNullOrEmpty(text))statusPanel.SetActive(false);else Message(text);}
        void Message(string text){statusLabel.text=text;statusPanel.SetActive(true);statusPanel.transform.SetAsLastSibling();hideStatusAt=Time.unscaledTime+7;}
        [UnityEngine.Scripting.Preserve]
        public void OnCalendarImageResult(string json)
        {
            var result=JsonUtility.FromJson<Result>(json);if(result==null||result.token!=request||!IsBusy)return;
            Complete(result.status=="ready"?"":result.message);
        }
    }
}
