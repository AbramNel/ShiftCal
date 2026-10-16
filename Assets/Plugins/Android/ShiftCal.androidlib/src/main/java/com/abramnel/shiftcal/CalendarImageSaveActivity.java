package com.abramnel.shiftcal;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.provider.DocumentsContract;
import java.io.File;

// Android 8/9: the system grants access to the user's chosen destination, without storage permission.
public final class CalendarImageSaveActivity extends Activity {
    static final int CREATE_IMAGE=723;
    String token,path;
    @Override protected void onCreate(Bundle state){
        super.onCreate(state);token=getIntent().getStringExtra("token");path=getIntent().getStringExtra("path");
        if(state!=null)return;
        try{CalendarImageBridge.checkedFile(this,path);
            startActivityForResult(new Intent(Intent.ACTION_CREATE_DOCUMENT).addCategory(Intent.CATEGORY_OPENABLE)
                .setType("image/png").putExtra(Intent.EXTRA_TITLE,CalendarImageBridge.name(getIntent().getStringExtra("name"))),CREATE_IMAGE);
        }catch(Exception ex){CalendarImageBridge.result(token,"error","Couldn't open a location to save the image.");finish();}
    }
    @Override protected void onActivityResult(int request,int result,Intent data){
        super.onActivityResult(request,result,data);if(request!=CREATE_IMAGE)return;
        if(result!=RESULT_OK||data==null||data.getData()==null){CalendarImageBridge.result(token,"cancelled","Save canceled.");finish();return;}
        Uri uri=data.getData();CalendarImageBridge.IO.execute(()->{
            try{File file=CalendarImageBridge.checkedFile(this,path);CalendarImageBridge.copy(file,getContentResolver(),uri);CalendarImageBridge.result(token,"saved","Calendar image saved to your chosen location.");}
            catch(Exception ex){try{DocumentsContract.deleteDocument(getContentResolver(),uri);}catch(Exception ignored){}CalendarImageBridge.result(token,"error","Couldn't save the image. Please try another location.");}
            runOnUiThread(this::finish);
        });
    }
}
