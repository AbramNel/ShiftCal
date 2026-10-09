package com.abramnel.shiftcal;

import android.app.*;
import android.content.*;
import org.json.*;
import org.junit.*;
import org.junit.runner.RunWith;
import org.robolectric.*;
import org.robolectric.annotation.Config;
import org.robolectric.shadows.*;
import static org.junit.Assert.*;
import static org.robolectric.Shadows.shadowOf;

@RunWith(RobolectricTestRunner.class)
@Config(sdk=33,shadows=WindowsAtomicFileShadow.class)
public class NativeAlarmTest {
    Context context;
    JSONObject root,account;
    @Before public void setup() throws Exception{
        context=RuntimeEnvironment.getApplication();context.deleteFile("shiftcal-alarms-v3.json");context.deleteFile("shiftcal-alarms-v3.json.bak");
        root=AlarmStore.read(context).put("active",true).put("account","test-account");account=AlarmStore.account(root);
        JSONObject save=new OccurrenceEngineTest().save().put("version",3).put("account","test-account");account.put("save",save);
        AlarmStore.write(context,root);assertTrue("Store file exists: "+context.getFilesDir(),AlarmStore.file(context).getBaseFile().exists());assertEquals(root.toString(),AlarmStore.read(context).toString());NativeBridge.channels(context);
    }
    JSONObject occurrence(String id,long at) throws Exception{return OccurrenceEngine.make(id,"r","2026-07-01","Wake Up",at,true,new JSONObject().put("snoozeMinutes",7).put("sound","silent").put("vibration",false));}
    @Test public void migrationEvidenceIsAccountScopedAndNeverChangesDelivery() throws Exception{
        account.getJSONObject("save").getJSONArray("rules").put(new JSONObject().put("id","original-wake").put("shiftType",2));
        account.getJSONObject("ledger").put("test:skipped",new JSONObject().put("state","skipped"));
        AlarmStore.write(context,root);
        Activity activity=Robolectric.buildActivity(Activity.class).setup().get();
        String before=AlarmStore.read(context).toString();
        assertEquals("original-wake",new JSONObject(NativeBridge.savedDefinitions(activity,"test-account")).getJSONArray("rules").getJSONObject(0).getString("id"));
        assertEquals("",NativeBridge.savedDefinitions(activity,"second-account"));
        assertEquals(before,AlarmStore.read(context).toString());
        activity.finish();
    }
    @Test public void openRingingDismissesOnlyThatOccurrence() throws Exception{
        JSONObject first=occurrence("test:first",System.currentTimeMillis()),other=occurrence("test:other",System.currentTimeMillis());
        account.getJSONObject("ledger").put("test:first",new JSONObject().put("state","ringing").put("occurrence",first)).put("test:other",new JSONObject().put("state","ringing").put("occurrence",other));
        AlarmStore.write(context,root);assertEquals("Second store write",root.toString(),AlarmStore.read(context).toString());
        AlarmScheduler.prepareOpen(context,"test:first","test-account");
        JSONObject read=AlarmStore.read(context),ledger=AlarmStore.account(read).getJSONObject("ledger");
        assertEquals("dismissed",ledger.getJSONObject("test:first").getString("state"));assertEquals("ringing",ledger.getJSONObject("test:other").getString("state"));assertEquals("test:first",read.getString("pendingOpen"));
    }
    @Test public void openingUpcomingPreservesFutureDeliveryAndAccountIsolation() throws Exception{
        JSONObject o=occurrence("test:next",System.currentTimeMillis()+3600000);account.put("test",o);AlarmStore.write(context,root);AlarmScheduler.reconcile(context);
        AlarmScheduler.prepareOpen(context,"test:next","test-account");JSONObject read=AlarmStore.read(context);
        assertFalse(AlarmStore.account(read).getJSONObject("ledger").has("test:next"));assertTrue(read.getJSONObject("occurrences").has("test:next"));
        try{AlarmScheduler.prepareOpen(context,"test:next","other-account");fail();}catch(IllegalStateException expected){}
    }
    @Test public void dismissAndSnoozeStopServiceButPreserveSimultaneousAlarm() throws Exception{
        JSONObject first=occurrence("test:first",System.currentTimeMillis()),other=occurrence("test:other",System.currentTimeMillis());
        account.getJSONObject("ledger").put("test:first",new JSONObject().put("state","ringing").put("occurrence",first)).put("test:other",new JSONObject().put("state","ringing").put("occurrence",other));AlarmStore.write(context,root);
        var controller=Robolectric.buildService(RingingService.class).create();RingingService service=controller.get();
        service.onStartCommand(new Intent(context,RingingService.class).putExtra("account","test-account").putExtra("occurrence",first.toString()),0,1);
        service.onStartCommand(new Intent(context,RingingService.class).putExtra("account","test-account").putExtra("occurrence",other.toString()),0,2);
        assertEquals(2,service.ringing.size());AlarmScheduler.prepareOpen(context,"test:first","test-account");
        assertEquals(1,service.ringing.size());assertFalse(service.timeouts.containsKey("test:first"));assertTrue(service.ringing.containsKey("test:other"));
        AlarmScheduler.action(context,"snooze","test:other");assertTrue(service.ringing.isEmpty());assertTrue(service.timeouts.isEmpty());assertNull(service.player);
        JSONObject state=AlarmStore.account(AlarmStore.read(context)).getJSONObject("ledger").getJSONObject("test:other");
        assertEquals("snoozed",state.getString("state"));assertTrue(Math.abs(7*60000L-(state.getLong("at")-System.currentTimeMillis()))<1000);controller.destroy();
    }
    @Test public void disablingNoticesRetainsAlarmQueueAndClearsNotice() throws Exception{
        JSONObject e=new OccurrenceEngineTest().event().put("dateKey",java.time.LocalDate.now().plusDays(1).toString()).put("recurrence",0).put("zone","device");
        account.getJSONObject("save").getJSONArray("events").put(e);AlarmStore.write(context,root);AlarmScheduler.reconcile(context);
        JSONObject read=AlarmStore.read(context),queue=read.getJSONObject("occurrences");String id=queue.keys().next();NativeBridge.upcoming(context,queue.getJSONObject(id));
        assertTrue(shadowOf((NotificationManager)context.getSystemService(Context.NOTIFICATION_SERVICE)).getAllNotifications().size()>0);
        read.put("preferences",new JSONObject().put("upcomingNotices",false));AlarmStore.write(context,read);AlarmScheduler.reconcile(context);
        read=AlarmStore.read(context);assertTrue(read.getJSONObject("occurrences").has(id));assertTrue(read.getJSONObject("occurrences").getJSONObject(id).getBoolean("audible"));assertEquals(0,read.getJSONObject("occurrences").getJSONObject(id).getInt("advanceMinutes"));
        assertFalse(AlarmStore.account(read).getJSONObject("ledger").has(id));assertTrue(shadowOf((NotificationManager)context.getSystemService(Context.NOTIFICATION_SERVICE)).getAllNotifications().isEmpty());
    }
    @Test public void nativePickersInitializeAndNeverAcceptOnCancel() throws Exception{
        Activity activity=Robolectric.buildActivity(Activity.class).setup().get();
        assertEquals("",NativeBridge.pickDate(activity,"date-token","2026-10-08"));ShadowLooper.runUiThreadTasksIncludingDelayedTasks();
        DatePickerDialog date=(DatePickerDialog)ShadowDialog.getLatestDialog();assertEquals(2026,date.getDatePicker().getYear());assertEquals(9,date.getDatePicker().getMonth());assertEquals(8,date.getDatePicker().getDayOfMonth());
        assertFalse(NativeBridge.pickTime(activity,"overlap","04:00").isEmpty());date.cancel();shadowOf(android.os.Looper.getMainLooper()).idle();
        assertEquals("",NativeBridge.pickTime(activity,"time-token","04:00"));ShadowLooper.runUiThreadTasksIncludingDelayedTasks();assertTrue(ShadowDialog.getLatestDialog() instanceof TimePickerDialog);ShadowDialog.getLatestDialog().cancel();activity.finish();
    }
    @Test public void movedShiftDeliveryRemainsScheduledAndOverrideStillControlsIt() throws Exception{
        java.time.LocalDate shiftDate=java.time.LocalDate.now().plusDays(90);
        JSONObject save=account.getJSONObject("save");save.getJSONObject("group").put("pattern",new JSONArray("[2]"));
        save.getJSONArray("rules").put(new JSONObject().put("id","move").put("shiftType",2).put("beforeMinutes",90));
        String id="shift:move:"+shiftDate;long at=System.currentTimeMillis()+3600000;
        account.put("shiftChanges",new JSONObject().put(id,at));AlarmStore.write(context,root);AlarmScheduler.reconcile(context);
        assertEquals(at,AlarmStore.read(context).getJSONObject("occurrences").getJSONObject(id).getLong("at"));
        root=AlarmStore.read(context);account=AlarmStore.account(root);account.getJSONObject("save").getJSONArray("overrides").put(new JSONObject().put("dateKey",shiftDate.toString()).put("shiftType",1));AlarmStore.write(context,root);AlarmScheduler.reconcile(context);
        assertFalse(AlarmStore.read(context).getJSONObject("occurrences").has(id));
    }
    @Test public void skipPersistsAcrossOffAndRestoredShift() throws Exception{
        java.time.LocalDate date=java.time.LocalDate.now().plusDays(1);JSONObject save=account.getJSONObject("save");save.getJSONObject("group").put("pattern",new JSONArray("[2]"));
        save.getJSONArray("rules").put(new JSONObject().put("id","wake").put("shiftType",2).put("beforeMinutes",90));String id="shift:wake:"+date;
        AlarmStore.write(context,root);AlarmScheduler.reconcile(context);AlarmScheduler.action(context,"skip",id);
        root=AlarmStore.read(context);account=AlarmStore.account(root);save=account.getJSONObject("save");save.getJSONArray("overrides").put(new JSONObject().put("dateKey",date.toString()).put("shiftType",1));AlarmStore.write(context,root);AlarmScheduler.reconcile(context);
        root=AlarmStore.read(context);account=AlarmStore.account(root);account.getJSONObject("save").getJSONArray("overrides").getJSONObject(0).put("shiftType",2);AlarmStore.write(context,root);AlarmScheduler.reconcile(context);
        root=AlarmStore.read(context);assertFalse(root.getJSONObject("occurrences").has(id));assertEquals("skipped",AlarmStore.account(root).getJSONObject("ledger").getJSONObject(id).getString("state"));
        AlarmScheduler.action(context,"undo",id);assertTrue(AlarmStore.read(context).getJSONObject("occurrences").has(id));
    }
    @Test public void nativeActivityDisplaysRealDataAndOpenDismisses() throws Exception{
        JSONObject o=occurrence("test:screen",System.currentTimeMillis());account.getJSONObject("ledger").put("test:screen",new JSONObject().put("state","ringing").put("occurrence",o));root.put("theme",2);AlarmStore.write(context,root);
        Intent intent=new Intent(context,AlarmActivity.class).putExtra("id","test:screen").putExtra("account","test-account");
        var controller=Robolectric.buildActivity(AlarmActivity.class,intent).setup();AlarmActivity activity=controller.get();
        android.view.ViewGroup content=activity.findViewById(android.R.id.content);
        assertNotNull(findText(content,"Wake Up"));assertNotNull(findText(content,android.text.format.DateFormat.getTimeFormat(activity).format(new java.util.Date(o.getLong("at")))));
        assertEquals(android.graphics.Color.parseColor("#F4F3EF"),activity.getWindow().getStatusBarColor());
        findText(content,"Open Calendar").performClick();assertEquals("dismissed",AlarmStore.account(AlarmStore.read(context)).getJSONObject("ledger").getJSONObject("test:screen").getString("state"));assertTrue(activity.isFinishing());controller.destroy();
    }
    private android.widget.TextView findText(android.view.View view,String text){
        if(view instanceof android.widget.TextView&&((android.widget.TextView)view).getText().toString().equals(text))return (android.widget.TextView)view;
        if(view instanceof android.view.ViewGroup){android.view.ViewGroup group=(android.view.ViewGroup)view;for(int i=0;i<group.getChildCount();i++){android.widget.TextView found=findText(group.getChildAt(i),text);if(found!=null)return found;}}
        return null;
    }
}
