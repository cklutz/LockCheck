using System;
using System.IO;

namespace LockCheck.Linux
{
    public static class Extensions
    {
        public static bool IsFileLocked(IOException exception)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            return exception.HResult == NativeMethods.EWOULDBLOCK;
        }
    }
}
