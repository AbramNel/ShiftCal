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
            List<JSONObject> resolved=OccurrenceEngine.resolve(save,now,45);Set<String> valid=new HashSet<>();for(JSONObject o:resolved){valid.add(o.optString("id"));if(o.optLong("at")>now&&!OccurrenceEngine.terminal(ledger,o.optString("id")))future.add(o);}
            JSONObject test=account.optJSONObject("test");if(test!=null&&test.optLong("at")>now&&!OccurrenceEngine.terminal(ledger,test.optString("id")))future.add(test);
            Iterator<String> keys=ledger.keys();
            while(keys.hasNext()){
                String id=keys.next();JSONObject state=ledger.getJSONObject(id);
                if((state.optString("state").equals("snoozed")||state.optString("state").equals("ringing"))&&!valid.contains(id)&&!id.startsWith("test:")){state.put("state","cancelled");NativeBridge.stop(c,id);NativeBridge.remove(c,id);continue;}
                if(state.optString("state").equals("snoozed")&&state.optLong("at")>now){JSONObject o=new JSONObject(state.getJSONObject("occurrence").toString());o.put("at",state.optLong("at"));future.removeIf(x->x.optString("id").equals(id));future.add(o);}
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
            long advance=at-o.optInt("advanceMinutes")*60000L;
            if(o.optInt("advanceMinutes")>0&&!ledger.has(id)){
                if(advance>now)manager(c).setExactAndAllowWhileIdle(AlarmManager.RTC_WAKEUP,advance,intent(c,id,"advance"));
                else NativeBridge.upcoming(c,o);
            }
        }
        Set<String> current=new HashSet<>();for(int i=0;i<scheduled.length();i++)current.add(scheduled.getString(i));
        for(StatusBarNotificationWrapper n:NativeBridge.active(c))if(!current.contains(n.id)&&!n.ringing)NativeBridge.remove(c,n.id);
        manager(c).setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP,now+12*3600000L,intent(c,REPLENISH,"reconcile"));
    }
    static void action(Context c,String action,String id) throws Exception {
        synchronized(AlarmStore.LOCK){
            JSONObject root=AlarmStore.read(c), account=AlarmStore.account(root), ledger=account.getJSONObject("ledger");
            JSONObject o=root.optJSONObject("occurrences")==null?null:root.getJSONObject("occurrences").optJSONObject(id);
            if(o==null){JSONObject entry=ledger.optJSONObject(id);if(entry!=null)o=entry.optJSONObject("occurrence");}
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
