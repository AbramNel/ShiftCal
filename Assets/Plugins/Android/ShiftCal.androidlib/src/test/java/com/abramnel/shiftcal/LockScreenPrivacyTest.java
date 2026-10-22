package com.abramnel.shiftcal;

import android.app.*;
import android.content.*;
import android.media.RingtoneManager;
import android.os.*;
import android.provider.Settings;
import android.view.*;
import android.widget.TextView;
import org.json.*;
import org.junit.*;
import org.junit.runner.RunWith;
import org.robolectric.*;
import org.robolectric.annotation.*;
import org.robolectric.shadows.*;
import org.robolectric.shadows.util.DataSource;
import java.time.Duration;
import static org.junit.Assert.*;
import static org.robolectric.Shadows.shadowOf;

@RunWith(RobolectricTestRunner.class)
@Config(sdk={26,33,35},shadows={WindowsAtomicFileShadow.class,LockScreenPrivacyTest.FullScreenNotificationShadow.class})
public class LockScreenPrivacyTest {
    Context context;JSONObject root,account;
    @Implements(NotificationManager.class)
    public static class FullScreenNotificationShadow extends ShadowNotificationManager {
        static boolean allowed=true;
        @Implementation(minSdk=34) protected boolean canUseFullScreenIntent(){return allowed;}
    }
    void locked(boolean value){var keyguard=shadowOf((KeyguardManager)context.getSystemService(Context.KEYGUARD_SERVICE));keyguard.setKeyguardLocked(value);keyguard.setIsDeviceLocked(value);keyguard.setIsKeyguardSecure(true);}
    @Before public void setup() throws Exception {
        context=RuntimeEnvironment.getApplication();context.deleteFile("shiftcal-alarms-v3.json");context.deleteFile("shiftcal-alarms-v3.json.bak");locked(false);FullScreenNotificationShadow.allowed=true;
        root=AlarmStore.read(context).put("active",true).put("account","privacy-test");account=AlarmStore.account(root);account.put("save",new OccurrenceEngineTest().save().put("account","privacy-test").put("version",3));AlarmStore.write(context,root);NativeBridge.channels(context);
        if(Build.VERSION.SDK_INT>=31)shadowOf((AlarmManager)context.getSystemService(Context.ALARM_SERVICE)).setCanScheduleExactAlarms(true);
    }
    JSONObject ringing(String id) throws Exception {
        JSONObject o=OccurrenceEngine.make(id,"r","2026-10-22","Private family appointment",System.currentTimeMillis(),true,new JSONObject().put("sound","silent").put("vibration",false).put("notes","Private notes"));
        account.getJSONObject("ledger").put(id,new JSONObject().put("state","ringing").put("occurrence",o));AlarmStore.write(context,root);return o;
    }
    Intent launch(String id){return new Intent(context,AlarmActivity.class).putExtra("id",id).putExtra("account","privacy-test");}
    void visibleOverLock(AlarmActivity activity,boolean expected){
        assertEquals(0,activity.getWindow().getAttributes().flags&WindowManager.LayoutParams.FLAG_DISMISS_KEYGUARD);
        if(Build.VERSION.SDK_INT>=27){assertEquals(expected,shadowOf(activity).getShowWhenLocked());assertEquals(expected,shadowOf(activity).getTurnScreenOn());}
        else{int flags=WindowManager.LayoutParams.FLAG_SHOW_WHEN_LOCKED|WindowManager.LayoutParams.FLAG_TURN_SCREEN_ON;assertEquals(expected?flags:0,activity.getWindow().getAttributes().flags&flags);}
    }
    @Test public void onlyRingingNotificationsHaveFullScreenIntentAndUnlockedActions() throws Exception {
        JSONObject o=ringing("test:privacy");
        for(int kind=0;kind<3;kind++){
            Notification n=NativeBridge.notification(context,o,kind==0,kind==1);
            assertEquals(kind==0,n.fullScreenIntent!=null);assertEquals(kind==0?Notification.VISIBILITY_PUBLIC:Notification.VISIBILITY_SECRET,n.visibility);
            assertFalse(n.extras.toString().contains("Private family appointment"));assertFalse(n.extras.toString().contains("Private notes"));assertNotNull(n.contentIntent);
            assertEquals(AlarmActivity.class.getName(),shadowOf(n.contentIntent).getSavedIntent().getComponent().getClassName());
            if(kind==0){Intent intent=shadowOf(n.fullScreenIntent).getSavedIntent();assertEquals("privacy-test",intent.getStringExtra("account"));assertEquals("test:privacy",intent.getStringExtra("id"));assertTrue((intent.getFlags()&Intent.FLAG_ACTIVITY_NEW_TASK)!=0);assertTrue(shadowOf(n.fullScreenIntent).isImmutable());}
            if(Build.VERSION.SDK_INT>=31)for(Notification.Action action:n.actions)assertEquals(kind!=0,action.isAuthenticationRequired());
        }
    }
    @Test public void lockedAlarmScreenShowsControlsAndDismissesWithoutUnlock() throws Exception {
        ringing("test:screen");ringing("test:other");locked(true);
        var controller=Robolectric.buildActivity(AlarmActivity.class,launch("test:screen")).setup();var activity=controller.get();
        assertTrue(activity.canShowOverLockScreen());visibleOverLock(activity,true);assertFalse(text(activity).contains("Private family appointment"));assertNotNull(find(activity,"Dismiss"));assertTrue(text(activity).contains("Snooze"));
        find(activity,"Dismiss").performClick();assertTrue(activity.isFinishing());
        JSONObject ledger=AlarmStore.account(AlarmStore.read(context)).getJSONObject("ledger");assertEquals("dismissed",ledger.getJSONObject("test:screen").getString("state"));assertEquals("ringing",ledger.getJSONObject("test:other").getString("state"));assertTrue(LockScreenPrivacy.locked(context));controller.pause().stop().destroy();
    }
    @Test public void lockedAlarmScreenSnoozesOnlyTheSelectedOccurrence() throws Exception {
        JSONObject o=ringing("test:snooze");ringing("test:other");locked(true);
        var controller=Robolectric.buildActivity(AlarmActivity.class,launch("test:snooze")).setup();var activity=controller.get();
        find(activity,"Snooze \u2022 "+o.optInt("snoozeMinutes",10)+" min").performClick();
        JSONObject ledger=AlarmStore.account(AlarmStore.read(context)).getJSONObject("ledger");assertEquals("snoozed",ledger.getJSONObject("test:snooze").getString("state"));assertTrue(ledger.getJSONObject("test:snooze").getLong("at")>System.currentTimeMillis());assertEquals("ringing",ledger.getJSONObject("test:other").getString("state"));assertTrue(LockScreenPrivacy.locked(context));controller.pause().stop().destroy();
    }
    @Test public void ordinaryActivitiesStillCannotShowOverOrDismissKeyguard(){
        var controller=Robolectric.buildActivity(Activity.class).setup();Activity activity=controller.get();
        int takeover=WindowManager.LayoutParams.FLAG_SHOW_WHEN_LOCKED|WindowManager.LayoutParams.FLAG_TURN_SCREEN_ON|WindowManager.LayoutParams.FLAG_DISMISS_KEYGUARD;
        activity.getWindow().addFlags(takeover);LockScreenPrivacy.install(activity);assertEquals(0,activity.getWindow().getAttributes().flags&takeover);
        if(Build.VERSION.SDK_INT>=27){assertFalse(shadowOf(activity).getShowWhenLocked());assertFalse(shadowOf(activity).getTurnScreenOn());}
        activity.getWindow().addFlags(takeover);controller.pause().resume();assertEquals(0,activity.getWindow().getAttributes().flags&takeover);controller.pause().stop().destroy();
    }
    @Test public void lockedNotificationActionsControlRingingButRejectStaleSnooze() throws Exception {
        ringing("test:first");ringing("test:second");locked(true);
        Intent dismiss=new Intent(context,AlarmReceiver.class).setAction("dismiss").putExtra("id","test:first").putExtra("account","privacy-test");new AlarmReceiver().onReceive(context,dismiss);
        JSONObject ledger=AlarmStore.account(AlarmStore.read(context)).getJSONObject("ledger");assertEquals("dismissed",ledger.getJSONObject("test:first").getString("state"));assertEquals("ringing",ledger.getJSONObject("test:second").getString("state"));
        new AlarmReceiver().onReceive(context,new Intent(dismiss).setAction("snooze"));assertEquals("dismissed",AlarmStore.account(AlarmStore.read(context)).getJSONObject("ledger").getJSONObject("test:first").getString("state"));
        new AlarmReceiver().onReceive(context,new Intent(dismiss).putExtra("id","test:second").setAction("snooze"));assertEquals("snoozed",AlarmStore.account(AlarmStore.read(context)).getJSONObject("ledger").getJSONObject("test:second").getString("state"));
        new AlarmReceiver().onReceive(context,new Intent(dismiss).putExtra("id","test:second"));assertEquals("snoozed",AlarmStore.account(AlarmStore.read(context)).getJSONObject("ledger").getJSONObject("test:second").getString("state"));
    }
    @Test public void futureOrOtherAccountDeliveriesNeverGetLockScreenControls() throws Exception {
        JSONObject o=ringing("test:scope");account.getJSONObject("ledger").getJSONObject("test:scope").put("state","snoozed");AlarmStore.write(context,root);locked(true);
        var controller=Robolectric.buildActivity(AlarmActivity.class,launch("test:scope")).setup();var activity=controller.get();visibleOverLock(activity,false);assertNull(find(activity,"Dismiss"));assertNull(NativeBridge.notification(context,o,true,false).fullScreenIntent);controller.pause().stop().destroy();
        root=AlarmStore.read(context);account=AlarmStore.account(root);ringing("test:scope");var wrong=Robolectric.buildActivity(AlarmActivity.class,launch("test:scope").putExtra("account","someone-else")).setup();assertTrue(wrong.get().isFinishing());visibleOverLock(wrong.get(),false);wrong.pause().stop().destroy();
        assertFalse(AlarmScheduler.checkedRingingAction(context,"dismiss","test:scope","someone-else"));assertEquals("ringing",AlarmStore.account(AlarmStore.read(context)).getJSONObject("ledger").getJSONObject("test:scope").getString("state"));
    }
    @Test public void externallyDismissedAlarmClosesItsScreenAndRemovesWakeFlags() throws Exception {
        ringing("test:external");locked(true);var controller=Robolectric.buildActivity(AlarmActivity.class,launch("test:external")).setup();var activity=controller.get();
        AlarmScheduler.checkedRingingAction(context,"dismiss","test:external","privacy-test");shadowOf(Looper.getMainLooper()).idleFor(Duration.ofSeconds(1));assertTrue(activity.isFinishing());visibleOverLock(activity,false);assertEquals(0,activity.getWindow().getAttributes().flags&WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);controller.pause().stop().destroy();
    }
    @Test public void calendarOpenRequiresSystemUnlockBeforeDismissingAlarm() throws Exception {
        ringing("test:open");locked(true);var controller=Robolectric.buildActivity(AlarmActivity.class,launch("test:open")).setup();var activity=controller.get();
        find(activity,"Open Calendar").performClick();assertFalse(AlarmStore.read(context).has("pendingOpen"));assertEquals("ringing",AlarmStore.account(AlarmStore.read(context)).getJSONObject("ledger").getJSONObject("test:open").getString("state"));
        locked(false);find(activity,"Open Calendar").performClick();assertEquals("test:open",AlarmStore.read(context).getString("pendingOpen"));assertEquals("dismissed",AlarmStore.account(AlarmStore.read(context)).getJSONObject("ledger").getJSONObject("test:open").getString("state"));controller.pause().stop().destroy();
    }
    @Test public void ringingServicePublishesFullScreenAlarmAndKeepsAudioWhileLocked() throws Exception {
        JSONObject o=ringing("test:audio").put("sound","alarm").put("vibration",true);account.getJSONObject("ledger").getJSONObject("test:audio").put("occurrence",o);AlarmStore.write(context,root);locked(true);
        ShadowMediaPlayer.addMediaInfo(DataSource.toDataSource(context,RingtoneManager.getDefaultUri(RingtoneManager.TYPE_ALARM)),new ShadowMediaPlayer.MediaInfo(10000,0));
        var controller=Robolectric.buildService(RingingService.class).create();RingingService service=controller.get();service.onStartCommand(new Intent(context,RingingService.class).putExtra("account","privacy-test").putExtra("occurrence",o.toString()),0,1);
        assertTrue(service.ringing.containsKey("test:audio"));assertNotNull(service.player);assertTrue(service.player.isPlaying());assertTrue(shadowOf(service.vibrator).isVibrating());assertTrue(service.wake.isHeld());
        Notification n=shadowOf((NotificationManager)context.getSystemService(Context.NOTIFICATION_SERVICE)).getNotification(42);assertNotNull(n);assertNotNull(n.fullScreenIntent);controller.destroy();
    }
    @Test @Config(sdk={34,35}) public void deniedFullScreenPermissionStillAllowsRingingAndShowsSettingsGuidance() throws Exception {
        FullScreenNotificationShadow.allowed=false;JSONObject o=ringing("test:permission");assertNull(NativeBridge.notification(context,o,true,false).fullScreenIntent);
        Activity activity=Robolectric.buildActivity(Activity.class).setup().get();assertTrue(NativeBridge.permissionSummary(activity),NativeBridge.permissionSummary(activity).contains("Full-screen alarms off"));assertTrue(NativeBridge.status(activity).contains("Full-screen alarms: off"));
        NativeBridge.action(activity,"fullscreen","");Intent page=shadowOf(activity).getNextStartedActivity();assertEquals(Settings.ACTION_MANAGE_APP_USE_FULL_SCREEN_INTENT,page.getAction());assertEquals("package:"+activity.getPackageName(),page.getDataString());
        root=AlarmStore.read(context);account=AlarmStore.account(root);account.put("test",OccurrenceEngine.make("test:future","test","","Alarm",System.currentTimeMillis()+60000,true,new JSONObject()));AlarmStore.write(context,root);shadowOf((AlarmManager)context.getSystemService(Context.ALARM_SERVICE)).setCanScheduleExactAlarms(true);AlarmScheduler.reconcile(context);assertTrue(AlarmStore.read(context).getJSONObject("occurrences").has("test:future"));activity.finish();
    }
    @Test public void alarmChannelSettingsUseTheExistingAudibleChannel() throws Exception {
        Activity activity=Robolectric.buildActivity(Activity.class).setup().get();NativeBridge.action(activity,"alarmChannel","");Intent page=shadowOf(activity).getNextStartedActivity();assertEquals(Settings.ACTION_CHANNEL_NOTIFICATION_SETTINGS,page.getAction());assertEquals("shiftcal_ring",page.getStringExtra(Settings.EXTRA_CHANNEL_ID));
        NotificationManager manager=(NotificationManager)context.getSystemService(Context.NOTIFICATION_SERVICE);NotificationChannel low=manager.getNotificationChannel("shiftcal_ring");low.setImportance(NotificationManager.IMPORTANCE_LOW);manager.createNotificationChannel(low);assertFalse(AlarmPresentation.highImportance(context));assertTrue(NativeBridge.permissionSummary(activity).contains("Alarm pop-ups off"));activity.finish();
    }
    @Test public void pendingCalendarOpenWaitsForNormalUnlock() throws Exception {
        JSONObject o=ringing("test:pending");root.put("pendingOpen","test:pending");AlarmStore.write(context,root);Activity a=Robolectric.buildActivity(Activity.class).setup().get();locked(true);assertEquals("",NativeBridge.opened(a));locked(false);assertEquals(o.getString("id"),new JSONObject(NativeBridge.opened(a)).getString("id"));a.finish();
    }
    static TextView find(Activity activity,String label){return find(activity.findViewById(android.R.id.content),label);}
    static TextView find(View view,String label){if(view instanceof TextView&&((TextView)view).getText().toString().equals(label))return (TextView)view;if(view instanceof ViewGroup)for(int i=0;i<((ViewGroup)view).getChildCount();i++){TextView found=find(((ViewGroup)view).getChildAt(i),label);if(found!=null)return found;}return null;}
    static String text(Activity activity){return text(activity.findViewById(android.R.id.content));}
    static String text(View view){String result=view instanceof TextView?((TextView)view).getText().toString():"";if(view instanceof ViewGroup)for(int i=0;i<((ViewGroup)view).getChildCount();i++)result+=text(((ViewGroup)view).getChildAt(i));return result;}
}
