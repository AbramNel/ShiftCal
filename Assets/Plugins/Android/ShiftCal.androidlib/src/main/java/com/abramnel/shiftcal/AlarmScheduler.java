package com.abramnel.shiftcal;

import android.app.*;
import android.content.*;
import android.net.Uri;
import android.os.Build;
import org.json.*;
import java.util.*;

final class AlarmScheduler {
    static final String REPLENISH="replenish";
    static AlarmManager manager(Context c){return (AlarmManager)c.getSystemService(Context.ALARM_SERVICE);}
    static boolean exact(Context c){return Build.VERSION.SDK_INT<31||manager(c).canScheduleExactAlarms();}
    static boolean notifications(Context c){return ((NotificationManager)c.getSystemService(Context.NOTIFICATION_SERVICE)).areNotificationsEnabled();}
    static PendingIntent intent(Context c,String id,String action){
        String account="";try{account=AlarmStore.read(c).optString("account");}catch(Exception ignored){}
        Intent i=new Intent(c,AlarmReceiver.class).setAction(action).setData(Uri.parse("shiftcal://delivery/"+Uri.encode(account)+"/"+Uri.encode(id)+"/"+action)).putExtra("id",id).putExtra("account",account);
        return PendingIntent.getBroadcast(c,0,i,PendingIntent.FLAG_UPDATE_CURRENT|PendingIntent.FLAG_IMMUTABLE);
    }
    static void cancel(Context c,JSONObject root) throws Exception {
        JSONArray old=OccurrenceEngine.array(root,"scheduled");
        for(int i=0;i<old.length();i++){String id=old.optString(i);manager(c).cancel(intent(c,id,"fire"));manager(c).cancel(intent(c,id,"advance"));}
        manager(c).cancel(intent(c,REPLENISH,"reconcile"));
        root.put("scheduled",new JSONArray());
    }
    static void reconcile(Context c) throws Exception {
        synchronized(AlarmStore.LOCK){JSONObject root=AlarmStore.read(c);reconcile(c,root);}
    }
    static void reconcile(Context c,JSONObject root) throws Exception {
        cancel(c,root);
        long now=System.currentTimeMillis();
        JSONObject account=AlarmStore.account(root), ledger=account.getJSONObject("ledger"), save=account.optJSONObject("save");
        List<JSONObject> future=new ArrayList<>();
        if(root.optBoolean("active")&&save!=null){
            List<JSONObject> resolved=OccurrenceEngine.resolve(save,now,45);
            JSONObject changes=account.optJSONObject("shiftChanges");
            // A delivery moved away from its qualifying shift date still belongs
            // to that date. Resolve stored edits through the same engine even when
            // the original date lies outside the rolling native queue window.
            if(changes!=null){Set<String> present=new HashSet<>();for(JSONObject o:resolved)present.add(o.optString("id"));
                Iterator<String> changed=changes.keys();while(changed.hasNext()){
                    String id=changed.next();if(present.contains(id))continue;
                    long at=changes.optLong(id);if(at<=now||at>now+45L*86400000)continue;
                    try{String date=id.substring(id.lastIndexOf(':')+1);long anchor=OccurrenceEngine.instant(date,"12:00","device");
                        for(JSONObject o:OccurrenceEngine.resolve(save,anchor,0))if(o.optString("id").equals(id)){resolved.add(o);break;}
                    }catch(IllegalArgumentException invalid){}
                }
            }
            for(JSONObject o:resolved){String id=o.optString("id");if(changes!=null&&id.startsWith("shift:")&&changes.has(id))o.put("at",changes.getLong(id));AlarmPolicy.apply(o,AlarmPolicy.preferences(root));}
            Set<String> valid=new HashSet<>();for(JSONObject o:resolved){valid.add(o.optString("id"));if(o.optLong("at")>now&&!OccurrenceEngine.terminal(ledger,o.optString("id")))future.add(o);}
            JSONObject test=account.optJSONObject("test");if(test!=null&&test.optLong("at")>now&&!OccurrenceEngine.terminal(ledger,test.optString("id")))future.add(test);
            Iterator<String> keys=ledger.keys();
            while(keys.hasNext()){
                String id=keys.next();JSONObject state=ledger.getJSONObject(id);
                if((state.optString("state").equals("snoozed")||state.optString("state").equals("ringing"))&&!valid.contains(id)&&!id.startsWith("test:")){state.put("state","cancelled");NativeBridge.stop(c,id);NativeBridge.remove(c,id);continue;}
                if(state.optString("state").equals("snoozed")&&state.optLong("at")>now){JSONObject o=new JSONObject(state.getJSONObject("occurrence").toString());o.put("at",state.optLong("at"));AlarmPolicy.apply(o,AlarmPolicy.preferences(root));future.removeIf(x->x.optString("id").equals(id));future.add(o);}
            }
        }
        future.sort(Comparator.comparingLong(o->o.optLong("at")));
        // A bounded queue is replenished by every firing and an independent daily wakeup.
        JSONArray scheduled=new JSONArray(); JSONObject occurrences=new JSONObject();
        for(JSONObject o:future){if(scheduled.length()>=128)break;String id=o.optString("id");scheduled.put(id);occurrences.put(id,o);}
        root.put("scheduled",scheduled).put("occurrences",occurrences).put("next",future.isEmpty()?JSONObject.NULL:future.get(0)).put("error","");
        AlarmStore.write(c,root); // Durable state always precedes PendingIntent registration.
        NativeBridge.channels(c);
        if(!root.optBoolean("active")||!exact(c)||!notifications(c)){NativeBridge.removeAllNotifications(c);return;}
        for(int i=0;i<scheduled.length();i++){
            String id=scheduled.getString(i);JSONObject o=occurrences.getJSONObject(id);long at=o.getLong("at");
            if(o.optBoolean("audible"))manager(c).setAlarmClock(new AlarmManager.AlarmClockInfo(at,NativeBridge.open(c,id)),intent(c,id,"fire"));
            else manager(c).setExactAndAllowWhileIdle(AlarmManager.RTC_WAKEUP,at,intent(c,id,"fire"));
            long advance=AlarmPolicy.advanceAt(o,now);
            JSONObject notices=account.optJSONObject("notices");
            if(advance>0&&!ledger.has(id)&&(notices==null||notices.optLong(id)!=at))
                manager(c).setExactAndAllowWhileIdle(AlarmManager.RTC_WAKEUP,advance,intent(c,id,"advance"));
        }
        Set<String> current=new HashSet<>();for(int i=0;i<scheduled.length();i++)current.add(scheduled.getString(i));
        for(StatusBarNotificationWrapper n:NativeBridge.active(c)){
            JSONObject o=occurrences.optJSONObject(n.id);
            if(!n.ringing&&(!current.contains(n.id)||n.upcoming&&(o==null||o.optInt("advanceMinutes")==0)))NativeBridge.remove(c,n.id);
        }
        manager(c).setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP,now+12*3600000L,intent(c,REPLENISH,"reconcile"));
    }
    // Check current ledger state while holding the account lock, so a notice that
    // starts ringing while its UI is open is dismissed before navigation too.
    static void prepareOpen(Context c,String id,String expectedAccount) throws Exception {
        synchronized(AlarmStore.LOCK){
            JSONObject root=AlarmStore.read(c);
            if(!root.optString("account").equals(expectedAccount))throw new IllegalStateException("Calendar account changed.");
            JSONObject entry=AlarmStore.account(root).getJSONObject("ledger").optJSONObject(id);
            if(AlarmPolicy.shouldDismissOnOpen(entry))action(c,"dismiss",id);
            root=AlarmStore.read(c);root.put("pendingOpen",id);AlarmStore.write(c,root);
        }
    }
    static void checkedAction(Context c,String action,String id,String expectedAccount) throws Exception {
        synchronized(AlarmStore.LOCK){
            if(!AlarmStore.read(c).optString("account").equals(expectedAccount))throw new IllegalStateException("Calendar account changed.");
            action(c,action,id);
        }
    }
    static void action(Context c,String action,String id) throws Exception {
        synchronized(AlarmStore.LOCK){
            JSONObject root=AlarmStore.read(c), account=AlarmStore.account(root), ledger=account.getJSONObject("ledger");
            JSONObject o=root.optJSONObject("occurrences")==null?null:root.getJSONObject("occurrences").optJSONObject(id);
            if(o==null){JSONObject entry=ledger.optJSONObject(id);if(entry!=null)o=entry.optJSONObject("occurrence");}
            if(o==null&&id.startsWith("shift:")){
                JSONObject save=account.optJSONObject("save");
                if(save!=null)try{String date=id.substring(id.lastIndexOf(':')+1);
                    for(JSONObject candidate:OccurrenceEngine.resolve(save,OccurrenceEngine.instant(date,"12:00","device"),0,true))
                        if(candidate.optString("id").equals(id)){o=candidate;JSONObject changes=account.optJSONObject("shiftChanges");if(changes!=null&&changes.has(id))o.put("at",changes.getLong(id));break;}
                }catch(IllegalArgumentException invalid){}
            }
            if(action.equals("undo")){
                JSONObject state=ledger.optJSONObject(id);
                if(state!=null&&state.optString("state").equals("skipped")&&state.optLong("originalAt")>System.currentTimeMillis())ledger.remove(id);
            }else if(action.equals("snooze")&&o!=null){
                ledger.put(id,new JSONObject().put("state","snoozed").put("at",System.currentTimeMillis()+o.optInt("snoozeMinutes",10)*60000L).put("occurrence",o));
            }else if(action.equals("skip")||action.equals("dismiss")){
                if(o==null){JSONObject previous=ledger.optJSONObject(id);if(previous!=null)o=previous.optJSONObject("occurrence");}
                ledger.put(id,new JSONObject().put("state",action.equals("skip")?"skipped":"dismissed").put("originalAt",o==null?0:o.optLong("at")).put("occurrence",o));
            }
            AlarmStore.write(c,root);
            NativeBridge.stop(c,id);NativeBridge.remove(c,id);
            reconcile(c,root);
        }
    }
}
