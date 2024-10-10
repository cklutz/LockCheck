using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
#if NETFRAMEWORK
using System.Reflection;
#endif

namespace LockCheck
{
    /// <summary>
    /// Extension methods.
    /// </summary>
    public static class ExceptionUtils
    {
#if NETFRAMEWORK
        // In .NET Framework the Exception.HResult property has no setter.
        private static readonly Lazy<MethodInfo> s_setErrorCodeMethod = new(
            () => typeof(Exception).GetMethod("SetErrorCode",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.InvokeMethod,
                null, [typeof(int)], null));
#endif

        /// <summary>
        /// Determines if the current <see cref="IOException"/> is likely due to a locked file condition.
        /// </summary>
        /// <param name="exception">The exception to check.</param>
        /// <returns><c>true</c> if the <paramref name="exception"/> is due to a locked file, <c>false</c> otherwise.</returns>
        public static bool IsFileLocked(this IOException exception)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Windows.Extensions.IsFileLocked(exception);
            }
#if NET
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return Linux.Extensions.IsFileLocked(exception);
            }
#endif

            return false;
        }

        /// <summary>
        /// Throws a new <see cref="IOException"/> that contains the given exception as an inner exception,
        /// if the <paramref name="ex"/> is due to a file being locked. Otherwise does not throw and exception.
        /// </summary>
        /// <param name="ex">The exception to check and rethrow</param>
        /// <param name="fileName">The file name to check for being locked.</param>
        /// <param name="features">Optional features.</param>
        /// <example>
        /// <![CDATA[
        ///    string fileName = "C:\\temp\\foo.txt";
        ///    try
        ///    {
        ///        var file2 = File.Open(fileName, FileMode.Open, FileAccess.ReadWrite);
        ///    }
        ///    catch (Exception ex)
        ///    {
        ///        if (!ex.RethrowWithLockingInformation(fileName))
        ///        {
        ///            // RethrowWithLockingInformation() has not rethrown the exception, because it
        ///            // didn't appear that "fileName" is locked. Rethrow ourselves - or do something
        ///            // else..
        ///            throw;
        ///        }
        ///    }
        /// ]]>
        /// </example>
        /// <returns>
        /// <c>false</c> when <paramref name="ex"/> has not been rethrown.
        /// </returns>
        public static bool RethrowWithLockingInformation(this Exception ex, string fileName, LockManagerFeatures features = default)
        {
            return RethrowWithLockingInformation(ex, [fileName], features);
        }

        /// <summary>
        /// Throws a new <see cref="IOException"/> that contains the given exception as an inner exception,
        /// if the <paramref name="ex"/> is due to a file being locked. Otherwise does not throw and exception.
        /// </summary>
        /// <param name="ex">The exception to check and rethrow</param>
        /// <param name="fileNames">The file names to check for being locked.</param>
        /// <param name="features">Optional features.</param>
        /// <param name="maxProcesses">Maximum number of processes to include in the output.
        /// If this value is <c>null</c> an internal default will be applied.
        /// If this value is <c>-1</c> all found processes will be output.</param>
        /// <example>
        /// <![CDATA[
        ///    string fileName = "C:\\temp\\foo.txt";
        ///    try
        ///    {
        ///        var file2 = File.Open(fileName, FileMode.Open, FileAccess.ReadWrite);
        ///    }
        ///    catch (Exception ex)
        ///    {
        ///        if (!ex.RethrowWithLockingInformation(fileName))
        ///        {
        ///            // RethrowWithLockingInformation() has not rethrown the exception, because it
        ///            // didn't appear that "fileName" is locked. Rethrow ourselves - or do something
        ///            // else..
        ///            throw;
        ///        }
        ///    }
        /// ]]>
        /// </example>
        /// <returns>
        /// <c>false</c> when <paramref name="ex"/> has not been rethrown.
        /// </returns>
        public static bool RethrowWithLockingInformation(this Exception ex, string[] fileNames, LockManagerFeatures features = default, int? maxProcesses = null)
        {
            if (fileNames?.Length > 0)
            {
                if (ex is IOException ioEx && ioEx.IsFileLocked())
                {
                    // It is a race to get the lockers, while they are still there. So do this as early as possible.
                    var lockers = LockManager.GetLockingProcessInfos(fileNames, features);

                    if (lockers.Any())
                    {
                        // Alter behavior of Format(). It would return int.MaxValue on null also.
                        // Here we want to return a baked in default in this case.
                        int max = maxProcesses == -1 ? int.MaxValue : maxProcesses.GetValueOrDefault(10);
                        var sb = new StringBuilder();
                        sb.Append(ex.Message);
                        sb.Append(' ');
                        ProcessInfo.Format(sb, lockers, fileNames, max);

                        var exception = new IOException(sb.ToString(), ex);
#if NETFRAMEWORK
                        s_setErrorCodeMethod.Value?.Invoke(exception, [ex.HResult]);
#endif
#if NET
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