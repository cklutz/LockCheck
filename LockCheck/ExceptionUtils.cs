using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace LockCheck
{
    public static class ExceptionUtils
    {
#if SELF_TEST
        static void Test()
        {
            using (var file = File.Open("C:\\temp\\foo.txt", FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                try
                {
                    var file2 = File.Open("C:\\temp\\foo.txt", FileMode.Open, FileAccess.ReadWrite);
                }
                catch (Exception ex)
                {
                    if (!ex.RethrowWithLockingInformation("C:\\temp\\foo.txt"))
                        throw;
                }
            }
        }
#endif

        private static readonly Lazy<MethodInfo> s_setErrorCodeMethod = new Lazy<MethodInfo>(
            () => typeof(Exception).GetMethod("SetErrorCode",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.InvokeMethod,
                null, new[] {typeof(int)}, null));

        public static bool IsFileLocked(this IOException exception)
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

        public static bool RethrowWithLockingInformation(this Exception ex, params string[] fileNames)
        {
            var ioex = ex as IOException;
            if (ioex != null && ioex.IsFileLocked())
            {
                // It is a race to get the lockers, while they are still there. So do this as early as possible.
                var lockers = RestartManager.GetLockingProcessInfos(fileNames).ToList();

                if (lockers.Any())
                {
                    const int max = 10;
                    var sb = new StringBuilder();
                    sb.Append(ex.Message);
                    sb.Append(" ");
                    ProcessInfo.Format(sb, lockers, fileNames, max);

                    // We either have a ctor that allows us to set HResult or InnerException, but not both.
                    // But we want both, so we need to use reflection anyway. Since there is an internal
                    // method "Exception.SetErrorCode(int)" but no equivalent for InnerException, we use
                    // the ctor that allows us to set the InnerException and set HResult via reflection.
                    var exception = new IOException(sb.ToString(), ex);
                    if (s_setErrorCodeMethod.Value != null)
                    {
                        s_setErrorCodeMethod.Value.Invoke(exception, new object[] {ex.HResult});
                    }

                    throw exception;
                }
            }
            return false;
        }
    }
}