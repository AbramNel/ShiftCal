package com.abramnel.shiftcal;

import org.json.*;
import java.time.*;
import java.time.format.*;
import java.util.*;

/** Version 3 ScheduleSave contract. Delivery ledger is intentionally device-only. */
public final class OccurrenceEngine {
    static JSONArray array(JSONObject o, String name) { JSONArray a=o.optJSONArray(name); return a==null?new JSONArray():a; }
    static LocalTime time(String s) {
        String n=s.trim().toUpperCase(Locale.US).replace(".", "").replaceAll("\\s+", " ");
        for(String f:new String[]{"H:mm","h:mm a","h:mma","h a","ha"})
            try { return LocalTime.parse(n, DateTimeFormatter.ofPattern(f,Locale.US)); } catch(Exception ignored) {}
        throw new IllegalArgumentException("Invalid start time: "+s);
    }
    static ZoneId zone(String s) { return s==null||s.isEmpty()||s.equals("device")?ZoneId.systemDefault():ZoneId.of(s); }
    static long instant(String date, String clock, String zone) { return LocalDate.parse(date).atTime(time(clock)).atZone(zone(zone)).toInstant().toEpochMilli(); }
    static boolean matches(JSONObject e, LocalDate d) {
        LocalDate start=LocalDate.parse(e.optString("dateKey"));
        long days=java.time.temporal.ChronoUnit.DAYS.between(start,d); if(days<0)return false;
        int n=Math.max(1,e.optInt("interval",1)), kind=e.optInt("recurrence");
        if(kind==0)return days==0;
        if(kind==1)return days%n==0;
        if(kind==2){
            if(((days+start.getDayOfWeek().getValue()%7)/7)%n!=0)return false;
            JSONArray w=array(e,"weekdays"); for(int i=0;i<w.length();i++) if(w.optInt(i)==d.getDayOfWeek().getValue()%7)return true;
            return false;
        }
        int months=(d.getYear()-start.getYear())*12+d.getMonthValue()-start.getMonthValue();
        return months%n==0&&d.getDayOfMonth()==Math.min(start.getDayOfMonth(),d.lengthOfMonth());
    }
    static List<LocalDate> dates(JSONObject e, LocalDate from, LocalDate to) {
        LocalDate start=LocalDate.parse(e.optString("dateKey")), end=to;
        String until=e.optString("until"); if(!until.isEmpty()&&LocalDate.parse(until).isBefore(end))end=LocalDate.parse(until);
        int ordinal=0,max=e.optInt("count"); List<LocalDate> result=new ArrayList<>();
        for(LocalDate d=start;!d.isAfter(end);d=d.plusDays(1)) if(matches(e,d)){
            if(max>0&&++ordinal>max)break;
            if(!d.isBefore(from))result.add(d);
            if(e.optInt("recurrence")==0)break;
        }
        return result;
    }
    static JSONObject make(String id,String source,String date,String title,long at,boolean audible,JSONObject options) throws JSONException {
        return new JSONObject().put("id",id).put("sourceId",source).put("dateKey",date).put("title",title).put("at",at)
            .put("audible",audible).put("vibration",options.optBoolean("vibration",true)).put("sound",options.optString("sound","alarm"))
            .put("useDefaultAlarmSettings",options.optBoolean("useDefaultAlarmSettings")).put("notes",options.optString("notes"))
            .put("snoozeMinutes",Math.max(1,options.optInt("snoozeMinutes",10))).put("advanceMinutes",audible?options.optInt("advanceMinutes"):0);
    }
    static void event(List<JSONObject> list,JSONObject series,JSONObject detail,String original) throws JSONException {
        if(!detail.optBoolean("enabled",true))return;
        String id=series.optString("id"), date=detail==series?original:detail.optString("dateKey");
        long at=instant(date,detail.optString("startTime"),detail.optString("zone","device"));
        if(detail.optBoolean("reminder")&&(!detail.optBoolean("alarm")||detail.optInt("reminderMinutes",15)>0))list.add(make("event:"+id+":"+original+":reminder",id,original,detail.optString("title")+" - reminder",at-detail.optInt("reminderMinutes",15)*60000L,false,detail).put("isEvent",true).put("calendarDateKey",date));
        if(detail.optBoolean("alarm"))list.add(make("event:"+id+":"+original+":alarm",id,original,detail.optString("title"),at,true,detail).put("isEvent",true).put("calendarDateKey",date));
    }
    public static List<JSONObject> resolve(JSONObject save,long now,int horizon) throws JSONException {
        return resolve(save,now,horizon,false);
    }
    static List<JSONObject> resolve(JSONObject save,long now,int horizon,boolean includeCalendarOnly) throws JSONException {
        LocalDate from=Instant.ofEpochMilli(now).atZone(ZoneId.systemDefault()).toLocalDate().minusDays(2), to=from.plusDays(horizon+3);
        List<JSONObject> list=new ArrayList<>(); JSONArray events=array(save,"events"), exceptions=array(save,"exceptions"), muted=array(save,"mutedEvents");
        for(int i=0;i<events.length();i++){
            JSONObject e=events.getJSONObject(i); String id=e.optString("id"); boolean mute=false;
            for(int j=0;j<muted.length();j++)if(muted.optString(j).equals(id))mute=true;
            if(mute||!e.optBoolean("enabled",true))continue;
            for(LocalDate d:dates(e,from,to)){
                boolean except=false;
                for(int j=0;j<exceptions.length();j++){
                    JSONObject x=exceptions.getJSONObject(j);
                    if(x.optString("seriesId").equals(id)&&x.optString("originalDate").equals(d.toString())){except=true;break;}
                }
                if(!except)event(list,e,e,d.toString());
            }
            for(int j=0;j<exceptions.length();j++){
                JSONObject x=exceptions.getJSONObject(j), replacement=x.optJSONObject("replacement");
                if(x.optString("seriesId").equals(id)&&!x.optBoolean("cancelled")&&replacement!=null){
                    LocalDate d=LocalDate.parse(replacement.optString("dateKey"));
                    if(!d.isBefore(from)&&!d.isAfter(to))event(list,e,replacement,x.optString("originalDate"));
                }
            }
        }
        JSONObject group=save.getJSONObject("group"); JSONArray pattern=array(group,"pattern"), shifts=array(group,"shiftTypes"), overrides=array(save,"overrides"), rules=array(save,"rules");
        LocalDate anchor=LocalDate.parse(group.optString("startDateKey"));
        for(LocalDate d=from;!d.isAfter(to);d=d.plusDays(1)){
            String key=d.toString(); int type=pattern.length()==0?0:pattern.optInt(Math.floorMod((int)java.time.temporal.ChronoUnit.DAYS.between(anchor,d),pattern.length()));
            for(int i=0;i<overrides.length();i++){JSONObject o=overrides.getJSONObject(i);if(o.optString("dateKey").equals(key)&&!o.optBoolean("scheduledShift"))type=o.optInt("shiftType");}
            JSONObject shift=null; for(int i=0;i<shifts.length();i++)if(shifts.getJSONObject(i).optInt("id")==type)shift=shifts.getJSONObject(i);
            if(shift==null)continue;
            for(int i=0;i<rules.length();i++){
                JSONObject r=rules.getJSONObject(i);boolean mutedRule=false;JSONArray muteRules=array(save,"mutedRules");for(int j=0;j<muteRules.length();j++)if(muteRules.optString(j).equals(r.optString("id")))mutedRule=true;
                if(mutedRule)continue;if(!r.optString("groupId").isEmpty()&&!r.optString("groupId").equals(group.optString("groupId")))continue;if(!r.optBoolean("enabled",true)||r.optInt("shiftType")!=type)continue;
                if(r.optBoolean("calendarOnly")&&!includeCalendarOnly||!r.optString("fromDate").isEmpty()&&key.compareTo(r.optString("fromDate"))<0)continue;
                String clock=r.optInt("timingMode")==1?r.optString("fixedTime"):shift.optString("startTime");
                long at;try{at=instant(key,clock,"device")-(r.optInt("timingMode")==0?r.optInt("beforeMinutes",90)*60000L:0);}catch(IllegalArgumentException invalid){continue;}
                list.add(make("shift:"+r.optString("id")+":"+key,r.optString("id"),key,r.optString("label"),at,r.optBoolean("audible",true),r)
                    .put("calendarDateKey",key).put("subtitle", "Repeats on "+shift.optString("name")+" shifts").put("calendarOnly",r.optBoolean("calendarOnly")));
            }
        }
        list.sort(Comparator.comparingLong(o->o.optLong("at"))); return list;
    }
    public static boolean terminal(JSONObject ledger,String id) {
        JSONObject entry=ledger.optJSONObject(id); if(entry==null)return false;
        return !entry.optString("state").equals("snoozed");
    }
}
