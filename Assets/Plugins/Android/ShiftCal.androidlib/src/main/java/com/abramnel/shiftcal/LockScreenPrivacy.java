package com.abramnel.shiftcal;

import android.app.*;
import android.content.Context;
import android.os.*;
import android.view.WindowManager;

/** The ringing alarm may show controls; the calendar still requires normal unlock. */
final class LockScreenPrivacy implements Application.ActivityLifecycleCallbacks {
    private static Application installedFor;
    static boolean locked(Context context) {
        KeyguardManager keyguard=(KeyguardManager)context.getSystemService(Context.KEYGUARD_SERVICE);
        return keyguard!=null&&(keyguard.isKeyguardLocked()||keyguard.isDeviceLocked());
    }
    static synchronized void install(Activity activity) {
        if(installedFor!=activity.getApplication()){activity.getApplication().registerActivityLifecycleCallbacks(new LockScreenPrivacy());installedFor=activity.getApplication();}
        apply(activity);
    }
    static void apply(Activity activity) {
        boolean ringing=activity instanceof AlarmActivity&&((AlarmActivity)activity).canShowOverLockScreen();
        if(Build.VERSION.SDK_INT>=27){activity.setShowWhenLocked(ringing);activity.setTurnScreenOn(ringing);}
        int presentation=WindowManager.LayoutParams.FLAG_SHOW_WHEN_LOCKED|WindowManager.LayoutParams.FLAG_TURN_SCREEN_ON;
        activity.getWindow().clearFlags(WindowManager.LayoutParams.FLAG_DISMISS_KEYGUARD);
        if(ringing){
            activity.getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
            if(Build.VERSION.SDK_INT<27)activity.getWindow().addFlags(presentation);
        }else{
            activity.getWindow().clearFlags(presentation);
            if(activity instanceof AlarmActivity)activity.getWindow().clearFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        }
    }
    public void onActivityCreated(Activity a,Bundle state){apply(a);}
    public void onActivityStarted(Activity a){apply(a);}
    public void onActivityResumed(Activity a){apply(a);}
    public void onActivityPaused(Activity a){apply(a);}
    public void onActivityStopped(Activity a){apply(a);}
    public void onActivitySaveInstanceState(Activity a,Bundle state){}
    public void onActivityDestroyed(Activity a){}
}
