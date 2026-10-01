using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ShiftCal.App;
using ShiftCal.Data;
#if SHIFT_CAL_USE_FIREBASE
using Firebase.Firestore;
using Firebase.Extensions;
#endif
namespace ShiftCal.Firebase
{
    public class FirestoreService : MonoBehaviour
    {
        public static FirestoreService Instance;
        public string Status = "Local mode. Google/Firebase not configured.";
        public readonly List<string> MemberIds=new List<string>();
        public readonly List<string> MemberLabels=new List<string>();
        private bool uploading;
        private string uploadKey,uploadJson;
        private bool uploadDeleted;
        private long uploadRevision;
        private int generation;
#if SHIFT_CAL_USE_FIREBASE
        private readonly List<ListenerRegistration> listeners = new List<ListenerRegistration>();
        private FirebaseFirestore DB => FirebaseBootstrap.Instance.DB;
        private CollectionReference Groups => DB.Collection("shiftcal").Document("v1").Collection("groups");
        private CollectionReference Private => DB.Collection("shiftcal").Document("v1").Collection("users").Document(AppSession.Instance.Data.account).Collection("records");
        private CollectionReference CalendarRecords => AppSession.Instance.CurrentGroup.groupId.StartsWith("local-") ? DB.Collection("shiftcal").Document("v1").Collection("users").Document(AppSession.Instance.Data.account).Collection("calendar") : Groups.Document(AppSession.Instance.CurrentGroup.groupId).Collection("records");
        private bool Ready => FirebaseBootstrap.Instance != null && FirebaseBootstrap.Instance.Ready && AuthService.Instance.IsSignedIn && AppSession.Instance.Data.account == AuthService.Instance.UserId;
        private DocumentReference Document(string key) => (key.StartsWith("rule/") ? Private : CalendarRecords).Document(key.Replace("/", "~"));
#endif
        private void Awake() { if (Instance != null) { Destroy(this); return; } Instance = this; }
        public void TrackChanges(ScheduleSave data)
        {
            if (data.account == "local") return;
            var actual = new Dictionary<string,string>();
            actual["rotation/main"] = JsonUtility.ToJson(new GroupData { name=data.group.name, startDateKey=data.group.startDateKey, pattern=data.group.pattern, groupId=data.group.groupId });
            foreach (var x in data.group.shiftTypes) actual["shift/" + x.id] = JsonUtility.ToJson(x);
            foreach (var x in data.overrides) actual["override/" + x.dateKey] = JsonUtility.ToJson(x);
            foreach (var x in data.events) actual["event/" + x.id] = JsonUtility.ToJson(x);
            foreach (var x in data.exceptions) actual["exception/" + x.id] = JsonUtility.ToJson(x);
            foreach (var x in data.rules) actual["rule/" + x.id] = JsonUtility.ToJson(x);
            foreach (var pair in actual)
            {
                var record = data.records.Find(r => r.key == pair.Key);
                if (record == null) { record = new SyncRecord { key=pair.Key }; data.records.Add(record); }
                if (record.json != pair.Value || record.deleted) { record.json=pair.Value; record.deleted=false; record.pending=true; }
            }
            foreach (var record in data.records) if (!actual.ContainsKey(record.key) && !record.deleted) { record.deleted=true; record.pending=true; }
        }
        public void StopListening()
        {
            generation++; uploading=false;uploadKey=null;MemberIds.Clear();MemberLabels.Clear();
#if SHIFT_CAL_USE_FIREBASE
            foreach (var l in listeners) l.Stop(); listeners.Clear();
#endif
        }
        public void StartListening()
        {
            StopListening();
#if SHIFT_CAL_USE_FIREBASE
            if (!Ready) return;
            int g=generation;
            foreach (var c in new[] { CalendarRecords, Private })
            {
                listeners.Add(c.Listen(snapshot => {
                    if (g != generation) return;
                    string previous=JsonUtility.ToJson(AppSession.Instance.Data);
                    try {
                        foreach (var doc in snapshot.Documents) {
                            var map=doc.ToDictionary();
                            Merge((string)map["key"],(string)map["json"],(bool)map["deleted"],Convert.ToInt64(map["revision"]));
                        }
                        AppSession.Instance.ApplyRemote();
                        Status=AppSession.Instance.Data.records.Any(r=>r.conflict)?"Conflicting edits. Choose Keep mine or Use remote in Options.":snapshot.Metadata.IsFromCache?"Offline / cached calendar":"Synced";
                        Flush();
                    } catch(Exception ex) { AppSession.Instance.RestoreSnapshot(previous);Status="Sync error; previous calendar retained: "+ex.Message; }
                }));
                listeners[listeners.Count-1].ListenerTask.ContinueWithOnMainThread(t=>{if(g==generation&&t.IsFaulted)Status="Sync listener failed. Local calendar retained. Retry: "+t.Exception.GetBaseException().Message;});
            }
#endif
        }
        public void Merge(string key,string json,bool deleted,long revision)
        {
            var s=AppSession.Instance.Data;var r=s.records.Find(x=>x.key==key);
            if(r==null){r=new SyncRecord{key=key};s.records.Add(r);}
            if(r.pending&&revision!=r.revision){
                // A snapshot can acknowledge our in-flight write after a newer local edit.
                if(uploading&&key==uploadKey&&json==uploadJson&&deleted==uploadDeleted&&revision==uploadRevision+1){r.revision=revision;return;}
                if(r.json==json&&r.deleted==deleted){r.pending=false;r.revision=revision;}
                else {r.conflict=true;r.remoteJson=json;r.remoteDeleted=deleted;r.remoteRevision=revision;Status="Conflicting edits. Choose Keep mine or Use remote in Options.";}
                return;
            }
            if(revision<r.revision||r.pending)return;
            Apply(s,key,json,deleted);r.json=json;r.deleted=deleted;r.revision=revision;
        }
        public static void Apply(ScheduleSave s,string key,string json,bool deleted)
        {
            string[] p=key.Split('/');string id=p[1];
            switch(p[0]){
                case "rotation": if(!deleted){var g=JsonUtility.FromJson<GroupData>(json);Core.DateKeyUtility.FromDateKey(g.startDateKey);s.group.name=g.name;s.group.startDateKey=g.startDateKey;s.group.pattern=g.pattern;}break;
                case "shift": s.group.shiftTypes.RemoveAll(x=>x.id.ToString()==id);if(!deleted)s.group.shiftTypes.Add(JsonUtility.FromJson<ShiftTypeDefinitionData>(json));break;
                case "override": s.overrides.RemoveAll(x=>x.dateKey==id);if(!deleted)s.overrides.Add(JsonUtility.FromJson<DayOverrideData>(json));break;
                case "event": s.events.RemoveAll(x=>x.id==id);if(!deleted){var e=JsonUtility.FromJson<EventSeries>(json);if(!Core.RecurrenceEngine.Validate(e,out string error))throw new ArgumentException(error);s.events.Add(e);}break;
                case "exception": s.exceptions.RemoveAll(x=>x.id==id);if(!deleted)s.exceptions.Add(JsonUtility.FromJson<EventException>(json));break;
                case "rule": s.rules.RemoveAll(x=>x.id==id);if(!deleted)s.rules.Add(JsonUtility.FromJson<ShiftAlarmRule>(json));break;
            }
        }
        public void ResolveConflicts(bool mine)
        {
            var s=AppSession.Instance.Data;
            foreach(var r in s.records.Where(x=>x.conflict)){
                r.revision=r.remoteRevision;r.conflict=false;
                if(mine)r.pending=true;
                else{Apply(s,r.key,r.remoteJson,r.remoteDeleted);r.json=r.remoteJson;r.deleted=r.remoteDeleted;r.pending=false;}
            }
            AppSession.Instance.ApplyRemote();Flush();
        }
        public void Flush()
        {
#if SHIFT_CAL_USE_FIREBASE
            if(!Ready||uploading)return;
            var r=AppSession.Instance.Data.records.Find(x=>x.pending&&!x.conflict);if(r==null)return;
            uploading=true;int g=generation;string key=r.key,json=r.json,author=AppSession.Instance.Data.account;bool deleted=r.deleted;long rev=r.revision;
            uploadKey=key;uploadJson=json;uploadDeleted=deleted;uploadRevision=rev;
            var reference=Document(key);
            DB.RunTransactionAsync(async transaction=>{
                var current=await transaction.GetSnapshotAsync(reference);
                long remote=current.Exists?current.GetValue<long>("revision"):0;
                if(remote!=rev)return false;
                transaction.Set(reference,new Dictionary<string,object>{{"key",key},{"json",json},{"deleted",deleted},{"revision",rev+1},{"author",author}});
                return true;
            }).ContinueWithOnMainThread(t=>{
                if(g!=generation)return;uploading=false;
                if(t.IsFaulted||t.IsCanceled){Status="Offline or sync failed. Changes saved locally. Retry: "+t.Exception?.GetBaseException().Message;return;}
                if(!t.Result){Status="Remote edit conflict. Refreshing.";StartListening();return;}
                r.revision=Math.Max(r.revision,rev+1);if(r.json==json&&r.deleted==deleted)r.pending=false;
                ScheduleStorage.Write(AppSession.Instance.Data);Status="Synced";Flush();
            });
#endif
        }
        public void CreateGroup(string name)
        {
#if SHIFT_CAL_USE_FIREBASE
            if(!Ready){Status="Sign in first.";return;}
            string uid=AuthService.Instance.UserId;var doc=Groups.Document();int g=generation;
            var batch=DB.StartBatch();batch.Set(doc,new Dictionary<string,object>{{"name",name},{"owner",uid}});
            batch.Set(doc.Collection("members").Document(uid),new Dictionary<string,object>{{"role","owner"},{"name",AuthService.Instance.DisplayName},{"email",FirebaseBootstrap.Instance.Auth.CurrentUser.Email}});
            batch.CommitAsync().ContinueWithOnMainThread(t=>{if(g!=generation)return;if(t.IsFaulted){Status=t.Exception.GetBaseException().Message;return;}ArchiveGroup();AppSession.Instance.CurrentGroup.groupId=doc.Id;AppSession.Instance.CurrentGroup.name=name;AppSession.Instance.Data.records.RemoveAll(x=>!x.key.StartsWith("rule/"));AppSession.Instance.SaveLocal();StartListening();});
#else
            Status="Group sharing requires Firebase configuration.";
#endif
        }
        public void Invite(string email)
        {
#if SHIFT_CAL_USE_FIREBASE
            if(!Ready)return;string group=AppSession.Instance.CurrentGroup.groupId,code=Guid.NewGuid().ToString("N");int g=generation;
            Groups.Document(group).Collection("invites").Document(code).SetAsync(new Dictionary<string,object>{{"email",email.Trim().ToLowerInvariant()},{"expires",Timestamp.FromDateTime(DateTime.UtcNow.AddDays(7))}}).ContinueWithOnMainThread(t=>{if(g==generation)Status=t.IsFaulted?t.Exception.GetBaseException().Message:"Invitation: "+group+":"+code+" (7 days)";});
#else
            Status="Configure Firebase to invite people.";
#endif
        }
        public void Join(string code)
        {
#if SHIFT_CAL_USE_FIREBASE
            if(!Ready)return;var parts=code.Trim().Split(':');if(parts.Length!=2){Status="Enter the invitation code.";return;}
            string uid=AuthService.Instance.UserId;int g=generation;
            var member=Groups.Document(parts[0]).Collection("members").Document(uid);
            DB.RunTransactionAsync(async tx=>{var existing=await tx.GetSnapshotAsync(member);if(!existing.Exists)tx.Set(member,new Dictionary<string,object>{{"role","member"},{"invite",parts[1]},{"name",AuthService.Instance.DisplayName},{"email",FirebaseBootstrap.Instance.Auth.CurrentUser.Email}});}).ContinueWithOnMainThread(t=>{
                if(g!=generation)return;if(t.IsFaulted){Status=t.Exception.GetBaseException().Message;return;}
                ArchiveGroup();var data=AppSession.Instance.Data;data.group.groupId=parts[0];data.group.shiftTypes.Clear();data.overrides.Clear();data.events.Clear();data.exceptions.Clear();data.records.RemoveAll(x=>!x.key.StartsWith("rule/"));AppSession.Instance.ApplyRemote();StartListening();
            });
#else
            Status="Configure Firebase to join a group.";
#endif
        }
        public void RefreshMembers()
        {
#if SHIFT_CAL_USE_FIREBASE
            if(!Ready||AppSession.Instance.CurrentGroup.groupId.StartsWith("local-")){Status="Choose a shared group first.";return;}
            int g=generation;Groups.Document(AppSession.Instance.CurrentGroup.groupId).Collection("members").GetSnapshotAsync().ContinueWithOnMainThread(t=>{if(g!=generation)return;if(t.IsFaulted){Status=t.Exception.GetBaseException().Message;return;}MemberIds.Clear();MemberLabels.Clear();foreach(var doc in t.Result.Documents){MemberIds.Add(doc.Id);var map=doc.ToDictionary();MemberLabels.Add((map.TryGetValue("name",out var name)?name.ToString():"Member")+(map.TryGetValue("email",out var email)?" / "+email:"")+" ("+map["role"]+")");}Status="Group members refreshed.";});
#endif
        }
        public void RemoveMember(int index)
        {
#if SHIFT_CAL_USE_FIREBASE
            if(!Ready||index<0||index>=MemberIds.Count)return;int g=generation;Groups.Document(AppSession.Instance.CurrentGroup.groupId).Collection("members").Document(MemberIds[index]).DeleteAsync().ContinueWithOnMainThread(t=>{if(g!=generation)return;if(t.IsFaulted){Status="Only the owner can remove another member. Owners cannot remove themselves. "+t.Exception.GetBaseException().Message;return;}Status="Member removed.";RefreshMembers();});
#endif
        }
        public void LeaveGroup()
        {
#if SHIFT_CAL_USE_FIREBASE
            if(!Ready||AppSession.Instance.CurrentGroup.groupId.StartsWith("local-"))return;
            string uid=AuthService.Instance.UserId;int g=generation;
            Groups.Document(AppSession.Instance.CurrentGroup.groupId).Collection("members").Document(uid).DeleteAsync().ContinueWithOnMainThread(t=>{if(g!=generation)return;if(t.IsFaulted){Status="The group owner cannot leave their own group. "+t.Exception.GetBaseException().Message;return;}
                ArchiveGroup();var s=AppSession.Instance.Data;s.group.groupId="local-default";s.group.name="Personal calendar";s.records.RemoveAll(x=>!x.key.StartsWith("rule/"));s.events.Clear();s.exceptions.Clear();s.overrides.Clear();AppSession.Instance.ApplyRemote();StartListening();Status="Left group. Previous shared calendar retained in local backup.";});
#endif
        }
        private void ArchiveGroup(){var s=AppSession.Instance.Data;System.IO.File.WriteAllText(ScheduleStorage.PathFor(s.account)+".group-"+DateTime.UtcNow.Ticks+".json",JsonUtility.ToJson(s));}
        private void OnDestroy(){StopListening();if(Instance==this)Instance=null;}
    }
}
