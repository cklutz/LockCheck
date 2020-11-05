using System;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace LockCheck.Linux
{
    internal static class NativeMethods
    {
        public const int EAGAIN = 11; // Resource unavailable, try again (same value as EWOULDBLOCK),
        public const int EWOULDBLOCK = EAGAIN; // Operation would block.
        public const int ERANGE = 34;

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

        public static bool TryGetUid(string path, out uint uid)
        {
            if (Stat(path, out var status) >= 0)
            {
                uid = status.Uid;
                return true;
            }

            uid = 0;
            return false;
        }

        public static string GetUserName(uint uid)
        {
            string userName;
            unsafe
            {
                // First try with a buffer that should suffice for 99% of cases.
                // Note that, theoretically, userHomeDirectory may be null in the success case
                // if we simply couldn't find a home directory for the current user.
                // In that case, we pass back the null value and let the caller decide
                // what to do.
                const int BufLen = Passwd.InitialBufferSize;
                byte* stackBuf = stackalloc byte[BufLen];
                if (TryGetUserName(uid, stackBuf, BufLen, out userName))
                    return userName;

                // Fallback to heap allocations if necessary, growing the buffer until
                // we succeed.  TryGetHomeDirectory will throw if there's an unexpected error.
                int lastBufLen = BufLen;
                while (true)
                {
                    lastBufLen *= 2;
                    byte[] heapBuf = new byte[lastBufLen];
                    fixed (byte* buf = &heapBuf[0])
                    {
                        if (TryGetUserName(uid, buf, heapBuf.Length, out userName))
                            return userName;
                    }
                }
            }
        }

        private static unsafe bool TryGetUserName(uint uid, byte* buf, int bufLen, out string userName)
        {
            // Call getpwuid_r to get the passwd struct
            Passwd passwd;
            int error = GetPwUidR(uid, out passwd, buf, bufLen);

            // If the call succeeds, give back the home directory path retrieved
            if (error == 0)
            {
                userName = Marshal.PtrToStringAnsi((IntPtr)passwd.Name);
                return true;
            }

            // If the current user's entry could not be found, give back null
            // name, but still return true as false indicates the buffer was
            // too small.
            if (error == -1)
            {
                userName = null;
                return true;
            }

            // If the call failed because the buffer was too small, return false to
            // indicate the caller should try again with a larger buffer.
            if (error == ERANGE)
            {
                userName = null;
                return false;
            }

            // Otherwise, fail.
            throw new IOException($"Couldn't get user name for '{uid}' (errno = {error})");
        }

        internal unsafe struct Passwd
        {
            internal const int InitialBufferSize = 256;

            internal byte* Name;
            internal byte* Password;
            internal uint UserId;
            internal uint GroupId;
            internal byte* UserInfo;
            internal byte* HomeDirectory;
            internal byte* Shell;
        }

        [DllImport(SystemNative, EntryPoint = "SystemNative_GetPwUidR", SetLastError = false)]
        internal static extern unsafe int GetPwUidR(uint uid, out Passwd pwd, byte* buf, int bufLen);
    }
}