package com.abramnel.shiftcal;
import org.junit.Test;
import org.json.*;
import java.time.*;
import java.util.*;
import static org.junit.Assert.*;

public class OccurrenceEngineTest {
    JSONObject event() throws Exception{return new JSONObject().put("id","meeting").put("title","Shutdown meeting").put("dateKey","2026-07-02").put("startTime","1:00 PM").put("zone","America/Chicago").put("recurrence",2).put("interval",1).put("weekdays",new JSONArray("[4]")).put("alarm",true).put("enabled",true);}
    JSONObject save() throws Exception{return new JSONObject().put("group",new JSONObject().put("startDateKey","2026-07-01").put("pattern",new JSONArray("[2,1]")).put("shiftTypes",new JSONArray("[{\"id\":2,\"name\":\"Day-12\",\"startTime\":\"5:30 AM\"},{\"id\":1,\"name\":\"OFF\",\"startTime\":\"\"}]"))).put("events",new JSONArray()).put("rules",new JSONArray()).put("overrides",new JSONArray());}
    @Test public void thursdayMeetingIndependentOfOff() throws Exception{
        JSONObject s=save();s.getJSONArray("events").put(event());List<JSONObject> o=OccurrenceEngine.resolve(s,Instant.parse("2026-07-01T00:00:00Z").toEpochMilli(),14);
        assertTrue(o.stream().anyMatch(x->x.optString("id").equals("event:meeting:2026-07-02:alarm")&&x.optLong("at")==Instant.parse("2026-07-02T18:00:00Z").toEpochMilli()));
        for(JSONObject x:o)assertEquals(DayOfWeek.THURSDAY,Instant.ofEpochMilli(x.optLong("at")).atZone(ZoneId.of("America/Chicago")).getDayOfWeek());
    }
    @Test public void monthlyClampsAndCounts() throws Exception{
        JSONObject e=event().put("dateKey","2026-01-31").put("recurrence",3).put("count",3);
        assertEquals(Arrays.asList(LocalDate.parse("2026-01-31"),LocalDate.parse("2026-02-28"),LocalDate.parse("2026-03-31")),OccurrenceEngine.dates(e,LocalDate.parse("2026-01-01"),LocalDate.parse("2026-12-31")));
    }
    @Test public void weeklyEveryTwoWeeks() throws Exception{
        JSONObject e=event().put("interval",2);assertEquals(Arrays.asList(LocalDate.parse("2026-07-02"),LocalDate.parse("2026-07-16"),LocalDate.parse("2026-07-30")),OccurrenceEngine.dates(e,LocalDate.parse("2026-07-01"),LocalDate.parse("2026-07-31")));
    }
    @Test public void dstGapAndFoldDeterministic(){
        assertEquals(Instant.parse("2026-03-08T08:30:00Z").toEpochMilli(),OccurrenceEngine.instant("2026-03-08","2:30 AM","America/Chicago"));
        assertEquals(Instant.parse("2026-11-01T06:30:00Z").toEpochMilli(),OccurrenceEngine.instant("2026-11-01","1:30 AM","America/Chicago"));
    }
    @Test public void offOverrideCancelsShiftAlarm() throws Exception{
        JSONObject s=save();s.getJSONArray("rules").put(new JSONObject().put("id","wake").put("shiftType",2).put("beforeMinutes",90).put("enabled",true));
        s.getJSONArray("overrides").put(new JSONObject().put("dateKey","2026-07-01").put("shiftType",1));
        assertFalse(OccurrenceEngine.resolve(s,Instant.parse("2026-07-01T00:00:00Z").toEpochMilli(),3).stream().anyMatch(x->x.optString("dateKey").equals("2026-07-01")));
    }
    @Test public void beforeMidnightBelongsToWorkday() throws Exception{
        TimeZone.setDefault(TimeZone.getTimeZone("America/Chicago"));JSONObject s=save();s.getJSONObject("group").getJSONArray("shiftTypes").getJSONObject(0).put("startTime","12:30 AM");
        s.getJSONArray("rules").put(new JSONObject().put("id","wake").put("shiftType",2).put("beforeMinutes",90).put("enabled",true));
        JSONObject o=OccurrenceEngine.resolve(s,Instant.parse("2026-06-30T00:00:00Z").toEpochMilli(),3).stream().filter(x->x.optString("dateKey").equals("2026-07-01")).findFirst().get();
        assertEquals(Instant.parse("2026-07-01T04:00:00Z").toEpochMilli(),o.optLong("at"));assertTrue(o.optString("id").endsWith("2026-07-01"));
    }
    @Test public void exceptionMoveNotDuplicated() throws Exception{
        JSONObject s=save(),e=event();s.getJSONArray("events").put(e);
        s.put("exceptions",new JSONArray().put(new JSONObject().put("seriesId","meeting").put("originalDate","2026-07-02").put("replacement",new JSONObject(e.toString()).put("dateKey","2026-07-03"))));
        List<JSONObject> o=OccurrenceEngine.resolve(s,Instant.parse("2026-07-01T00:00:00Z").toEpochMilli(),3);assertEquals(1,o.size());assertEquals(Instant.parse("2026-07-03T18:00:00Z").toEpochMilli(),o.get(0).optLong("at"));
    }
    @Test public void skipDismissSnoozeAreOccurrenceSpecific() throws Exception{
        JSONObject ledger=new JSONObject().put("today",new JSONObject().put("state","skipped"));assertTrue(OccurrenceEngine.terminal(ledger,"today"));assertFalse(OccurrenceEngine.terminal(ledger,"tomorrow"));
        ledger.put("today",new JSONObject().put("state","dismissed"));assertTrue(OccurrenceEngine.terminal(ledger,"today"));
        ledger.put("today",new JSONObject().put("state","snoozed"));assertFalse(OccurrenceEngine.terminal(ledger,"today"));
    }
    @Test public void stableIdentitiesAndSeparateReminder() throws Exception{
        JSONObject s=save();s.getJSONArray("events").put(event().put("reminder",true).put("reminderMinutes",15));long now=Instant.parse("2026-07-01T00:00:00Z").toEpochMilli();
        List<JSONObject> a=OccurrenceEngine.resolve(s,now,3),b=OccurrenceEngine.resolve(s,now,3);assertEquals(2,a.size());assertEquals(a.toString(),b.toString());assertNotEquals(a.get(0).optString("id"),a.get(1).optString("id"));assertEquals(15*60000,a.get(1).optLong("at")-a.get(0).optLong("at"));
    }
    @Test public void exceptionHasItsOwnReminderPreferences() throws Exception{
        JSONObject s=save(),e=event();s.getJSONArray("events").put(e);
        JSONObject detail=new JSONObject(e.toString()).put("alarm",false).put("reminder",true).put("reminderMinutes",30).put("sound","silent");
        s.put("exceptions",new JSONArray().put(new JSONObject().put("seriesId","meeting").put("originalDate","2026-07-02").put("replacement",detail)));
        List<JSONObject> o=OccurrenceEngine.resolve(s,Instant.parse("2026-07-01T00:00:00Z").toEpochMilli(),3);
        assertEquals(1,o.size());assertFalse(o.get(0).optBoolean("audible"));assertEquals("silent",o.get(0).optString("sound"));assertEquals(Instant.parse("2026-07-02T17:30:00Z").toEpochMilli(),o.get(0).optLong("at"));
    }
}
