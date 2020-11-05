using System;
using System.Globalization;

namespace LockCheck.Linux
{
    internal struct InodeInfo
    {
        public static bool TryParse(string field, out InodeInfo value)
        {
            string[] inode = field.Split(new [] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            if (inode.Length != 3 ||
                !int.TryParse(inode[0], NumberStyles.HexNumber, null, out int major) ||
                !int.TryParse(inode[1], NumberStyles.HexNumber, null, out int minor) ||
                !long.TryParse(inode[2], NumberStyles.Integer, null, out long number))
            {
                value = default;
                return false;
            }

            value = new InodeInfo(major, minor, number);
            return true;
        }

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
