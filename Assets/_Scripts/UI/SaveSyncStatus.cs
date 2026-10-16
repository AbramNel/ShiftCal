using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using ShiftCal.App;
using ShiftCal.Data;
using ShiftCal.Firebase;
namespace ShiftCal.UI
{
    public class SaveSyncStatus : MonoBehaviour
    {
        public Text label;
        public Button retry;
        float next;
        public static string Describe(ScheduleSave save,string error,string status,bool uploading,bool acknowledged,bool cached)
        {
            if(!string.IsNullOrEmpty(error)||save.records.Any(r=>r.conflict)||status.StartsWith("Sync error")||status.StartsWith("Sync listener failed")||status.StartsWith("Sync needs attention"))return "Needs attention";
            if(save.account=="local")return "Saved on this device";
            if(uploading)return "Syncing";
            if(save.records.Any(r=>r.pending))return cached||status.StartsWith("Offline")?"Offline — changes pending":"Changes pending";
            return acknowledged&&!cached?"Synced":cached?"Offline — saved on this device":"Saved on this device";
        }
        void OnEnable(){next=0;}
        void Update(){if(Time.unscaledTime<next)return;next=Time.unscaledTime+.5f;Refresh();}
        public void Refresh()
        {
            var s=AppSession.Instance;var fs=FirestoreService.Instance;if(s?.Data==null)return;
            label.text=Describe(s.Data,s.Error,fs?.Status??"",fs?.IsUploading==true,fs?.HasServerAcknowledgement==true,fs?.IsCached==true);
            retry.gameObject.SetActive(label.text=="Needs attention"||label.text.Contains("pending"));
        }
        public void Retry(){AppSession.Instance.SaveLocal();FirestoreService.Instance?.StartListening();FirestoreService.Instance?.Flush();Refresh();}
    }
}
