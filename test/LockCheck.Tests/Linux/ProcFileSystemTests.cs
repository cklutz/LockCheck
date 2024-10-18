using System;
using System.Diagnostics;
using System.Text;
using LockCheck.Linux;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockCheck.Tests.Linux
{
    [SupportedTestClassPlatform("linux")]
    public partial class ProcFileSystemTests
    {
        [TestMethod]
        public void GetProcessExecutablePathFromCmdLine_ShouldReturnExecutableName_WhenNoPermissionToProcess()
        {
            using var self = Process.GetCurrentProcess();
            Assert.IsNotNull(self.MainModule, "No permissions to current process");
            StringAssert.Contains(self.MainModule!.FileName, ProcFileSystem.GetProcessExecutablePathFromCmdLine(self.Id));
        }

        [TestMethod]
        public void GetProcessExecutablePathFromCmdLine_ShouldReturnNull_WhenProcessDoesNotExist()
        {
            Assert.IsNull(ProcFileSystem.GetProcessExecutablePathFromCmdLine(-1));
        }

        [DataTestMethod]
        [DataRow("", new string[] { })]
        [DataRow("\0\0", new string[] { })]
        [DataRow("/\0\0", new string[] { "/" })]
        [DataRow("/usr/bin/sh\0\0", new string[] { "/usr/bin/sh" })]
        [DataRow("/usr/bin/sh\0-c echo \"Hello World\"\0\0", new string[] { "/usr/bin/sh", "-c echo \"Hello World\"" })]
        [DataRow("/usr/bin/sh\0-c echo \"Hello World\"\0123\0\0", new string[] { "/usr/bin/sh", "-c echo \"Hello World\"", "123" })]
        public void ConvertToArgs_ShouldReturnAllArgs_WhenMaxArgsIsNotSet(string cmdline, string[] expected)
        {
            var span = Encoding.UTF8.GetBytes(cmdline).AsSpan();
            string[] args = ProcFileSystem.ConvertToArgs(ref span);
            Assert.AreEqual(expected.Length, args.Length);
            CollectionAssert.AreEqual(expected, args);
        }

        [DataTestMethod]
        [DataRow(0, "", new string[] { })]
        [DataRow(1, "", new string[] { })]
        [DataRow(1, "\0\0", new string[] { })]
        [DataRow(1, "/\0\0", new string[] { "/" })]
        [DataRow(0, "/usr/bin/sh\0\0", new string[] { })]
        [DataRow(1, "/usr/bin/sh\0\0", new string[] { "/usr/bin/sh" })]
        [DataRow(2, "/usr/bin/sh\0\0", new string[] { "/usr/bin/sh" })]
        [DataRow(1, "/usr/bin/sh\0-c echo \"Hello World\"\0\0", new string[] { "/usr/bin/sh" })]
        [DataRow(2, "/usr/bin/sh\0-c echo \"Hello World\"\0\0", new string[] { "/usr/bin/sh", "-c echo \"Hello World\"" })]
        [DataRow(3, "/usr/bin/sh\0-c echo \"Hello World\"\0\0", new string[] { "/usr/bin/sh", "-c echo \"Hello World\"" })]
        [DataRow(1, "/usr/bin/sh\0-c echo \"Hello World\"\0123\0\0", new string[] { "/usr/bin/sh" })]
        [DataRow(2, "/usr/bin/sh\0-c echo \"Hello World\"\0123\0\0", new string[] { "/usr/bin/sh", "-c echo \"Hello World\"" })]
        [DataRow(3, "/usr/bin/sh\0-c echo \"Hello World\"\0123\0\0", new string[] { "/usr/bin/sh", "-c echo \"Hello World\"", "123" })]
        [DataRow(4, "/usr/bin/sh\0-c echo \"Hello World\"\0123\0\0", new string[] { "/usr/bin/sh", "-c echo \"Hello World\"", "123" })]
        public void ConvertToArgs_ShouldReturnMaxArgs_WhenMaxArgsIsSet(int maxArgs, string cmdline, string[] expected)
        {
            var span = Encoding.UTF8.GetBytes(cmdline).AsSpan();
            string[] args = ProcFileSystem.ConvertToArgs(ref span, maxArgs);
            Assert.AreEqual(expected.Length, args.Length);
            CollectionAssert.AreEqual(expected, args);
        }
    }
}
