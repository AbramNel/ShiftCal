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

/** Native wake-up controls; calendar navigation still uses normal system unlock. */
public final class AlarmActivity extends Activity {
    private String id,account;
    private int background,surface,text,muted,accent,onAccent;
    private boolean ringing;
    private boolean presentedRinging;
    private final Handler updates=new Handler(Looper.getMainLooper());
    private final Runnable refresh=()->refreshState();
    public void onCreate(Bundle state){super.onCreate(state);
        LockScreenPrivacy.install(this);
        render();
    }
    @Override protected void onResume(){super.onResume();LockScreenPrivacy.apply(this);render();updates.removeCallbacks(refresh);updates.postDelayed(refresh,500);}
    @Override protected void onPause(){updates.removeCallbacks(refresh);setContentView(new FrameLayout(this));super.onPause();}
    @Override protected void onDestroy(){updates.removeCallbacks(refresh);super.onDestroy();}
    @Override protected void onNewIntent(Intent intent){super.onNewIntent(intent);setIntent(intent);presentedRinging=false;LockScreenPrivacy.apply(this);render();}
    boolean canShowOverLockScreen(){return getIntent()!=null&&AlarmPresentation.ringing(this,getIntent().getStringExtra("id"),getIntent().getStringExtra("account"));}
    private void refreshState(){
        if(isFinishing())return;
        if(!currentAccount()||presentedRinging&&!canShowOverLockScreen()){LockScreenPrivacy.apply(this);finish();return;}
        if(!presentedRinging&&canShowOverLockScreen()){LockScreenPrivacy.apply(this);render();}
        updates.postDelayed(refresh,500);
    }
    private void render(){
        id=getIntent().getStringExtra("id");account=getIntent().getStringExtra("account");
        if(id==null||!currentAccount()){finish();return;}
        boolean active=canShowOverLockScreen();
        if(presentedRinging&&!active){LockScreenPrivacy.apply(this);finish();return;}
        if(LockScreenPrivacy.locked(this)&&!active){setContentView(new FrameLayout(this));return;}
        JSONObject occurrence=null;int theme=0;
        try{synchronized(AlarmStore.LOCK){JSONObject root=AlarmStore.read(this),entry=AlarmStore.account(root).getJSONObject("ledger").optJSONObject(id);
            JSONObject queue=root.optJSONObject("occurrences");occurrence=queue==null?null:queue.optJSONObject(id);
            if(entry!=null&&entry.optJSONObject("occurrence")!=null)occurrence=entry.getJSONObject("occurrence");
            ringing=active;presentedRinging|=ringing;theme=root.optInt("theme");
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
        // Keep the ringing surface generic, even if keyguard appears without an Activity pause.
        label(column,ringing?"ShiftCal alarm":occurrence.optString("title"),28,text,0,8,true);
        String subtitle=ringing?"":occurrence.optString("subtitle");if(!subtitle.isEmpty())label(column,subtitle,16,muted,0,26,false);
        else label(column,"ShiftCal",16,muted,0,26,false);
        button(column,ringing?"Dismiss":"Skip this alarm",true,()->perform(ringing?"dismiss":"skip"));
        if(ringing)button(column,"Snooze \u2022 "+occurrence.optInt("snoozeMinutes",10)+" min",false,()->perform("snooze"));
        TextView open=label(column,"Open Calendar",17,accent,20,0,true);open.setPadding(dp(12),dp(16),dp(12),dp(16));open.setOnClickListener(v->openCalendar());
    }
    private void perform(String action){try{
        if(canShowOverLockScreen())AlarmScheduler.checkedRingingAction(this,"skip".equals(action)?"dismiss":action,id,account);
        else if(!LockScreenPrivacy.locked(this)&&!presentedRinging&&"skip".equals(action))AlarmScheduler.checkedAction(this,action,id,account);
        finish();
    }catch(Exception error){Toast.makeText(this,error.getMessage(),Toast.LENGTH_LONG).show();}}
    private void openCalendar(){
        if(LockScreenPrivacy.locked(this)){
            ((KeyguardManager)getSystemService(KEYGUARD_SERVICE)).requestDismissKeyguard(this,new KeyguardManager.KeyguardDismissCallback(){
                @Override public void onDismissSucceeded(){if(!isFinishing()&&!LockScreenPrivacy.locked(AlarmActivity.this))openCalendarUnlocked();}
            });return;
        }
        openCalendarUnlocked();
    }
    private void openCalendarUnlocked(){
        if(LockScreenPrivacy.locked(this))return;
        try{
            AlarmScheduler.prepareOpen(this,id,account);
            Intent launch=getPackageManager().getLaunchIntentForPackage(getPackageName());
            if(launch!=null){launch.putExtra("shiftcalOccurrence",id).addFlags(Intent.FLAG_ACTIVITY_REORDER_TO_FRONT);startActivity(launch);}finish();
        }catch(Exception e){Toast.makeText(this,e.getMessage(),Toast.LENGTH_LONG).show();}
    }
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
