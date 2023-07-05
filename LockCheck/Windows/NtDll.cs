using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace LockCheck.Windows
{
    internal static class NtDll
    {
        public static IEnumerable<ProcessInfo> GetLockingProcessInfos(params string[] paths)
        {
            if (paths == null)
                throw new ArgumentNullException(nameof(paths));

            var result = new List<ProcessInfo>();

            foreach (string path in paths)
            {
                GetLockingProcessInfo(path, result);
            }

            return result;
        }

        private static void GetLockingProcessInfo(string path, List<ProcessInfo> result)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            var bufferPtr = IntPtr.Zero;
            var statusBlock = new NativeMethods.IO_STATUS_BLOCK();

            try
            {
                using (var handle = GetFileOrDirectoryHandle(path))
                {
                    if (handle.IsInvalid)
                    {
                        // The file does not exist or is gone already. Could be a race condition. There is nothing we can contribute.
                        // Doing this, exhibits the same behavior as the RestartManager implementation.
                        return;
                    }

                    uint bufferSize = 0x4000;
                    bufferPtr = Marshal.AllocHGlobal((int)bufferSize);

                    uint status;
                    while ((status = NativeMethods.NtQueryInformationFile(handle,
                        ref statusBlock, bufferPtr, bufferSize,
                        NativeMethods.FILE_INFORMATION_CLASS.FileProcessIdsUsingFileInformation))
                        == NativeMethods.STATUS_INFO_LENGTH_MISMATCH)
                    {
                        Marshal.FreeHGlobal(bufferPtr);
                        bufferPtr = IntPtr.Zero;
                        bufferSize *= 2;
                        bufferPtr = Marshal.AllocHGlobal((int)bufferSize);
                    }

                    if (status != NativeMethods.STATUS_SUCCESS)
                    {
                        throw GetException(status, "NtQueryInformationFile", "Failed to get file process IDs");
                    }

                    // Buffer contains:
                    //    struct FILE_PROCESS_IDS_USING_FILE_INFORMATION
                    //    {
                    //        ULONG NumberOfProcessIdsInList;
                    //        ULONG_PTR ProcessIdList[1];
                    //    }

                    var readBuffer = bufferPtr;
                    int numEntries = Marshal.ReadInt32(readBuffer); // NumberOfProcessIdsInList
                    readBuffer += IntPtr.Size;

                    for (int i = 0; i < numEntries; i++)
                    {
                        int processId = Marshal.ReadIntPtr(readBuffer).ToInt32(); // A single ProcessIdList[] element
                        result.Add(ProcessInfoWindows.Create(processId));
                        readBuffer += IntPtr.Size;
                    }
                }
            }
            finally
            {
                if (bufferPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(bufferPtr);
                }
            }
        }

        private static Exception GetException(uint status, string apiName, string message)
        {
            int res = NativeMethods.RtlNtStatusToDosError(status);
            if (res == NativeMethods.ERROR_MR_MID_NOT_FOUND)
            {
                return new NtException(status, $"{message} ({apiName}() status {status} (0x{status:8X})");
            }

            return new NtException(res, status, $"{message} ({apiName}() status {status} (0x{status:8X})");
        }

        private static SafeFileHandle GetFileOrDirectoryHandle(string path)
        {
            return Directory.Exists(path)
                ? NativeMethods.GetDirectoryHandle(path)
                : NativeMethods.GetFileHandle(path);
        }
    }

    public class NtException : Win32Exception
    {
        public NtException(uint status, string message)
            : base(message)
        {
            HResult = unchecked((int)status);
        }

        public NtException(int error, uint status, string message)
            : base(error, message)
        {
            HResult = unchecked((int)status);
        }
    }
}