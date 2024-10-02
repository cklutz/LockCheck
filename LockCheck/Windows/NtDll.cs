using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using static LockCheck.Windows.NativeMethods;

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
                using (var handle = NativeMethods.GetFileHandle(path))
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

        /// <summary>
        /// Gets the processes for which the current directory is the given directory, or a[n indirect] parent directory.
        /// If the resulting list is not empty, the given directory cannot be deleted, as it is locked by the processes
        /// returned.
        /// </summary>
        public static IEnumerable<int> GetProcessesWithCurrentDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return Array.Empty<int>();
            }

            var list = new List<int>();

            var processes = GetProcessIds();
            foreach (var id in processes)
            {
                string currentDirectory = GetCurrentDirectory(id);
                if (currentDirectory != null && currentDirectory.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(id);
                }
            }

            return list;
        }

        public static string GetCurrentDirectory(int processId)
        {
            try
            {
                return GetProcessParametersString(processId);
            }
            catch
            {
                return null;
            }
        }

        private static string GetProcessParametersString(int processId)
        {
            using var handle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, bInheritHandle: false, processId);
            if (handle.IsInvalid)
            {
                return null;
            }

            bool IsTargetWow64Process = GetProcessIsWow64(handle);
            bool IsTarget64BitProcess = Is64BitOperatingSystem && !IsTargetWow64Process;

            long offset = IsTarget64BitProcess ? 0x38 : 0x24;
            long processParametersOffset = IsTarget64BitProcess ? 0x20 : 0x10;

            long pebAddress = 0;
            if (IsTargetWow64Process) // OS: 64Bit, Current: 32 or 64, Target: 32bit
            {
                IntPtr peb32 = new IntPtr();

                int hr = NtQueryInformationProcess(handle, (int)PROCESSINFOCLASS.ProcessWow64Information, ref peb32, IntPtr.Size, IntPtr.Zero);
                if (hr != 0)
                {
                    return null;
                }

                pebAddress = peb32.ToInt64();

                IntPtr pp = new IntPtr();
                if (!ReadProcessMemory(handle, new IntPtr(pebAddress + processParametersOffset), ref pp, new IntPtr(Marshal.SizeOf(pp)), IntPtr.Zero))
                {
                    return null;
                }

                UNICODE_STRING_32 us = new UNICODE_STRING_32();
                if (!ReadProcessMemory(handle, new IntPtr(pp.ToInt64() + offset), ref us, new IntPtr(Marshal.SizeOf(us)), IntPtr.Zero))
                {
                    return null;
                }

                if ((us.Buffer == 0) || (us.Length == 0))
                {
                    return null;
                }

                string s = new string('\0', us.Length / 2);
                if (!ReadProcessMemory(handle, new IntPtr(us.Buffer), s, new IntPtr(us.Length), IntPtr.Zero))
                {
                    return null;
                }

                return s;
            }
            else if (IsCurrentProcessWOW64) // OS: 64Bit, Current: 32, Target: 64
            {
                // NtWow64QueryInformationProcess64 is an "undocumented API", see issue 1702
                PROCESS_BASIC_INFORMATION_WOW64 pbi = new PROCESS_BASIC_INFORMATION_WOW64();
                int hr = NtWow64QueryInformationProcess64(handle, (int)PROCESSINFOCLASS.ProcessBasicInformation, ref pbi, Marshal.SizeOf(pbi), IntPtr.Zero);
                if (hr != 0)
                {
                    return null;
                }

                pebAddress = pbi.PebBaseAddress;

                long pp = 0;
                hr = NtWow64ReadVirtualMemory64(handle, pebAddress + processParametersOffset, ref pp, Marshal.SizeOf(pp), IntPtr.Zero);
                if (hr != 0)
                {
                    return null;
                }

                UNICODE_STRING_WOW64 us = new UNICODE_STRING_WOW64();
                hr = NtWow64ReadVirtualMemory64(handle, pp + offset, ref us, Marshal.SizeOf(us), IntPtr.Zero);
                if (hr != 0)
                {
                    return null;
                }

                if ((us.Buffer == 0) || (us.Length == 0))
                {
                    return null;
                }

                string s = new string('\0', us.Length / 2);
                hr = NtWow64ReadVirtualMemory64(handle, us.Buffer, s, us.Length, IntPtr.Zero);
                if (hr != 0)
                {
                    return null;
                }

                return s;
            }
            else // OS, Current, Target: 64 or 32
            {
                PROCESS_BASIC_INFORMATION pbi = new PROCESS_BASIC_INFORMATION();
                int hr = NtQueryInformationProcess(handle, (int)PROCESSINFOCLASS.ProcessBasicInformation, ref pbi, Marshal.SizeOf(pbi), IntPtr.Zero);
                if (hr != 0)
                {
                    return null;
                }

                pebAddress = pbi.PebBaseAddress.ToInt64();

                IntPtr pp = new IntPtr();
                if (!ReadProcessMemory(handle, new IntPtr(pebAddress + processParametersOffset), ref pp, new IntPtr(Marshal.SizeOf(pp)), IntPtr.Zero))
                {
                    return null;
                }

                UNICODE_STRING us = new UNICODE_STRING();
                if (!ReadProcessMemory(handle, new IntPtr((long)pp + offset), ref us, new IntPtr(Marshal.SizeOf(us)), IntPtr.Zero))
                {
                    return null;
                }

                if (us.Buffer == IntPtr.Zero || us.Length == 0)
                {
                    return null;
                }

                string s = new string('\0', us.Length / 2);
                if (!ReadProcessMemory(handle, us.Buffer, s, new IntPtr(us.Length), IntPtr.Zero))
                {
                    return null;
                }

                return s;
            }
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