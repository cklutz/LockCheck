using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockCheck.Tests
{
    internal static class TestHelper
    {
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

        public static void CreateProcessWithCurrentDirectory(
            bool target64Bit,
            Action<(string TemporaryDirectory, int ProcessId, int SessionId, DateTime ProcessStartTime, string ProcessName, string ExecutableFullPath)> action)
        {
            string id = Guid.NewGuid().ToString("N");
            string tempDirectoryName = Path.Combine(Path.GetTempPath(), id + ".test");
            int sleep = 0; // forever

            Process process = null;
            var tempDir = new DirectoryInfo(tempDirectoryName);
            try
            {
                tempDir.Create();

                string clientName;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    clientName = $"LockCheck.Tests.Client.{(target64Bit ? "win-x64" : "win-x86")}.exe";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    clientName = $"LockCheck.Tests.Client.linux-x64.exe";
                }
                else
                {
                    throw new PlatformNotSupportedException();
                }

                var si = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    FileName = Path.Combine(AppContext.BaseDirectory, clientName),
                    Arguments = $"{id} \"{tempDirectoryName}\" {sleep}"
                };

                Console.WriteLine("===> " + si.FileName + " " + si.Arguments);

                process = new Process();
                process.StartInfo = si;
                process.OutputDataReceived += (_, e) =>
                {
                    Console.WriteLine(">>> " + e.Data);
                };

                if (!process.Start())
                {
                    throw new InvalidOperationException($"Failed to start process: {si.FileName} {si.Arguments}");
                }

                process.BeginOutputReadLine();

                using (var server = new NamedPipeServerStream(id))
                {
                    var connectionTask = server.WaitForConnectionAsync();
                    if (!connectionTask.Wait(20_000))
                    {
                        Assert.Fail("Giving up after 20 secs");
                    }
                    else
                    {
                        using (var reader = new StreamReader(server))
                        {
                            string message = reader.ReadLine();
                            Console.WriteLine($"Received message from client: {message}");
                        }

                        action((tempDir.FullName, process.Id, process.SessionId, process.StartTime, process.ProcessName, si.FileName));
                    }
                }
            }
            finally
            {
                try
                {
                    Console.WriteLine($"KILL IT .... {process.Id}");
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


        public static void CreateProcessWithCurrentDirectory_XX(Action<(string TemporaryDirectory, int ProcessId, int SessionId, DateTime ProcessStartTime, string ProcessName, string ExecutableFullPath)> action)
        {
            string tempDirectoryName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".test");
            const string sentinel = "TEST_READY";

            Process process = null;
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
                    Console.WriteLine($"KILL IT .... {process.Id}");
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
