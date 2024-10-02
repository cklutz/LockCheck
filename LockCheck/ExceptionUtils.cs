using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace LockCheck
{
    public static class ExceptionUtils
    {
#if NETFRAMEWORK
        private static readonly Lazy<MethodInfo> s_setErrorCodeMethod = new Lazy<MethodInfo>(
            () => typeof(Exception).GetMethod("SetErrorCode",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.InvokeMethod,
                null, new[] { typeof(int) }, null));
#endif

        public static bool IsFileLocked(this IOException exception)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Windows.Extensions.IsFileLocked(exception);
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return Linux.Extensions.IsFileLocked(exception);
            }

            return false;
        }

        public static bool RethrowWithLockingInformation(this Exception ex, string fileName, LockManagerFeatures features = default)
        {
            return RethrowWithLockingInformation(ex, new[] { fileName }, features);
        }

        public static bool RethrowWithLockingInformation(this Exception ex, string[] fileNames, LockManagerFeatures features = default)
        {
            if (fileNames?.Length > 0)
            {
                var ioex = ex as IOException;
                if (ioex != null && ioex.IsFileLocked())
                {
                    // It is a race to get the lockers, while they are still there. So do this as early as possible.
                    var lockers = LockManager.GetLockingProcessInfos(fileNames, features).ToList();

                    if (lockers.Any())
                    {
                        const int max = 10;
                        var sb = new StringBuilder();
                        sb.Append(ex.Message);
                        sb.Append(" ");
                        ProcessInfo.Format(sb, lockers, fileNames, max);

                        var exception = new IOException(sb.ToString(), ex);
#if NETFRAMEWORK
                    if (s_setErrorCodeMethod.Value != null)
                    {
                        s_setErrorCodeMethod.Value.Invoke(exception, new object[] { ex.HResult });
                    }
#endif
#if NETCOREAPP
                        exception.HResult = ex.HResult;
#endif

                        throw exception;
                    }
                }
            }
            return false;
        }
    }
}