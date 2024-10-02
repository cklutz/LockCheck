using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Xml.Schema;

namespace LockCheck.Windows
{
    internal static class NtDll
    {
        public static HashSet<ProcessInfo> GetLockingProcessInfos(List<string> paths, ref List<string> directories)
        {
            if (paths == null)
                throw new ArgumentNullException(nameof(paths));

            var result = new HashSet<ProcessInfo>();

            foreach (string path in paths)
            {
                if (Directory.Exists(path))
                {
                    directories?.Add(path);
                    continue;
                }

                GetLockingProcessInfo(path, result);
            }

            return result;
        }

        private static void GetLockingProcessInfo(string path, HashSet<ProcessInfo> result)
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

        internal static Dictionary<(int, DateTime), ProcessInfo> GetProcessesByWorkingDirectory(List<string> directories)
        {
            return EnumerateSystemProcesses(null, directories,
                static (dirs, currentPtr, idx, pi) =>
                {
                    var peb = new Peb(pi);
                    if (!peb.HasError && 
                        !string.IsNullOrEmpty(peb.CurrentDirectory) &&
                        dirs.Find(d => d.StartsWith(peb.CurrentDirectory, StringComparison.OrdinalIgnoreCase)) != null)
                    {
                        return (ProcessInfo)ProcessInfoWindows.Create(peb, pi);
                    }

                    return null;
                });
        }

        private static long[] s_cachedBuffer;

