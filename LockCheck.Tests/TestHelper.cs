using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

#nullable enable

        public static void CreateFolderWithOpenedProcess(Action<string, Process?> action)
        {
            var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".testdir");
            Process? process = null;
            try
            {
                Directory.CreateDirectory(tempFolder);
                process = LaunchPowershellInDirectory(tempFolder);
                action(tempFolder, process);
            }
            finally
            {
                process?.Kill();
                process?.WaitForExit();

                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder);
                }
            }
        }

        public static void CreateFolderWithOpenedProcessInSubDir(Action<string, Process?> action)
        {
            var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".testdir");
            Process? process = null;
            try
            {
                var innerFolder = Path.Join(tempFolder, "inner");
                Directory.CreateDirectory(innerFolder);
                process = LaunchPowershellInDirectory(innerFolder);
                action(tempFolder, process);
            }
            finally
            {
                process?.Kill();
                process?.WaitForExit();

                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder, true);
                }
            }
        }

        static Process? LaunchPowershellInDirectory(string workingDirectory)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    WorkingDirectory = workingDirectory,
                    Arguments = "-NoProfile -Command \"echo 'process has been loaded'; sleep 10\"",
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            var output = new List<string>();
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null) output.Add(e.Data);
            };
            process.Start();
            process.BeginOutputReadLine();

            var startTime = DateTime.Now;
            while (!(output.LastOrDefault() ?? "").EndsWith("process has been loaded") && !process.HasExited)
            {
                Thread.Sleep(50);
                if (DateTime.Now.Subtract(startTime).TotalSeconds > 2)
                {
                    throw new Exception("Gave up after waiting 2 seconds");
                }
            }
            return process;
        }

#nullable disable
    }
}
