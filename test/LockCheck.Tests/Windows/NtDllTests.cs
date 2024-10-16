using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LockCheck.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockCheck.Tests.Windows
{
    [SupportedTestClassPlatform("windows")]
    public class NtDllTests
    {
        [TestMethod]
        public void GetLockingProcessInfos_ShouldThrowArgumentNullException_WhenPathsIsNull()
        {
            var directories = new List<string>();
            Assert.ThrowsException<ArgumentNullException>(() => NtDll.GetLockingProcessInfos(null, ref directories));
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
                di.Delete();
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
                fi.Delete();
            }
        }

        [TestMethod]
        public void GetLockingProcessInfo_ShouldThrowArgumentNullException_WhenPathsIsNull()
        {
            var directories = new List<string>();
            Assert.ThrowsException<ArgumentNullException>(() => NtDll.GetLockingProcessInfos(null, ref directories));
        }

        [TestMethod]
        public void GetLockingProcessInfo_ShouldThrowArgumentNullException_WhenAPathIsNull()
        {
            var directories = new List<string>();
            Assert.ThrowsException<ArgumentNullException>(() => NtDll.GetLockingProcessInfos([null], ref directories));
        }

        [TestMethod]
        public void EnumerateSystemProcesses_ShouldContainOnlySpecifiedProcess_WhenFilterIsSpecified()
        {
            using var self = Process.GetCurrentProcess();
            bool found = false;
            int count = 0;
            var result = NtDll.EnumerateSystemProcesses([self.Id], self.Id, (mp, currentPtr, idx, pi) =>
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
            var result = NtDll.EnumerateSystemProcesses(null, self.Id, (mp, currentPtr, idx, pi) =>
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
    }
}
