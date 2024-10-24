using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LockCheck.Tests.Windows;

namespace LockCheck.Tests.Tooling;

internal interface IJobObject : IDisposable
{
    void AttachProcess(Process process);
}

internal static class JobObject
{
    // Provide a toggle to generally disable job objects - whether supported by the platform or not.
    // This can be used in situations where they might cause issues (due to permissions, etc.).
    private static readonly bool s_disabled = Environment.GetEnvironmentVariable("LOCKCHECK_DISABLE_JOBOBJECT") == "true";

    public static IJobObject Create(string? name = null)
    {
        if (!s_disabled)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new Win32JobObject(name);
            }
        }

        return new NoopJobObject();
    }

    private class NoopJobObject : IJobObject
    {
        public void AttachProcess(Process process)
        {
        }

        public void Dispose()
        {
        }
    }
}
