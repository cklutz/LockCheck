using System;
using System.IO;

namespace LockCheck.Linux;

internal class LockInfo
{
    public string? LockType { get; private set; }
    public string? LockMode { get; private set; }
    public string? LockAccess { get; private set; }
    public int ProcessId { get; private set; }
    public InodeInfo InodeInfo { get; private set; }

    public static LockInfo ParseLine(string line)
    {
        // Each line has 8 (or 9 if you count the "->" marker) fields separated by spaces.
        // Additional fields after that are possible, but can be ignored.
        // The values for the LockType, LockMode, and LockAccess fields are manifold.
        // So we don't interpret them, but just store them as strings. They are only
        // provided as informational values anyway and not used for program logic.
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
        //
        // The major:minor device numbers (e.g. 08:01:...) might not be actual inode numbers
        // in case the respective FS is not a physical one (e.g. not ext4, but tempfs or procfs).
        // https://utcc.utoronto.ca/~cks/space/blog/linux/ProcLocksNotes has a nice explanation.
        // In our case we don't care, because calling code looks up "in reverse" anyway. That is,
        // it has an inode for an actual file/directory and needs this information to see if it
        // is "locked".

        var span = line.AsSpan();
        int count = span.Count(' ') + 1;
        if (count < 6)
        {
            throw new IOException($"Unexpected number of fields {count} in '/proc/locks' ({line})");
        }

        Span<Range> ranges = count < 128 ? stackalloc Range[count] : new Range[count];
        int num = MemoryExtensions.Split(span, ranges, ' ', StringSplitOptions.RemoveEmptyEntries);

        int offset = 0;
        offset++; // Ignore first item (always the "ID" (e.g. "1:")
        if (span[ranges[offset]] == "->")
        {
            // "Blocked" optional marker
            offset++;
        }

        var result = new LockInfo
        {
            LockType = span[ranges[offset++]].ToString(),
            LockMode = span[ranges[offset++]].ToString(),
            LockAccess = span[ranges[offset++]].ToString()
        };

        if (!int.TryParse(span[ranges[offset++]], out int processId))
        {
            throw new IOException($"Invalid process ID '{span[ranges[offset]]}' in '/proc/locks' ({line})");
        }

        result.ProcessId = processId;

        if (!InodeInfo.TryParse(span[ranges[offset++]], out var inodeInfo))
        {
            throw new IOException($"Invalid Inode '{span[ranges[offset]]}' specification in '/proc/locks' ({line})");
        }

        result.InodeInfo = inodeInfo;

        return result;
    }
}
