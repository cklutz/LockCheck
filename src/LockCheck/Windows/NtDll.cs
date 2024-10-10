using System;
using System.Collections.Generic;
using System.IO;
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
            {
                throw new ArgumentNullException(nameof(paths));
            }

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
            {
                throw new ArgumentNullException(nameof(path));
            }

            IntPtr bufferPtr = IntPtr.Zero;
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

                    uint bufferSize = 16384;
                    bufferPtr = Marshal.AllocHGlobal((int)bufferSize);

                    uint status;
                    while ((status = NtQueryInformationFile(handle, ref statusBlock, bufferPtr, bufferSize,
                        FILE_INFORMATION_CLASS.FileProcessIdsUsingFileInformation)) == STATUS_INFO_LENGTH_MISMATCH)
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

                    IntPtr readBuffer = bufferPtr;
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

        private static NtException GetException(uint status, string apiName, string message)
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
                        SYSTEM_INFORMATION_CLASS.SystemProcessInformation,
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
                    nint currentPtr = checked((IntPtr)(dataPtr.ToInt64() + totalOffset));
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
                        throw new OutOfMemoryException($"Existing buffer size {existingBufferSize:N0} bytes, attempting to allocate {newSize:N0} would overflow.");
                    }
                    return newSize;
                }
                else
                {
                    // allocating a few more kilo bytes just in case there are some new process
                    // kicked in since new call to NtQuerySystemInformation
                    int newSize = requiredSize + (1024 * 10);
                    if (newSize < requiredSize)
                    {
                        throw new OutOfMemoryException($"Required buffer size {requiredSize:N0} bytes, attempting to allocate {newSize:N0} would overflow.");
                    }
                    return newSize;
                }
            }
        }
    }
}