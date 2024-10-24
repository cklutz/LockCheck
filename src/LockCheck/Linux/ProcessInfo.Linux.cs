using System;
using System.IO;

namespace LockCheck.Linux;

internal class ProcessInfoLinux : ProcessInfo
{
    public static ProcessInfoLinux Create(ProcInfo pi)
    {
        var result = new ProcessInfoLinux(pi.ProcessId, pi.StartTime)
        {
            ExecutableFullPath = pi.ExecutableFullPath,
            ExecutableName = pi.ExecutableFullPath != null ? Path.GetFileName(pi.ExecutableFullPath) : null,
            SessionId = pi.SessionId,
            Owner = pi.Owner
        };

        result.ApplicationName = result.ExecutableName;

        return result;
    }

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

        return Create(new ProcInfo(li.ProcessId));
    }

    private ProcessInfoLinux(int processId, DateTime startTime)
        : base(processId, startTime)
    {
    }
}
