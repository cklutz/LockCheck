using System;
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
            var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".test");
            Process? process = null;
            try
            {
                Directory.CreateDirectory(tempFolder);
                process = LaunchCMDInDirectory(tempFolder);
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
            var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".test");
            Process? process = null;
            try
            {
                Directory.CreateDirectory(tempFolder);
                var innerFolder = Path.Join(tempFolder, "inner");
                Directory.CreateDirectory(innerFolder);
                process = LaunchCMDInDirectory(innerFolder);
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

        static Process? LaunchCMDInDirectory(string workingDirectory)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            var process = new Process();
            process.StartInfo = new ProcessStartInfo {
                FileName = "cmd",
                WorkingDirectory = workingDirectory,
                Arguments = "/c \"echo loaded\" & pause",
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            string output = "";
            process.OutputDataReceived += (sender, e) =>
            {
                output += e.Data;
            };
            process.Start();
            process.BeginOutputReadLine();

            while (!output.StartsWith("loaded"))
            {
                Thread.Sleep(50);
            }
            return process;
        }

        #nullable disable
    }
}
