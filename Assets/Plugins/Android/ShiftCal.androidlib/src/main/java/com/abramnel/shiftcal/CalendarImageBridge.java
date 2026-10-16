package com.abramnel.shiftcal;

import android.app.Activity;
import android.content.*;
import android.graphics.BitmapFactory;
import android.net.Uri;
import android.os.Build;
import android.provider.MediaStore;
import androidx.core.content.FileProvider;
import org.json.JSONObject;
import java.io.*;
import java.security.MessageDigest;
import java.util.*;
import java.util.concurrent.Executors;

// Image operations have no dependency on AlarmStore, Firebase or private calendar records.
public final class CalendarImageBridge {
    static final String FOLDER="shiftcal-calendar-images";
    static final java.util.concurrent.Executor IO=Executors.newSingleThreadExecutor();
    static File folder(Context context) throws IOException {
        File folder=new File(context.getCacheDir(),FOLDER);
        if(!folder.isDirectory()&&!folder.mkdirs())throw new IOException("Image cache unavailable");
        return folder;
    }
    static void prune(Context context,File keep) throws IOException {
        File[] files=folder(context).listFiles();if(files==null)return;
        Arrays.sort(files,Comparator.comparingLong(File::lastModified).reversed());long now=System.currentTimeMillis();int retained=0;
        for(File file:files){if(!file.isFile()||!file.getName().matches("ShiftCal-[A-Za-z0-9-]+\\.png(\\.tmp)?"))continue;
            if(file.equals(keep)){retained++;continue;}
            long age=now-file.lastModified();
            // Never expire a receiving app's recent grant during a burst of exports.
            if(age>48L*60*60*1000||(retained>=32&&age>60L*60*1000)){Uri uri=FileProvider.getUriForFile(context,context.getPackageName()+".shiftcal.files",file);context.revokeUriPermission(uri,Intent.FLAG_GRANT_READ_URI_PERMISSION);file.delete();}
            else retained++;
        }
    }
    static File checkedFile(Context context,String path) throws IOException {
        if(path==null)throw new IOException("Calendar image unavailable");
        File file=new File(path).getCanonicalFile();
        if(!folder(context).getCanonicalFile().equals(file.getParentFile())||!file.getName().endsWith(".png")||!file.isFile()||file.length()<8||file.length()>20*1024*1024)
            throw new IOException("Calendar image unavailable");
        byte[] signature=new byte[8];try(InputStream input=new FileInputStream(file)){if(input.read(signature)!=8||!Arrays.equals(signature,new byte[]{(byte)137,80,78,71,13,10,26,10}))throw new IOException("Invalid calendar PNG");}
        BitmapFactory.Options bounds=new BitmapFactory.Options();bounds.inJustDecodeBounds=true;BitmapFactory.decodeFile(file.getPath(),bounds);
        if(bounds.outWidth<1||bounds.outHeight<1||bounds.outWidth>4096||bounds.outHeight>4096)throw new IOException("Invalid calendar image size");
        return file;
    }
    static String name(String value){return value!=null&&value.matches("ShiftCal-[A-Za-z0-9-]+\\.png")?value:"ShiftCal-Calendar.png";}
    static Intent sendIntent(Context context,File file,String name){
        Uri uri=FileProvider.getUriForFile(context,context.getPackageName()+".shiftcal.files",file);
        Intent intent=new Intent(Intent.ACTION_SEND).setType("image/png").putExtra(Intent.EXTRA_STREAM,uri)
            .putExtra(Intent.EXTRA_TITLE,name).addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
        intent.setClipData(new ClipData(name,new String[]{"image/png"},new ClipData.Item(uri)));return intent;
    }
    static boolean usable(Activity activity){return activity!=null&&!activity.isFinishing()&&!activity.isDestroyed();}
    public static String directory(Activity activity){try{if(!usable(activity))return "";prune(activity,null);return folder(activity).getAbsolutePath();}catch(Exception ignored){return "";}}
    public static String share(Activity activity,String path,String displayName,String token){
        if(!usable(activity))return "Calendar sharing is unavailable. Reopen ShiftCal and try again.";
        try{File file=checkedFile(activity,path);prune(activity,file);Intent send=sendIntent(activity,file,name(displayName));
            activity.runOnUiThread(()->{try{
                if(!usable(activity))throw new ActivityNotFoundException();
                if(activity.getPackageManager().queryIntentActivities(send,0).isEmpty()){result(token,"error","No installed app can share PNG images.");return;}
                Intent chooser=Intent.createChooser(send,"Share ShiftCal Calendar");
                chooser.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);chooser.setClipData(send.getClipData());
                activity.startActivity(chooser);result(token,"ready","Choose an app to share your calendar.");
            }catch(ActivityNotFoundException ex){result(token,"error","No sharing app is available. You can use Save Image instead.");}
            catch(Exception ex){result(token,"error","Couldn't open sharing. Please try again.");}});
            return "";
        }catch(Exception ex){return "Calendar image unavailable. Please try Share again.";}
    }
    public static String save(Activity activity,String path,String displayName,String token){
        if(!usable(activity))return "Saving is unavailable. Reopen ShiftCal and try again.";
        try{File file=checkedFile(activity,path);prune(activity,file);String title=name(displayName);
            if(Build.VERSION.SDK_INT>=29){Context context=activity.getApplicationContext();IO.execute(()->{
                try{saveMedia(context,file,title);result(token,"saved","Calendar saved to Pictures/ShiftCal.");}
                catch(Exception ex){result(token,"error","Couldn't save the image. Check available storage and try again.");}
            });}
            else activity.runOnUiThread(()->{try{if(!usable(activity))throw new ActivityNotFoundException();
                activity.startActivity(new Intent(activity,CalendarImageSaveActivity.class).putExtra("path",file.getPath()).putExtra("name",title).putExtra("token",token));
            }catch(Exception ex){result(token,"error","Couldn't open Save Image. Please try again.");}});
            return "";
        }catch(Exception ex){return "Calendar image unavailable. Please try Save Image again.";}
    }
    static String digest(File file) throws Exception {
        try(InputStream input=new FileInputStream(file)){return digest(input);}
    }
    static String digest(InputStream input) throws Exception {
        MessageDigest digest=MessageDigest.getInstance("SHA-256");byte[] buffer=new byte[16384];int count;while((count=input.read(buffer))!=-1)digest.update(buffer,0,count);
        StringBuilder key=new StringBuilder();for(byte b:digest.digest())key.append(String.format(Locale.US,"%02x",b&255));return key.toString();
    }
    @androidx.annotation.RequiresApi(29)
    static Uri saveMedia(Context context,File file,String title) throws Exception {
        ContentResolver resolver=context.getContentResolver();var saved=context.getSharedPreferences("calendar-image-saves",Context.MODE_PRIVATE);String key=digest(file),existing=saved.getString(key,"");
        if(!existing.isEmpty())try(InputStream input=resolver.openInputStream(Uri.parse(existing))){if(input!=null&&key.equals(digest(input)))return Uri.parse(existing);}catch(Exception ignored){}
        ContentValues values=new ContentValues();values.put(MediaStore.Images.Media.DISPLAY_NAME,title);values.put(MediaStore.Images.Media.MIME_TYPE,"image/png");
        values.put(MediaStore.Images.Media.RELATIVE_PATH,"Pictures/ShiftCal");values.put(MediaStore.Images.Media.IS_PENDING,1);
        Uri uri=resolver.insert(MediaStore.Images.Media.EXTERNAL_CONTENT_URI,values);if(uri==null)throw new IOException("Image destination unavailable");
        try{
            copy(file,resolver,uri);ContentValues complete=new ContentValues();complete.put(MediaStore.Images.Media.IS_PENDING,0);
            if(resolver.update(uri,complete,null,null)<1)throw new IOException("Image could not be published");
            var edits=saved.edit();if(saved.getAll().size()>=64)edits.remove(saved.getAll().keySet().iterator().next());edits.putString(key,uri.toString()).apply();return uri;
        }catch(Exception ex){try{resolver.delete(uri,null,null);}catch(Exception ignored){}throw ex;}
    }
    static void copy(File file,ContentResolver resolver,Uri uri) throws IOException {
        if(uri==null||!"content".equals(uri.getScheme()))throw new IOException("Invalid image destination");
        try(InputStream input=new FileInputStream(file);OutputStream output=resolver.openOutputStream(uri,"w")){
            if(output==null)throw new IOException("Image destination unavailable");byte[] buffer=new byte[16384];int count;
            while((count=input.read(buffer))!=-1)output.write(buffer,0,count);output.flush();
        }
    }
    static void result(String token,String status,String message){try{NativeBridge.message("OnCalendarImageResult",new JSONObject().put("token",token).put("status",status).put("message",message).toString());}catch(Exception ignored){}}
}
