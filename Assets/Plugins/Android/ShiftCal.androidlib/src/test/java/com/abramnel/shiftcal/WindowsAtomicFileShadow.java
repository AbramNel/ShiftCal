package com.abramnel.shiftcal;

import android.util.AtomicFile;
import org.robolectric.annotation.*;
import java.io.*;
import java.nio.file.*;

/** Android's rename replaces a destination. Windows File.renameTo in the SDK
 * simulation does not, leaving stale data after the first AtomicFile write.
 * Give the host tests the same replacement semantics as a real Android device. */
@Implements(AtomicFile.class)
public class WindowsAtomicFileShadow {
    @RealObject private AtomicFile file;
    @Implementation protected void finishWrite(FileOutputStream stream) {
        if (stream == null) return;
        try {
            stream.getFD().sync();stream.close();
            Files.move(Paths.get(file.getBaseFile().getPath()+".new"),file.getBaseFile().toPath(),StandardCopyOption.REPLACE_EXISTING);
        } catch(IOException error) { throw new IllegalStateException(error); }
    }
}
