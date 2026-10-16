package com.abramnel.shiftcal;

import android.app.*;
import android.content.*;
import android.content.pm.*;
import android.database.Cursor;
import android.graphics.Bitmap;
import android.net.Uri;
import android.os.*;
import android.provider.MediaStore;
import androidx.core.content.FileProvider;
import com.unity3d.player.UnityPlayer;
import org.json.JSONObject;
import org.junit.*;
import org.junit.runner.RunWith;
import org.robolectric.*;
import org.robolectric.annotation.Config;
import org.robolectric.annotation.Implements;
import org.robolectric.annotation.Implementation;
import org.robolectric.shadows.ShadowContentResolver;
import java.io.*;
import java.nio.file.Files;
import java.util.*;
import static org.junit.Assert.*;
import static org.robolectric.Shadows.shadowOf;

@RunWith(RobolectricTestRunner.class)
@Config(sdk={26,29,35},shadows={WindowsAtomicFileShadow.class,CalendarImageTest.WindowsFileProvider.class})
public class CalendarImageTest {
    Context context;Activity activity;File image;MemoryMedia provider;
    @Before public void setup() throws Exception {
        context=RuntimeEnvironment.getApplication();activity=Robolectric.buildActivity(Activity.class).setup().get();
        File directory=CalendarImageBridge.folder(context);File[] old=directory.listFiles();if(old!=null)for(File file:old)file.delete();
        image=new File(directory,"ShiftCal-November-2026-test.png");Bitmap bitmap=Bitmap.createBitmap(32,40,Bitmap.Config.ARGB_8888);bitmap.eraseColor(0xff82b2ff);
        try(OutputStream out=new FileOutputStream(image)){bitmap.compress(Bitmap.CompressFormat.PNG,100,out);}bitmap.recycle();
        provider=new MemoryMedia();ProviderInfo mediaInfo=new ProviderInfo();mediaInfo.authority="media";provider.attachInfo(context,mediaInfo);ShadowContentResolver.registerProviderInternal("media",provider);
        context.getSharedPreferences("calendar-image-saves",0).edit().clear().commit();UnityPlayer.method=UnityPlayer.value=null;
    }
    @After public void teardown(){activity.finish();}
    @Test public void streamHasPngMimeContentUriClipAndReadGrant() throws Exception {
        File file=CalendarImageBridge.checkedFile(context,image.getPath());Intent intent=CalendarImageBridge.sendIntent(context,file,"ShiftCal-November-2026.png");
        assertEquals(Intent.ACTION_SEND,intent.getAction());assertEquals("image/png",intent.getType());Uri uri=intent.getParcelableExtra(Intent.EXTRA_STREAM);
        assertEquals("content",uri.getScheme());assertEquals(context.getPackageName()+".shiftcal.files",uri.getAuthority());assertEquals(uri,intent.getClipData().getItemAt(0).getUri());
        assertTrue(intent.getClipData().getDescription().hasMimeType("image/png"));
        assertEquals(Intent.FLAG_GRANT_READ_URI_PERMISSION,intent.getFlags()&Intent.FLAG_GRANT_READ_URI_PERMISSION);
        shadowOf(context.getContentResolver()).registerInputStream(uri,new FileInputStream(file));
        try(InputStream input=context.getContentResolver().openInputStream(uri)){assertArrayEquals(Files.readAllBytes(file.toPath()),input.readAllBytes());}
    }
    @Test public void chooserIsGenericAndNeverReportsMessageSent() throws Exception {
        Intent send=CalendarImageBridge.sendIntent(context,image,"ShiftCal-November-2026.png");ResolveInfo info=new ResolveInfo();info.activityInfo=new ActivityInfo();info.activityInfo.packageName="compatible.images";info.activityInfo.name="ImageReceiver";
        shadowOf(context.getPackageManager()).addResolveInfoForIntent(send,info);
        assertEquals("",CalendarImageBridge.share(activity,image.getPath(),"ShiftCal-November-2026.png","share-test"));
        Intent chooser=shadowOf(activity).getNextStartedActivity();assertNotNull(chooser);assertEquals(Intent.ACTION_CHOOSER,chooser.getAction());assertEquals("Share ShiftCal Calendar",chooser.getStringExtra(Intent.EXTRA_TITLE));
        Intent attached=chooser.getParcelableExtra(Intent.EXTRA_INTENT);assertEquals("image/png",attached.getType());assertNull(attached.getPackage());
        assertEquals("ready",new JSONObject(UnityPlayer.value).getString("status"));assertFalse(UnityPlayer.value.contains("sent"));
    }
    @Test public void noShareTargetsAndUnavailableActivityAreHandled() throws Exception {
        assertEquals("",CalendarImageBridge.share(activity,image.getPath(),"ShiftCal-November-2026.png","none"));assertNull(shadowOf(activity).getNextStartedActivity());
        assertEquals("error",new JSONObject(UnityPlayer.value).getString("status"));activity.finish();assertFalse(CalendarImageBridge.share(activity,image.getPath(),"calendar.png","closed").isEmpty());
    }
    @Test public void rejectsTraversalNonPngAndUnrelatedPrivateFiles() throws Exception {
        File outside=new File(context.getFilesDir(),"private.png");Files.copy(image.toPath(),outside.toPath(),java.nio.file.StandardCopyOption.REPLACE_EXISTING);
        try{CalendarImageBridge.checkedFile(context,outside.getPath());fail();}catch(IOException expected){}
        File bad=new File(CalendarImageBridge.folder(context),"ShiftCal-invalid.png");Files.write(bad.toPath(),"Private alarm JSON is not an image".getBytes(java.nio.charset.StandardCharsets.UTF_8));
        try{CalendarImageBridge.checkedFile(context,bad.getPath());fail();}catch(IOException expected){}
        assertEquals("ShiftCal-Calendar.png",CalendarImageBridge.name("../private.json"));
    }
    @Test public void cacheRetainsRecentAndCurrentSharesButExpiresOldFiles() throws Exception {
        long now=System.currentTimeMillis();
        for(int i=0;i<38;i++){File file=new File(CalendarImageBridge.folder(context),"ShiftCal-old-"+i+".png");Files.copy(image.toPath(),file.toPath());assertTrue(file.setLastModified(now-(i+2L)*3600000));}
        File expired=new File(CalendarImageBridge.folder(context),"ShiftCal-expired.png");Files.copy(image.toPath(),expired.toPath());expired.setLastModified(now-49L*3600000);
        CalendarImageBridge.prune(context,image);assertTrue(image.exists());assertFalse(expired.exists());assertEquals(32,CalendarImageBridge.folder(context).listFiles().length);
    }
    @Test @Config(sdk={29,35}) public void mediaStorePublishesOnlyAfterCompleteCopyAndDeduplicates() throws Exception {
        if(Build.VERSION.SDK_INT<29)throw new AssertionError("MediaStore test requires API 29+");
        Uri first=CalendarImageBridge.saveMedia(context,image,"ShiftCal-November-2026.png");assertEquals(1,provider.inserts);assertEquals(1,provider.initial.getAsInteger(MediaStore.MediaColumns.IS_PENDING).intValue());
        assertEquals("Pictures/ShiftCal",provider.initial.getAsString(MediaStore.MediaColumns.RELATIVE_PATH));assertEquals("image/png",provider.initial.getAsString(MediaStore.MediaColumns.MIME_TYPE));assertEquals(0,provider.published.getAsInteger(MediaStore.MediaColumns.IS_PENDING).intValue());
        assertArrayEquals(Files.readAllBytes(image.toPath()),Files.readAllBytes(provider.file.toPath()));assertEquals(first,CalendarImageBridge.saveMedia(context,image,"ShiftCal-November-2026.png"));assertEquals(1,provider.inserts);
        Files.write(provider.file.toPath(),new byte[]{1,2,3});assertNotEquals(first,CalendarImageBridge.saveMedia(context,image,"ShiftCal-November-2026.png"));assertEquals(2,provider.inserts);assertArrayEquals(Files.readAllBytes(image.toPath()),Files.readAllBytes(provider.file.toPath()));
    }
    @Test @Config(sdk={29,35}) public void failedSaveRemovesPendingMediaAndReportsNoSuccess() throws Exception {
        if(Build.VERSION.SDK_INT<29)throw new AssertionError("MediaStore test requires API 29+");
        provider.failWrite=true;
        try{CalendarImageBridge.saveMedia(context,image,"ShiftCal-November-2026.png");fail();}catch(IOException expected){}
        assertEquals(1,provider.deletes);assertNull(provider.published);
        provider.failWrite=false;provider.failPublish=true;
        try{CalendarImageBridge.saveMedia(context,image,"ShiftCal-November-2026.png");fail();}catch(IOException expected){}
        assertEquals(2,provider.deletes);
    }
    @Test public void olderAndroidUsesCreateDocumentAndCancelIsNotSuccess() throws Exception {
        Intent intent=new Intent(context,CalendarImageSaveActivity.class).putExtra("path",image.getPath()).putExtra("name","ShiftCal-November-2026.png").putExtra("token","saf-test");
        var controller=Robolectric.buildActivity(CalendarImageSaveActivity.class,intent).setup();var save=controller.get();
        var started=shadowOf(save).getNextStartedActivityForResult();assertEquals(Intent.ACTION_CREATE_DOCUMENT,started.intent.getAction());assertEquals("image/png",started.intent.getType());assertTrue(started.intent.hasCategory(Intent.CATEGORY_OPENABLE));assertEquals("ShiftCal-November-2026.png",started.intent.getStringExtra(Intent.EXTRA_TITLE));
        save.onActivityResult(CalendarImageSaveActivity.CREATE_IMAGE,Activity.RESULT_CANCELED,null);assertTrue(save.isFinishing());assertEquals("cancelled",new JSONObject(UnityPlayer.value).getString("status"));controller.pause().stop().destroy();
    }
    @Test public void imageOperationsNeverTouchNativeAlarmLedger() throws Exception {
        String before=AlarmStore.read(context).toString();CalendarImageBridge.checkedFile(context,image.getPath());CalendarImageBridge.sendIntent(context,image,"ShiftCal-November-2026.png");CalendarImageBridge.prune(context,image);
        assertEquals(before,AlarmStore.read(context).toString());
    }
    @Test public void documentCopyRequiresContentUriAndWritesExactPng() throws Exception {
        Uri destination=provider.insert(MediaStore.Images.Media.EXTERNAL_CONTENT_URI,new ContentValues());CalendarImageBridge.copy(image,context.getContentResolver(),destination);
        assertArrayEquals(Files.readAllBytes(image.toPath()),Files.readAllBytes(provider.file.toPath()));
        try{CalendarImageBridge.copy(image,context.getContentResolver(),Uri.fromFile(provider.file));fail();}catch(IOException expected){}
    }
    // AndroidX's real strategy appends '/' to paths; Windows JVM File uses '\\'.
    // Keep URI/intent tests portable, while the packaged manifest and phone acceptance
    // verify the actual Android FileProvider configuration.
    @Implements(FileProvider.class)
    public static class WindowsFileProvider {
        @Implementation public static Uri getUriForFile(Context context,String authority,File file) {
            try{if(!CalendarImageBridge.folder(context).getCanonicalFile().equals(file.getCanonicalFile().getParentFile()))throw new IllegalArgumentException("Outside calendar image root");}
            catch(IOException ex){throw new IllegalArgumentException(ex);}
            return new Uri.Builder().scheme("content").authority(authority).appendPath("calendar-images").appendPath(file.getName()).build();
        }
    }
    public static class MemoryMedia extends ContentProvider {
        File file;int inserts,deletes;ContentValues initial,published;boolean failWrite,failPublish;
        public boolean onCreate(){return true;}
        public Uri insert(Uri uri,ContentValues values){try{file=File.createTempFile("saved-calendar-",".png",getContext().getCacheDir());initial=new ContentValues(values);inserts++;return Uri.parse("content://media/external/images/media/"+inserts);}catch(Exception e){throw new RuntimeException(e);}}
        public ParcelFileDescriptor openFile(Uri uri,String mode) throws FileNotFoundException {if(failWrite)throw new FileNotFoundException("No storage");return ParcelFileDescriptor.open(file,mode.contains("w")?ParcelFileDescriptor.MODE_WRITE_ONLY|ParcelFileDescriptor.MODE_TRUNCATE:ParcelFileDescriptor.MODE_READ_ONLY);}
        public int update(Uri uri,ContentValues values,String where,String[] args){if(failPublish)return 0;published=new ContentValues(values);return 1;}
        public int delete(Uri uri,String where,String[] args){deletes++;file.delete();return 1;}
        public Cursor query(Uri uri,String[] columns,String where,String[] args,String sort){return null;}
        public String getType(Uri uri){return "image/png";}
    }
}
