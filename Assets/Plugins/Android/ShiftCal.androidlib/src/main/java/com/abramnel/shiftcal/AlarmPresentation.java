package com.abramnel.shiftcal;

import android.app.*;
import android.content.Context;
import android.os.Build;
import org.json.*;

/** Only a currently ringing delivery may expose controls above the system keyguard. */
final class AlarmPresentation {
    static boolean ringing(Context context,String id,String expectedAccount){
        if(id==null||expectedAccount==null)return false;
        try{synchronized(AlarmStore.LOCK){
            JSONObject root=AlarmStore.read(context);
            if(!root.optBoolean("active")||!root.optString("account").equals(expectedAccount))return false;
            JSONObject entry=AlarmStore.account(root).getJSONObject("ledger").optJSONObject(id);
            return AlarmPolicy.shouldDismissOnOpen(entry)&&entry.optJSONObject("occurrence")!=null
                &&entry.getJSONObject("occurrence").optBoolean("audible",true);
        }}catch(Exception ignored){return false;}
    }
    static boolean fullScreenAllowed(Context context){
        return Build.VERSION.SDK_INT<34||((NotificationManager)context.getSystemService(Context.NOTIFICATION_SERVICE)).canUseFullScreenIntent();
    }
    static boolean highImportance(Context context){
        NotificationChannel channel=((NotificationManager)context.getSystemService(Context.NOTIFICATION_SERVICE)).getNotificationChannel("shiftcal_ring");
        return channel==null||channel.getImportance()>=NotificationManager.IMPORTANCE_HIGH;
    }
}
