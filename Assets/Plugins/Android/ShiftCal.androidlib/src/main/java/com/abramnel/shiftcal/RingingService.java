package com.abramnel.shiftcal;
import android.app.*;
import android.content.*;
import android.media.*;
import android.os.*;
import android.net.Uri;
import org.json.*;
import java.util.*;

public final class RingingService extends Service {
    static volatile boolean running;
    static volatile RingingService instance;
    final Map<String,Runnable> timeouts=new HashMap<>();
    String ringingAccount="";
    final Map<String,JSONObject> ringing=new LinkedHashMap<>();
    final Handler handler=new Handler(Looper.getMainLooper());
    MediaPlayer player;Vibrator vibrator;PowerManager.WakeLock wake;AudioManager audio;AudioFocusRequest focus;
    public IBinder onBind(Intent i){return null;}
    public void onCreate(){super.onCreate();running=true;instance=this;wake=((PowerManager)getSystemService(POWER_SERVICE)).newWakeLock(PowerManager.PARTIAL_WAKE_LOCK,"ShiftCal:alarm");wake.acquire(11*60000L);}
    public int onStartCommand(Intent i,int flags,int startId){
        if(i==null){stopSelf();return START_NOT_STICKY;}
        try{
            if("stop".equals(i.getAction())){stopOccurrence(i.getStringExtra("id"));return START_NOT_STICKY;}
            else{
                JSONObject o=new JSONObject(i.getStringExtra("occurrence"));String id=o.getString("id");
                synchronized(AlarmStore.LOCK){JSONObject root=AlarmStore.read(this),entry=AlarmStore.account(root).getJSONObject("ledger").optJSONObject(id);if(!root.optString("account").equals(i.getStringExtra("account"))||!root.optBoolean("active")||entry==null||!entry.optString("state").equals("ringing")){if(ringing.isEmpty())stopSelf();return START_NOT_STICKY;}}
                if(!ringingAccount.equals(i.getStringExtra("account"))){ringing.clear();handler.removeCallbacksAndMessages(null);timeouts.clear();}
                ringingAccount=i.getStringExtra("account");
                if(wake!=null&&!wake.isHeld())wake.acquire(11*60000L);ringing.put(id,o);
                Runnable old=timeouts.remove(id);if(old!=null)handler.removeCallbacks(old);
                final String deliveryAccount=ringingAccount;
                Runnable timeout=()->{try{AlarmScheduler.checkedAction(this,"dismiss",id,deliveryAccount);}catch(Exception ignored){}};timeouts.put(id,timeout);handler.postDelayed(timeout,10*60000L);
            }
            refreshRinging();
        }catch(Exception e){NativeBridge.error(this,e);android.util.Log.e("ShiftCal","Ringing failed",e);stopForeground(STOP_FOREGROUND_REMOVE);stopSelf();}
        return START_NOT_STICKY;
    }
    void stopOccurrence(String id){
        if(Looper.myLooper()!=Looper.getMainLooper()){handler.post(()->stopOccurrence(id));return;}
        ringing.remove(id);Runnable timeout=timeouts.remove(id);if(timeout!=null)handler.removeCallbacks(timeout);NativeBridge.remove(this,id);
        try{refreshRinging();}catch(Exception e){NativeBridge.error(this,e);stopSelf();}
    }
    private void refreshRinging() throws Exception {
            if(ringing.isEmpty()){releaseAudio();stopForeground(STOP_FOREGROUND_REMOVE);stopSelf();return;}
            JSONObject first=ringing.values().iterator().next();
            NativeBridge.remove(this,first.optString("id"));
            startForeground(42,NativeBridge.notification(this,first,true,false));
            for(JSONObject other:ringing.values())if(other!=first)NativeBridge.nm(this).notify(other.optString("id"),1,NativeBridge.notification(this,other,true,false));
            releaseAudio();
            boolean vibrate=false;JSONObject audible=null;for(JSONObject o:ringing.values()){vibrate|=o.optBoolean("vibration");if(!o.optString("sound").equals("silent"))audible=o;}
            if(audible!=null){
                AudioAttributes attrs=new AudioAttributes.Builder().setUsage(AudioAttributes.USAGE_ALARM).setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION).build();
                audio=(AudioManager)getSystemService(AUDIO_SERVICE);focus=new AudioFocusRequest.Builder(AudioManager.AUDIOFOCUS_GAIN_TRANSIENT).setAudioAttributes(attrs).setOnAudioFocusChangeListener(change->{}).build();
                audio.requestAudioFocus(focus);
                Uri sound=RingtoneManager.getDefaultUri(audible.optString("sound").equals("notification")?RingtoneManager.TYPE_NOTIFICATION:RingtoneManager.TYPE_ALARM);
                if(sound==null)sound=RingtoneManager.getDefaultUri(RingtoneManager.TYPE_NOTIFICATION);
                player=new MediaPlayer();player.setAudioAttributes(attrs);player.setDataSource(this,sound);player.setLooping(true);player.prepare();player.start();
            }
            if(vibrate){vibrator=(Vibrator)getSystemService(VIBRATOR_SERVICE);vibrator.vibrate(VibrationEffect.createWaveform(new long[]{0,500,700},0));}
    }
    void releaseAudio(){if(player!=null){try{player.stop();}catch(Exception ignored){}player.release();player=null;}if(vibrator!=null)vibrator.cancel();if(audio!=null&&focus!=null)audio.abandonAudioFocusRequest(focus);focus=null;}
    public void onDestroy(){handler.removeCallbacksAndMessages(null);releaseAudio();for(String id:ringing.keySet())NativeBridge.remove(this,id);if(wake!=null&&wake.isHeld())wake.release();running=false;instance=null;super.onDestroy();}
}
