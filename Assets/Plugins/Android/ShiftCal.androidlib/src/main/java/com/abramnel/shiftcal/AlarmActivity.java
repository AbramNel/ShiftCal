package com.abramnel.shiftcal;

import android.app.*;
import android.os.*;
import android.content.*;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.view.*;
import android.widget.*;
import org.json.*;
import java.util.Date;

/** Entirely native: usable on the lock screen before Unity has initialized. */
public final class AlarmActivity extends Activity {
    private String id,account;
    private int background,surface,text,muted,accent,onAccent;
    private boolean ringing;
    public void onCreate(Bundle state){super.onCreate(state);
        if(Build.VERSION.SDK_INT>=27){setShowWhenLocked(true);setTurnScreenOn(true);}else getWindow().addFlags(WindowManager.LayoutParams.FLAG_SHOW_WHEN_LOCKED|WindowManager.LayoutParams.FLAG_TURN_SCREEN_ON);
        render();
    }
    @Override protected void onNewIntent(Intent intent){super.onNewIntent(intent);setIntent(intent);render();}
    private void render(){
        id=getIntent().getStringExtra("id");account=getIntent().getStringExtra("account");
        if(id==null||!currentAccount()){finish();return;}
        JSONObject occurrence=null;int theme=0;
        try{synchronized(AlarmStore.LOCK){JSONObject root=AlarmStore.read(this),entry=AlarmStore.account(root).getJSONObject("ledger").optJSONObject(id);
            JSONObject queue=root.optJSONObject("occurrences");occurrence=queue==null?null:queue.optJSONObject(id);
            if(entry!=null&&entry.optJSONObject("occurrence")!=null)occurrence=entry.getJSONObject("occurrence");
            ringing=AlarmPolicy.shouldDismissOnOpen(entry);theme=root.optInt("theme");
        }}catch(Exception ignored){}
        if(occurrence==null){finish();return;}
        String[][] palettes={{"#171B22","#232933","#F2F5F9","#ADB9CA","#9BD5FF","#102237"},
            {"#10292E","#193B41","#EDF9F6","#ACCDC9","#A4EBD4","#103C35"},
            {"#F4F3EF","#FFFFFF","#17263C","#57667B","#245EA0","#FFFFFF"}};
        String[] palette=palettes[Math.max(0,Math.min(2,theme))];background=Color.parseColor(palette[0]);surface=Color.parseColor(palette[1]);text=Color.parseColor(palette[2]);muted=Color.parseColor(palette[3]);accent=Color.parseColor(palette[4]);onAccent=Color.parseColor(palette[5]);
        getWindow().setStatusBarColor(background);getWindow().setNavigationBarColor(background);
        getWindow().getDecorView().setSystemUiVisibility(theme==2?View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR|View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR:0);
        ScrollView scroll=new ScrollView(this);scroll.setFillViewport(true);scroll.setBackgroundColor(background);scroll.setClipToPadding(false);
        LinearLayout column=new LinearLayout(this);column.setOrientation(LinearLayout.VERTICAL);column.setGravity(Gravity.CENTER);column.setPadding(dp(28),dp(24),dp(28),dp(24));
        scroll.addView(column,new ScrollView.LayoutParams(-1,-1));setContentView(scroll);
        scroll.setOnApplyWindowInsetsListener((v,insets)->{
            int left,right,top,bottom;
            if(Build.VERSION.SDK_INT>=30){android.graphics.Insets safe=insets.getInsets(WindowInsets.Type.systemBars()|WindowInsets.Type.displayCutout());left=safe.left;right=safe.right;top=safe.top;bottom=safe.bottom;}
            else{left=insets.getSystemWindowInsetLeft();right=insets.getSystemWindowInsetRight();top=insets.getSystemWindowInsetTop();bottom=insets.getSystemWindowInsetBottom();}
            // When decor already fits system windows, content is safe; avoid doubling them.
            if(Build.VERSION.SDK_INT>=35){column.setPadding(dp(28)+left,dp(24)+top,dp(28)+right,dp(24)+bottom);}
            return insets;
        });scroll.requestApplyInsets();
        ImageView icon=new ImageView(this);icon.setImageResource(android.R.drawable.ic_lock_idle_alarm);icon.setColorFilter(accent);column.addView(icon,rowParams(48,48,0,18));
        label(column,ringing?(id.startsWith("shift:")?"SHIFT ALARM":"EVENT ALARM"):"UPCOMING ALARM",14,muted,0,16,true);
        label(column,android.text.format.DateFormat.getTimeFormat(this).format(new Date(occurrence.optLong("at"))),64,text,0,6,true);
        label(column,android.text.format.DateFormat.getMediumDateFormat(this).format(new Date(occurrence.optLong("at"))),16,muted,0,22,false);
        label(column,occurrence.optString("title"),28,text,0,8,true);
        String subtitle=occurrence.optString("subtitle");if(!subtitle.isEmpty())label(column,subtitle,16,muted,0,26,false);
        else label(column,"ShiftCal",16,muted,0,26,false);
        button(column,ringing?"Dismiss":"Skip this alarm",true,()->perform(ringingNow()?"dismiss":"skip"));
        if(ringing)button(column,"Snooze \u2022 "+occurrence.optInt("snoozeMinutes",10)+" min",false,()->perform("snooze"));
        TextView open=label(column,"Open Calendar",17,accent,20,0,true);open.setPadding(dp(12),dp(16),dp(12),dp(16));open.setOnClickListener(v->openCalendar());
    }
    private void perform(String action){try{AlarmScheduler.checkedAction(this,action,id,account);finish();}catch(Exception error){Toast.makeText(this,error.getMessage(),Toast.LENGTH_LONG).show();}}
    private void openCalendar(){
        try{
            AlarmScheduler.prepareOpen(this,id,account);
            Intent launch=getPackageManager().getLaunchIntentForPackage(getPackageName());
            if(launch!=null){launch.putExtra("shiftcalOccurrence",id).addFlags(Intent.FLAG_ACTIVITY_REORDER_TO_FRONT);startActivity(launch);}finish();
        }catch(Exception e){Toast.makeText(this,e.getMessage(),Toast.LENGTH_LONG).show();}
    }
    private boolean ringingNow(){try{synchronized(AlarmStore.LOCK){return AlarmPolicy.shouldDismissOnOpen(AlarmStore.account(AlarmStore.read(this)).getJSONObject("ledger").optJSONObject(id));}}catch(Exception e){return false;}}
    boolean currentAccount(){try{synchronized(AlarmStore.LOCK){return AlarmStore.read(this).optString("account").equals(getIntent().getStringExtra("account"));}}catch(Exception e){return false;}}
    private int dp(int value){return Math.round(value*getResources().getDisplayMetrics().density);}
    private LinearLayout.LayoutParams rowParams(int width,int height,int top,int bottom){LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(width<0?width:dp(width),height<0?height:dp(height));p.setMargins(0,dp(top),0,dp(bottom));p.gravity=Gravity.CENTER;return p;}
    private TextView label(LinearLayout parent,String value,int size,int color,int top,int bottom,boolean bold){TextView v=new TextView(this);v.setText(value);v.setTextSize(size);v.setTextColor(color);v.setGravity(Gravity.CENTER);if(bold)v.setTypeface(Typeface.DEFAULT,Typeface.BOLD);parent.addView(v,rowParams(-1,-2,top,bottom));return v;}
    private void button(LinearLayout parent,String name,boolean primary,Runnable action){
        Button b=new Button(this);b.setText(name);b.setAllCaps(false);b.setTextSize(18);b.setTypeface(Typeface.DEFAULT,Typeface.BOLD);b.setTextColor(primary?onAccent:text);
        GradientDrawable shape=new GradientDrawable();shape.setColor(primary?accent:surface);shape.setCornerRadius(dp(24));b.setBackground(new android.graphics.drawable.RippleDrawable(android.content.res.ColorStateList.valueOf(Color.argb(40,Color.red(text),Color.green(text),Color.blue(text))),shape,null));b.setElevation(primary?dp(3):0);
        parent.addView(b,rowParams(-1,64,8,8));b.setOnClickListener(v->{if(currentAccount())action.run();else finish();});
    }
}
