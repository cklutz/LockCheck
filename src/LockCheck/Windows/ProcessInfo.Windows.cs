using System.IO;

namespace LockCheck.Windows;

internal class ProcessInfoWindows : ProcessInfo
{
    public static ProcessInfoWindows? Create(NativeMethods.RM_PROCESS_INFO pi)
    {
        var peb = new Peb((int)pi.Process.dwProcessId, pi.GetStartTime());
        return peb.HasError ? null : new ProcessInfoWindows(peb);
    }

    public static ProcessInfoWindows? Create(int processId)
    {
        var peb = new Peb(processId, NativeMethods.GetProcessStartTime(processId));
        return peb.HasError ? null : new ProcessInfoWindows(peb);
    }

    public ProcessInfoWindows(Peb peb)
        : base(peb.ProcessId, peb.StartTime)
    {
        ParentProcessId = peb.ParentProcessId;
        ExecutableFullPath = peb.ExecutableFullPath;
        ExecutableName = Path.GetFileName(peb.ExecutableFullPath);
        ApplicationName = peb.ProcessName;
        SessionId = peb.SessionId;
        Owner = peb.Owner;
        IsCritical = peb.IsCritical;
        ProcessStartKey = peb.ProcessStartKey;
    }
}
