package com.abramnel.shiftcal;
import android.content.*;
import org.json.*;

public final class AlarmReceiver extends BroadcastReceiver {
    public void onReceive(Context c,Intent i){
        try{
            String action=i.getAction(),id=i.getStringExtra("id");
            synchronized(AlarmStore.LOCK){if(!AlarmStore.read(c).optString("account").equals(i.getStringExtra("account")))return;}
            if("reconcile".equals(action)){AlarmScheduler.reconcile(c);return;}
            if("skip".equals(action)||"dismiss".equals(action)||"snooze".equals(action)){AlarmScheduler.action(c,action,id);return;}
            synchronized(AlarmStore.LOCK){
                JSONObject root=AlarmStore.read(c);if(!root.optBoolean("active"))return;
                JSONObject ledger=AlarmStore.account(root).getJSONObject("ledger"),occurrences=root.optJSONObject("occurrences");
                JSONObject o=occurrences==null?null:occurrences.optJSONObject(id);if(o==null||OccurrenceEngine.terminal(ledger,id))return;
                if(!AlarmScheduler.notifications(c)||!NativeBridge.channel(c,o.optBoolean("audible")?"shiftcal_ring":"shiftcal_reminder"))return;
                // Old PendingIntents cannot ring an occurrence whose trigger changed.
                if("fire".equals(action)&&(o.optLong("at")>System.currentTimeMillis()+1000||System.currentTimeMillis()-o.optLong("at")>10*60000L)){AlarmScheduler.reconcile(c,root);return;}
                if("advance".equals(action)){
                    if(!o.optBoolean("audible")||o.optInt("advanceMinutes")==0||o.optLong("at")<=System.currentTimeMillis()||ledger.has(id))return;
                    long trigger=o.optLong("at")-o.optInt("advanceMinutes")*60000L;
                    if(trigger>System.currentTimeMillis()+1000)return;
                    JSONObject account=AlarmStore.account(root),notices=account.optJSONObject("notices");
                    if(notices==null){notices=new JSONObject();account.put("notices",notices);}
                    if(notices.optLong(id)==o.optLong("at"))return;
                    notices.put(id,o.optLong("at"));AlarmStore.write(c,root);NativeBridge.upcoming(c,o);return;
                }
                ledger.put(id,new JSONObject().put("state",o.optBoolean("audible")?"ringing":"delivered").put("occurrence",o));
                AlarmStore.write(c,root);NativeBridge.remove(c,id);
                if(o.optBoolean("audible"))c.startForegroundService(new Intent(c,RingingService.class).setAction("ring").putExtra("occurrence",o.toString()).putExtra("account",root.optString("account")));
                else NativeBridge.nm(c).notify(id,1,NativeBridge.notification(c,o,false,false));
                AlarmScheduler.reconcile(c,root);
            }
        }catch(Exception e){NativeBridge.error(c,e);android.util.Log.e("ShiftCal","Alarm delivery failed",e);}
    }
}
