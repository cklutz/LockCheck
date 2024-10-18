using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LockCheck.Tests.Tooling;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockCheck.Tests
{
    [TestClass]
    public class LockManagerTests
    {
        [DataTestMethod]
        [DataRow(LockManagerFeatures.None)]
        [DataRow(LockManagerFeatures.UseLowLevelApi)]
        public void GetLockingProcessInfos_ShouldReturnProcess_WhenHasLock(LockManagerFeatures features)
        {
            var process = Process.GetCurrentProcess();

            TestHelper.CreateLockSituation((_, fileName) =>
            {
                var processInfos = LockManager.GetLockingProcessInfos([fileName], features).ToList();
                Assert.AreEqual(1, processInfos.Count);
                Assert.AreEqual(process.Id, processInfos[0].ProcessId);
                Assert.AreEqual(process.SessionId, processInfos[0].SessionId);
                Assert.AreEqual(process.StartTime, processInfos[0].StartTime);
                Assert.IsNotNull(processInfos[0].ApplicationName);
                Assert.AreEqual(process.MainModule!.FileName.ToLowerInvariant(), processInfos[0].ExecutableFullPath?.ToLowerInvariant());
                // Might contain domain, computer name, etc. in SAM form
                StringAssert.Contains(processInfos[0].Owner?.ToLowerInvariant(), Environment.UserName.ToLowerInvariant());
                // Might have an .exe suffix or not.
                StringAssert.Contains(processInfos[0].ExecutableName?.ToLowerInvariant(), process.ProcessName.ToLowerInvariant());
            });
        }


        [DataTestMethod]
        [DataRow(LockManagerFeatures.None | LockManagerFeatures.CheckDirectories, true)]
        [DataRow(LockManagerFeatures.None | LockManagerFeatures.CheckDirectories, false)]
        [DataRow(LockManagerFeatures.UseLowLevelApi | LockManagerFeatures.CheckDirectories, true)]
        [DataRow(LockManagerFeatures.UseLowLevelApi | LockManagerFeatures.CheckDirectories, false)]
        public void GetLockingProcessInfos_ShouldReturnProcess_WhenWorkingDirectoryMatches(LockManagerFeatures features, bool target64Bit)
        {
            if (!Environment.Is64BitOperatingSystem)
            {
                throw new PlatformNotSupportedException("Expected 64bit operating system");
            }

            TestHelper.CreateProcessWithCurrentDirectory(target64Bit,
                ((string TemporaryDirectory, int ProcessId, int SessionId, DateTime ProcessStartTime, string ProcessName, string ExecutableFullPath) args) =>
                {
                    var processInfos = LockManager.GetLockingProcessInfos([args.TemporaryDirectory], features).ToList();

                    // Since "working directory" matches by path prefix, we might actually see multiple matches.
                    // Make sure we find at least the one we explicitly started.
                    var processInfo = processInfos.FirstOrDefault(pi => pi.ProcessId == args.ProcessId);
                    Assert.IsNotNull(processInfo, $"Expected process with ID {args.ProcessId}/{args.ProcessName} not found as a match");
                    Assert.AreEqual(args.ProcessId, processInfo.ProcessId);
                    Assert.AreEqual(args.SessionId, processInfo.SessionId);
                    Assert.AreEqual(args.ProcessStartTime, processInfo.StartTime);
                    Assert.AreEqual(args.ExecutableFullPath.ToLowerInvariant(), processInfo.ExecutableFullPath?.ToLowerInvariant());
                    // Might contain domain, computer name, etc. in SAM form
                    StringAssert.Contains(processInfo.Owner?.ToLowerInvariant(), Environment.UserName.ToLowerInvariant());
                    // Might have an .exe suffix or not.
                    StringAssert.Contains(processInfo.ExecutableName?.ToLowerInvariant(), args.ProcessName.ToLowerInvariant());
                });
        }

        [TestMethod]
        public void GetLockingProcessInfos_ShouldThrowArgumentNullException_WhenPathsIsNull()
        {
            Assert.ThrowsException<ArgumentNullException>(() => LockManager.GetLockingProcessInfos(null!));
        }

        [TestMethod]
        public void GetLockingProcessInfos_ShouldReturnEmpty_WhenPathsIsEmpty()
        {
            string[] paths = [];

            var result = LockManager.GetLockingProcessInfos(paths);

            Assert.IsFalse(result.Any());
        }

        [TestMethod]
        public void GetLockingProcessInfos_ShouldReturnEmpty_WhenFileDoesNotExist()
        {
            string[] paths = [Path.Combine(Path.GetTempPath(), "nonexistentfile.txt")];

            var result = LockManager.GetLockingProcessInfos(paths);

            Assert.IsFalse(result.Any());
        }

    }
}
