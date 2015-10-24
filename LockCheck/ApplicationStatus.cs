using System;

namespace LockCheck
{
    [Flags]
    public enum ApplicationStatus
    {
        // Members must have the same values as in NativeMethods.RM_APP_STATUS

        Unknown = 0x0,
        Running = 0x1,
        Stopped = 0x2,
        StoppedOther = 0x4,
        Restarted = 0x8,
        ErrorOnStop = 0x10,
        ErrorOnRestart = 0x20,
        ShutdownMasked = 0x40,
        RestartMasked = 0x80
    }
}