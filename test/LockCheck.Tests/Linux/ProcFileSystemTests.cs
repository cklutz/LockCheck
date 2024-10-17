using System;
using System.Diagnostics;
using System.IO;
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
            using var init = Process.GetProcessById(1);

            // This check will only work with "official" Microsoft images.
            if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
            {
                // Running inside a container, PID 1 will not be /sbin/init, but the process that was
                // initially started in the container.
                Assert.IsNotNull(init.MainModule);
                // MainModule.FileName might just be the file name, not the full path, as documented.
                StringAssert.Contains(init.MainModule!.FileName, ProcFileSystem.GetProcessExecutablePathFromCmdLine(1));
            }
            else
            {
                // Not running in a container, PID 1 is the init process.
                // MainModule should be null, because we expect the unit tests to not run as root and thus
                // don't have permissions to access details of the process.
                Assert.IsNull(init.MainModule);
                Assert.AreEqual("/sbin/init", ProcFileSystem.GetProcessExecutablePathFromCmdLine(1));
            }
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