        internal static Dictionary<(int, DateTime), T> EnumerateSystemProcesses<T, TData>(
            HashSet<int> processIds,
            TData data,
            Func<TData, IntPtr, int, NativeMethods.SYSTEM_PROCESS_INFORMATION, T> newEntry)
        {
            var processInfos = new Dictionary<(int, DateTime), T>();
            var bufferHandle = new GCHandle();

            // Start with the default buffer size.
            int bufferSize = 4096 * 128;

            // Get the cached buffer.
            long[] buffer = Interlocked.Exchange(ref s_cachedBuffer, null);

            try
            {
                // Retry until we get all the data
                int status;
                do
                {
                    if (buffer == null)
                    {
                        // Allocate buffer of longs since some platforms require the buffer to be 64-bit aligned.
                        buffer = new long[(bufferSize + 7) / 8];
                    }
                    else
                    {
                        // If we have cached buffer, set the size properly.
                        bufferSize = buffer.Length * sizeof(long);
                    }
                    bufferHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

                    status = NativeMethods.NtQuerySystemInformation(
                        NativeMethods.NtQuerySystemProcessInformation,
                        bufferHandle.AddrOfPinnedObject(),
                        bufferSize,
                        out int requiredSize);

                    if ((uint)status == NativeMethods.STATUS_INFO_LENGTH_MISMATCH)
                    {
                        if (bufferHandle.IsAllocated)
                        {
                            bufferHandle.Free();
                        }

                        buffer = null;
                        bufferSize = GetNewBufferSize(bufferSize, requiredSize);
                    }
                } while ((uint)status == NativeMethods.STATUS_INFO_LENGTH_MISMATCH);

                if (status < 0)
                {
                    throw GetException((uint)status, "NtQuerySystemInformation", "Could not get process info");
                }

                // Parse the data block to get process information
                IntPtr dataPtr = bufferHandle.AddrOfPinnedObject();

                long totalOffset = 0;
                int count = 0;

                while (true)
                {
                    IntPtr currentPtr = (IntPtr)((long)dataPtr + totalOffset);
                    var pi = new NativeMethods.SYSTEM_PROCESS_INFORMATION();

                    Marshal.PtrToStructure(currentPtr, pi);

                    int pid = pi.UniqueProcessId.ToInt32();
                    if (processIds == null || processIds.Contains(pid))
                    {
                        var startTime = DateTime.FromFileTime(pi.CreateTime);
                        var entry = newEntry(data, currentPtr, count, pi);
                        if (entry != null)
                        {
                            processInfos.Add((pid, startTime), entry);
                        }
                    }

                    if (pi.NextEntryOffset == 0)
                    {
                        break;
                    }
                    totalOffset += pi.NextEntryOffset;
                    count++;
                }
            }
            finally
            {
                // Cache the final buffer for use on the next call.
                Interlocked.Exchange(ref s_cachedBuffer, buffer);

                if (bufferHandle.IsAllocated)
                {
                    bufferHandle.Free();
                }
            }

            return processInfos;

            static int GetNewBufferSize(int existingBufferSize, int requiredSize)
            {
                if (requiredSize == 0)
                {
                    //
                    // On some old OS like win2000, requiredSize will not be set if the buffer
                    // passed to NtQuerySystemInformation is not enough.
                    //
                    int newSize = existingBufferSize * 2;
                    if (newSize < existingBufferSize)
                    {
                        // In reality, we should never overflow.
                        // Adding the code here just in case it happens.
                        throw new OutOfMemoryException();
                    }
                    return newSize;
                }
                else
                {
                    // allocating a few more kilo bytes just in case there are some new process
                    // kicked in since new call to NtQuerySystemInformation
                    int newSize = requiredSize + 1024 * 10;
                    if (newSize < requiredSize)
                    {
                        throw new OutOfMemoryException();
                    }
                    return newSize;
                }
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

    public class Peb
    {
        private bool hasError;

        public int ProcessId { get; private set; }
        public string CommandLine { get; private set; }
        public string CurrentDirectory { get; private set; }
        public string WindowTitle { get; private set; }
        public string ExecutableFullPath { get; private set; }
        public string DesktopInfo { get; private set; }

        public bool HasError
        {
            get => hasError;
            set
            {
                if (!hasError)
                {
                    hasError = value;
                }
            }
        }

        private bool StillOK
        {
            set
            {
                HasError = !value;
            }
        }

        internal Peb(NativeMethods.SYSTEM_PROCESS_INFORMATION pi)
        {
            ProcessId = (int)pi.UniqueProcessId;

            using var process = NativeMethods.OpenProcessRead(ProcessId);

            if (process.IsInvalid)
            {
                HasError = true;
                return;
            }

            // Need to check if either the current process, or the target process is 32bit or 64bit.
            // Additionally, if it is a 32bit process on a 64bit OS (WOW64).

            bool os64 = Environment.Is64BitOperatingSystem;
            bool self64 = Environment.Is64BitProcess;
            bool target64 = false;

            if (os64)
            {
                if (!NativeMethods.IsWow64Process(process, out bool isWow64Target))
                {
                    HasError = true;
                    return;
                }

                target64 = !isWow64Target;
            }

            var offsets = NativeMethods.PebOffsets.Get(target64);

            if (os64)
            {
                if (!target64)
                {
                    // os: 64bit, self: any, target: 32bit
                    FillTargetIs32BitProcess(process, offsets, this);
                }
                else if (!self64)
                {
                    // os: 64bit, self: 32bit, target: 64bit
                    FillTargetIs64BitProcess(process, offsets, this);
                }
                else
                {
                    // os: 64bit, self: 64bit, target: 64bit
                    FillTargetIsAnyProcess(process, offsets, this);
                }
            }
            else
            {
                // os: 32bit, self: 32bit, target: 32bit
                FillTargetIsAnyProcess(process, offsets, this);
            }
        }

        private static void FillTargetIsAnyProcess(SafeProcessHandle handle, NativeMethods.PebOffsets offsets, Peb peb)
        {
            var pbi = new NativeMethods.PROCESS_BASIC_INFORMATION();
            if (NativeMethods.NtQueryInformationProcess(handle, NativeMethods.PROCESS_INFORMATION_CLASS.ProcessBasicInformation, ref pbi, Marshal.SizeOf(pbi), IntPtr.Zero) == NativeMethods.STATUS_SUCCESS)
            {
                var pp = new IntPtr();
                if (NativeMethods.ReadProcessMemory(handle, new IntPtr(pbi.PebBaseAddress.ToInt64() + offsets.ProcessParametersOffset), ref pp, new IntPtr(Marshal.SizeOf(pp)), IntPtr.Zero))
                {
                    (peb.StillOK, peb.CommandLine) = TryGetString(handle, pp, offsets.CommandLineOffset);
                    (peb.StillOK, peb.CurrentDirectory) = TryGetString(handle, pp, offsets.CurrentDirectoryOffset);
                    (peb.StillOK, peb.ExecutableFullPath) = TryGetString(handle, pp, offsets.ImagePathNameOffset);
                    (peb.StillOK, peb.WindowTitle) = TryGetString(handle, pp, offsets.WindowTitleOffset);
                    (peb.StillOK, peb.DesktopInfo) = TryGetString(handle, pp, offsets.DesktopInfoOffset);
                }
            }
        }


        private static void FillTargetIs64BitProcess(SafeProcessHandle handle, NativeMethods.PebOffsets offsets, Peb peb)
        {
            var pbi = new NativeMethods.PROCESS_BASIC_INFORMATION_WOW64();
            if (NativeMethods.NtWow64QueryInformationProcess64(handle, NativeMethods.PROCESS_INFORMATION_CLASS.ProcessBasicInformation, ref pbi, Marshal.SizeOf(pbi), IntPtr.Zero) != NativeMethods.STATUS_SUCCESS)
            {
                long pp = 0;
                if (NativeMethods.NtWow64ReadVirtualMemory64(handle, pbi.PebBaseAddress + offsets.ProcessParametersOffset, ref pp, Marshal.SizeOf(pp), IntPtr.Zero) != NativeMethods.STATUS_SUCCESS)
                {
                    (peb.StillOK, peb.CommandLine) = TryGetWow64String(handle, pp, offsets.CommandLineOffset);
                    (peb.StillOK, peb.CurrentDirectory) = TryGetWow64String(handle, pp, offsets.CurrentDirectoryOffset);
                    (peb.StillOK, peb.ExecutableFullPath) = TryGetWow64String(handle, pp, offsets.ImagePathNameOffset);
                    (peb.StillOK, peb.WindowTitle) = TryGetWow64String(handle, pp, offsets.WindowTitleOffset);
                    (peb.StillOK, peb.DesktopInfo) = TryGetWow64String(handle, pp, offsets.DesktopInfoOffset);
                }
            }
        }

        private static void FillTargetIs32BitProcess(SafeProcessHandle handle, NativeMethods.PebOffsets offsets, Peb peb)
        {
            var pbi = new NativeMethods.PROCESS_BASIC_INFORMATION();
            if (NativeMethods.NtQueryInformationProcess(handle, NativeMethods.PROCESS_INFORMATION_CLASS.ProcessBasicInformation, ref pbi, Marshal.SizeOf(pbi), IntPtr.Zero) != NativeMethods.STATUS_SUCCESS)
            {
                // A 32bit process on a 64bit OS has a separate PEB structure.
                var peb32 = new IntPtr();
                if (NativeMethods.NtQueryInformationProcessWow64(handle, NativeMethods.PROCESS_INFORMATION_CLASS.ProcessWow64Information, ref peb32, IntPtr.Size, IntPtr.Zero) != NativeMethods.STATUS_SUCCESS)
                {
                    var pp = new IntPtr();
                    if (NativeMethods.ReadProcessMemory(handle, new IntPtr(peb32.ToInt64() + offsets.ProcessParametersOffset), ref pp, new IntPtr(Marshal.SizeOf(pp)), IntPtr.Zero))
                    {
                        (peb.StillOK, peb.CommandLine) = TryGetString32(handle, pp, offsets.CommandLineOffset);
                        (peb.StillOK, peb.CurrentDirectory) = TryGetString32(handle, pp, offsets.CurrentDirectoryOffset);
                        (peb.StillOK, peb.ExecutableFullPath) = TryGetString32(handle, pp, offsets.ImagePathNameOffset);
                        (peb.StillOK, peb.WindowTitle) = TryGetString32(handle, pp, offsets.WindowTitleOffset);
                        (peb.StillOK, peb.DesktopInfo) = TryGetString32(handle, pp, offsets.DesktopInfoOffset);
                    }
                }
            }
        }

        private static (bool, IntPtr) TryGetIntPtr32(SafeProcessHandle handle, IntPtr pp, int offset)
        {
            var ptr = IntPtr.Zero;
            if (NativeMethods.ReadProcessMemory(handle, pp + offset, ref ptr, new IntPtr(sizeof(int)), IntPtr.Zero))
            {
                return (true, ptr);
            }

            return (false, ptr);
        }

        private static (bool, string) TryGetString32(SafeProcessHandle handle, IntPtr pp, int offset)
        {
            string str = null;
            var us = new NativeMethods.UNICODE_STRING_32();
            if (NativeMethods.ReadProcessMemory(handle, pp + offset, ref us, new IntPtr(Marshal.SizeOf(us)), IntPtr.Zero))
            {
                if ((us.Buffer != 0) && (us.Length != 0))
                {
                    string lpBuffer = us.GetLpBuffer();
                    if (NativeMethods.ReadProcessMemory(handle, new IntPtr(us.Buffer), lpBuffer, new IntPtr(us.Length), IntPtr.Zero))
                    {
                        str = lpBuffer;
                        return (true, str);
                    }
                }
            }

            return (false, str);
        }

        private static (bool, IntPtr) TryGetWow64IntPtr(SafeProcessHandle handle, long pp, int offset)
        {
            var ptr = IntPtr.Zero;
            uint buf = 0;
            if (NativeMethods.NtWow64ReadVirtualMemory64(handle, pp + offset, ref buf, sizeof(uint), IntPtr.Zero) == NativeMethods.STATUS_SUCCESS)
            {
                ptr = new IntPtr(buf);
                return (true, ptr);
            }

            return (false, ptr);
        }

        private static (bool, string) TryGetWow64String(SafeProcessHandle handle, long pp, int offset)
        {
            string str = null;
            var us = new NativeMethods.UNICODE_STRING_WOW64();
            if (NativeMethods.NtWow64ReadVirtualMemory64(handle, pp + offset, ref us, Marshal.SizeOf(us), IntPtr.Zero) == NativeMethods.STATUS_SUCCESS)
            {
                if ((us.Buffer != 0) && (us.Length != 0))
                {
                    string lpBuffer = us.GetLpBuffer();
                    if (NativeMethods.NtWow64ReadVirtualMemory64(handle, us.Buffer, lpBuffer, us.Length, IntPtr.Zero) == NativeMethods.STATUS_SUCCESS)
                    {
                        str = lpBuffer;
                        return (true, str);
                    }
                }
            }

            return (false, str);
        }

        private static (bool, IntPtr) TryGetIntPtr(SafeProcessHandle handle, IntPtr pp, int offset)
        {
            var ptr = IntPtr.Zero;
            if (NativeMethods.ReadProcessMemory(handle, pp + offset, ref ptr, new IntPtr(IntPtr.Size), IntPtr.Zero))
            {
                return (true, ptr);
            }

            return (false, ptr);
        }

        private static (bool, string) TryGetString(SafeProcessHandle handle, IntPtr pp, int offset)
        {
            string str = null;
            var us = new NativeMethods.UNICODE_STRING();
            if (NativeMethods.ReadProcessMemory(handle, pp + offset, ref us, new IntPtr(Marshal.SizeOf(us)), IntPtr.Zero))
            {
                if ((us.Buffer != IntPtr.Zero) && (us.Length != 0))
                {
                    string lpBuffer = us.GetLpBuffer();
                    if (NativeMethods.ReadProcessMemory(handle, us.Buffer, lpBuffer, new IntPtr(us.Length), IntPtr.Zero))
                    {
                        str = lpBuffer;
                        return (true, str);
                    }
                }
            }
            return (false, str);
        }
    }
}