# LockCheck
Uses Windows Restart Manager APIs to find processes locking one or multiple files.

The code is CodeAnalysis clean (using Microsoft recommended rules) and has been tested on Windows 7 and Windows 10 (both x64).
It is actually meant to be included in a library or such, but for quick tests a "Main" method is provided.

Example:

* Open/create a file "C:\temp\foo.xlsx" in Microsoft Excel - you can use any other application that actually locks a file, of course.
* Run the following command: 

       LockCheck.exe c:\temp\foo.xlsx
  
* The ouput should be like

        Process ID        : 1296
        Process Start Time: Saturday, 24th October 2015 16:17:58
        Application Type  : MainWindow
        Application Status: Running
        Application Name  : Microsoft Excel
        TS Session ID     : 1


## Enriching Exceptions with Lock Information ##

The method ExceptionUtils.RethrowWithLockingInformation() can be used to enrich exceptions
with lock information, if available.

Here is a phony example. The inner Open call causes an IOException, because the outer
Open call already opened the file exclusively (albeit in the same process, but that
doesn't matter for the cause of the example):

        static void Test()
        {
            using (var file = File.Open("c:\\temp\\foo.txt", FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                try
                {
                    var file2 = File.Open(""c:\\temp\\foo.txt", FileMode.Open, FileAccess.ReadWrite);
                }
                catch (Exception ex)
                {
                    if (!ex.RethrowWithLockingInformation("C:\\temp\\foo.txt"))
                        throw;
                }
            }
        }

If the RethrowWithLockingInformation() method could deduce any lockers, it will create an IOException
that has the original exception as inner exception, but additionally includes lock information in the
message text.

If the RethrowWithLockingInformation() method could not deduce any lockers or the original exception
did not signify a locking/sharing violation, it will return false and NOT raise any exception. In this
case, as show above, you should simply rethrow the original exception.

A view notes:

* Getting the lockers, if any, is of course a matter of timing. By the time you got the original exception
  and your code comes around to determine them, they might already be gone.
* Currently, this is not an all purpose solution, because you need to know the original file- or directory
  name in question.
* Performance could be an issue. It is comparatively expensive to determine the lockers - albeit you might
  argue that when the error happens performance is not so much an issue anymore.

Personally, I use this helper only in situations where I know locking issues are "common", for example
when attempting to recursively delete a directory tree, etc.

Finally, here is the exception output without the RethrowWithLockingInformation() call:

     System.IO.IOException: The process cannot access the file 'C:\temp\foo.txt' because it is being used by another process.
        at System.IO.__Error.WinIOError(Int32 errorCode, String maybeFullPath)
        at System.IO.FileStream.Init(String path, FileMode mode, FileAccess access, Int32 rights, ...
        at System.IO.FileStream..ctor(String path, FileMode mode, FileAccess access, FileShare share)
        at System.IO.File.Open(String path, FileMode mode, FileAccess access)
        ExceptionUtils.cs(25,0): at LockCheck.ExceptionUtils.Test()

And this is it, with that information included:

    System.IO.IOException: The process cannot access the file 'C:\temp\foo.txt' because it is being used by another process.
    File C:\temp\foo.txt locked by: [MyApp 1.0.0.0, pid=18860, started 2017-07-16 12:28:57.714]
       ---> System.IO.IOException: The process cannot access the file 'C:\temp\foo.txt' because it is being used by another process.
        at System.IO.__Error.WinIOError(Int32 errorCode, String maybeFullPath)
        at System.IO.FileStream.Init(String path, FileMode mode, FileAccess access, Int32 rights, ...
        at System.IO.FileStream..ctor(String path, FileMode mode, FileAccess access, FileShare share)
        at System.IO.File.Open(String path, FileMode mode, FileAccess access)
        ExceptionUtils.cs(20,0): at LockCheck.ExceptionUtils.Test()
       --- End of inner exception stack trace ---
        ExceptionUtils.cs(80,0): at LockCheck.ExceptionUtils.RethrowWithLockingInformation(Exception ex, String[] fileNames)
        ExceptionUtils.cs(24,0): at LockCheck.ExceptionUtils.Test()
