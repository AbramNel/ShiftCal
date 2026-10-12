using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ShiftCal.App;
using ShiftCal.Data;
namespace ShiftCal.UI
{
    public class FamilyProfileController : MonoBehaviour
    {
        public GameObject panel;
        public SelectionField person,color,link,deviceProfile;
        public InputField displayName,initials;
        public Toggle active,shiftOwner;
        public Text message;
        public Button save,archive;
        public string[] colors={"#60A5FA","#F59AC8","#9FF4F1","#FBBF24","#A78BFA","#F4A261","#8ED6A5","#FB7185"};
        List<string> ids=new List<string>(),uids=new List<string>();
        ScheduleSave Data=>AppSession.Instance.Data;
        string editing;
        ActivityEditor returnEditor;
        public void OpenFromEditor(ActivityEditor editor){returnEditor=editor;Open();}
        public void ReturnToDraft(){if(returnEditor==null)return;var editor=returnEditor;returnEditor=null;editor.RestoreAfterProfiles();}
        public void Open(){Hook();AppNavigation.Instance.Open(panel);Reload();}
        bool hooked;
        void Start(){Hook();AppSession.Instance.Changed+=Changed;}
        void Hook(){if(hooked)return;hooked=true;person.onValueChanged.AddListener(Select);deviceProfile.onValueChanged.AddListener(_=>{if(Data.account=="local"&&DeviceCalendarStore.State!=null){DeviceCalendarStore.State.localProfileId=deviceProfile.options[deviceProfile.value].id;DeviceCalendarStore.Write();}});}
        void OnDestroy(){if(AppSession.Instance!=null)AppSession.Instance.Changed-=Changed;}
        void Changed(){if(panel.activeSelf)Reload();}
        public void Reload()
        {
            ids=Data.profiles.Select(x=>x.id).ToList();person.SetOptions(new[]{new SelectionItem("Add Person",null,"Create a family profile")}.Concat(Data.profiles.Select(p=>new SelectionItem(p.name,p.id,p.active?null:"Archived"){color=p.color})));person.SetValueWithoutNotify(0);
            color.SetOptions(colors.Select((hex,i)=>new SelectionItem(new[]{"Blue","Pink","Aqua","Gold","Purple","Orange","Green","Rose"}[i],hex){color=hex}));
            deviceProfile.transform.parent.gameObject.SetActive(Data.account=="local");deviceProfile.SetOptions(new[]{new SelectionItem("No device profile",null)}.Concat(FamilySelections.People(Data)));deviceProfile.SetValueWithoutNotify(Math.Max(0,deviceProfile.options.FindIndex(p=>p.id==DeviceCalendarStore.State?.localProfileId)));
            uids=new List<string>{""};var labels=new List<string>{"Unlinked (no login required)"};var fs=Firebase.FirestoreService.Instance;
            if(Firebase.AuthService.Instance?.IsSignedIn==true){uids.Add(Firebase.AuthService.Instance.UserId);labels.Add("My signed-in account");}
            if(fs?.CanManageProfiles==true)for(int i=0;i<fs.MemberIds.Count;i++)if(!uids.Contains(fs.MemberIds[i])){uids.Add(fs.MemberIds[i]);labels.Add(fs.MemberLabels[i]);}
            link.ClearOptions();link.AddOptions(labels);Select(0);
        }
        void Select(int index)
        {
            editing=index>0&&index<=ids.Count?ids[index-1]:null;var p=Data.profiles.Find(x=>x.id==editing);
            displayName.text=p?.name??"";initials.text=p?.initials??"";if(!string.IsNullOrEmpty(p?.color)&&!color.options.Any(o=>o.id==p.color))color.AddOptions(new List<SelectionItem>{new SelectionItem("Current custom color",p.color){color=p.color}});color.SetValueWithoutNotify(Math.Max(0,color.options.FindIndex(o=>o.id==p?.color)));active.isOn=p?.active??true;shiftOwner.isOn=p!=null&&Data.group.shiftOwnerProfileId==p.id;
            if(!string.IsNullOrEmpty(p?.linkedUid)&&!uids.Contains(p.linkedUid)){uids.Add(p.linkedUid);link.AddOptions(new List<string>{"Previously linked account"});}
            link.SetValueWithoutNotify(Math.Max(0,uids.IndexOf(p?.linkedUid??"")));bool allowed=Firebase.FirestoreService.Instance?.CanManageProfiles??Data.group.groupId.StartsWith("local-");save.interactable=allowed;archive.interactable=allowed&&p!=null;
            link.interactable=allowed;color.interactable=allowed;shiftOwner.interactable=allowed;active.interactable=allowed;displayName.interactable=allowed;initials.interactable=allowed;
            message.text=allowed?"Link by authenticated account, never by name. One profile can own this calendar's work rotation.":"The calendar owner manages family profiles and work-schedule ownership.";
        }
        public void Save()
        {
            if(!save.interactable||Firebase.FirestoreService.Instance?.CanManageProfiles!=true)return;if(string.IsNullOrWhiteSpace(displayName.text)){message.text="Add a display name.";return;}
            string uid=uids[Math.Min(link.value,uids.Count-1)];if(uid!=""&&Data.profiles.Exists(x=>x.id!=editing&&x.linkedUid==uid&&x.active)){message.text="That account is already linked to an active profile.";return;}
            var p=Data.profiles.Find(x=>x.id==editing)??new FamilyProfile();bool isNew=editing==null;p.name=displayName.text.Trim();p.initials=string.IsNullOrWhiteSpace(initials.text)?string.Concat(p.name.Split(' ').Where(x=>x.Length>0).Take(2).Select(x=>x[0])):initials.text.Trim();p.color=color.options[color.value].id;p.active=active.isOn;p.linkedUid=uid;
            if(isNew)Data.profiles.Add(p);if(shiftOwner.isOn&&p.active)Data.group.shiftOwnerProfileId=p.id;else if(Data.group.shiftOwnerProfileId==p.id)Data.group.shiftOwnerProfileId=null;
            AppSession.Instance.SaveLocal();Reload();
        }
        public void Archive()=>AppNavigation.Instance.Confirm("Archive this person? Existing assignments keep their stable profile ID.",()=>{var p=Data.profiles.Find(x=>x.id==editing);if(p==null)return;p.active=false;if(Data.group.shiftOwnerProfileId==p.id)Data.group.shiftOwnerProfileId=null;AppSession.Instance.SaveLocal();Reload();});
        public void RefreshMembers(){Firebase.FirestoreService.Instance?.RefreshMembers();message.text="Refresh accounts, then reopen Family profiles once the member list has loaded.";}
    }
}
