using System;
using System.Globalization;

namespace LockCheck.Linux
{
    internal readonly struct InodeInfo
    {
        public static bool TryParse(ReadOnlySpan<char> field, out InodeInfo value)
        {
#if NET9_0_OR_GREATER
            // This implementation *look* more complex and thus slower, but is actually faster:
            //
            // | Method | Mean     | Error    | StdDev   | Ratio | RatioSD | Code Size | Allocated | Alloc Ratio |
            // |------- |---------:|---------:|---------:|------:|--------:|----------:|----------:|------------:|
            // | Old    | 71.97 ns | 1.462 ns | 1.900 ns |  1.00 |    0.04 |   1,604 B |         - |          NA |
            // | NET9.0 | 51.40 ns | 0.800 ns | 0.668 ns |  0.71 |    0.02 |   1,355 B |         - |          NA |

            int num = 0;
            int major = 0;
            int minor = 0;
            long number = 0;

            foreach (var range in field.Split(':'))
            {
                var fieldContent = field[range].Trim();

                switch (num)
                {
                    case 0:
                        if (!int.TryParse(fieldContent, NumberStyles.HexNumber, null, out major))
                        {
                            goto fail;
                        }
                        break;
                    case 1:
                        if (!int.TryParse(fieldContent, NumberStyles.HexNumber, null, out minor))
                        {
                            goto fail;
                        }
                        break;
                    case 2:
                        if (!long.TryParse(fieldContent, NumberStyles.Integer, null, out number))
                        {
                            goto fail;
                        }
                        break;
                }

                if (++num > 2)
                {
                    // Ignore additional fields
                    break;
                }
            }

            if (num < 2)
            {
                // Not enough fields
                goto fail;
            }

            value = new InodeInfo(major, minor, number);
            return true;
fail:
            value = default;
            return false;
#else
            int count = field.Count(':') + 1;
            Span<Range> ranges = count < 128 ? stackalloc Range[count] : new Range[count];
            int num = MemoryExtensions.Split(field, ranges, ':', StringSplitOptions.TrimEntries);
            if (num < 3 ||
                !int.TryParse(field[ranges[0]], NumberStyles.HexNumber, null, out int major) ||
                !int.TryParse(field[ranges[1]], NumberStyles.HexNumber, null, out int minor) ||
                !long.TryParse(field[ranges[2]], NumberStyles.Integer, null, out long number))
            {
                value = default;
                return false;
            }

            value = new InodeInfo(major, minor, number);
            return true;
#endif
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
