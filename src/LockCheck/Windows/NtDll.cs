using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;

using static LockCheck.Windows.NativeMethods;

namespace LockCheck.Windows
{
    internal static class NtDll
    {
        public static HashSet<ProcessInfo> GetLockingProcessInfos(string[] paths, ref List<string> directories)
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
            var statusBlock = new IO_STATUS_BLOCK();

            try
            {
                using (var handle = GetFileHandle(path))
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
                    while ((status = NtQueryInformationFile(handle, ref statusBlock, bufferPtr, bufferSize, FILE_INFORMATION_CLASS.FileProcessIdsUsingFileInformation)) == STATUS_INFO_LENGTH_MISMATCH)
                    {
                        Marshal.FreeHGlobal(bufferPtr);
                        bufferPtr = IntPtr.Zero;
                        bufferSize *= 2;
                        bufferPtr = Marshal.AllocHGlobal((int)bufferSize);
                    }

                    if (status != STATUS_SUCCESS)
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
            int res = RtlNtStatusToDosError(status);
            if (res == ERROR_MR_MID_NOT_FOUND)
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

                    if (!peb.HasError && !string.IsNullOrEmpty(peb.CurrentDirectory))
                    {
                        // If the process' current directory is the search path itself, or it is somewhere nested below it,
                        // we have to take it into account. This will also account for differences in the two when the
                        // search path does not end with a '\', but the PEB's current directory does (which is always the
                        // case).
                        if (dirs.FindIndex(d => peb.CurrentDirectory.StartsWith(d, StringComparison.OrdinalIgnoreCase)) != -1)
                        {
                            return (ProcessInfo)ProcessInfoWindows.Create(peb);
                        }
                    }

                    return null;
                });
        }

        private static long[] s_cachedBuffer;

        internal static Dictionary<(int, DateTime), T> EnumerateSystemProcesses<T, TData>(
            HashSet<int> processIds,
            TData data,
            Func<TData, IntPtr, int, SYSTEM_PROCESS_INFORMATION, T> newEntry)
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

                    status = NtQuerySystemInformation(
                        NtQuerySystemProcessInformation,
                        bufferHandle.AddrOfPinnedObject(),
                        bufferSize,
                        out int requiredSize);

                    if ((uint)status == STATUS_INFO_LENGTH_MISMATCH)
                    {
                        if (bufferHandle.IsAllocated)
                        {
                            bufferHandle.Free();
                        }

                        buffer = null;
                        bufferSize = GetNewBufferSize(bufferSize, requiredSize);
                    }
                } while ((uint)status == STATUS_INFO_LENGTH_MISMATCH);

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
                    var pi = new SYSTEM_PROCESS_INFORMATION();

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

    public interface ICanHaveError
    {
        void SetError();
        bool HasError { get; }
    }

    [DebuggerDisplay("{HasError} {ProcessId} {ExecutableFullPath}")]
    public class Peb : ICanHaveError
    {
        private bool _hasError;
#if DEBUG
        private string _errorStack;
#endif

        public int ProcessId { get; private set; }
        public int SessionId { get; private set; }
        public string CommandLine { get; private set; }
        public string CurrentDirectory { get; private set; }
        public string WindowTitle { get; private set; }
        public string ExecutableFullPath { get; private set; }
        public string DesktopInfo { get; private set; }

        public string Owner { get; private set; }
        public DateTime StartTime { get; private set; }

        public bool HasError => _hasError;

        public void SetError()
        {
            if (!_hasError)
            {
                _hasError = true;
#if DEBUG
                _errorStack = Environment.StackTrace;
#endif
            }
        }

        internal Peb(SYSTEM_PROCESS_INFORMATION pi)
        {
            ProcessId = (int)pi.UniqueProcessId;

            using var process = OpenProcessRead(ProcessId);

            if (process.IsInvalid)
            {
                SetError();
                return;
            }

            // Need to check if either the current process, or the target process is 32bit or 64bit.
            // Additionally, if it is a 32bit process on a 64bit OS (WOW64).

            bool os64 = Environment.Is64BitOperatingSystem;
            bool self64 = Environment.Is64BitProcess;
            bool target64 = false;

            if (os64)
            {
                if (!IsWow64Process(process, out bool isWow64Target))
                {
                    SetError();
                    return;
                }

                target64 = !isWow64Target;
            }

            var offsets = PebOffsets.Get(target64);

            if (os64)
            {
                if (!target64)
                {
                    // os: 64bit, self: any, target: 32bit
                    InitTarget32SelfAny(process, offsets, this);
                }
                else if (!self64)
                {
                    // os: 64bit, self: 32bit, target: 64bit
                    InitTarget64Self32(process, offsets, this);
                }
                else
                {
                    // os: 64bit, self: 64bit, target: 64bit
                    InitTargetAnySelfAny(process, offsets, this);
                }
            }
            else
            {
                // os: 32bit, self: 32bit, target: 32bit
                InitTargetAnySelfAny(process, offsets, this);
            }

            // Make sure that the current directory always ends with a backslash. AFAICT that is always the case,
            // so this should really be a noop, but we need to make sure to ensure hassle free comparison later.
            if (!string.IsNullOrEmpty(CurrentDirectory) && CurrentDirectory[CurrentDirectory.Length - 1] != '\\')
            {
                CurrentDirectory += "\\";
            }

            // Owner is not really part of the native PEB, but since we have the process handle
            // here anyway, and going to need this value later on, we get it here as well.
            Owner = GetProcessOwner(process);

            // Also not part of native PEB, but useful and need later on.
            StartTime = DateTime.FromFileTime(pi.CreateTime);
        }

        private static void InitTargetAnySelfAny(SafeProcessHandle handle, PebOffsets offsets, Peb peb)
        {
            var pbi = new PROCESS_BASIC_INFORMATION();
            if (SUCCESS(NtQueryInformationProcess(handle, PROCESS_INFORMATION_CLASS.ProcessBasicInformation, ref pbi, Marshal.SizeOf(pbi), IntPtr.Zero), peb))
            {
                var pp = new IntPtr();
                if (SUCCESS(ReadProcessMemory(handle, new IntPtr(pbi.PebBaseAddress.ToInt64() + offsets.ProcessParametersOffset), ref pp, new IntPtr(Marshal.SizeOf(pp)), IntPtr.Zero), peb))
                {
                    peb.CommandLine = GetString(handle, pp, offsets.CommandLineOffset, peb);
                    peb.CurrentDirectory = GetString(handle, pp, offsets.CurrentDirectoryOffset, peb);
                    peb.ExecutableFullPath = GetString(handle, pp, offsets.ImagePathNameOffset, peb);
                    peb.WindowTitle = GetString(handle, pp, offsets.WindowTitleOffset, peb);
                    peb.DesktopInfo = GetString(handle, pp, offsets.DesktopInfoOffset, peb);
                }

                peb.SessionId = GetInt32(handle, pbi.PebBaseAddress, offsets.SessionIdOffset, peb);
            }
        }


        private static void InitTarget64Self32(SafeProcessHandle handle, PebOffsets offsets, Peb peb)
        {
            var pbi = new PROCESS_BASIC_INFORMATION_WOW64();
            if (SUCCESS(NtWow64QueryInformationProcess64(handle, PROCESS_INFORMATION_CLASS.ProcessBasicInformation, ref pbi, Marshal.SizeOf(pbi), IntPtr.Zero), peb))
            {
                long pp = 0;
                if (SUCCESS(NtWow64ReadVirtualMemory64(handle, pbi.PebBaseAddress + offsets.ProcessParametersOffset, ref pp, Marshal.SizeOf(pp), IntPtr.Zero), peb))
                {
                    peb.CommandLine = GetStringTarget64(handle, pp, offsets.CommandLineOffset, peb);
                    peb.CurrentDirectory = GetStringTarget64(handle, pp, offsets.CurrentDirectoryOffset, peb);
                    peb.ExecutableFullPath = GetStringTarget64(handle, pp, offsets.ImagePathNameOffset, peb);
                    peb.WindowTitle = GetStringTarget64(handle, pp, offsets.WindowTitleOffset, peb);
                    peb.DesktopInfo = GetStringTarget64(handle, pp, offsets.DesktopInfoOffset, peb);
                }

                peb.SessionId = GetInt32Target64(handle, pbi.PebBaseAddress, offsets.SessionIdOffset, peb);
            }
        }

        private static void InitTarget32SelfAny(SafeProcessHandle handle, PebOffsets offsets, Peb peb)
        {
            var pbi = new PROCESS_BASIC_INFORMATION();
            if (SUCCESS(NtQueryInformationProcess(handle, PROCESS_INFORMATION_CLASS.ProcessBasicInformation, ref pbi, Marshal.SizeOf(pbi), IntPtr.Zero), peb))
            {
                // A 32bit process on a 64bit OS has a separate PEB structure.
                var peb32 = new IntPtr();
                if (SUCCESS(NtQueryInformationProcessWow64(handle, PROCESS_INFORMATION_CLASS.ProcessWow64Information, ref peb32, IntPtr.Size, IntPtr.Zero), peb))
                {
                    var pp = new IntPtr();
                    if (SUCCESS(ReadProcessMemory(handle, new IntPtr(peb32.ToInt64() + offsets.ProcessParametersOffset), ref pp, new IntPtr(Marshal.SizeOf(pp)), IntPtr.Zero), peb))
                    {
                        peb.CommandLine = GetStringTarget32(handle, pp, offsets.CommandLineOffset, peb);
                        peb.CurrentDirectory = GetStringTarget32(handle, pp, offsets.CurrentDirectoryOffset, peb);
                        peb.ExecutableFullPath = GetStringTarget32(handle, pp, offsets.ImagePathNameOffset, peb);
                        peb.WindowTitle = GetStringTarget32(handle, pp, offsets.WindowTitleOffset, peb);
                        peb.DesktopInfo = GetStringTarget32(handle, pp, offsets.DesktopInfoOffset, peb);
                    }

                    peb.SessionId = GetInt32Target32(handle, new IntPtr(peb32.ToInt64()), offsets.SessionIdOffset, peb);
                }
            }
        }

        private static int GetInt32Target32(SafeProcessHandle handle, IntPtr pp, int offset, ICanHaveError he)
        {
            var ptr = IntPtr.Zero;
            if (SUCCESS(ReadProcessMemory(handle, pp + offset, ref ptr, new IntPtr(sizeof(int)), IntPtr.Zero), he))
            {
                return ptr.ToInt32();
            }

            he.SetError();
            return default;
        }

        private static string GetStringTarget32(SafeProcessHandle handle, IntPtr pp, int offset, ICanHaveError he)
        {
            var us = new UNICODE_STRING_32();
            if (SUCCESS(ReadProcessMemory(handle, pp + offset, ref us, new IntPtr(Marshal.SizeOf(us)), IntPtr.Zero), he))
            {
                if ((us.Buffer != 0) && (us.Length != 0))
                {
                    string lpBuffer = us.GetLpBuffer();
                    if (SUCCESS(ReadProcessMemory(handle, new IntPtr(us.Buffer), lpBuffer, new IntPtr(us.Length), IntPtr.Zero), he))
                    {
                        return lpBuffer;
                    }
                }
            }

            he.SetError();
            return null;
        }

        private static int GetInt32Target64(SafeProcessHandle handle, long pp, int offset, ICanHaveError he)
        {
            var ptr = IntPtr.Zero;
            uint buf = 0;
            if (SUCCESS(NtWow64ReadVirtualMemory64(handle, pp + offset, ref buf, sizeof(uint), IntPtr.Zero), he))
            {
                ptr = new IntPtr(buf);
                return ptr.ToInt32();
            }

            he.SetError();
            return default;
        }

        private static string GetStringTarget64(SafeProcessHandle handle, long pp, int offset, ICanHaveError he)
        {
            var us = new UNICODE_STRING_WOW64();
            if (SUCCESS(NtWow64ReadVirtualMemory64(handle, pp + offset, ref us, Marshal.SizeOf(us), IntPtr.Zero), he))
            {
                if ((us.Buffer != 0) && (us.Length != 0))
                {
                    string lpBuffer = us.GetLpBuffer();
                    if (SUCCESS(NtWow64ReadVirtualMemory64(handle, us.Buffer, lpBuffer, us.Length, IntPtr.Zero), he))
                    {
                        return lpBuffer;
                    }
                }
            }

            he.SetError();
            return null;
        }

        private static int GetInt32(SafeProcessHandle handle, IntPtr pp, int offset, ICanHaveError he)
        {
            var ptr = IntPtr.Zero;
            if (SUCCESS(ReadProcessMemory(handle, pp + offset, ref ptr, new IntPtr(IntPtr.Size), IntPtr.Zero), he))
            {
                return ptr.ToInt32();
            }

            he.SetError();
            return 0;
        }

        private static string GetString(SafeProcessHandle handle, IntPtr pp, int offset, ICanHaveError he)
        {
            var us = new UNICODE_STRING();
            if (SUCCESS(ReadProcessMemory(handle, pp + offset, ref us, new IntPtr(Marshal.SizeOf(us)), IntPtr.Zero), he))
            {
                if ((us.Buffer != IntPtr.Zero) && (us.Length != 0))
                {
                    string lpBuffer = us.GetLpBuffer();
                    if (SUCCESS(ReadProcessMemory(handle, us.Buffer, lpBuffer, new IntPtr(us.Length), IntPtr.Zero), he))
                    {
                        return lpBuffer;
                    }
                }
            }

            he.SetError();
            return null;
        }

        private static bool SUCCESS(uint status, ICanHaveError he, [CallerMemberName] string callerName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int lineNumber = 0)
        {
            if (status != STATUS_SUCCESS)
            {
                he.SetError();
                return false;
            }

            return true;
        }

        private static bool SUCCESS(int status, ICanHaveError he, [CallerMemberName] string callerName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int lineNumber = 0)
        {
            if (status != STATUS_SUCCESS)
            {
                he.SetError();
                return false;
            }

            return true;
        }

        private static bool SUCCESS(bool result, ICanHaveError he, [CallerMemberName] string callerName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int lineNumber = 0)
        {
            if (!result)
            {
                he.SetError();
                return false;
            }

            return true;
        }
    }
}