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
        public GameObject agendaPanel, eventPanel;
        public Transform agendaContent;
        public ScheduleListRow rowPrefab;
        public List<InputField> eventFields = new List<InputField>();
        public Dropdown recurrence, editScope, linkedShift, timingMode, repeatEnding, dayContextChoice, eventSound;
        public PickerField eventDate, eventTime, endingDate;
        public Toggle[] weekdays;
        public Toggle eventAlarm, eventEnabled, eventVibration, defaultSettings;
        public DurationChoice reminderChoice, beforeShift, snoozeOverride;
        public GameObject intervalSection, weekdaySection, endingSection, endingDateSection, countSection, shiftSection,
            beforeSection, timeSection, scopeSection, contextSection, advancedSection, advancedButton, overrideSection, deleteButton;
        public Text editorHeading, intervalLabel, timingPreview;
        public Image linkedSwatch;
        public Text messageLabel, readinessLabel, accountLabel, syncLabel, loginStatus, accountDetailsLabel, accountStateLabel;
        public InputField groupName, inviteEmail, invitation;
        public GameObject permissionDetails, upcomingSection, undoButton;
        public Text permissionSummary, upcomingLabel;
        public Button showMore;
        public Transform upcomingContent;
        private bool upcomingOpen, loading, initialized;
        private int upcomingLimit = 4;
        private string sourceFilter, editorSnapshot;
        private EventSeries eventTemplate;
        private ShiftAlarmRule ruleTemplate;
        private readonly List<ShiftTypeDefinitionData> shiftChoices = new List<ShiftTypeDefinitionData>();
        private string EditorSignature => string.Join("|", eventFields.Select(f => f.text)) + "|" + eventDate.value + "|" + eventTime.value + "|" + endingDate.value
            + string.Join("|", new[]{eventAlarm,eventEnabled,eventVibration,defaultSettings}.Concat(weekdays).Select(t => t.isOn.ToString()))
            + string.Join("|", new[]{recurrence,editScope,linkedShift,timingMode,repeatEnding,eventSound}.Select(d => d.value.ToString()))
            + string.Join("|", new[]{reminderChoice,beforeShift,snoozeOverride}.Select(d => d.choice.value + ":" + d.custom.text));
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
            InitializeEditor();
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
            if(accountLabel!=null)accountLabel.text=identity;
            if(accountDetailsLabel!=null)accountDetailsLabel.text=identity+(string.IsNullOrEmpty(auth.Email)?"":"\n"+auth.Email);
            if(accountStateLabel!=null)accountStateLabel.text=(auth.IsSignedIn ? "Google account connected" : auth.LocalMode || AppSession.Instance.Data.account == "local" ? "Local calendar on this device" : auth.HasCachedAccess ? "Offline access to your saved account" : "No Google account connected") + "\n" + auth.Status + "\n" + FirestoreService.Instance.Status;
            if(AppNavigation.Instance!=null){var connect=AppNavigation.Instance.GetComponentsInChildren<Button>(true).FirstOrDefault(x=>x.name=="Connect Google account");if(connect!=null)connect.gameObject.SetActive(!auth.IsSignedIn);}
            if(syncLabel!=null)syncLabel.text=string.Join("\n",new[]{FirestoreService.Instance.Status,AppSession.Instance.Error,ScheduleStorage.Status}.Where(s=>!string.IsNullOrWhiteSpace(s)));
            string signature=string.Join("|",FirestoreService.Instance.MemberLabels);
            if(memberChoice!=null&&signature!=memberSignature){memberSignature=signature;memberChoice.ClearOptions();memberChoice.AddOptions(FirestoreService.Instance.MemberLabels.Count==0?new List<string>{"No members loaded"}:FirestoreService.Instance.MemberLabels);}
            if(readinessLabel!=null&&agendaPanel.activeSelf){readinessLabel.text=AndroidBridge.Readiness();permissionSummary.text=AndroidBridge.Call<string>("permissionSummary")??"Alarm readiness - Android device required";}
            if(AuthService.Instance.HasCachedAccess){string opened=AndroidBridge.Call<string>("opened");if(!string.IsNullOrEmpty(opened)){var hit=JsonUtility.FromJson<Occurrence>(opened);if(hit.sourceId!=null&&hit.sourceId.StartsWith("activity-")){var family=GetComponent<FamilyWorkbench>();var d=DateKeyUtility.FromDateKey(string.IsNullOrEmpty(hit.calendarDateKey)?hit.dateKey:hit.calendarDateKey);var item=ActivityResolver.Resolve(Save,d,d).FirstOrDefault(x=>"activity-"+x.seriesId==hit.sourceId&&x.originalDate==hit.dateKey);if(item!=null)family.Details(item);}else if(hit.id.StartsWith("event:")&&Save.events.Exists(e=>e.id==hit.sourceId))EditEvent(hit.sourceId,hit.dateKey);else if(Save.rules.Exists(r=>r.id==hit.sourceId)){editingRule=hit.sourceId;LoadRule(Save.rules.Find(r=>r.id==hit.sourceId),hit.dateKey);}else ShowAgenda();}}
        }
        public void Hide(){agendaPanel.SetActive(false);eventPanel.SetActive(false);if(messageLabel!=null)messageLabel.text="";}
        public void ShowAgenda(){ CloseEditors(); if (AppNavigation.Instance != null) AppNavigation.Instance.Open(agendaPanel); else agendaPanel.SetActive(true); agendaPanel.SetActive(true); Refresh(); }
        private void CloseEditors() { eventPanel.SetActive(false);  Message(""); }
        public void CancelEditor() => AppNavigation.Instance.Back();
        public void TogglePermissions() => permissionDetails.SetActive(!permissionDetails.activeSelf);
        public void ToggleUpcoming() { upcomingOpen = !upcomingOpen; sourceFilter = null; upcomingLimit = 4; Refresh(); }
        private void UpcomingFor(string id) { sourceFilter = id; upcomingOpen = true; upcomingLimit = 4; Refresh(); }
        public void MoreUpcoming() { upcomingLimit += 4; Refresh(); }
        private void InitializeEditor()
        {
            if (initialized) return; initialized = true;
            recurrence.onValueChanged.AddListener(_ => ConditionalSections());
            timingMode.onValueChanged.AddListener(_ => ConditionalSections());
            repeatEnding.onValueChanged.AddListener(_ => ConditionalSections());
            linkedShift.onValueChanged.AddListener(_ => ConditionalSections());
            defaultSettings.onValueChanged.AddListener(_ => ConditionalSections());
            editScope.onValueChanged.AddListener(ScopeChanged);
            dayContextChoice.onValueChanged.AddListener(value => { if (!loading) { recurrence.value = value == 1 ? 4 : 0; ConditionalSections(); } });
            eventDate.changed.AddListener(_ => {
                if (string.IsNullOrEmpty(editingEvent) && ruleTemplate == null)
                    for (int i = 0; i < 7; i++) weekdays[i].isOn = i == (int)DateKeyUtility.FromDateKey(eventDate.value).DayOfWeek;
                ConditionalSections();
            });
            eventTime.changed.AddListener(_ => ConditionalSections());
            beforeShift.choice.onValueChanged.AddListener(_ => ConditionalSections());
            beforeShift.custom.onValueChanged.AddListener(_ => ConditionalSections());
        }
        public void NewEvent()=>NewEvent(DateKeyUtility.ToDateKey(DateTime.Today));
        public void NewEvent(string date) => NewEvent(date, -1);
        public void NewEvent(string date, int shiftId)
        {
            editingEvent = editingRule = null; originalDate = date; ruleTemplate = null;
            LoadEvent(new EventSeries { title = "", dateKey = date, startTime = "13:00", zone = "device", alarm = true,
                useDefaultAlarmSettings = true, weekdays = new List<int> { (int)DateKeyUtility.FromDateKey(date).DayOfWeek } });
            PopulateShifts(shiftId);
            var shift = shiftChoices.Find(x => x.id == shiftId);
            contextSection.SetActive(shift != null);
            dayContextChoice.ClearOptions(); dayContextChoice.AddOptions(new List<string> { "This date only", "Every " + (shift?.name ?? "matching") + " shift" });
            dayContextChoice.SetValueWithoutNotify(0); editorSnapshot = EditorSignature;
        }
        private void PopulateShifts(int id)
        {
            shiftChoices.Clear(); shiftChoices.AddRange(Save.group.shiftTypes.Where(x => !x.retired || x.id == id));
            linkedShift.ClearOptions(); linkedShift.AddOptions(shiftChoices.Select(x => x.name).ToList());
            ((ShiftDropdown)linkedShift).shiftColors = shiftChoices.Select(x => ShiftStyleUtility.ToColor(x.colorHex)).ToList();
            linkedShift.SetValueWithoutNotify(Math.Max(0, shiftChoices.FindIndex(x => x.id == id))); ConditionalSections();
        }
        public void EditEvent(string id,string date)
        {
            var e=Save.events.Find(x=>x.id==id);if(e==null)return;
            editingEvent=id; editingRule=null; ruleTemplate=null; originalDate=date;
            var ex=Save.exceptions.Find(x=>x.seriesId==id&&x.originalDate==date);
            var copy=JsonUtility.FromJson<EventSeries>(JsonUtility.ToJson(ex?.replacement??e));
            DeviceCalendarStore.Apply(copy,DeviceCalendarStore.Find(id,date));
            if(ex?.replacement==null)copy.dateKey=date;
            LoadEvent(copy);
        }
        private void LoadEvent(EventSeries e)
        {
            InitializeEditor(); loading = true;
            eventTemplate = JsonUtility.FromJson<EventSeries>(JsonUtility.ToJson(e));
            CloseEditors(); eventPanel.SetActive(true); eventPanel.transform.SetAsLastSibling();
            editorHeading.text = string.IsNullOrEmpty(editingEvent) ? "New event" : "Edit event";
            Set(eventFields,"title",e.title); Set(eventFields,"notes",e.notes); Set(eventFields,"interval",e.interval.ToString()); Set(eventFields,"count",Math.Max(1,e.count).ToString());
            eventDate.Set(e.dateKey); eventTime.Set(e.startTime); endingDate.Set(string.IsNullOrEmpty(e.until) ? e.dateKey : e.until);
            recurrence.ClearOptions();recurrence.AddOptions(new List<string>{"Once","Daily","Weekly","Monthly"});if(string.IsNullOrEmpty(editingEvent))recurrence.AddOptions(new List<string>{"On shift days"});recurrence.SetValueWithoutNotify((int)e.recurrence); editScope.ClearOptions(); editScope.AddOptions(new List<string>{"This occurrence","This and future occurrences","Entire series"}); editScope.SetValueWithoutNotify(0);
            timingMode.SetValueWithoutNotify(0);
            repeatEnding.SetValueWithoutNotify(!string.IsNullOrEmpty(e.until)?1:e.count>0?2:0);
            eventAlarm.isOn=e.alarm; eventEnabled.isOn=e.enabled; eventVibration.isOn=e.vibration; eventSound.SetValueWithoutNotify(SoundIndex(e.sound)); defaultSettings.isOn=e.useDefaultAlarmSettings;
            reminderChoice.Set(e.reminder ? e.reminderMinutes == 0 ? -1 : e.reminderMinutes : 0); snoozeOverride.Set(e.snoozeMinutes); beforeShift.Set(90);
            for(int i=0;i<weekdays.Length;i++)weekdays[i].isOn=e.weekdays.Contains(i);
            contextSection.SetActive(false); advancedSection.SetActive(false); PopulateShifts(-1); loading = false;
            ConditionalSections(); Message(""); editorSnapshot = EditorSignature;
            eventPanel.GetComponent<ModalPanel>().HasChanges=()=>HasEditorChanges; eventPanel.GetComponent<ModalPanel>().Close=CloseEditors;
        }
        public void ConditionalSections()
        {
            if (recurrence == null || loading) return;
            bool linked = recurrence.value == 4, single = ruleTemplate != null && editScope.value == 0;
            bool repeating = !linked && recurrence.value > 0;
            intervalSection.SetActive(repeating); weekdaySection.SetActive(!linked && recurrence.value == 2);
            endingSection.SetActive(repeating); endingDateSection.SetActive(repeating && repeatEnding.value == 1); countSection.SetActive(repeating && repeatEnding.value == 2);
            shiftSection.SetActive(linked && !single); beforeSection.SetActive(linked && !single && timingMode.value == 0);
            timeSection.SetActive(!linked || single || timingMode.value == 1);
            scopeSection.SetActive(ruleTemplate != null || !string.IsNullOrEmpty(editingEvent) && Save.events.Find(x=>x.id==editingEvent)?.recurrence != RecurrenceKind.Once);
            recurrence.interactable = ruleTemplate == null;
            reminderChoice.gameObject.SetActive(!linked); timingPreview.gameObject.SetActive(linked);
            foreach(var f in eventFields.Where(x=>x.name=="title"||x.name=="notes"))f.interactable=!single;
            eventAlarm.interactable=!single;
            advancedButton.SetActive(!single); if(single)advancedSection.SetActive(false);
            // Existing EventSeries remain EventSeries; changing engine requires a deliberate new definition.
            if (!string.IsNullOrEmpty(editingEvent) && recurrence.value == 4) { recurrence.SetValueWithoutNotify((int)eventTemplate.recurrence); linked = false; }
            deleteButton.SetActive(!string.IsNullOrEmpty(editingEvent) || ruleTemplate != null);
            deleteButton.GetComponentInChildren<Text>().text = ruleTemplate != null ? single ? "Skip this occurrence" : "Delete rule" : "Delete event";
            overrideSection.SetActive(!defaultSettings.isOn);
            intervalLabel.text = "Every N " + (recurrence.value == 1 ? "days" : recurrence.value == 2 ? "weeks" : "months");
            if (shiftChoices.Count > 0) linkedSwatch.color = ShiftStyleUtility.ToColor(shiftChoices[Mathf.Clamp(linkedShift.value,0,shiftChoices.Count-1)].colorHex);
            timingPreview.text = single ? "Changes delivery for the " + originalDate + " shift only." : "";
            if (linked && !single && shiftChoices.Count > 0)
            {
                var shift = shiftChoices[linkedShift.value];
                timingPreview.text = "Follows the resolved " + shift.name + " shift, including date changes.";
                if (timingMode.value == 0 && ShiftTimeUtility.TryParseTime(shift.startTime,out _))
                    try { var at = RecurrenceEngine.Instant(DateKeyUtility.FromDateKey(eventDate.value),shift.startTime,"device") - beforeShift.Minutes * 60000L;
                        timingPreview.text += "\nFor this shift date: " + DateTimeOffset.FromUnixTimeMilliseconds(at).ToLocalTime().ToString("MMM d, h:mm tt"); } catch (Exception) { }
            }
        }
        public void ToggleAdvanced() => advancedSection.SetActive(!advancedSection.activeSelf);
        public void SaveEvent()
        {
            try
            {
                if (recurrence.value == 4) { SaveLinked(); return; }
                var e=JsonUtility.FromJson<EventSeries>(JsonUtility.ToJson(eventTemplate)); e.id=Guid.NewGuid().ToString("N");
                e.title=Get(eventFields,"title"); e.dateKey=eventDate.value; e.startTime=eventTime.value; e.notes=Get(eventFields,"notes");
                e.recurrence=(RecurrenceKind)recurrence.value; e.interval=e.recurrence==RecurrenceKind.Once?Math.Max(1,e.interval):Number(eventFields,"interval");
                // Hidden legacy recurrence limits stay untouched on once-only records.
                if(e.recurrence!=RecurrenceKind.Once){e.until=repeatEnding.value==1?endingDate.value:"";e.count=repeatEnding.value==2?Number(eventFields,"count"):0;}
                if(e.recurrence!=RecurrenceKind.Once&&repeatEnding.value==2&&e.count<1)throw new ArgumentException("Use at least 1 occurrence.");
                int reminder=reminderChoice.Minutes; e.reminder=reminder!=0; if(e.reminder)e.reminderMinutes=Math.Max(0,reminder);
                e.alarm=eventAlarm.isOn; e.enabled=eventEnabled.isOn; e.useDefaultAlarmSettings=defaultSettings.isOn;
                if(!defaultSettings.isOn){e.vibration=eventVibration.isOn;e.sound=eventSound.value==SoundIndex(eventTemplate.sound)?eventTemplate.sound:Sound(eventSound.value);e.snoozeMinutes=snoozeOverride.Minutes;}
                e.weekdays.Clear();for(int i=0;i<7;i++)if(weekdays[i].isOn)e.weekdays.Add(i);
                if(!RecurrenceEngine.Validate(e,out string error)){Message(error);return;}
                var old=Save.events.Find(x=>x.id==editingEvent);
                if(old!=null&&editScope.value>0)
                {
                    // These fields are hidden in the compact editor. An occurrence
                    // replacement may have different legacy metadata from its series.
                    e.zone=old.zone;e.endTime=old.endTime;e.advanceMinutes=old.advanceMinutes;
                    if(e.sound==eventTemplate.sound)e.sound=old.sound;
                    if(e.vibration==eventTemplate.vibration)e.vibration=old.vibration;
                    if(e.snoozeMinutes==eventTemplate.snoozeMinutes)e.snoozeMinutes=old.snoozeMinutes;
                    if(!RecurrenceEngine.Validate(e,out error)){Message(error);return;}
                }
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
                var subscription=DeviceCalendarStore.FromEvent(e);subscription.sourceId=old!=null&&editScope.value==0?old.id:e.id;subscription.originalDate=old!=null&&editScope.value==0&&old.recurrence!=RecurrenceKind.Once?originalDate:null;DeviceCalendarStore.Subscribe(subscription);
                e.alarm=false;e.reminder=false;AppSession.Instance.SaveLocal();ShowAgenda();
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
            if (loading) return;
            if (ruleTemplate != null) { LoadRule(ruleTemplate, originalDate, scope); return; }
            var e=Save.events.Find(x=>x.id==editingEvent);
            if(e!=null)
            {
                string date=scope==2?e.dateKey:Save.exceptions.Find(x=>x.seriesId==e.id&&x.originalDate==originalDate)?.replacement?.dateKey??originalDate;
                eventDate.Set(date);
                // An exception's replacement is once-only. Moving its edit scope
                // back to the series must restore that series' recurrence contract.
                if(scope>0&&eventTemplate.recurrence==RecurrenceKind.Once&&e.recurrence!=RecurrenceKind.Once)
                {
                    recurrence.SetValueWithoutNotify((int)e.recurrence);Set(eventFields,"interval",e.interval.ToString());
                    repeatEnding.SetValueWithoutNotify(!string.IsNullOrEmpty(e.until)?1:e.count>0?2:0);
                    endingDate.Set(string.IsNullOrEmpty(e.until)?e.dateKey:e.until);Set(eventFields,"count",Math.Max(1,e.count).ToString());
                    for(int i=0;i<7;i++)weekdays[i].isOn=e.weekdays.Contains(i);
                }
                ConditionalSections();
            }
        }
        public void DeleteEvent() => AppNavigation.Instance.Confirm("Delete the selected event scope?", () => {
            if (ruleTemplate != null) {
                if (editScope.value == 0) AndroidBridge.Action("skip", "shift:" + ruleTemplate.id + ":" + originalDate);
                else { Save.rules.RemoveAll(x=>x.id==ruleTemplate.id); Save.mutedRules.Remove(ruleTemplate.id); AppSession.Instance.SaveLocal(); }
                ShowAgenda();
            } else DeleteEventConfirmed();
        });
        private void DeleteEventConfirmed()
        {
            var e=Save.events.Find(x=>x.id==editingEvent);if(e==null){ShowAgenda();return;}
            if(editScope.value==0&&e.recurrence!=RecurrenceKind.Once){var ex=Exception(e.id,originalDate);ex.cancelled=true;ex.replacement=null;}
            else if(editScope.value==1&&e.recurrence!=RecurrenceKind.Once&&originalDate!=e.dateKey){e.until=DateKeyUtility.ToDateKey(DateKeyUtility.FromDateKey(originalDate).AddDays(-1));Save.exceptions.RemoveAll(x=>x.seriesId==e.id&&string.CompareOrdinal(x.originalDate,originalDate)>=0);}
            else{Save.events.Remove(e);Save.exceptions.RemoveAll(x=>x.seriesId==e.id);}
            AppSession.Instance.SaveLocal();ShowAgenda();
        }
        public void NewRule()
        {
            NewEvent(); recurrence.value = 4; Set(eventFields,"title","Wake Up");
            PopulateShifts((int)ShiftTypeId.Day12); timingMode.value = 0; ConditionalSections(); editorSnapshot = EditorSignature;
        }
        public void LoadRule(ShiftAlarmRule r, string date = null, int scope = -1)
        {
            if (r == null) return;
            editingRule = r.id; editingEvent = null;
            originalDate = date ?? DateKeyUtility.ToDateKey(DateTime.Today);
            ruleTemplate = null;
            // Load ordinary controls once, then switch to the rule's authoritative identity.
            LoadEvent(new EventSeries { title=r.label,notes=r.notes,dateKey=date??DateKeyUtility.ToDateKey(DateTime.Today),startTime=string.IsNullOrEmpty(r.fixedTime)?"04:00":r.fixedTime,
                alarm=!r.calendarOnly&&r.audible,enabled=r.enabled&&!Save.mutedRules.Contains(r.id),vibration=r.vibration,sound=r.sound,snoozeMinutes=r.snoozeMinutes,useDefaultAlarmSettings=r.useDefaultAlarmSettings });
            ruleTemplate=JsonUtility.FromJson<ShiftAlarmRule>(JsonUtility.ToJson(r)); loading=true;
            editorHeading.text="Edit shift event"; recurrence.SetValueWithoutNotify(4);
            editScope.ClearOptions();editScope.AddOptions(new List<string>{"This occurrence","Entire rule"});editScope.SetValueWithoutNotify(scope>=0?scope:date==null?1:0);
            timingMode.SetValueWithoutNotify((int)r.timingMode); beforeShift.Set(r.beforeMinutes); reminderChoice.Set(0);
            PopulateShifts(r.shiftType); loading=false;
            if(editScope.value==0)
            {
                var hit=RecurrenceEngine.ShiftDates(Save,DateKeyUtility.FromDateKey(originalDate),DateKeyUtility.FromDateKey(originalDate),true).Find(x=>x.sourceId==r.id);
                if(hit!=null){var actual=DateTimeOffset.FromUnixTimeMilliseconds(hit.at).ToLocalTime();eventDate.Set(DateKeyUtility.ToDateKey(actual.Date));eventTime.Set(actual.ToString("HH:mm"));}
                timingPreview.text="Changes delivery for the " + originalDate + " shift only.";
            }
            ConditionalSections(); Message("");editorSnapshot=EditorSignature;
        }
        private void SaveLinked()
        {
            if(ruleTemplate!=null&&editScope.value==0)
            {
                long at=RecurrenceEngine.Instant(DateKeyUtility.FromDateKey(eventDate.value),eventTime.value,"device");
                if(at<=DateKeyUtility.UnixMsNow())throw new ArgumentException("Choose a future delivery time.");
                ShiftDeliveryExceptions.Change("shift:"+ruleTemplate.id+":"+originalDate,at);ShowAgenda();return;
            }
            if(shiftChoices.Count==0)throw new ArgumentException("Add a shift type first.");
            var shift=shiftChoices[linkedShift.value];
            if(timingMode.value==0&&!ShiftTimeUtility.TryParseTime(shift.startTime,out _))throw new ArgumentException("Set a start time for "+shift.name+" in Manage Shifts first, or choose Specific time.");
            var r=ruleTemplate==null?new ShiftAlarmRule{groupId=Save.group.groupId,fromDate=eventDate.value,useDefaultAlarmSettings=true}
                :JsonUtility.FromJson<ShiftAlarmRule>(JsonUtility.ToJson(ruleTemplate));
            r.label=Get(eventFields,"title");if(string.IsNullOrWhiteSpace(r.label))throw new ArgumentException("Enter an event title.");
            r.notes=Get(eventFields,"notes");r.shiftType=shift.id;r.timingMode=(ShiftTimingMode)timingMode.value;r.beforeMinutes=beforeShift.Minutes;r.fixedTime=eventTime.value;
            if(r.timingMode==ShiftTimingMode.FixedTime&&!ShiftTimeUtility.TryParseTime(r.fixedTime,out _))throw new ArgumentException("Choose a time.");
            r.audible=eventAlarm.isOn;
            // Existing silent notifications remain silent notifications unless Alarm is deliberately changed.
            if(ruleTemplate==null||eventAlarm.isOn!=(!ruleTemplate.calendarOnly&&ruleTemplate.audible))r.calendarOnly=!eventAlarm.isOn;
            r.useDefaultAlarmSettings=defaultSettings.isOn;
            if(!defaultSettings.isOn){r.vibration=eventVibration.isOn;r.sound=ruleTemplate!=null&&eventSound.value==SoundIndex(ruleTemplate.sound)?ruleTemplate.sound:Sound(eventSound.value);r.snoozeMinutes=snoozeOverride.Minutes;}
            r.enabled=true;if(!eventEnabled.isOn&&!Save.mutedRules.Contains(r.id))Save.mutedRules.Add(r.id);else if(eventEnabled.isOn)Save.mutedRules.Remove(r.id);
            Save.rules.RemoveAll(x=>x.id==r.id);Save.rules.Add(r);AppSession.Instance.SaveLocal();ShowAgenda();
        }
        public void Refresh()
        {
            if(AppSession.Instance==null||Save==null)return;
            if(!agendaPanel.activeSelf)return;
            foreach(var row in rows) { row.gameObject.SetActive(false); if(Application.isPlaying)Destroy(row.gameObject);else DestroyImmediate(row.gameObject); } rows.Clear();
            readinessLabel.text=AndroidBridge.Readiness();
            if (permissionSummary != null) permissionSummary.text = AndroidBridge.Call<string>("permissionSummary") ?? "Alarm readiness - Android device required";
            upcomingSection.SetActive(upcomingOpen);
            upcomingLabel.text = "Upcoming";
            if (undoButton != null) undoButton.SetActive(!string.IsNullOrEmpty(undoId));
            try
            {
                long now=DateKeyUtility.UnixMsNow();
                var occurrences=RecurrenceEngine.Resolve(DeviceCalendarStore.Effective(Save),DateTime.Today.AddDays(-1),DateTime.Today.AddYears(2));
                var states=AndroidBridge.DeliveryStates();
                foreach(var o in occurrences)
                    if(states.TryGetValue(o.id,out var state)&&state.state=="snoozed"){o.at=state.at;o.state=state.state;}
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
                    var next=occurrences.Where(o=>!o.isEvent&&o.sourceId==r.id&&o.at>now&&
                        (!states.TryGetValue(o.id,out var state)||state.state=="snoozed")).OrderBy(o=>o.at).FirstOrDefault();
                    bool active=r.enabled&&!Save.mutedRules.Contains(r.id);
                    string shift=Save.group.shiftTypes.Find(x=>x.id==r.shiftType)?.name??"Missing shift";
                    Row(r.label,shift+" shifts • "+(r.timingMode==ShiftTimingMode.FixedTime?r.fixedTime:r.beforeMinutes+" min before start")+"\n"+(next==null?"No upcoming delivery":"Next: "+DayInformation.Time(next))+" • "+(active?"Active":"Paused"),"Edit",()=>{editingRule=r.id;LoadRule(r);},active?"Pause":"Resume",()=>{r.enabled=true;if(active)Save.mutedRules.Add(r.id);else Save.mutedRules.Remove(r.id);AppSession.Instance.SaveLocal();},"Upcoming",()=>UpcomingFor(r.id));
                }
                if(!upcomingOpen)return;
                string deliveries=AndroidBridge.Call<string>("specialDeliveries");
                var special=string.IsNullOrEmpty(deliveries)?new List<Occurrence>():JsonUtility.FromJson<OccurrenceList>(deliveries).items;
                string queuedJson=AndroidBridge.Call<string>("queuedDeliveries");
                var queued=string.IsNullOrEmpty(queuedJson)?occurrences.Where(o=>o.at>now&&o.at<now+45L*86400000).ToList():JsonUtility.FromJson<OccurrenceList>(queuedJson).items;
                if(!string.IsNullOrEmpty(queuedJson))queued.AddRange(occurrences.Where(o=>o.state=="view"&&o.at>now&&o.at<now+45L*86400000));
                var upcoming=special.Concat(queued.Where(o=>o.at>now&&!states.ContainsKey(o.id)&&!special.Exists(x=>x.id==o.id))).Where(o=>sourceFilter==null||o.sourceId==sourceFilter).OrderBy(o=>o.at).ToList();
                showMore.gameObject.SetActive(upcoming.Count>upcomingLimit);
                foreach(var o in upcoming.Take(upcomingLimit))
                {
                    bool skipped=o.state=="skipped",ringing=o.state=="ringing",view=o.id.EndsWith(":view")||o.state=="view"||string.IsNullOrEmpty(queuedJson)&&!skipped&&!ringing;
                    Action edit=()=>{if(o.isEvent&&o.sourceId.StartsWith("activity-")){var family=GetComponent<FamilyWorkbench>();var d=DateKeyUtility.FromDateKey(o.calendarDateKey??o.dateKey);var item=ActivityResolver.Resolve(Save,d,d).FirstOrDefault(x=>"activity-"+x.seriesId==o.sourceId&&x.originalDate==o.dateKey);if(item!=null)family.Details(item);}else if(o.isEvent)EditEvent(o.sourceId,o.dateKey);else{var rule=Save.rules.Find(x=>x.id==o.sourceId);if(rule!=null){editingRule=rule.id;LoadRule(rule,o.dateKey);}}};
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
            foreach (var panel in new[]{eventPanel})
            {
                var scroll=panel.GetComponentInChildren<ScrollRect>(true);
                if (scroll != null) scroll.GetComponent<RectTransform>().offsetMin = new Vector2(20,string.IsNullOrEmpty(text)?180:360);
            }
            bool editor=eventPanel.activeSelf;
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
