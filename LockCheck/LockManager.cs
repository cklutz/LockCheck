using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using LockCheck.Linux;
using LockCheck.Windows;

namespace LockCheck
{
    public static class LockManager
    {
        public static IEnumerable<ProcessInfo> GetLockingProcessInfos(string[] paths, LockManagerFeatures features = default)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if ((features & LockManagerFeatures.UseLowLevelApi) != 0)
                {
                    return NtDll.GetLockingProcessInfos(paths);
                }

                return RestartManager.GetLockingProcessInfos(paths);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return ProcFileSystem.GetLockingProcessInfos(paths);
            }
            else
            {
                if ((features & LockManagerFeatures.ThrowIfNotSupported) != 0)
                {
                    throw new NotSupportedException("Current OS platform is not supported");
                }

                return Enumerable.Empty<ProcessInfo>();
            }
        }
    }
}
