using System;
using System.Collections.Generic;
using System.IO;

namespace LockCheck.Linux
{
    internal static class ProcFileSystem
    {
        public static HashSet<ProcessInfo> GetLockingProcessInfos(List<string> paths)
        {
            if (paths == null)
                throw new ArgumentNullException(nameof(paths));

            Dictionary<long, string> inodesToPaths = null;
            var result = new HashSet<ProcessInfo>();

            using (var reader = new StreamReader("/proc/locks"))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (inodesToPaths == null)
                    {
                        inodesToPaths = GetInodeToPaths(paths);
                    }

                    var lockInfo = LockInfo.ParseLine(line);
                    if (inodesToPaths.ContainsKey(lockInfo.InodeInfo.INodeNumber))
                    {
                        var processInfo = ProcessInfoLinux.Create(lockInfo);
                        if (processInfo != null)
                        {
                            result.Add(processInfo);
                        }
                    }
                }
            }

            return result;
        }

        private static Dictionary<long, string> GetInodeToPaths(List<string> paths)
        {
            var inodesToPaths = new Dictionary<long, string>();
            foreach (string path in paths)
            {
                long inode = NativeMethods.GetInode(path);
                if (inode != -1)
                {
                    inodesToPaths.Add(inode, path);
                }
            }

            return inodesToPaths;
        }
    }
}
