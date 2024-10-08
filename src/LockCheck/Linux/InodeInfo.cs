using System;
using System.Globalization;

namespace LockCheck.Linux
{
    internal readonly struct InodeInfo
    {
        public static bool TryParse(ReadOnlySpan<char> field, out InodeInfo value)
        {
#if NETFRAMEWORK
            string[] inode = field.ToString().Split(new [] { ':' }, StringSplitOptions.RemoveEmptyEntries);

            if (inode.Length != 3 ||
                !int.TryParse(inode[0], NumberStyles.HexNumber, null, out int major) ||
                !int.TryParse(inode[1], NumberStyles.HexNumber, null, out int minor) ||
                !long.TryParse(inode[2], NumberStyles.Integer, null, out long number))
            {
                value = default;
                return false;
            }
#else
            int count = field.Count(':') + 1;
            Span<Range> ranges = count < 128 ? stackalloc Range[count] : new Range[count];
            int num = MemoryExtensions.Split(field, ranges, ':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (num < 3 ||
                !int.TryParse(field[ranges[0]], NumberStyles.HexNumber, null, out int major) ||
                !int.TryParse(field[ranges[1]], NumberStyles.HexNumber, null, out int minor) ||
                !long.TryParse(field[ranges[2]], NumberStyles.Integer, null, out long number))
            {
                value = default;
                return false;
            }
#endif
            value = new InodeInfo(major, minor, number);
            return true;
        }

        private InodeInfo(int majorDeviceId, int minorDeviceId, long iNodeNumber)
        {
            MajorDeviceId = majorDeviceId;
            MinorDeviceId = minorDeviceId;
            INodeNumber = iNodeNumber;
        }

        public readonly int MajorDeviceId { get; }
        public readonly int MinorDeviceId { get; }
        public readonly long INodeNumber { get; }
    }
}
