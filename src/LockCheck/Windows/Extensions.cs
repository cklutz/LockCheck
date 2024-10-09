using System;
using System.IO;

namespace LockCheck.Windows
{
    internal static class Extensions
    {
        public static bool IsFileLocked(IOException exception)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            // Generally it is not safe / stable to convert HRESULTs to Win32 error codes. It works here,
            // because we exactly know where we're at. So resist refactoring the following code into an
            // (maybe even externally visible) method.
            int errorCode = exception.HResult & ((1 << 16) - 1);

            if (errorCode == NativeMethods.ERROR_LOCK_VIOLATION ||
                errorCode == NativeMethods.ERROR_SHARING_VIOLATION)
            {
                return true;
            }

            return false;
        }
    }
}
