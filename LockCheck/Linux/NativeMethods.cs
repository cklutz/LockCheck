using System;
using System.Data;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace LockCheck.Linux
{
    internal static class NativeMethods
    {

        public const int EAGAIN = 11; // Resource unavailable, try again (same value as EWOULDBLOCK),
        public const int EWOULDBLOCK = EAGAIN; // Operation would block.

        // Copied from github.com/dotnet/runtime, MIT licensed.
        // ------------------------------------------------------------------------------------
        private const string SystemNative = "System.Native";

        [StructLayout(LayoutKind.Sequential)]
        internal struct FileStatus
        {
            internal int Flags;
            internal int Mode;
            internal uint Uid;
            internal uint Gid;
            internal long Size;
            internal long ATime;
            internal long ATimeNsec;
            internal long MTime;
            internal long MTimeNsec;
            internal long CTime;
            internal long CTimeNsec;
            internal long BirthTime;
            internal long BirthTimeNsec;
            internal long Dev;
            internal long Ino;
            internal uint UserFlags;
        }

        [DllImport(SystemNative, EntryPoint = "SystemNative_Stat", SetLastError = true)]
        private static extern int Stat(string pathname, out FileStatus status);

        public static long GetInode(string path)
        {
            if (Stat(path, out var status) >= 0)
            {
                return status.Ino;
            }

            return -1;
        }

        [DllImport(SystemNative, EntryPoint = "SystemNative_ReadLink", SetLastError = true)]
        private static extern unsafe int ReadLink(string path, byte[] buffer, int bufferSize);

        public static string ReadLink(string path)
        {
            int bufferSize = 256;
            while (true)
            {
                byte[] buffer = new byte[bufferSize];

                int resultLength = ReadLink(path, buffer, buffer.Length);
                if (resultLength < 0)
                {
                    // error
                    return null;
                }
                else if (resultLength < buffer.Length)
                {
                    // success
                    return Encoding.UTF8.GetString(buffer, 0, resultLength);
                }

                // buffer was too small, loop around again and try with a larger buffer.
                bufferSize *= 2;
            }
        }
    }
}