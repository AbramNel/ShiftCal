using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using ShiftCal.App;
using ShiftCal.Core;
using ShiftCal.Data;
using ShiftCal.Firebase;

namespace ShiftCal.UI
{
    public class ScheduleWorkbench : MonoBehaviour
    {
        public GameObject agendaPanel, eventPanel, rulePanel;
        public Transform agendaContent;
        public ScheduleListRow rowPrefab;
        public List<InputField> eventFields = new List<InputField>();
        public List<InputField> ruleFields = new List<InputField>();
        public Dropdown recurrence, editScope, ruleShift, eventSound, ruleSound;
        public Toggle[] weekdays;
        public Toggle eventReminder, eventAlarm, eventEnabled, eventVibration, ruleEnabled, ruleAudible, ruleVibration;
        public Text messageLabel, readinessLabel, accountLabel, syncLabel, loginStatus, accountDetailsLabel, accountStateLabel;
        public InputField groupName, inviteEmail, invitation;
        public GameObject permissionDetails, upcomingSection, undoButton;
        public Text permissionSummary, upcomingLabel;
        public Button showMore;
        public Transform upcomingContent;
        private bool upcomingOpen;
        private int upcomingLimit = 4;
        private string sourceFilter, editorSnapshot;
        private string EditorSignature => string.Join("|", eventFields.Concat(ruleFields).Select(f => f.text)) + string.Join("|", GetComponentsInChildren<Toggle>(true).Select(t => t.isOn.ToString())) + string.Join("|", new[]{recurrence, editScope, ruleShift, eventSound, ruleSound}.Select(d => d.value.ToString()));
        public bool HasEditorChanges => EditorSignature != editorSnapshot;
        public Dropdown memberChoice;
        private string memberSignature="";
        private string editingEvent, originalDate, editingRule;
        private string undoId;
        private readonly List<ScheduleListRow> rows = new List<ScheduleListRow>();
        private ScheduleSave Save => AppSession.Instance.Data;
        private void Start()
        {
            AppSession.Instance.Changed += Refresh;
            ThemeManager.Changed += Refresh;
            editScope.onValueChanged.AddListener(ScopeChanged);
            Hide(); Refresh();
        }
        private void OnDestroy() { if(AppSession.Instance!=null)AppSession.Instance.Changed-=Refresh; ThemeManager.Changed -= Refresh; }
        private float nextRefresh;
        private void Update()
        {
            if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+2;
            if(loginStatus!=null)loginStatus.text=AuthService.Instance.Status;
            var auth = AuthService.Instance;
            string identity = string.IsNullOrWhiteSpace(auth.DisplayName) ? "On this device" : auth.DisplayName;
            if(accountLabel!=null)accountLabel.text=identity + "  >";
            if(accountDetailsLabel!=null)accountDetailsLabel.text=identity;
            if(accountStateLabel!=null)accountStateLabel.text=(auth.IsSignedIn ? "Google account connected" : auth.LocalMode || AppSession.Instance.Data.account == "local" ? "Local calendar on this device" : auth.HasCachedAccess ? "Offline access to your saved account" : "No Google account connected") + "\n" + auth.Status + "\n" + FirestoreService.Instance.Status;
            if(syncLabel!=null)syncLabel.text=string.Join("\n",new[]{FirestoreService.Instance.Status,AppSession.Instance.Error,ScheduleStorage.Status}.Where(s=>!string.IsNullOrWhiteSpace(s)));
            string signature=string.Join("|",FirestoreService.Instance.MemberLabels);
            if(memberChoice!=null&&signature!=memberSignature){memberSignature=signature;memberChoice.ClearOptions();memberChoice.AddOptions(FirestoreService.Instance.MemberLabels.Count==0?new List<string>{"No members loaded"}:FirestoreService.Instance.MemberLabels);}
            if(readinessLabel!=null&&agendaPanel.activeSelf){readinessLabel.text=AndroidBridge.Readiness();permissionSummary.text=AndroidBridge.Call<string>("permissionSummary")??"Alarm readiness - Android device required";}
            if(AuthService.Instance.HasCachedAccess){string opened=AndroidBridge.Call<string>("opened");if(!string.IsNullOrEmpty(opened)){var hit=JsonUtility.FromJson<Occurrence>(opened);if(hit.id.StartsWith("event:")&&Save.events.Exists(e=>e.id==hit.sourceId))EditEvent(hit.sourceId,hit.dateKey);else if(Save.rules.Exists(r=>r.id==hit.sourceId)){editingRule=hit.sourceId;LoadRule(Save.rules.Find(r=>r.id==hit.sourceId));}else ShowAgenda();}}
        }
        public void Hide(){agendaPanel.SetActive(false);eventPanel.SetActive(false);rulePanel.SetActive(false);if(messageLabel!=null)messageLabel.text="";}
        public void ShowAgenda(){ CloseEditors(); if (AppNavigation.Instance != null) AppNavigation.Instance.Open(agendaPanel); else agendaPanel.SetActive(true); agendaPanel.SetActive(true); Refresh(); }
        private void CloseEditors() { eventPanel.SetActive(false); rulePanel.SetActive(false); Message(""); }
        public void CancelEditor() => AppNavigation.Instance.Back();
        public void TogglePermissions() => permissionDetails.SetActive(!permissionDetails.activeSelf);
        public void ToggleUpcoming() { upcomingOpen = !upcomingOpen; sourceFilter = null; upcomingLimit = 4; Refresh(); }
        private void UpcomingFor(string id) { sourceFilter = id; upcomingOpen = true; upcomingLimit = 4; Refresh(); }
        public void MoreUpcoming() { upcomingLimit += 4; Refresh(); }
        public void NewEvent()=>NewEvent(DateKeyUtility.ToDateKey(DateTime.Today));
        public void NewEvent(string date)
        {
            editingEvent=null;originalDate=date;
            LoadEvent(new EventSeries{title="",dateKey=date,startTime="1:00 PM",weekdays=new List<int>{4}});
        }
        private void EditEvent(string id,string date)
        {
            var e=Save.events.Find(x=>x.id==id);if(e==null)return;
            editingEvent=id;originalDate=date;
            var ex=Save.exceptions.Find(x=>x.seriesId==id&&x.originalDate==date);
            var copy=JsonUtility.FromJson<EventSeries>(JsonUtility.ToJson(ex?.replacement??e));
            if(ex?.replacement==null)copy.dateKey=date;
            LoadEvent(copy);
        }
        private void LoadEvent(EventSeries e)
        {
            CloseEditors();eventPanel.SetActive(true);eventPanel.transform.SetAsLastSibling();
            Set(eventFields,"title",e.title);Set(eventFields,"date",e.dateKey);Set(eventFields,"start",e.startTime);Set(eventFields,"end",e.endTime);
            Set(eventFields,"notes",e.notes);Set(eventFields,"zone",e.zone);Set(eventFields,"interval",e.interval.ToString());Set(eventFields,"until",e.until);Set(eventFields,"count",e.count.ToString());
            Set(eventFields,"reminder",e.reminderMinutes.ToString());Set(eventFields,"snooze",e.snoozeMinutes.ToString());Set(eventFields,"advance",e.advanceMinutes.ToString());
            recurrence.value=(int)e.recurrence;editScope.value=0;eventReminder.isOn=e.reminder;eventAlarm.isOn=e.alarm;eventEnabled.isOn=e.enabled;eventVibration.isOn=e.vibration;eventSound.value=SoundIndex(e.sound);
            for(int i=0;i<weekdays.Length;i++)weekdays[i].isOn=e.weekdays.Contains(i);
            Message(""); editorSnapshot = EditorSignature; eventPanel.GetComponent<ModalPanel>().HasChanges = () => HasEditorChanges; eventPanel.GetComponent<ModalPanel>().Close = CloseEditors;
        }
        public void SaveEvent()
        {
            try
            {
                var e=new EventSeries{title=Get(eventFields,"title"),dateKey=Get(eventFields,"date"),startTime=Get(eventFields,"start"),endTime=Get(eventFields,"end"),notes=Get(eventFields,"notes"),zone=Get(eventFields,"zone"),recurrence=(RecurrenceKind)recurrence.value,
                    interval=Number(eventFields,"interval"),until=Get(eventFields,"until"),count=Number(eventFields,"count"),reminder=eventReminder.isOn,alarm=eventAlarm.isOn,enabled=eventEnabled.isOn,vibration=eventVibration.isOn,sound=Sound(eventSound.value),
                    reminderMinutes=Number(eventFields,"reminder"),snoozeMinutes=Number(eventFields,"snooze"),advanceMinutes=Number(eventFields,"advance")};
                for(int i=0;i<weekdays.Length;i++)if(weekdays[i].isOn)e.weekdays.Add(i);
                if(!RecurrenceEngine.Validate(e,out string error)){Message(error);return;}
                var old=Save.events.Find(x=>x.id==editingEvent);
                if(old==null)Save.events.Add(e);
                else if(editScope.value==0 && old.recurrence!=RecurrenceKind.Once)
                {
                    e.recurrence=RecurrenceKind.Once;e.count=0;e.until="";
                    var ex=Exception(old.id,originalDate);ex.cancelled=false;ex.replacement=e;
                }
                else if(editScope.value==1&&old.recurrence!=RecurrenceKind.Once)
                {
                    DateTime cut=DateKeyUtility.FromDateKey(originalDate);
                    int consumed=RecurrenceEngine.Dates(old,DateKeyUtility.FromDateKey(old.dateKey),cut.AddDays(-1)).Count();
                    if(old.count>0 && e.count==old.count)e.count=Math.Max(1,old.count-consumed);
                    old.until=DateKeyUtility.ToDateKey(cut.AddDays(-1));
                    if(cut<=DateKeyUtility.FromDateKey(old.dateKey)){Save.events.Remove(old);Save.exceptions.RemoveAll(x=>x.seriesId==old.id);}
                    else Save.exceptions.RemoveAll(x=>x.seriesId==old.id&&string.CompareOrdinal(x.originalDate,originalDate)>=0);
                    Save.events.Add(e);
                }
                else { e.id=old.id; Save.events.Remove(old);Save.events.Add(e); }
                AppSession.Instance.SaveLocal();ShowAgenda();
            }
            catch(Exception ex){Message(ex.Message);}
        }
        private EventException Exception(string id,string date)
        {
            var ex=Save.exceptions.Find(x=>x.seriesId==id&&x.originalDate==date);
            if(ex==null){ex=new EventException{id=id+"_"+date,seriesId=id,originalDate=date};Save.exceptions.Add(ex);}return ex;
        }
        private void ScopeChanged(int scope)
        {
            var e=Save.events.Find(x=>x.id==editingEvent);if(e!=null)Set(eventFields,"date",scope==2?e.dateKey:Save.exceptions.Find(x=>x.seriesId==e.id&&x.originalDate==originalDate)?.replacement?.dateKey??originalDate);
        }
        public void DeleteEvent() => AppNavigation.Instance.Confirm("Delete the selected event scope?", DeleteEventConfirmed);
        private void DeleteEventConfirmed()
        {
            var e=Save.events.Find(x=>x.id==editingEvent);if(e==null){ShowAgenda();return;}
            if(editScope.value==0&&e.recurrence!=RecurrenceKind.Once){var ex=Exception(e.id,originalDate);ex.cancelled=true;ex.replacement=null;}
            else if(editScope.value==1&&e.recurrence!=RecurrenceKind.Once&&originalDate!=e.dateKey){e.until=DateKeyUtility.ToDateKey(DateKeyUtility.FromDateKey(originalDate).AddDays(-1));Save.exceptions.RemoveAll(x=>x.seriesId==e.id&&string.CompareOrdinal(x.originalDate,originalDate)>=0);}
            else{Save.events.Remove(e);Save.exceptions.RemoveAll(x=>x.seriesId==e.id);}
            AppSession.Instance.SaveLocal();ShowAgenda();
        }
        public void NewRule(){editingRule=null;LoadRule(new ShiftAlarmRule{shiftType=(int)ShiftTypeId.Day12});}
        private void LoadRule(ShiftAlarmRule r)
        {
            CloseEditors();rulePanel.SetActive(true);rulePanel.transform.SetAsLastSibling();ruleShift.ClearOptions();ruleShift.AddOptions(Save.group.shiftTypes.Select(x=>x.name).ToList());
            ruleShift.value=Math.Max(0,Save.group.shiftTypes.FindIndex(x=>x.id==r.shiftType));
            Set(ruleFields,"label",r.label);Set(ruleFields,"offset",r.beforeMinutes.ToString());Set(ruleFields,"snooze",r.snoozeMinutes.ToString());Set(ruleFields,"advance",r.advanceMinutes.ToString());
            ruleEnabled.isOn=r.enabled&&!Save.mutedRules.Contains(r.id);ruleAudible.isOn=r.audible;ruleVibration.isOn=r.vibration;ruleSound.value=SoundIndex(r.sound);Message(""); editorSnapshot = EditorSignature; rulePanel.GetComponent<ModalPanel>().HasChanges = () => HasEditorChanges; rulePanel.GetComponent<ModalPanel>().Close = CloseEditors;
        }
        public void SaveRule()
        {
            try{
                var shift=Save.group.shiftTypes[ruleShift.value];if(!ShiftTimeUtility.TryParseTime(shift.startTime,out _)){Message("Set a start time for "+shift.name+" in Shift Settings first.");return;}
                var r=new ShiftAlarmRule{id=editingRule??Guid.NewGuid().ToString("N"),groupId=Save.group.groupId,shiftType=shift.id,label=Get(ruleFields,"label"),beforeMinutes=Number(ruleFields,"offset"),snoozeMinutes=Number(ruleFields,"snooze"),advanceMinutes=Number(ruleFields,"advance"),enabled=ruleEnabled.isOn,audible=ruleAudible.isOn,vibration=ruleVibration.isOn,sound=Sound(ruleSound.value)};
                if(string.IsNullOrWhiteSpace(r.label)||r.beforeMinutes<0||r.beforeMinutes>1440||r.advanceMinutes<0||r.advanceMinutes>10080||r.snoozeMinutes<1||r.snoozeMinutes>120){Message("Enter a label; offset 0-1440, advance 0-10080, snooze 1-120 minutes.");return;}
                if(!ruleEnabled.isOn&&!Save.mutedRules.Contains(r.id))Save.mutedRules.Add(r.id);else if(ruleEnabled.isOn)Save.mutedRules.Remove(r.id);r.enabled=true;
                Save.rules.RemoveAll(x=>x.id==r.id);Save.rules.Add(r);AppSession.Instance.SaveLocal();ShowAgenda();
            }catch(Exception ex){Message(ex.Message);}
        }
        public void DeleteRule() => AppNavigation.Instance.Confirm("Delete this alarm rule?", DeleteRuleConfirmed);
        private void DeleteRuleConfirmed(){Save.rules.RemoveAll(x=>x.id==editingRule);AppSession.Instance.SaveLocal();ShowAgenda();}
        public void Refresh()
        {
            if(AppSession.Instance==null||Save==null)return;
            if(!agendaPanel.activeSelf)return;
            foreach(var row in rows) { row.gameObject.SetActive(false); if(Application.isPlaying)Destroy(row.gameObject);else DestroyImmediate(row.gameObject); } rows.Clear();
            readinessLabel.text=AndroidBridge.Readiness();
            if (permissionSummary != null) permissionSummary.text = AndroidBridge.Call<string>("permissionSummary") ?? "Alarm readiness - Android device required";
            upcomingSection.SetActive(upcomingOpen);
            upcomingLabel.text = upcomingOpen ? "Upcoming  ^" : "Upcoming  v";
            if (undoButton != null) undoButton.SetActive(!string.IsNullOrEmpty(undoId));
            try
            {
                long now=DateKeyUtility.UnixMsNow();
                var occurrences=RecurrenceEngine.Resolve(Save,DateTime.Today.AddDays(-1),DateTime.Today.AddYears(2));
                foreach(var e in Save.events.ToArray())
                {
                    var next=RecurrenceEngine.EventDates(Save,e,DateTime.Today.AddDays(-1),DateTime.Today.AddYears(2)).FirstOrDefault(x=>x.at>now);
                    string date=next?.dateKey??e.dateKey;
                    bool active=e.enabled&&!Save.mutedEvents.Contains(e.id);
                    string state=(e.recurrence==RecurrenceKind.Once?"One time":"Repeating")+" • "+(active?"Active":"Paused");
                    Row(e.title,Cadence(e)+"\n"+(next==null?"No upcoming occurrence":"Next: "+DayInformation.Time(next))+" • "+state,"Edit",()=>EditEvent(e.id,date),active?"Pause":"Resume",()=>{e.enabled=true;if(active)Save.mutedEvents.Add(e.id);else Save.mutedEvents.Remove(e.id);AppSession.Instance.SaveLocal();},"Upcoming",()=>UpcomingFor(e.id));
                }
                foreach(var r in Save.rules.Where(r=>string.IsNullOrEmpty(r.groupId)||r.groupId==Save.group.groupId).ToArray())
                {
                    var next=occurrences.FirstOrDefault(o=>!o.isEvent&&o.sourceId==r.id&&o.at>now);
                    bool active=r.enabled&&!Save.mutedRules.Contains(r.id);
                    string shift=Save.group.shiftTypes.Find(x=>x.id==r.shiftType)?.name??"Missing shift";
                    Row(r.label,shift+" • "+r.beforeMinutes+" min before start\n"+(next==null?"No upcoming delivery":"Next: "+DayInformation.Time(next))+" • "+(active?"Active":"Paused"),"Edit",()=>{editingRule=r.id;LoadRule(r);},active?"Pause":"Resume",()=>{r.enabled=true;if(active)Save.mutedRules.Add(r.id);else Save.mutedRules.Remove(r.id);AppSession.Instance.SaveLocal();},"Upcoming",()=>UpcomingFor(r.id));
                }
                if(!upcomingOpen)return;
                var states=AndroidBridge.DeliveryStates();
                string deliveries=AndroidBridge.Call<string>("specialDeliveries");
                var special=string.IsNullOrEmpty(deliveries)?new List<Occurrence>():JsonUtility.FromJson<OccurrenceList>(deliveries).items;
                string queuedJson=AndroidBridge.Call<string>("queuedDeliveries");
                var queued=string.IsNullOrEmpty(queuedJson)?occurrences.Where(o=>o.at>now&&o.at<now+45L*86400000).ToList():JsonUtility.FromJson<OccurrenceList>(queuedJson).items;
                var upcoming=special.Concat(queued.Where(o=>o.at>now&&!states.ContainsKey(o.id)&&!special.Exists(x=>x.id==o.id))).Where(o=>sourceFilter==null||o.sourceId==sourceFilter).OrderBy(o=>o.at).ToList();
                showMore.gameObject.SetActive(upcoming.Count>upcomingLimit);
                foreach(var o in upcoming.Take(upcomingLimit))
                {
                    bool skipped=o.state=="skipped",ringing=o.state=="ringing",view=o.id.EndsWith(":view")||string.IsNullOrEmpty(queuedJson)&&!skipped&&!ringing;
                    Action edit=()=>{if(o.isEvent)EditEvent(o.sourceId,o.dateKey);else{var rule=Save.rules.Find(x=>x.id==o.sourceId);if(rule!=null){editingRule=rule.id;LoadRule(rule);}}};
                    RowTo(upcomingContent,o.title,DayInformation.Time(o)+(string.IsNullOrEmpty(o.state)?" • Device time":" • "+o.state),view?"Edit":skipped?"Undo skip":ringing?"Dismiss":"Skip",()=>{if(view){edit();return;}AndroidBridge.Action(skipped?"undo":ringing?"dismiss":"skip",o.id);undoId=skipped?null:o.id;Refresh();},ringing?"Snooze":view?"":"Edit",ringing?(Action)(()=>{AndroidBridge.Action("snooze",o.id);Refresh();}):view?null:edit,"",null);
                }
            }catch(Exception ex){Message(ex.Message);}
        }
        private static string Cadence(EventSeries e)
        {
            string rhythm=e.recurrence==RecurrenceKind.Once?DateKeyUtility.FromDateKey(e.dateKey).ToString("MMM d")
                :e.recurrence==RecurrenceKind.Weekly?"Every "+(e.interval>1?e.interval+" weeks • ":"")+string.Join(", ",e.weekdays.Select(d=>System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames[d]))
                :e.recurrence==RecurrenceKind.Daily?e.interval==1?"Every day":"Every "+e.interval+" days":"Every "+e.interval+" month(s)";
            return rhythm+" • "+e.startTime;
        }
        private void Row(string title,string subtitle,string a,Action first,string b,Action second,string c,Action third)
        {
            RowTo(agendaContent,title,subtitle,a,first,b,second,c,third);
        }
        private void RowTo(Transform content,string title,string subtitle,string a,Action first,string b,Action second,string c,Action third)
        {
            var row=Instantiate(rowPrefab,content); rows.Add(row); row.Bind(title,subtitle,a,first,b,second,c,third);
        }
        public void UndoSkip(){if(!string.IsNullOrEmpty(undoId)){AndroidBridge.Action("undo",undoId);undoId=null;Refresh();Message("Skip undone if the alarm is still in the future.");}}
        public void Permissions()=>AndroidBridge.Action("permissions");
        public void ExactAccess()=>AndroidBridge.Action("exact");
        public void FullscreenAccess()=>AndroidBridge.Action("fullscreen");
        public void TestAlarm(){AndroidBridge.Action("test");Message("Test requested for 15 seconds from now. Check readiness.");}
        public void Retry(){FirestoreService.Instance.StartListening();FirestoreService.Instance.Flush();Refresh();}
        public void ImportLocal(){AppSession.Instance.ImportLocal();}
        public void CreateGroup()=>FirestoreService.Instance.CreateGroup(groupName.text);
        public void Invite()=>FirestoreService.Instance.Invite(inviteEmail.text);
        public void Join()=>FirestoreService.Instance.Join(invitation.text);
        public void Members()=>FirestoreService.Instance.RefreshMembers();
        public void RemoveMember()=>FirestoreService.Instance.RemoveMember(memberChoice.value);
        public void LeaveGroup()=>FirestoreService.Instance.LeaveGroup();
        public void KeepMine()=>FirestoreService.Instance.ResolveConflicts(true);
        public void UseRemote()=>FirestoreService.Instance.ResolveConflicts(false);
        public void ExportBackup()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            string error=AndroidBridge.Call<string>("backup",JsonUtility.ToJson(Save,true));Message(error??"");
#else
            string path=System.IO.Path.Combine(Application.persistentDataPath,"ShiftCal-backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".json");
            System.IO.File.WriteAllText(path,JsonUtility.ToJson(Save,true));Message("Backup: "+path);
#endif
        }
        public void ClearMessage() => Message("");
        private void Message(string text)
        {
            if (messageLabel == null) return;
            messageLabel.text = text; messageLabel.transform.SetAsLastSibling();
            foreach (var panel in new[]{eventPanel,rulePanel})
            {
                var scroll=panel.GetComponentInChildren<ScrollRect>(true);
                if (scroll != null) scroll.GetComponent<RectTransform>().offsetMin = new Vector2(20,string.IsNullOrEmpty(text)?180:360);
            }
            bool editor=eventPanel.activeSelf||rulePanel.activeSelf;
            messageLabel.rectTransform.offsetMin=new Vector2(28,editor?168:24);
            messageLabel.rectTransform.offsetMax=new Vector2(-28,editor?342:180);
        }
        private static void Set(List<InputField> fields,string key,string value){fields.Find(x=>x.name==key).text=value??"";}
        private static string Get(List<InputField> fields,string key)=>fields.Find(x=>x.name==key).text.Trim();
        private static int Number(List<InputField> fields,string key){if(!int.TryParse(Get(fields,key),out int n))throw new ArgumentException("Enter a whole number for "+key);return n;}
        private static string Sound(int index)=>index==1?"notification":index==2?"silent":"alarm";
        private static int SoundIndex(string sound)=>sound=="notification"?1:sound=="silent"?2:0;
    }
}
