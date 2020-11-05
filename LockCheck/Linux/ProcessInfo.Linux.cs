using System;
using System.Diagnostics;
using System.IO;
using LockCheck.Linux;

namespace LockCheck
{
    public partial class ProcessInfo
    {
        internal ProcessInfo(LockInfo lockInfo)
        {
            SessionId = -1;
            ProcessId = lockInfo.ProcessId;
            LockType = lockInfo.LockType;
            LockMode = lockInfo.LockMode;
            LockAccess = lockInfo.LockAccess;

            FillDetailsLinux();
        }

        public string LockType { get; set; }
        public string LockMode { get; set; }
        public string LockAccess { get; set; }

        private void FillDetailsLinux()
        {
            if (ProcessId == -1)
            {
                // Can happen for OFD (open file descriptor) locks which are not
                // bound to a specific process.
                ApplicationName = LockType;
                return;
            }

            try
            {
                using (var process = Process.GetProcessById(ProcessId))
                {
                    StartTime = process.StartTime;
                    SessionId = process.SessionId;
                    ApplicationName = process.ProcessName;

                    // MainModule may be null, if no permissions, etc.
                    // Note: alternative of "readlink -f /proc/<pid>/exe" will
                    // also yield results in this case.
                    if (process.MainModule != null)
                    {
                        FilePath = process.MainModule.FileName;
                        ExecutableName = Path.GetFileName(FilePath);
                    }
                    else
                    {
                        FilePath = process.ProcessName;
                        ExecutableName = process.ProcessName;
                    }
                }
            }
            catch (ArgumentException)
            {
                // Process already gone/does not exist.
            }

            // TryGetUid() fails if process is gone (because directory is gone);
            // GetUserName() should not fail because it looks up information in
            // passwd, which is not bound in lifetime to the process of course.
            if (NativeMethods.TryGetUid($"/proc/{ProcessId}", out uint uid))
            {
                UserName = NativeMethods.GetUserName(uid);
            }
        }
    }
}
