# LockCheck

Uses platform APIs to find processes locking one or multiple files or directories.

[![MIT License](https://img.shields.io/github/license/cklutz/LockCheck?color=%230b0&style=flat-square)](https://github.com/cklutz/LockCheck/blob/master/LICENSE) 
[![nuget](https://img.shields.io/nuget/v/LockCheck?style=flat-square)](https://www.nuget.org/packages/LockCheck/)
[![windows](https://github.com/cklutz/LockCheck/workflows/Windows/badge.svg)](https://github.com/cklutz/LockCheck/actions?query=workflow%3AWindows)
[![ubuntu](https://github.com/cklutz/LockCheck/workflows/Ubuntu/badge.svg)](https://github.com/cklutz/LockCheck/actions?query=workflow%3AUbuntu)

[![](https://cklutz.github.io/LockCheck/ubuntu-net8.0-release/badge.svg?cache-control=no-cache)](https://cklutz.github.io/LockCheck/ubuntu-net8.0-release)

[![](https://cklutz.github.io/LockCheck/windows-net8.0-release/badge.svg?cache-control=no-cache)](https://cklutz.github.io/LockCheck/windows-net8.0-release)

[![](https://cklutz.github.io/LockCheck/windows-net481-release/badge.svg?cache-control=no-cache)](https://cklutz.github.io/LockCheck/windows-net481-release)


### Platforms

## Windows

On the Windows platform there are two possible engines to provide the lock information:

* Windows RestartManager API (default)
* Windows NTDLL functions (using `LockManagerFeatures.UseLowLevelApi`)

The RestartManager API has the advantage of being a documented interface, but
the disadvantage that it might introduce a rather big overhead. For example,
The backing [`RmRegisterResources`](https://docs.microsoft.com/en-us/windows/win32/api/restartmanager/nf-restartmanager-rmregisterresources)
Win32-API is rather expensive, which is why you should probably resort to using `UseLowLevelApi` until
it is promoted to be default in a future version.

Since the "who locks the file" information is potentially highly volatile, and the
locking processing might already be gone when you start looking for it using this
library after you got an exception, this might be too much. YMMV.

Also note, that the RestartManager can only have a maximum of 64 restart manager
sessions per user session - this might not be a real world issue, as the API is
usually only used by installers and setup applications, but again, YMMV.

Finally, note that if the calling process does not have the required permissions to
access information about other processes (that might hold a lock), these processes
are simply not returned. In other words, it is a best effort approach.

## Linux

On Linux, the `/proc/locks` file is used as basis for finding processes holding a lock
on a file. Linux supports multiple lock types (see this [article](https://gavv.github.io/articles/file-locks/)
for an overview). Not all those lock types are directly associated with a single process,
so the `ProcesInfo.ProcessID` member can be `-1` here. Additionally, on Linux, the
`ProcessInfo.LockType`, `ProcessInfo.LockMode` and `ProcessInfo.LockAccess` properties
return the respective values. These properties are `null` on Windows.

Linux support should be considered experimental. Albeit the Unit-Tests and example application works,
it has not been used in a real world scenario. The Linux version has been developed on WSL2.

If you have any improvements / PRs please let me know.

### Usage

## Getting lock information on demand

### Basic Usage

To get the lockers of a file, if any, use the `LockManager.GetLockingProcessInfos()` function.

```
foreach (var processInfo in LockManager.GetLockingProcessInfos("c:\\temp\\foo.xlsx"))
{
    // Do something with the information.
}
```

### Inspect processes current working directories

On Windows if you attempt to delete a directory that is the current working directory of a
process, this will result in "access denied".
`LockManager` can also check paths (directories) that are passed to `GetLockingProcessInfos()`
on whether they are the current working directory of any process, and if so, include them
in the result.  To enable this feature you need to pass the `LockManagerFeatures.CheckDirectories`
flag.

## Enriching Exceptions with Lock Information

The `ExceptionUtils.RethrowWithLockingInformation()` method can be used to enrich exceptions
with lock information, if available.

Here is a phony example. The inner `File.Open()` call causes an `IOException`, because the outer
`File.Open()` call already opened the file exclusively (albeit in the same process, but that
doesn't matter for the cause of the example):

```
static void Test()
{
    using (var file = File.Open("c:\\temp\\foo.txt", FileMode.OpenOrCreate, FileAccess.ReadWrite))
    {
        try
        {
            var file2 = File.Open("c:\\temp\\foo.txt", FileMode.Open, FileAccess.ReadWrite);
        }
        catch (Exception ex)
        {
            if (!ex.RethrowWithLockingInformation("C:\\temp\\foo.txt"))
                throw;
        }
    }
}
```

If the `RethrowWithLockingInformation()` method could deduce any lockers, it will create an `IOException`
that has the original exception as inner exception, but additionally includes lock information in the
message text.

If the `RethrowWithLockingInformation()` method could not deduce any lockers or the original exception
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

Finally, here is the exception output without the `RethrowWithLockingInformation()` call:

     System.IO.IOException: The process cannot access the file 'C:\temp\foo.txt' because it is being used by another process.
        at System.IO.__Error.WinIOError(Int32 errorCode, String maybeFullPath)
        at System.IO.FileStream.Init(String path, FileMode mode, FileAccess access, Int32 rights, ...
        at System.IO.FileStream..ctor(String path, FileMode mode, FileAccess access, FileShare share)
        at System.IO.File.Open(String path, FileMode mode, FileAccess access)
        ExceptionUtils.cs(25,0): at LockCheck.ExceptionUtils.Test()

And this is it, with that information included:

    System.IO.IOException: The process cannot access the file 'C:\temp\foo.txt' because it is being used by another process.
    File C:\temp\foo.txt locked by: [MyApp 1.0.0.0, pid=18860, user=cklutz started=2017-07-16 12:28:57.714]
       ---> System.IO.IOException: The process cannot access the file 'C:\temp\foo.txt' because it is being used by another process.
        at System.IO.__Error.WinIOError(Int32 errorCode, String maybeFullPath)
        at System.IO.FileStream.Init(String path, FileMode mode, FileAccess access, Int32 rights, ...
        at System.IO.FileStream..ctor(String path, FileMode mode, FileAccess access, FileShare share)
        at System.IO.File.Open(String path, FileMode mode, FileAccess access)
        ExceptionUtils.cs(20,0): at LockCheck.ExceptionUtils.Test()
       --- End of inner exception stack trace ---
        ExceptionUtils.cs(80,0): at LockCheck.ExceptionUtils.RethrowWithLockingInformation(Exception ex, String[] fileNames)
        ExceptionUtils.cs(24,0): at LockCheck.ExceptionUtils.Test()

### Examples

A standalone tool (`LockCheckTool`) is included and is available for both .NET 8+, and .NET Framework (4.8.1+).
You can test the functionality as follows:

* Open/create a file "C:\temp\foo.xlsx" in Microsoft Excel - you can use any other application that actually locks a file, of course.
* Run the following command: 

       LockCheckTool.exe c:\temp\foo.xlsx
  
* The output should something like this

        Process ID        : 1296
        Process Start Time: Saturday, 24th October 2024 16:17:58
        Application Type  : MainWindow
        Application Status: Running
        Application Name  : Microsoft Excel
        Session ID        : 1
