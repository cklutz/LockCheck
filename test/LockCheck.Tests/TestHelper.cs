using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using LockCheck.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockCheck.Tests
{
    internal static class TestHelper
    {
        public static void RunWithInvariantCulture(Action action)
            => RunWithCulture(CultureInfo.InvariantCulture, action);

        public static void RunWithCulture(CultureInfo cultureInfo, Action action)
        {
            var oldUi = CultureInfo.CurrentUICulture;
            var old = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentUICulture = cultureInfo;
                CultureInfo.CurrentCulture = cultureInfo;

                action();
            }
            finally
            {
                CultureInfo.CurrentUICulture = oldUi;
                CultureInfo.CurrentCulture = old;
            }
        }

        public static NativeMethods.FILETIME ToNativeFileTime(this DateTime dateTime)
        {
            // Convert DateTime to a long value representing the file time
            long fileTime = dateTime.ToFileTime();

            // Split the long value into high and low parts
            NativeMethods.FILETIME fileTimeStruct;
            fileTimeStruct.dwLowDateTime = (uint)(fileTime & 0xFFFFFFFF);
            fileTimeStruct.dwHighDateTime = (uint)(fileTime >> 32);

            return fileTimeStruct;
        }

        public static void CreateLockSituation(Action<Exception, string> action)
        {
            string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".test");

            try
            {
                using (var file = File.Open(tempFile, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    try
                    {
                        var file2 = File.Open(tempFile, FileMode.Open, FileAccess.ReadWrite);
                        Assert.Fail("Expected exception");
                    }
                    catch (Exception ex)
                    {
                        action(ex, tempFile);
                    }
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        // Assign all child processes we launch to this job object. This ensures that they
        // will get terminated at the very least, when the test (testhost.exe) itself exits.
        private static readonly Lazy<IJobObject> s_selfJob = new(() => JobObject.Create(), LazyThreadSafetyMode.ExecutionAndPublication);

        public static void CreateProcessWithCurrentDirectory(
            bool target64Bit,
            Action<(string TemporaryDirectory, int ProcessId, int SessionId, DateTime ProcessStartTime, string ProcessName, string ExecutableFullPath)> action)
        {
            string id = Guid.NewGuid().ToString("N");
            string tempDirectoryName = Path.Combine(Path.GetTempPath(), id + ".test");
            int sleep = 0; // forever
            int wait = 20; // seconds

            Process? process = null;
            var tempDir = new DirectoryInfo(tempDirectoryName);
            try
            {
                tempDir.Create();

                var si = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                string clientFullPath = GetClientFullPath(target64Bit, out string? hostExecutable);
                if (hostExecutable != null)
                {
                    si.FileName = hostExecutable;
                    si.Arguments = $"\"{clientFullPath}\" {id} \"{tempDirectoryName}\" {sleep}";
                }
                else
                {
                    si.FileName = clientFullPath;
                    si.Arguments = $"{id} \"{tempDirectoryName}\" {sleep}";
                }

                Console.WriteLine($"Starting test target: {si.FileName} {si.Arguments}");

                process = new Process();
                process.StartInfo = si;
                process.OutputDataReceived += (p, e) =>
                {
                    if (e.Data != null)
                    {
                        Console.WriteLine($"{((Process)p).Id:00000}: {e.Data}");
                    }
                };
                process.ErrorDataReceived += (p, e) =>
                {
                    if (e.Data != null)
                    {
                        Console.WriteLine($"{((Process)p).Id:00000}: {e.Data}");
                    }
                };

                if (!process.Start())
                {
                    throw new InvalidOperationException($"Failed to start test target: {si.FileName} {si.Arguments}");
                }

                s_selfJob.Value.AttachProcess(process);

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using (var server = new NamedPipeServerStream(id))
                {
                    var connectionTask = server.WaitForConnectionAsync();
                    if (!connectionTask.Wait(wait * 1000))
                    {
                        Assert.Fail($"Test target has not signaled ready after {wait} seconds. Test target process with ID {process.Id} {(process.HasExited ? "has already exited" : "appears to be still running")}.");
                    }
                    else
                    {
                        using (var reader = new StreamReader(server))
                        {
                            string? message = reader.ReadLine();
                            Console.WriteLine($"Test target send: {message}");
                        }

                        action((tempDir.FullName, process.Id, process.SessionId, process.StartTime, process.ProcessName, si.FileName));
                    }
                }
            }
            finally
            {
                try
                {
                    Console.WriteLine($"Killing test target process with process ID {process?.Id} ...");
                    process?.Kill();
                    process?.WaitForExit();
                    Console.WriteLine("Process killed.");
                }
                catch (InvalidOperationException)
                {
                    // Process gone.
                }
                finally
                {
                    process?.Dispose();
                }

                tempDir.Delete();
            }
        }

        private static string GetClientFullPath(bool target64Bit, out string? hostExecutable)
        {
            string runtimeIdentifier;
            string extension;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                runtimeIdentifier = target64Bit ? "win-x64" : "win-x86";
                extension = ".exe";
                hostExecutable = null;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                runtimeIdentifier = "linux-x64";
                extension = ".dll";
                hostExecutable = "/usr/share/dotnet/dotnet";
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            // Assume the following directory structure:
            //
            //    test\LockCheck.Tests\bin\<Configuration>\<TargetFramework>
            //    test\LCTestTarget.<RID>\bin\<Configuration>\<TargetFramework>\<RID>\LCTestTarget<EXTENSION>
            //
            // Obviously, this would have to change if your directory layout changes (e.g. using ArtifactPath in MSBUILD).
            // But that would cause pretty obvious test failures and diagnosing them, will end looking here anyway.

            var myDir = AppContext.BaseDirectory.AsSpan().TrimEnd('\\').TrimEnd('/');
            int pos = myDir.LastIndexOfAny(['/', '\\']);
            Debug.Assert(pos != -1);
            var targetFramework = myDir.Slice(pos + 1);
            myDir = myDir.Slice(0, pos);
            pos = myDir.LastIndexOfAny(['/', '\\']);
            var configuration = myDir.Slice(pos + 1);

            string clientFullPath = Path.GetFullPath(
                Path.Combine(
                    AppContext.BaseDirectory, // in TargetFramework
                    "..", // in Configuration
                    "..", // in bin
                    "..", // in "LockCheck.Tests"
                    "..", // in "test"
                    $@"LCTestTarget.{runtimeIdentifier}",
                    "bin",
                    configuration.ToString(),
                    targetFramework.ToString(),
                    runtimeIdentifier,
                    $"LCTestTarget{extension}"));
            return clientFullPath;
        }

        public static void CreateShellWithCurrentDirectory(Action<(string TemporaryDirectory, int ProcessId, int SessionId, DateTime ProcessStartTime, string ProcessName, string ExecutableFullPath)> action)
        {
            string tempDirectoryName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".test");
            const string sentinel = "TEST_READY";

            Process? process = null;
            var tempDir = new DirectoryInfo(tempDirectoryName);
            try
            {
                tempDir.Create();

                var si = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    si.FileName = @"C:\Windows\System32\cmd.exe";
                    si.Arguments = $"/K \"cd {tempDir.FullName} && echo {sentinel}\"";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    si.FileName = "/bin/sh";
                    si.Arguments = $"-c \"cd {tempDir.FullName};echo {sentinel}; sleep 10; exit\"";
                }
                else
                {
                    throw new PlatformNotSupportedException();
                }

                using var waitForStartupComplete = new ManualResetEventSlim();

                process = new Process();
                process.StartInfo = si;
                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null && e.Data.Contains(sentinel))
                    {
                        waitForStartupComplete.Set();
                    }
                };

                if (!process.Start())
                {
                    throw new InvalidOperationException($"Failed to start process: {si.FileName} {si.Arguments}");
                }

                s_selfJob.Value.AttachProcess(process);

                process.BeginOutputReadLine();

                if (waitForStartupComplete.Wait(TimeSpan.FromSeconds(20)))
                {
                    action((tempDir.FullName, process.Id, process.SessionId, process.StartTime, process.ProcessName, si.FileName));
                }
                else
                {
                    Assert.Fail("Giving up after 20 secs");
                }
            }
            finally
            {
                try
                {
                    Console.WriteLine($"KILL IT .... {process?.Id}");
                    process?.Kill();
                    process?.WaitForExit();
                    Console.WriteLine("KILLED IT.");
                }
                catch (InvalidOperationException)
                {
                    // Process gone.
                }
                finally
                {
                    process?.Dispose();
                }

                tempDir.Delete();
            }
        }
    }
}
