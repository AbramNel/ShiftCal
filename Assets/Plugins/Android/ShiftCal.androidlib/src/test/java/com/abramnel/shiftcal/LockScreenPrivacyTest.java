package com.abramnel.shiftcal;

import android.app.*;
import android.content.*;
import android.media.RingtoneManager;
import android.os.*;
import android.view.*;
import org.json.*;
import org.junit.*;
import org.junit.runner.RunWith;
import org.robolectric.*;
import org.robolectric.annotation.Config;
import org.robolectric.shadows.*;
import org.robolectric.shadows.util.DataSource;
import static org.junit.Assert.*;
import static org.robolectric.Shadows.shadowOf;

@RunWith(RobolectricTestRunner.class)
@Config(sdk={26,33,35},shadows=WindowsAtomicFileShadow.class)
public class LockScreenPrivacyTest {
    Context context;JSONObject root,account;
    void locked(boolean value){var keyguard=shadowOf((KeyguardManager)context.getSystemService(Context.KEYGUARD_SERVICE));keyguard.setKeyguardLocked(value);keyguard.setIsDeviceLocked(value);keyguard.setIsKeyguardSecure(true);}
    @Before public void setup() throws Exception {
        context=RuntimeEnvironment.getApplication();context.deleteFile("shiftcal-alarms-v3.json");context.deleteFile("shiftcal-alarms-v3.json.bak");locked(false);
        root=AlarmStore.read(context).put("active",true).put("account","privacy-test");account=AlarmStore.account(root);account.put("save",new OccurrenceEngineTest().save().put("account","privacy-test").put("version",3));AlarmStore.write(context,root);NativeBridge.channels(context);
    }
    JSONObject ringing(String id) throws Exception {
        JSONObject o=OccurrenceEngine.make(id,"r","2026-10-15","Private family appointment",System.currentTimeMillis(),true,new JSONObject().put("sound","silent").put("vibration",false).put("notes","Private notes"));
        account.getJSONObject("ledger").put(id,new JSONObject().put("state","ringing").put("occurrence",o));AlarmStore.write(context,root);return o;
    }
    @Test public void everyNotificationIsGenericAndHasNoFullScreenLaunch() throws Exception {
        JSONObject o=ringing("test:privacy");
        for(int kind=0;kind<3;kind++){
            Notification n=NativeBridge.notification(context,o,kind==0,kind==1);assertNull(n.fullScreenIntent);assertEquals(Notification.VISIBILITY_SECRET,n.visibility);
            assertFalse(n.extras.toString().contains("Private family appointment"));assertFalse(n.extras.toString().contains("Private notes"));assertNotNull(n.contentIntent);
            assertEquals(AlarmActivity.class.getName(),shadowOf(n.contentIntent).getSavedIntent().getComponent().getClassName());
            if(Build.VERSION.SDK_INT>=31)for(Notification.Action action:n.actions)assertTrue(action.isAuthenticationRequired());
        }
        Activity a=Robolectric.buildActivity(Activity.class).setup().get();assertFalse(NativeBridge.permissionSummary(a).contains("Full-screen"));a.finish();
    }
    @Test public void activityRemainsRedactedUntilUnlockAndRedactsAgainOnPause() throws Exception {
        ringing("test:screen");locked(true);
        Intent intent=new Intent(context,AlarmActivity.class).putExtra("id","test:screen").putExtra("account","privacy-test");var controller=Robolectric.buildActivity(AlarmActivity.class,intent).setup();var activity=controller.get();
        assertFalse(text(activity).contains("Private family appointment"));assertFalse(activity.isFinishing());assertEquals("ringing",AlarmStore.account(AlarmStore.read(context)).getJSONObject("ledger").getJSONObject("test:screen").getString("state"));
        int takeover=WindowManager.LayoutParams.FLAG_SHOW_WHEN_LOCKED|WindowManager.LayoutParams.FLAG_TURN_SCREEN_ON|WindowManager.LayoutParams.FLAG_DISMISS_KEYGUARD;
        assertEquals(0,activity.getWindow().getAttributes().flags&takeover);
        controller.pause();locked(false);controller.resume();assertTrue(text(activity).contains("Private family appointment"));controller.pause();assertFalse(text(activity).contains("Private family appointment"));controller.stop().destroy();
    }
    @Test public void lifecycleGuardClearsTakeoverFlagsForOrdinaryActivities() {
        var controller=Robolectric.buildActivity(Activity.class).setup();Activity activity=controller.get();
        int takeover=WindowManager.LayoutParams.FLAG_SHOW_WHEN_LOCKED|WindowManager.LayoutParams.FLAG_TURN_SCREEN_ON|WindowManager.LayoutParams.FLAG_DISMISS_KEYGUARD;
        activity.getWindow().addFlags(takeover);LockScreenPrivacy.install(activity);assertEquals(0,activity.getWindow().getAttributes().flags&takeover);
        activity.getWindow().addFlags(takeover);controller.pause().resume();assertEquals(0,activity.getWindow().getAttributes().flags&takeover);controller.pause().stop().destroy();
    }
    @Test public void lockedNotificationActionDoesNotDismissAndUnlockAllowsIt() throws Exception {
        ringing("test:first");JSONObject second=ringing("test:second");locked(true);
        Intent dismiss=new Intent(context,AlarmReceiver.class).setAction("dismiss").putExtra("id","test:first").putExtra("account","privacy-test");new AlarmReceiver().onReceive(context,dismiss);
        JSONObject ledger=AlarmStore.account(AlarmStore.read(context)).getJSONObject("ledger");assertEquals("ringing",ledger.getJSONObject("test:first").getString("state"));assertEquals("ringing",ledger.getJSONObject("test:second").getString("state"));
        locked(false);new AlarmReceiver().onReceive(context,dismiss);ledger=AlarmStore.account(AlarmStore.read(context)).getJSONObject("ledger");assertEquals("dismissed",ledger.getJSONObject("test:first").getString("state"));
        // A separately ringing delivery is not dismissed by the selected action.
        assertEquals("ringing",ledger.getJSONObject("test:second").getString("state"));assertEquals("test:second",second.getString("id"));
    }
    @Test public void audibleServiceAndVibrationContinueWhileLockedWithoutLaunchingUi() throws Exception {
        JSONObject o=ringing("test:audio").put("sound","alarm").put("vibration",true);account.getJSONObject("ledger").getJSONObject("test:audio").put("occurrence",o);AlarmStore.write(context,root);locked(true);
        ShadowMediaPlayer.addMediaInfo(DataSource.toDataSource(context,RingtoneManager.getDefaultUri(RingtoneManager.TYPE_ALARM)),new ShadowMediaPlayer.MediaInfo(10000,0));
        var controller=Robolectric.buildService(RingingService.class).create();RingingService service=controller.get();service.onStartCommand(new Intent(context,RingingService.class).putExtra("account","privacy-test").putExtra("occurrence",o.toString()),0,1);
        assertTrue(service.ringing.containsKey("test:audio"));assertNotNull(service.player);assertTrue(service.player.isPlaying());assertTrue(shadowOf(service.vibrator).isVibrating());assertTrue(service.wake.isHeld());assertNull(shadowOf((Application)context).getNextStartedActivity());
        Notification n=shadowOf((NotificationManager)context.getSystemService(Context.NOTIFICATION_SERVICE)).getNotification(42);assertNotNull(n);assertNull(n.fullScreenIntent);controller.destroy();
    }
    @Test public void pendingCalendarOpenWaitsForNormalUnlock() throws Exception {
        JSONObject o=ringing("test:open");root.put("pendingOpen","test:open");AlarmStore.write(context,root);Activity a=Robolectric.buildActivity(Activity.class).setup().get();locked(true);
        assertEquals("",NativeBridge.opened(a));assertEquals("test:open",AlarmStore.read(context).getString("pendingOpen"));locked(false);assertEquals(o.getString("id"),new JSONObject(NativeBridge.opened(a)).getString("id"));a.finish();
    }
    String text(Activity a){return text(a.findViewById(android.R.id.content));}
    String text(View v){String result=v instanceof android.widget.TextView?((android.widget.TextView)v).getText().toString():"";if(v instanceof ViewGroup)for(int i=0;i<((ViewGroup)v).getChildCount();i++)result+=text(((ViewGroup)v).getChildAt(i));return result;}
}
