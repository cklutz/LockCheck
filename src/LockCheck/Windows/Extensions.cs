using System;
using System.IO;

namespace LockCheck.Windows;

internal static class Extensions
{
    public static bool IsFileLocked(Exception exception)
    {
        if (exception == null)
            throw new ArgumentNullException(nameof(exception));

        if (exception is IOException ioException)
        {
            // Generally it is not safe / stable to convert HRESULTs to Win32 error codes. It works here,
            // because we exactly know where we're at. So resist refactoring the following code into an
            // (maybe even externally visible) method.
            int errorCode = ioException.HResult & ((1 << 16) - 1);

            // Code coverage note: causing a ERROR_LOCK_VIOLATION is rather hard to achieve in a test.
            // Basically, you will mostly (always?) get a ERROR_SHARING_VIOLATION, unless you would
            // do the test via the network (e.g. using a share). Note that using the "\\<computer>\C$"
            // share will not cut it.
            //
            // Also note, that as of current (fall 2024), the .NET runtime does not raise IOException
            // with ERROR_LOCK_VIOLATION. Since technically, this error is a potential result of a
            // locking issue, we check for it anyway.
            if (errorCode == NativeMethods.ERROR_LOCK_VIOLATION ||
                errorCode == NativeMethods.ERROR_SHARING_VIOLATION)
            {
                return true;
            }
        }
        return false;
    }
}
