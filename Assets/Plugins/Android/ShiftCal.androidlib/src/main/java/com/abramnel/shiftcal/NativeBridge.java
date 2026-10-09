package com.abramnel.shiftcal;

import android.Manifest;
import android.app.*;
import android.content.*;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.net.Uri;
import android.os.*;
import android.provider.Settings;
import android.service.notification.StatusBarNotification;
import android.view.WindowManager;
import androidx.credentials.*;
import androidx.credentials.exceptions.*;
import com.google.android.libraries.identity.googleid.*;
import org.json.*;
import java.text.DateFormat;
import java.util.*;
import java.util.concurrent.Executor;

public final class NativeBridge {
    private static final Object PICKER_LOCK=new Object();
    private static Dialog picker;
    private static String pickerToken="";
    public static String pickDate(Activity a,String token,String value){
        java.time.LocalDate date;
        try{date=value.isEmpty()?java.time.LocalDate.now():java.time.LocalDate.parse(value);}catch(Exception e){return "Invalid date";}
        final java.time.LocalDate initial=date;
        synchronized(PICKER_LOCK){if(!pickerToken.isEmpty())return "A picker is already open.";pickerToken=token;}
        a.runOnUiThread(()->{
            boolean[] accepted={false};
            DatePickerDialog dialog=new DatePickerDialog(a,(view,year,month,day)->{
                accepted[0]=true;pickerResult(token,true,java.time.LocalDate.of(year,month+1,day).toString());
            },initial.getYear(),initial.getMonthValue()-1,initial.getDayOfMonth());
            Calendar min=Calendar.getInstance(),max=Calendar.getInstance();min.set(2000,0,1,0,0,0);max.set(2100,11,31,23,59,59);
            dialog.getDatePicker().setMinDate(min.getTimeInMillis());dialog.getDatePicker().setMaxDate(max.getTimeInMillis());
            showPicker(a,dialog,token,accepted);
        });return "";
    }
    public static String pickTime(Activity a,String token,String value){
        java.time.LocalTime time;
        try{time=value.isEmpty()?java.time.LocalTime.of(13,0):OccurrenceEngine.time(value);}catch(Exception e){return "Invalid time";}
        final java.time.LocalTime initial=time;
        synchronized(PICKER_LOCK){if(!pickerToken.isEmpty())return "A picker is already open.";pickerToken=token;}
        a.runOnUiThread(()->{
            boolean[] accepted={false};
            TimePickerDialog dialog=new TimePickerDialog(a,(view,hour,minute)->{
                accepted[0]=true;pickerResult(token,true,String.format(Locale.US,"%02d:%02d",hour,minute));
            },initial.getHour(),initial.getMinute(),android.text.format.DateFormat.is24HourFormat(a));
            showPicker(a,dialog,token,accepted);
        });return "";
    }
    private static void showPicker(Activity a,Dialog dialog,String token,boolean[] accepted){
        synchronized(PICKER_LOCK){
            if(!pickerToken.equals(token)||a.isFinishing()||a.isDestroyed()){pickerToken="";pickerResult(token,false,"");return;}
            picker=dialog;
        }
        dialog.setOnDismissListener(d->{synchronized(PICKER_LOCK){picker=null;pickerToken="";}if(!accepted[0])pickerResult(token,false,"");});
        dialog.show();
    }
    private static void pickerResult(String token,boolean accepted,String value){try{message("OnPickerResult",new JSONObject().put("token",token).put("accepted",accepted).put("value",value).toString());}catch(Exception ignored){}}
    public static String cancelPicker(Activity a,String token){a.runOnUiThread(()->{synchronized(PICKER_LOCK){if(pickerToken.equals(token)){if(picker!=null)picker.dismiss();else pickerToken="";}}});return "";}
    public static String preferences(Activity a,String json){try{synchronized(AlarmStore.LOCK){JSONObject root=AlarmStore.read(a);root.put("preferences",new JSONObject(json));AlarmStore.write(a,root);AlarmScheduler.reconcile(a,root);}return "";}catch(Exception e){error(a,e);return e.getMessage();}}
    public static String alarmTheme(Activity a,int theme){try{synchronized(AlarmStore.LOCK){JSONObject root=AlarmStore.read(a);root.put("theme",Math.max(0,Math.min(2,theme)));AlarmStore.write(a,root);}return "";}catch(Exception e){return e.getMessage();}}
    public static int keyboardHeight(Activity a){
        android.view.View root=a.getWindow().getDecorView();
        if(Build.VERSION.SDK_INT>=30){android.view.WindowInsets insets=root.getRootWindowInsets();if(insets!=null)return insets.isVisible(android.view.WindowInsets.Type.ime())
            ?Math.max(0,insets.getInsets(android.view.WindowInsets.Type.ime()).bottom-insets.getInsets(android.view.WindowInsets.Type.systemBars()).bottom):0;}
        android.graphics.Rect visible=new android.graphics.Rect();root.getWindowVisibleDisplayFrame(visible);
        int obscured=root.getRootView().getHeight()-visible.bottom;
        return obscured>root.getRootView().getHeight()*.15f?obscured:0;
    }
    public static String shiftDeliveryChanges(Activity a){try{synchronized(AlarmStore.LOCK){JSONObject changes=AlarmStore.account(AlarmStore.read(a)).optJSONObject("shiftChanges");JSONArray items=new JSONArray();if(changes!=null){Iterator<String> keys=changes.keys();while(keys.hasNext()){String id=keys.next();items.put(new JSONObject().put("id",id).put("at",changes.getLong(id)));}}return new JSONObject().put("items",items).toString();}}catch(Exception e){return "{\"items\":[]}";}}
    public static String changeShiftDelivery(Activity a,String id,long at){try{synchronized(AlarmStore.LOCK){
        if(!id.startsWith("shift:")||at<=System.currentTimeMillis())throw new IllegalArgumentException("Choose a future shift delivery.");
        JSONObject root=AlarmStore.read(a),account=AlarmStore.account(root),changes=account.optJSONObject("shiftChanges");if(changes==null){changes=new JSONObject();account.put("shiftChanges",changes);}
        JSONObject entry=account.getJSONObject("ledger").optJSONObject(id);
        if(entry!=null&&!entry.optString("state").equals("skipped"))throw new IllegalArgumentException("This occurrence has already been delivered or dismissed.");
        // A deliberate edit may undo a skip; schedule reconciliation alone never does.
        account.getJSONObject("ledger").remove(id);changes.put(id,at);AlarmStore.write(a,root);AlarmScheduler.reconcile(a,root);
    }return "";}catch(Exception e){return e.getMessage();}}
    static Executor mainExecutor(){return command->new Handler(Looper.getMainLooper()).post(command);}
    public static String backup(Activity a,String json){try{java.io.File folder=new java.io.File(a.getCacheDir(),"shiftcal-backups");folder.mkdirs();java.io.File file=new java.io.File(folder,"ShiftCal-backup.json");try(java.io.FileOutputStream out=new java.io.FileOutputStream(file)){out.write(json.getBytes(java.nio.charset.StandardCharsets.UTF_8));out.getFD().sync();}
        Uri uri=androidx.core.content.FileProvider.getUriForFile(a,a.getPackageName()+".shiftcal.files",file);Intent share=new Intent(Intent.ACTION_SEND).setType("application/json").putExtra(Intent.EXTRA_STREAM,uri).addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);share.setClipData(ClipData.newRawUri("ShiftCal backup",uri));a.startActivity(Intent.createChooser(share,"Export calendar backup"));return "";
    }catch(Exception e){return e.getMessage();}}
    public static long instant(Activity a,String date,String time,String zone){return OccurrenceEngine.instant(date,time,zone);}
    public static String commit(Activity a,String json){
        try{synchronized(AlarmStore.LOCK){
            JSONObject save=new JSONObject(json),root=AlarmStore.read(a);if(save.getInt("version")!=3)throw new IllegalArgumentException("Unsupported schedule version");
            String account=save.getString("account");
            if(!root.optString("account").equals(account)){AlarmScheduler.cancel(a,root);removeAllNotifications(a);a.stopService(new Intent(a,RingingService.class));}
            root.put("account",account).put("active",true);AlarmStore.account(root).put("save",save);
            AlarmStore.write(a,root);AlarmScheduler.reconcile(a,root);
        }return "";}catch(Exception e){error(a,e);return "Android scheduling error: "+e.getMessage();}
    }
    public static String action(Activity a,String action,String id){
        try{
            if(action.equals("background")){a.runOnUiThread(()->a.moveTaskToBack(true));return "";}
            if(action.equals("permissions")){a.runOnUiThread(()->{if(Build.VERSION.SDK_INT>=33)a.requestPermissions(new String[]{Manifest.permission.POST_NOTIFICATIONS},431);});return "";}
            if(action.equals("exact")){if(Build.VERSION.SDK_INT>=31)a.startActivity(new Intent(Settings.ACTION_REQUEST_SCHEDULE_EXACT_ALARM,Uri.parse("package:"+a.getPackageName())));return "";}
            if(action.equals("fullscreen")){if(Build.VERSION.SDK_INT>=34)a.startActivity(new Intent(Settings.ACTION_MANAGE_APP_USE_FULL_SCREEN_INTENT,Uri.parse("package:"+a.getPackageName())));return "";}
            if(action.equals("reconcile")){AlarmScheduler.reconcile(a);return "";}
            if(action.equals("deactivate")){synchronized(AlarmStore.LOCK){JSONObject root=AlarmStore.read(a);root.put("active",false);AlarmScheduler.cancel(a,root);AlarmStore.write(a,root);removeAllNotifications(a);a.stopService(new Intent(a,RingingService.class));}return "";}
            if(action.equals("test")){synchronized(AlarmStore.LOCK){JSONObject root=AlarmStore.read(a);JSONObject options=new JSONObject().put("vibration",true).put("snoozeMinutes",1).put("advanceMinutes",1);
                JSONObject o=OccurrenceEngine.make("test:"+UUID.randomUUID(),"test","","ShiftCal test alarm",System.currentTimeMillis()+15000,true,options);
                AlarmStore.account(root).put("test",o);AlarmStore.write(a,root);AlarmScheduler.reconcile(a,root);}return "";}
            AlarmScheduler.action(a,action,id);return "";
        }catch(Exception e){error(a,e);return e.getMessage();}
    }
    public static String status(Activity a){
        try{synchronized(AlarmStore.LOCK){JSONObject r=AlarmStore.read(a),save=AlarmStore.account(r).optJSONObject("save");
            String blocked=r.optString("error");
            if(!r.optBoolean("active"))blocked="Calendar not active";
            else if(!AlarmScheduler.notifications(a))blocked="Notification permission required";
            else if(!AlarmScheduler.exact(a))blocked="Exact alarm access required";
            else if(!channel(a,"shiftcal_ring"))blocked="Audible alarm notification channel is disabled";
            else if(!channel(a,"shiftcal_reminder"))blocked="Reminder notification channel is disabled";
            if(save!=null){JSONArray rules=OccurrenceEngine.array(save,"rules"),shifts=OccurrenceEngine.array(save.getJSONObject("group"),"shiftTypes");
                for(int i=0;i<rules.length();i++){JSONObject rule=rules.getJSONObject(i);if(!rule.optBoolean("enabled",true))continue;
                    if(!rule.optString("groupId").isEmpty()&&!rule.optString("groupId").equals(save.getJSONObject("group").optString("groupId")))continue;
                    boolean muted=false;JSONArray mutedRules=OccurrenceEngine.array(save,"mutedRules");for(int j=0;j<mutedRules.length();j++)if(mutedRules.optString(j).equals(rule.optString("id")))muted=true;if(muted)continue;
                    boolean usable=false;
                    for(int j=0;j<shifts.length();j++){JSONObject shift=shifts.getJSONObject(j);if(shift.optInt("id")==rule.optInt("shiftType")){try{OccurrenceEngine.time(shift.optString("startTime"));usable=true;}catch(Exception ignored){}}}
                    if(rule.optInt("timingMode")==1){try{OccurrenceEngine.time(rule.optString("fixedTime"));usable=true;}catch(Exception ignored){}}
                    if(!usable&&!rule.optBoolean("calendarOnly"))blocked="Add a valid start time for "+rule.optString("label");}}
            JSONObject next=r.optJSONObject("next");String text="Notifications: "+(AlarmScheduler.notifications(a)?"allowed":"blocked")+"\nExact alarms: "+(AlarmScheduler.exact(a)?"allowed":"blocked");
            text+="\n"+(blocked.isEmpty()?"Alarm capability ready":blocked);
            text+="\n"+(blocked.isEmpty()&&next!=null?"Next scheduled: "+next.optString("title")+" / "+DateFormat.getDateTimeInstance().format(new Date(next.optLong("at"))):"No confirmed scheduled alarm");
            return text;
        }}catch(Exception e){return "Alarm state could not be read: "+e.getMessage();}
    }
    public static String delivery(Activity a){try{synchronized(AlarmStore.LOCK){return AlarmStore.account(AlarmStore.read(a)).getJSONObject("ledger").toString();}}catch(Exception e){return "{}";}}
    public static String permissionSummary(Activity a){
        if(!AlarmScheduler.notifications(a))return "Notifications required • Tap to fix";
        if(!AlarmScheduler.exact(a))return "Exact alarm access required • Tap to fix";
        if(!channel(a,"shiftcal_ring")||!channel(a,"shiftcal_reminder"))return "Notification channel disabled • Tap for details";
        if(Build.VERSION.SDK_INT>=34&&!nm(a).canUseFullScreenIntent())return "Full-screen alarm access missing • Tap for details";
        return "Alarm permissions ready ✓ • Scheduling details";
    }
    public static String state(Activity a,String id){try{synchronized(AlarmStore.LOCK){JSONObject e=AlarmStore.account(AlarmStore.read(a)).getJSONObject("ledger").optJSONObject(id);return e==null?"":e.optString("state");}}catch(Exception e){return "";}}
    public static String opened(Activity a){try{synchronized(AlarmStore.LOCK){JSONObject root=AlarmStore.read(a);String id=root.optString("pendingOpen");if(id.isEmpty())return "";root.remove("pendingOpen");AlarmStore.write(a,root);JSONObject o=root.optJSONObject("occurrences")==null?null:root.getJSONObject("occurrences").optJSONObject(id);if(o==null){JSONObject state=AlarmStore.account(root).getJSONObject("ledger").optJSONObject(id);if(state!=null)o=state.optJSONObject("occurrence");}return o==null?"":o.toString();}}catch(Exception e){return "";}}
    public static String specialDeliveries(Activity a){try{synchronized(AlarmStore.LOCK){JSONObject ledger=AlarmStore.account(AlarmStore.read(a)).getJSONObject("ledger");JSONArray items=new JSONArray();Iterator<String> keys=ledger.keys();while(keys.hasNext()){String id=keys.next();JSONObject entry=ledger.getJSONObject(id),o=entry.optJSONObject("occurrence");String state=entry.optString("state");if(o==null)continue;
        if(state.equals("ringing")||state.equals("snoozed")||state.equals("skipped")&&entry.optLong("originalAt")>System.currentTimeMillis()){JSONObject item=new JSONObject(o.toString()).put("state",state);if(state.equals("snoozed"))item.put("at",entry.optLong("at"));items.put(item);}}
        return new JSONObject().put("items",items).toString();}}catch(Exception e){return "{\"items\":[]}";}}
    public static String systemBars(Activity a,boolean dark){a.runOnUiThread(()->{
        a.getWindow().clearFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN);
        a.getWindow().setStatusBarColor(dark?Color.rgb(20,23,28):Color.rgb(245,247,250));
        a.getWindow().setNavigationBarColor(dark?Color.rgb(20,23,28):Color.rgb(245,247,250));
        a.getWindow().getDecorView().setSystemUiVisibility(dark?0:android.view.View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR|android.view.View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR);
    });return "";}
    public static String themeBars(Activity a,boolean dark,String background){a.runOnUiThread(()->{
        a.getWindow().clearFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN);
        int color=Color.parseColor(background);
        a.getWindow().setStatusBarColor(color);a.getWindow().setNavigationBarColor(color);
        a.getWindow().getDecorView().setSystemUiVisibility(dark?0:android.view.View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR|android.view.View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR);
    });return "";}
    public static String deliveryStates(Activity a){try{synchronized(AlarmStore.LOCK){
        JSONObject ledger=AlarmStore.account(AlarmStore.read(a)).getJSONObject("ledger");JSONArray items=new JSONArray();
        Iterator<String> keys=ledger.keys();while(keys.hasNext()){String id=keys.next();JSONObject entry=ledger.getJSONObject(id);
            items.put(new JSONObject().put("id",id).put("state",entry.optString("state")).put("at",entry.optLong("at",entry.optJSONObject("occurrence")==null?0:entry.getJSONObject("occurrence").optLong("at"))));}
        return new JSONObject().put("items",items).toString();
    }}catch(Exception e){return "{\"items\":[]}";}}
    public static String queuedDeliveries(Activity a){try{synchronized(AlarmStore.LOCK){
        JSONObject root=AlarmStore.read(a),queue=root.optJSONObject("occurrences");JSONArray items=new JSONArray();
        if(queue!=null){Iterator<String> keys=queue.keys();while(keys.hasNext())items.put(queue.getJSONObject(keys.next()));}
        return new JSONObject().put("items",items).toString();
    }}catch(Exception e){return "{\"items\":[]}";}}
    static void message(String method,String value){try{Class.forName("com.unity3d.player.UnityPlayer").getMethod("UnitySendMessage",String.class,String.class,String.class).invoke(null,"Systems",method,value);}catch(Exception ignored){}}
    static void error(Context c,Exception e){try{synchronized(AlarmStore.LOCK){JSONObject root=AlarmStore.read(c);root.put("error",e.getMessage()).put("next",JSONObject.NULL);AlarmStore.write(c,root);}}catch(Exception ignored){}}
    public static String signIn(Activity a,String clientId){
        if(clientId==null||!clientId.endsWith(".apps.googleusercontent.com"))return "Set the Web/server OAuth client ID in ShiftCalConfig.";
        a.runOnUiThread(()->{
            CredentialManager cm=CredentialManager.create(a);
            GetSignInWithGoogleOption option=new GetSignInWithGoogleOption.Builder(clientId).build();
            GetCredentialRequest request=new GetCredentialRequest.Builder().addCredentialOption(option).build();
            cm.getCredentialAsync(a,request,new CancellationSignal(),mainExecutor(),new CredentialManagerCallback<GetCredentialResponse,GetCredentialException>(){
                public void onResult(GetCredentialResponse r){try{Credential c=r.getCredential();if(!(c instanceof CustomCredential))throw new IllegalStateException("Unexpected credential");
                    GoogleIdTokenCredential token=GoogleIdTokenCredential.createFrom(c.getData());message("OnGoogleToken",token.getIdToken());
                }catch(Exception e){message("OnGoogleError",e.getMessage());}}
                public void onError(GetCredentialException e){message("OnGoogleError",e instanceof GetCredentialCancellationException?"Sign-in cancelled.":"Google sign-in failed: "+e.getMessage());}
            });
        });return "";
    }
    public static String signOut(Activity a){a.runOnUiThread(()->CredentialManager.create(a).clearCredentialStateAsync(new ClearCredentialStateRequest(),new CancellationSignal(),mainExecutor(),new CredentialManagerCallback<Void,ClearCredentialException>(){
        public void onResult(Void result){} public void onError(ClearCredentialException e){message("OnGoogleError","Account chooser reset failed: "+e.getMessage());}
    }));return "";}
    static NotificationManager nm(Context c){return (NotificationManager)c.getSystemService(Context.NOTIFICATION_SERVICE);}
    static void channels(Context c){
        NotificationChannel ring=new NotificationChannel("shiftcal_ring","Audible alarms",NotificationManager.IMPORTANCE_HIGH);ring.setSound(null,null);ring.setLockscreenVisibility(Notification.VISIBILITY_PRIVATE);nm(c).createNotificationChannel(ring);
        NotificationChannel reminder=new NotificationChannel("shiftcal_reminder","Reminders",NotificationManager.IMPORTANCE_DEFAULT);nm(c).createNotificationChannel(reminder);
        NotificationChannel upcoming=new NotificationChannel("shiftcal_upcoming","Upcoming alarms",NotificationManager.IMPORTANCE_LOW);upcoming.setSound(null,null);nm(c).createNotificationChannel(upcoming);
    }
    static boolean channel(Context c,String id){NotificationChannel ch=nm(c).getNotificationChannel(id);return ch==null||ch.getImportance()!=NotificationManager.IMPORTANCE_NONE;}
    static PendingIntent open(Context c,String id){String account="";try{account=AlarmStore.read(c).optString("account");}catch(Exception ignored){}
        Intent i=new Intent(c,AlarmActivity.class).putExtra("id",id).putExtra("account",account).setData(Uri.parse("shiftcal://open/"+Uri.encode(account)+"/"+Uri.encode(id)));
        return PendingIntent.getActivity(c,0,i,PendingIntent.FLAG_UPDATE_CURRENT|PendingIntent.FLAG_IMMUTABLE);}
    static Notification notification(Context c,JSONObject o,boolean ringing,boolean upcoming){
        String id=o.optString("id"),channel=ringing?"shiftcal_ring":upcoming?"shiftcal_upcoming":"shiftcal_reminder";
        Notification.Builder b=new Notification.Builder(c,channel).setSmallIcon(android.R.drawable.ic_lock_idle_alarm)
            .setContentTitle((upcoming?"Upcoming: ":"")+o.optString("title")).setContentText(DateFormat.getDateTimeInstance().format(new Date(o.optLong("at"))))
            .setContentIntent(open(c,id)).setVisibility(Notification.VISIBILITY_PRIVATE).setCategory(ringing?Notification.CATEGORY_ALARM:Notification.CATEGORY_REMINDER)
            .setOnlyAlertOnce(true).setOngoing(ringing).setAutoCancel(!ringing&&!upcoming);
        if(ringing){b.addAction(new Notification.Action.Builder(null,"Dismiss",AlarmScheduler.intent(c,id,"dismiss")).build());b.addAction(new Notification.Action.Builder(null,"Snooze",AlarmScheduler.intent(c,id,"snooze")).build());
            if(Build.VERSION.SDK_INT<34||nm(c).canUseFullScreenIntent())b.setFullScreenIntent(open(c,id),true);
        }else b.addAction(new Notification.Action.Builder(null,upcoming?"Dismiss this alarm":"Dismiss",AlarmScheduler.intent(c,id,upcoming?"skip":"dismiss")).build());
        return b.build();
    }
    static void upcoming(Context c,JSONObject o){if(AlarmScheduler.notifications(c))nm(c).notify(o.optString("id"),1,notification(c,o,false,true));}
    static void remove(Context c,String id){nm(c).cancel(id,1);}
    static void removeAllNotifications(Context c){nm(c).cancelAll();}
    static void stop(Context c,String id){RingingService service=RingingService.instance;if(service!=null)service.stopOccurrence(id);else nm(c).cancel(42);}
    static List<StatusBarNotificationWrapper> active(Context c){List<StatusBarNotificationWrapper> result=new ArrayList<>();for(StatusBarNotification n:nm(c).getActiveNotifications())if(n.getTag()!=null)result.add(new StatusBarNotificationWrapper(n.getTag(),(n.getNotification().flags&Notification.FLAG_ONGOING_EVENT)!=0,"shiftcal_upcoming".equals(n.getNotification().getChannelId())));return result;}
}
final class StatusBarNotificationWrapper {final String id;final boolean ringing,upcoming;StatusBarNotificationWrapper(String id,boolean ringing,boolean upcoming){this.id=id;this.ringing=ringing;this.upcoming=upcoming;}}
