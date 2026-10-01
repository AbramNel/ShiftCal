package com.abramnel.shiftcal;

import android.content.Context;
import android.util.AtomicFile;
import org.json.*;
import java.io.*;
import java.nio.charset.StandardCharsets;

final class AlarmStore {
    static final Object LOCK=new Object();
    static JSONObject read(Context c) throws Exception {
        AtomicFile f=file(c);
        if(!f.getBaseFile().exists()&&!new File(f.getBaseFile().getPath()+".bak").exists())return new JSONObject().put("version",3).put("active",false).put("accounts",new JSONObject()).put("scheduled",new JSONArray());
        try(FileInputStream in=f.openRead()){ByteArrayOutputStream bytes=new ByteArrayOutputStream();byte[] buffer=new byte[8192];int n;while((n=in.read(buffer))!=-1)bytes.write(buffer,0,n);return new JSONObject(bytes.toString("UTF-8"));}
    }
    static AtomicFile file(Context c){return new AtomicFile(new File(c.getFilesDir(),"shiftcal-alarms-v3.json"));}
    static void write(Context c,JSONObject root) throws Exception {
        AtomicFile f=file(c);FileOutputStream out=null;
        try {out=f.startWrite();out.write(root.toString().getBytes(StandardCharsets.UTF_8));f.finishWrite(out);}
        catch(Exception e){if(out!=null)f.failWrite(out);throw e;}
    }
    static JSONObject account(JSONObject root) throws JSONException {
        JSONObject all=root.getJSONObject("accounts");String key=root.optString("account","local");
        if(!all.has(key))all.put(key,new JSONObject().put("ledger",new JSONObject()));
        return all.getJSONObject(key);
    }
}
