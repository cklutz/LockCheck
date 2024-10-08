using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LockCheck.Linux;
using LockCheck.Windows;

namespace LockCheck
{
    public static class LockManager
    {
        public static IEnumerable<ProcessInfo> GetLockingProcessInfos(string[] paths, LockManagerFeatures features = default)
        {
            if (paths == null)
                throw new ArgumentNullException(nameof(paths));

            HashSet<ProcessInfo> processInfos = [];

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                List<string> directories = (features & LockManagerFeatures.CheckDirectories) != 0 ? [] : null;

                if ((features & LockManagerFeatures.UseLowLevelApi) != 0)
                {
                    processInfos = NtDll.GetLockingProcessInfos(paths, ref directories);
                }
                else
                {
                    processInfos = RestartManager.GetLockingProcessInfos(paths, ref directories);
                }

                if (directories?.Count > 0)
                {
                    var matches = NtDll.GetProcessesByWorkingDirectory(directories);
                    foreach (var match in matches)
                    {
                        processInfos.Add(match.Value);
                    }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                List<string> directories = (features & LockManagerFeatures.CheckDirectories) != 0 ? [] : null;

                processInfos = ProcFileSystem.GetLockingProcessInfos(paths, ref directories);

                if (directories?.Count > 0)
                {
                    var matches = ProcFileSystem.GetProcessesByWorkingDirectory(directories);
                    foreach (var match in matches)
                    {
                        processInfos.Add(match.Value);
                    }
                }
            }
            else
            {
                if ((features & LockManagerFeatures.ThrowIfNotSupported) != 0)
                {
                    throw new PlatformNotSupportedException("Current OS platform is not supported");
                }
            }

            return processInfos;
        }
    }
}
