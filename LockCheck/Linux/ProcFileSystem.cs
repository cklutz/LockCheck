using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security;

namespace LockCheck.Linux
{
    internal static class ProcFileSystem
    {
        private const string LocksFile = "/proc/locks";
        private const string FdDirectoryFormat = "/proc/{0}/fd";
        private const string FdFileFormat = "/proc/{0}/fd/{1}";

        public static IEnumerable<ProcessInfo> GetLockingProcessInfos(params string[] paths)
        {
            if (paths == null)
                throw new ArgumentNullException(nameof(paths));

            var inodesToPaths = new Dictionary<long, string>();
            foreach (string path in paths)
            {
                long inode = NativeMethods.GetInode(path);
                if (inode != -1)
                {
                    inodesToPaths.Add(inode, path);
                }
            }

            using (var stream = File.OpenRead(LocksFile))
            using (var reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var lockInfo = new LockInfo(LocksFile, line);
                    if (inodesToPaths.ContainsKey(lockInfo.InodeInfo.INodeNumber))
                    {
                        yield return new ProcessInfo(lockInfo);
                    }

                }
            }
        }

        private static string GetFileName(int processId, long inodeNumber)
        {
            string fdFile = string.Format(FdFileFormat, processId, inodeNumber);
            return NativeMethods.ReadLink(fdFile);
        }
    }

    internal class LockInfo
    {
        private static readonly char[] s_space = new[] { ' ' };
        private static readonly char[] s_colon = new[] { ':' };

        public LockInfo(string fileName, string line)
        {
            FillFromProcFileSystem(fileName, line);
        }

        public string LockType { get; private set; }
        public string LockMode { get; private set; }
        public string LockAccess { get; private set; }
        public int ProcessId { get; private set; }
        public InodeInfo InodeInfo { get; set; }


        private void FillFromProcFileSystem(string fileName, string line)
        {
            //  1: POSIX ADVISORY  READ  5433 08:01:7864448 128 128
            //  2: FLOCK ADVISORY  WRITE 2001 08:01:7864554 0 EOF
            //  3: FLOCK ADVISORY  WRITE 1568 00:2f:32388 0 EOF
            //  4: POSIX ADVISORY  WRITE 699 00:16:28457 0 EOF
            //  5: POSIX ADVISORY  WRITE 764 00:16:21448 0 0
            //  6: POSIX ADVISORY  READ  3548 08:01:7867240 1 1
            //  7: POSIX ADVISORY  READ  3548 08:01:7865567 1826 2335
            //  8: OFDLCK ADVISORY  WRITE -1 08:01:8713209 128 191

            string[] fields = line.Split(s_space, StringSplitOptions.RemoveEmptyEntries);

            // Actual number of fields in /proc/locks might be larger, but we only need
            // up to field #5 (INODE)
            if (fields.Length < 6)
            {
                throw new IOException($"Unexpected number of fields {fields.Length} in {fileName}: {line}");
            }

            LockType = fields[1];
            LockMode = fields[2];
            LockAccess = fields[3];

            if (!int.TryParse(fields[4], out int processId))
            {
                throw new IOException($"Invalid process ID in {fileName}: {line}");
            }

            ProcessId = processId;

            string[] inode = fields[5].Split(s_colon, StringSplitOptions.RemoveEmptyEntries);
            if (inode.Length != 3 ||
                !int.TryParse(inode[0], NumberStyles.HexNumber, null, out int major) ||
                !int.TryParse(inode[1], NumberStyles.HexNumber, null, out int minor) ||
                !long.TryParse(inode[2], NumberStyles.Integer, null, out long number))
            {
                throw new IOException($"Invalid inode specification in {fileName}: {line}");
            }

            InodeInfo = new InodeInfo(major, minor, number);
        }
    }

    internal struct InodeInfo
    {
        public InodeInfo(int majorDeviceId, int minorDevideId, long iNodeNumber)
        {
            MajorDeviceId = majorDeviceId;
            MinorDevideId = minorDevideId;
            INodeNumber = iNodeNumber;
        }

        public int MajorDeviceId { get; }
        public int MinorDevideId { get; }
        public long INodeNumber { get; }
    }
}
