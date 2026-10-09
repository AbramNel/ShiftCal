package com.abramnel.shiftcal;

import org.json.*;

/** Device defaults never mutate the shared ScheduleSave definitions. */
final class AlarmPolicy {
    static JSONObject preferences(JSONObject root) { JSONObject p=root.optJSONObject("preferences");return p==null?new JSONObject():p; }
    static void apply(JSONObject o,JSONObject p) throws JSONException {
        if(o.optBoolean("useDefaultAlarmSettings"))
            o.put("sound",p.optString("sound","alarm")).put("vibration",p.optBoolean("vibration",true))
                .put("snoozeMinutes",Math.max(1,Math.min(120,p.optInt("snoozeMinutes",10))));
        o.put("advanceMinutes",o.optBoolean("audible")&&p.optBoolean("upcomingNotices",true)
            ?Math.max(1,Math.min(10080,p.optInt("noticeMinutes",15))):0);
    }
    static long advanceAt(JSONObject o,long now) {
        long at=o.optLong("at"),advance=at-o.optInt("advanceMinutes")*60000L;
        // Notices have their own future trigger. Short test alarms and newly added
        // alarms inside the notice window must not post a misleading immediate notice.
        return o.optBoolean("audible")&&!o.optString("id").startsWith("test:")&&o.optInt("advanceMinutes")>0&&advance>now?advance:0;
    }
    static boolean shouldDismissOnOpen(JSONObject entry) { return entry!=null&&entry.optString("state").equals("ringing"); }
}
