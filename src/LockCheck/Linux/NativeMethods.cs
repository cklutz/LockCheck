using System;
using System.Buffers;
using System.IO;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;

#pragma warning disable IDE1006 // Naming Styles - off here, because we want to use native names

namespace LockCheck.Linux
{
    internal static partial class NativeMethods
    {
        public const int EAGAIN = 11; // Resource unavailable, try again (same value as EWOULDBLOCK),
        public const int EACCES = 13; // Mandatory lock
        public const int EWOULDBLOCK = EAGAIN; // Operation would block.
        public const int ERANGE = 34;
       
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
                const int BufLen = Passwd.InitialBufferSize;
                byte* stackBuf = stackalloc byte[BufLen];
                if (TryGetUserName(uid, stackBuf, BufLen, out userName))
                {
                    return userName;
                }

                int lastBufLen = BufLen;
                while (true)
                {
                    lastBufLen *= 2;
                    byte[] heapBuf = new byte[lastBufLen];
                    fixed (byte* buf = &heapBuf[0])
                    {
                        if (TryGetUserName(uid, buf, heapBuf.Length, out userName))
                        {
                            return userName;
                        }
                    }
                }
            }
        }

        private static unsafe bool TryGetUserName(uint uid, byte* buf, int bufLen, out string userName)
        {
            int error = GetPwUidR(uid, out Passwd passwd, buf, bufLen);

            if (error == 0)
            {
                userName = Marshal.PtrToStringAnsi((IntPtr)passwd.Name);
                return true;
            }

            if (error == -1)
            {
                userName = null;
                return true;
            }

            if (error == ERANGE)
            {
                userName = null;
                return false;
            }

            throw new IOException($"Couldn't get user name for '{uid}' (errno = {error})");
        }

        // Copied (and modified) from github.com/dotnet/runtime, MIT licensed.
        // ---BEGIN---------------------------------------------------------------------------------

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
            internal long RDev;
            internal long Ino;
            internal uint UserFlags;
        }

        [LibraryImport(SystemNative, EntryPoint = "SystemNative_Stat", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
        private static partial int Stat(string pathname, out FileStatus status);

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

        [LibraryImport(SystemNative, EntryPoint = "SystemNative_GetPwUidR", SetLastError = false)]
        internal static unsafe partial int GetPwUidR(uint uid, out Passwd pwd, byte* buf, int bufLen);

        [LibraryImport(SystemNative, EntryPoint = "SystemNative_ReadLink", SetLastError = true)]
        private static partial int ReadLink(ref byte path, ref byte buffer, int bufferSize);

        internal static string ReadLink(ReadOnlySpan<char> path)
        {
            const int StackBufferSize = 256;

            Span<byte> spanBuffer = stackalloc byte[StackBufferSize];
            byte[] arrayBuffer = null;

            // Convert path to UTF-8 bytes, zero terminated - CLR code used internal ValueUtf8Converter for this.
            int maxSize = checked(Encoding.UTF8.GetMaxByteCount(path.Length) + 1);
            Span<byte> pathBytes = maxSize <= StackBufferSize ? stackalloc byte[maxSize] : new byte[maxSize];
            int pathBytesCount = Encoding.UTF8.GetBytes(path, pathBytes);
            pathBytes[pathBytesCount] = 0;
            pathBytes = pathBytes.Slice(0, pathBytesCount + 1);
            ref byte pathReference = ref MemoryMarshal.GetReference(pathBytes);

            while (true)
            {
                int error = 0;
                try
                {
                    int resultLength = ReadLink(ref pathReference, ref MemoryMarshal.GetReference(spanBuffer), spanBuffer.Length);

                    if (resultLength < 0)
                    {
                        // error
                        error = Marshal.GetLastPInvokeError();
                        return null;
                    }
                    else if (resultLength < spanBuffer.Length)
                    {
                        // success
                        return Encoding.UTF8.GetString(spanBuffer.Slice(0, resultLength));
                    }
                }
                finally
                {
                    if (arrayBuffer != null)
                    {
                        ArrayPool<byte>.Shared.Return(arrayBuffer);
                    }

                    if (error > 0)
                    {
                        Marshal.SetLastPInvokeError(error);
                    }
                }

                // Output buffer was too small, loop around again and try with a larger buffer.
                arrayBuffer = ArrayPool<byte>.Shared.Rent(spanBuffer.Length * 2);
                spanBuffer = arrayBuffer;
            }
        }

        // ---END-----------------------------------------------------------------------------------
    }
}
