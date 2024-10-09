using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LockCheck.Linux;
using LockCheck.Windows;

namespace LockCheck
{
    /// <summary>
    /// Retrieves information about locked files and directories.
    /// </summary>
    public static class LockManager
    {
        /// <summary>
        /// Attempt to find processes that lock the specified paths.
        /// </summary>
        /// <param name="paths">The paths to check.</param>
        /// <param name="features">Optional features</param>
        /// <returns>
        /// A list of processes that lock at least one of the specified paths.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="paths"/> is <c>null</c>.</exception>
        /// <exception cref="PlatformNotSupportedException">
        /// The current platform is not supported. This exception is only thrown, when the <paramref name="features"/>
        /// includes the <see cref="LockManagerFeatures.ThrowIfNotSupported"/> flag. Otherwise the function will
        /// simply return an empty enumeration when a platform is not supported.
        /// </exception>
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
