using System;
using System.IO;

namespace LockCheck.Linux
{
    internal class LockInfo
    {
        public string LockType { get; private set; }
        public string LockMode { get; private set; }
        public string LockAccess { get; private set; }
        public int ProcessId { get; private set; }
        public InodeInfo InodeInfo { get; set; }

        public static LockInfo ParseLine(string line)
        {
            // Each line has 8 (sometimes 9, see '->') blocks/fields:
            //
            //  1: POSIX ADVISORY  READ  5433 08:01:7864448 128 128
            //  2: FLOCK ADVISORY  WRITE 2001 08:01:7864554 0 EOF
            //  3: FLOCK ADVISORY  WRITE 1568 00:2f:32388 0 EOF
            //  4: POSIX ADVISORY  WRITE 699 00:16:28457 0 EOF
            //  5: POSIX ADVISORY  WRITE 764 00:16:21448 0 0
            //  5: -> POSIX ADVISORY  WRITE 766 00:16:21448 0 0
            //  6: POSIX ADVISORY  READ  3548 08:01:7867240 1 1
            //  7: POSIX ADVISORY  READ  3548 08:01:7865567 1826 2335
            //  8: OFDLCK ADVISORY  WRITE -1 08:01:8713209 128 191

            string[] fields = line.Split(new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // Actual number of fields in /proc/locks might be larger, but we only need
            // up to field #5 (INODE)
            if (fields.Length < 6)
            {
                throw new IOException($"Unexpected number of fields {fields.Length} in '/proc/locks'");
            }

            int offset = 0; // Always the "ID" (e.g. "1:")
            offset++;
            if (fields[offset] == "->")
            {
                // "Blocked" optional marker
                offset++;
            }

            var result = new LockInfo
            {
                LockType = fields[offset++],
                LockMode = fields[offset++],
                LockAccess = fields[offset++]
            };

            if (!int.TryParse(fields[offset++], out int processId))
            {
                throw new IOException($"Invalid process ID '{fields[offset]}' in '/proc/locks'");
            }

            result.ProcessId = processId;

            if (!InodeInfo.TryParse(fields[offset++], out var inodeInfo))
            {
                throw new IOException($"Invalid inode '{fields[offset]}' specification in '/proc/locks'");
            }

            result.InodeInfo = inodeInfo;

            return result;
        }
    }
}
