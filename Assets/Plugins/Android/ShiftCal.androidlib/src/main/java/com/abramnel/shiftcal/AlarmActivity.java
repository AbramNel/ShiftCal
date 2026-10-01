package com.abramnel.shiftcal;
import android.app.*;
import android.os.*;
import android.content.*;
import android.graphics.Color;
import android.widget.*;
import org.json.*;

public final class AlarmActivity extends Activity {
    public void onCreate(Bundle state){super.onCreate(state);
        if(Build.VERSION.SDK_INT>=27){setShowWhenLocked(true);setTurnScreenOn(true);}else getWindow().addFlags(android.view.WindowManager.LayoutParams.FLAG_SHOW_WHEN_LOCKED|android.view.WindowManager.LayoutParams.FLAG_TURN_SCREEN_ON);
        String id=getIntent().getStringExtra("id");
        if(!currentAccount()){finish();return;}
        LinearLayout column=new LinearLayout(this);column.setOrientation(LinearLayout.VERTICAL);column.setPadding(32,64,32,32);column.setBackgroundColor(Color.rgb(20,23,28));setContentView(column);
        TextView title=new TextView(this);title.setTextSize(28);title.setTextColor(Color.WHITE);column.addView(title);
        boolean ringing=false;
        try{synchronized(AlarmStore.LOCK){JSONObject root=AlarmStore.read(this),ledger=AlarmStore.account(root).getJSONObject("ledger"),entry=ledger.optJSONObject(id);
            JSONObject o=root.getJSONObject("occurrences").optJSONObject(id);if(o==null&&entry!=null)o=entry.optJSONObject("occurrence");
            title.setText(o==null?"ShiftCal":o.optString("title"));ringing=entry!=null&&entry.optString("state").equals("ringing");
        }}catch(Exception e){title.setText("ShiftCal");}
        if(ringing){button(column,"Dismiss",()->{NativeBridge.action(this,"dismiss",id);finish();});button(column,"Snooze",()->{NativeBridge.action(this,"snooze",id);finish();});}
        else button(column,"Dismiss this alarm",()->{NativeBridge.action(this,"skip",id);finish();});
        button(column,"Open calendar",()->{try{synchronized(AlarmStore.LOCK){JSONObject root=AlarmStore.read(this);root.put("pendingOpen",id);AlarmStore.write(this,root);}}catch(Exception ignored){}Intent launch=getPackageManager().getLaunchIntentForPackage(getPackageName());if(launch!=null){launch.putExtra("shiftcalOccurrence",id);startActivity(launch);}finish();});
    }
    boolean currentAccount(){try{synchronized(AlarmStore.LOCK){return AlarmStore.read(this).optString("account").equals(getIntent().getStringExtra("account"));}}catch(Exception e){return false;}}
    void button(LinearLayout parent,String name,Runnable action){Button b=new Button(this);b.setText(name);parent.addView(b,new LinearLayout.LayoutParams(-1,(int)(64*getResources().getDisplayMetrics().density)));b.setOnClickListener(v->{if(currentAccount())action.run();else finish();});}
}
