package com.abramnel.shiftcal;
import android.content.*;
public final class RestoreReceiver extends BroadcastReceiver {
    public void onReceive(Context c,Intent i){
        String a=i.getAction();
        if(!Intent.ACTION_BOOT_COMPLETED.equals(a)&&!Intent.ACTION_MY_PACKAGE_REPLACED.equals(a)&&!Intent.ACTION_TIME_CHANGED.equals(a)&&!Intent.ACTION_TIMEZONE_CHANGED.equals(a)&&!"android.app.action.SCHEDULE_EXACT_ALARM_PERMISSION_STATE_CHANGED".equals(a))return;
        // Boot only reschedules. It never launches a media playback foreground service.
        if(Intent.ACTION_TIMEZONE_CHANGED.equals(a))java.util.TimeZone.setDefault(null);
        try{AlarmScheduler.reconcile(c);}catch(Exception e){NativeBridge.error(c,e);android.util.Log.e("ShiftCal","Reschedule failed",e);}
    }
}
