using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using LockCheck.Tests.Tooling;
using LockCheck.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static System.Net.Mime.MediaTypeNames;

namespace LockCheck.Tests.Windows;

[SupportedTestClassPlatform("windows")]
[TestCategory("windows")]
public class NtDllTests
{
    [TestMethod]
    public void GetLockingProcessInfos_ShouldThrowArgumentNullException_WhenPathsIsNull()
    {
        var directories = new List<string>();
        Assert.ThrowsException<ArgumentNullException>(() => NtDll.GetLockingProcessInfos(null!, ref directories));
    }

    [TestMethod]
    public void GetLockingProcessInfos_ShouldAddDirectories_WhenPathIsDirectory()
    {
        var di = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        di.Create();

        try
        {
            var directories = new List<string>();
            var result = NtDll.GetLockingProcessInfos([di.FullName], ref directories);

            Assert.AreEqual(1, directories.Count);
            Assert.AreEqual(di.FullName, directories[0]);
            Assert.AreEqual(0, result.Count);
        }
        finally
        {
            di.TryDelete();
        }
    }

    [TestMethod]
    public void GetLockingProcessInfos_ShouldCallGetLockingProcessInfo_WhenPathIsFile()
    {
        var fi = new FileInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt"));

        try
        {
            using var stream = fi.Create();

            var directories = new List<string>();
            var result = NtDll.GetLockingProcessInfos([fi.FullName], ref directories);

            Assert.IsTrue(result.Count >= 0); // Just to ensure the method runs without exceptions
            Assert.AreEqual(0, directories.Count);
        }
        finally
        {
            fi.TryDelete();
        }
    }

    [TestMethod]
    public void GetLockingProcessInfo_ShouldThrowArgumentNullException_WhenPathsIsNull()
    {
        var directories = new List<string>();
        Assert.ThrowsException<ArgumentNullException>(() => NtDll.GetLockingProcessInfos(null!, ref directories));
    }

    [TestMethod]
    public void GetLockingProcessInfo_ShouldThrowArgumentNullException_WhenAPathIsNull()
    {
        var directories = new List<string>();
        Assert.ThrowsException<ArgumentNullException>(() => NtDll.GetLockingProcessInfos([null!], ref directories));
    }

    [TestMethod]
    public void EnumerateSystemProcesses_ShouldContainOnlySpecifiedProcess_WhenFilterIsSpecified()
    {
        using var self = Process.GetCurrentProcess();
        bool found = false;
        int count = 0;
        var result = NtDll.EnumerateSystemProcesses([self.Id], self.Id, (mp, idx, pi, seq) =>
        {
            if ((int)pi.UniqueProcessId == mp)
            {
                found = true;
            }
            count++;
            return 0;
        });
        Assert.IsTrue(found);
        Assert.IsTrue(count == 1);
        Assert.IsTrue(result.ContainsKey((self.Id, self.StartTime)));
        Assert.IsTrue(result.Count == 1);
    }

    [TestMethod]
    public void EnumerateSystemProcesses_ShouldContainAllProcesses_WhenNoProcessFilterIsSpecified()
    {
        using var self = Process.GetCurrentProcess();
        bool found = false;
        int count = 0;
        var result = NtDll.EnumerateSystemProcesses(null, self.Id, (mp, idx, pi, seq) =>
        {
            if ((int)pi.UniqueProcessId == mp)
            {
                found = true;
            }
            count++;
            return 0;
        });
        // Number of total processes if course highly volatile. We just check that we
        // found more than one.
        Assert.IsTrue(found);
        Assert.IsTrue(count > 1);
        Assert.IsTrue(result.ContainsKey((self.Id, self.StartTime)));
        Assert.IsTrue(result.Count > 1);
    }

    // This test must run as admin, because otherwise we cannot use "logman.exe".
    // Using Microsoft.Diagnostics.Tracing would not require running as admin,
    // but the included parser does not expose the "ProcessSequenceNumber".
    [SupportedTestMethodPlatform("windows", requiresAdminRights: true)]
    public void EnumerateSystemProcesses_ShouldContainProcessSequenceNumber_IfSupported()
    {
        // The .NET Framework implementation of EnumerateSystemProcesses() does not expose ProcessSequenceNumber,
        // an possibly never will.
#if NET
        var tempDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".test"));
        tempDir.Create();

        try
        {
            string traceId = "ProcessTrace." + Guid.NewGuid().ToString("N");
            string traceFile = $"{tempDir.FullName}\\{traceId}.etl";
            string reportFile = $"{tempDir.FullName}\\{traceId}.xml";
            RunTool("logman.exe", $"create trace {traceId} -p Microsoft-Windows-Kernel-Process 0xFFFFFFFF -o \"{traceFile}\" -ets -y");

            ulong? actualProcessSequenceNumber = null;
            int processId = 0;
            try
            {
                TestHelper.CreateSampleProcess(false,
                    (process, _) =>
                    {
                        var result = NtDll.EnumerateSystemProcesses(null, process.Id, (mp, idx, pi, seq) =>
                        {
                            if ((int)pi.UniqueProcessId == mp)
                            {
                                actualProcessSequenceNumber = seq;
                                processId = mp;
                            }
                            return 0;
                        });
                    });
            }
            finally
            {
                RunTool("logman.exe", $"stop {traceId} -ets");
                RunTool("tracerpt.exe", $"\"{traceFile}\" -o \"{reportFile}\" -of XML -y");
            }

            string? expectedProcessSequenceNumber = GetReportDataValue(reportFile, processId, "ProcessSequenceNumber");

            Assert.AreEqual(expectedProcessSequenceNumber, actualProcessSequenceNumber?.ToString());
        }
        finally
        {
            TestHelper.TryDelete(tempDir);
        }
#endif
    }

    private static string? GetReportDataValue(string reportFile, int processId, string dataId)
    {
        var xdoc = XDocument.Load(reportFile);
        var nsMgr = new XmlNamespaceManager(new NameTable());
        nsMgr.AddNamespace("ns", "http://schemas.microsoft.com/win/2004/08/events/event");

        string xpathQuery = $"//ns:Event[ns:EventData/ns:Data[@Name='ProcessID' and normalize-space(text())='{processId}']]/ns:EventData/ns:Data[@Name='{dataId}']";
        string? dataValue = xdoc.XPathSelectElement(xpathQuery, nsMgr)?.Value;
        return dataValue;
    }

    private static void RunTool(string executable, string arguments)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException();
        }

        var si = new ProcessStartInfo
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            Arguments = arguments
        };

        if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
        {
            // WOW64 process, file system redirection applies.
            si.FileName = Environment.ExpandEnvironmentVariables($@"%windir%\sysnative\{executable}");
        }
        else
        {
            si.FileName = Environment.ExpandEnvironmentVariables($@"%windir%\system32\{executable}");
        }


        Console.WriteLine($"Starting: {si.FileName} {si.Arguments}");

        using var process = new Process();
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
            throw new InvalidOperationException($"Failed to start: {si.FileName} {si.Arguments}");
        }

        TestHelper.AttachProcess(process);

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Process with ID {process.Id} and command line '{si.FileName} {si.Arguments}' failed with exit code {process.ExitCode}");
        }
    }
}
