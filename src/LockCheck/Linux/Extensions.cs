using System;
using System.IO;

namespace LockCheck.Linux;

internal static class Extensions
{
    public static bool IsFileLocked(Exception exception)
    {
        if (exception == null)
            throw new ArgumentNullException(nameof(exception));

        // Unix is... complicated. For EACCES, the runtime does throw a UnauthorizedAccessException,
        // with potentially an inner exception of type IOException.

        if (exception is UnauthorizedAccessException ua &&
            ua.InnerException is IOException ioException)
        {
            return ioException.HResult == NativeMethods.EACCES;
        }

        // EWOULDBLOCK is directly thrown as an IOException with the respective code.

        if (exception is IOException ioException2)
        {
            return ioException2.HResult == NativeMethods.EWOULDBLOCK;
        }

        return false;
    }
}
