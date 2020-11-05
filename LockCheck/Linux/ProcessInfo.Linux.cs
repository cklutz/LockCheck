using System;
using System.Diagnostics;
using System.IO;

namespace LockCheck.Linux
{
    internal class ProcessInfoLinux : ProcessInfo
    {
        public static ProcessInfoLinux Create(LockInfo li)
        {
            if (li.ProcessId == -1)
            {
                // Can happen for OFD (open file descriptor) locks which are not
                // bound to a specific process.
                return new ProcessInfoLinux(li.ProcessId, DateTime.MinValue)
                {
                    LockAccess = li.LockAccess,
                    LockMode = li.LockMode,
                    LockType = li.LockType,
                    ApplicationName = $"({li.LockType};{li.LockAccess};{li.LockMode})"
                };
            }

            ProcessInfoLinux result = null;

            try
            {
                using (var process = Process.GetProcessById(li.ProcessId))
                {
                    result = new ProcessInfoLinux(li.ProcessId, process.StartTime)
                    {
                        SessionId  = process.SessionId,
                        ApplicationName = process.ProcessName
                    };

                    // MainModule may be null, if no permissions, etc.
                    // Note: alternative of "readlink -f /proc/<pid>/exe" will
                    // also yield results in this case.
                    if (process.MainModule != null)
                    {
                        result.ExecutableFullPath = process.MainModule.FileName;
                        result.ExecutableName = Path.GetFileName(result.ExecutableFullPath);
                    }
                    else
                    {
                        result.ExecutableFullPath = process.ProcessName;
                        result.ExecutableName = process.ProcessName;
                    }
                }
            }
            catch (ArgumentException)
            {
                // Process already gone/does not exist.
            }

            if (result != null)
            {
                // TryGetUid() fails if process is gone (because directory is gone);
                // GetUserName() should not fail because it looks up information in
                // passwd, which is not bound in lifetime to the process of course.
                if (NativeMethods.TryGetUid($"/proc/{li.ProcessId}", out uint uid))
                {
                    result.Owner = NativeMethods.GetUserName(uid);
                }
            }

            return result;
        }

        private ProcessInfoLinux(int processId, DateTime startTime)
            : base(processId, startTime)
        {
        }
    }
}
